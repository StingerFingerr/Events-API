# Events API

Решение состоит из трёх независимо запускаемых ASP.NET Core API (.NET 10). У каждого сервиса четыре проекта: Domain содержит модели и правила, Application — сценарии и интерфейсы, Infrastructure — PostgreSQL, EF Core и адаптеры, Presentation — HTTP API и настройку приложения.

| API | HTTP с хоста → в контейнере | PostgreSQL: база, адрес внутри Compose | Порт БД с хоста |
| --- | --- | --- | --- |
| Users — регистрация и выдача JWT | `localhost:5101` → `8080` | `users`, `users-db:5432` | `5433` |
| Events — события и количество мест | `localhost:5102` → `8080` | `events`, `events-db:5432` | `5434` |
| Bookings — создание, подтверждение и отмена броней | `localhost:5103` → `8080` | `bookings`, `bookings-db:5432` | `5435` |

Kafka доступна как `localhost:9092` с хоста и `kafka:29092` из контейнеров. Сервисы не вызывают HTTP API друг друга: клиент обращается к каждому API сам, JWT проверяется локально, а Bookings и Events обмениваются сообщениями через брокер.

Каждая база имеет свой контекст и свою начальную миграцию в `Services/<сервис>/Infrastructure/DataAccess/Migrations`. Начальная миграция создаёт **новую** базу: данные из базы монолита не переносятся. Межсервисных внешних ключей и навигационных свойств нет. Users не зависит от других сервисов. Events и Bookings ссылаются на [Contracts](Contracts/BookingMessages.cs), но не на проекты друг друга.

## Запуск в Docker

Достаточно Docker с Compose. В корне проекта один раз создайте файл `.env` с общим ключом JWT (PowerShell):

```powershell
if (!(Test-Path .env)) { "JWT_SECRET=$([guid]::NewGuid().ToString('N'))$([guid]::NewGuid().ToString('N'))" | Set-Content -Encoding ascii .env }
```

Если файл уже есть, убедитесь, что в нём задан `JWT_SECRET` длиной не менее 32 байт. Файл не входит в Git или образы. Затем:

```powershell
docker compose up -d --build --wait
docker compose ps
```

Compose поднимает семь контейнеров: три API, три PostgreSQL и Kafka в режиме KRaft. Каждый API дожидается своей базы, а Events и Bookings также дожидаются Kafka. При первом запуске дождитесь применения миграций; откройте Swagger на портах 5101–5103. Для диагностики: `docker compose logs -f users-api events-api bookings-api`. Для остановки: `docker compose down`. Именованные тома сохраняют данные между запусками.

## Запуск API без контейнеров

Нужны .NET 10 SDK, Docker и файл `.env` из инструкции выше. Из корня репозитория:

```powershell
docker compose up -d --wait users-db events-db bookings-db kafka
dotnet build "Events API.sln"
```

Издатель и аудитория заданы в [общей конфигурации JWT](Configuration/jwtsettings.json), которая включается в публикацию каждого API. Их можно переопределить для всех процессов через `Jwt__Issuer` и `Jwt__Audience`. Секрет в репозитории не хранится. Срок действия токена задаётся только у Users.

Для локального запуска задайте одинаковый `Jwt__Secret` во всех трёх процессах (не меньше 32 байт). Следующий пример для PowerShell создаёт временные ключи и запускает сервисы в трёх окнах с одним окружением:

```powershell
$env:Jwt__Secret = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
foreach ($service in "Users", "Events", "Bookings") {
    Start-Process dotnet -ArgumentList "run --no-build --project Services/$service/Presentation"
}
```

Каждый процесс при запуске применяет миграции только к своей базе. Swagger UI доступен на `/swagger` каждого API. Для другого окружения задайте `ConnectionStrings__DefaultConnection` отдельно у каждого сервиса и `Kafka__BootstrapServers` у Events и Bookings. В конфигурации это раздел `Kafka`, ключ `BootstrapServers`: с хоста — `localhost:9092`, внутри сети Docker Compose — `kafka:29092`.

При старте Events через Kafka Admin API создаёт недостающие топики `reserve-seats`, `seats-decided`, `booking-confirmed` и `booking-cancelled`. Существующие топики остаются без изменений. Если брокер недоступен или нет права на создание, сервис пишет предупреждение и повторяет попытку в фоне. Группа задаётся через `Kafka:ConsumerGroup` (переменная `Kafka__ConsumerGroup`): экземпляры Events используют одну группу, Bookings — другую.

Только Users выдаёт JWT. Публичная регистрация создаёт учётную запись с ролью `User`; поля запроса не могут назначить роль `Admin`. Администратора назначает оператор в базе Users. После изменения роли нужно снова войти, чтобы получить токен с новой ролью. Просмотр событий открыт; для создания, изменения и удаления события нужен `Admin`. Для броней нужен JWT, владельца определяет идентификатор из `sub`.

## Жизненный цикл брони

1. Клиент получает JWT через `POST /auth/login` у Users. Events и Bookings проверяют токен локально. Для создания события используйте `POST /events` у Events (роль Admin).
2. `POST /events/{eventId}/book` у **Bookings** принимает бронь на одно место и возвращает `202 Accepted` со ссылкой на её статус. Лимит — 10 активных броней на пользователя.
3. Фоновый обработчик Bookings публикует запрос в топик Kafka `reserve-seats`. Events одной транзакцией сохраняет результат запроса и удерживает запрошенные места от других броней, не уменьшая `AvailableSeats`, затем публикует ответ в `seats-decided`. Несуществующее или начавшееся событие и нехватка мест приводят к статусу `Rejected`. Пока Events недоступен, бронь остаётся в ожидании.
4. После положительного ответа Bookings сохраняет статус `Confirmed` и публикует `BookingConfirmed` в топик `booking-confirmed`. Сообщение содержит только идентификаторы брони, события и пользователя, число мест и время подтверждения в UTC. Events уменьшает `AvailableSeats` на число мест в подтверждении и сохраняет результат в той же транзакции. Повторная доставка не списывает места второй раз. Подтверждение без предварительного резерва также обрабатывается, если есть свободные места. Если событие отсутствует или мест недостаточно, Events пропускает сообщение с предупреждением.
5. `DELETE /bookings/{id}` у Bookings доступен владельцу и администратору. После отмены сообщение `booking-cancelled` возвращает место. Проверить результат можно через `GET /bookings/{id}` у Bookings и `GET /events/{eventId}` у Events.

Маршрут подтверждения: [обработчик решения в Bookings](Services/Bookings/Infrastructure/Messaging/ReservationDecisionHandler.cs) → [издатель Kafka](Services/Bookings/Infrastructure/Messaging/KafkaBookingPublisher.cs) → `booking-confirmed` → [подписчик Events](Services/Events/Infrastructure/Messaging/BookingMessagesConsumer.cs) → [списание мест в базе Events](Services/Events/Application/Messaging/ReservationSubscriber.cs). Предварительное удержание не меняет `AvailableSeats`; списание за бронь выполняется при обработке подтверждения.

Издатель сериализует общий контракт в JSON и передаёт в Kafka ключ `EventId`. Все подтверждения одного события попадают в один раздел топика `booking-confirmed`. Между разными топиками общего порядка нет. Bookings не обращается к Events напрямую и не изменяет остаток мест.

При подтверждении Bookings одной транзакцией сохраняет статус и время ожидающего публикации подтверждения. Затем отправляет сообщение и только после подтверждения Kafka очищает отметку доставки. Позиция подписчика продвигается после обработки: при сбое сообщение может прийти повторно. Events распознаёт повторы по идентификатору брони и запоминает отмену, даже если она пришла до запроса места.

## Проверка

```powershell
dotnet test Test/Test.csproj
dotnet test "Integration tests/Integration tests.csproj"
```

Интеграционные тесты используют временные PostgreSQL и Kafka через Testcontainers; необходим работающий Docker. Сквозной тест проверяет вход, бронь последнего места, отказ следующей брони, отмену и повторное бронирование.
