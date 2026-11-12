# Events API

Решение состоит из трёх независимо запускаемых ASP.NET Core API (.NET 10). У каждого сервиса четыре проекта: Domain содержит модели и правила, Application — сценарии и интерфейсы, Infrastructure — PostgreSQL, EF Core и адаптеры, Presentation — HTTP API и настройку приложения.

| Сервис | Адрес локально | Данные и ответственность |
| --- | --- | --- |
| Users | http://localhost:5101 | База `users`, порт 5433. Регистрация, хеши паролей, вход и выпуск JWT. |
| Events | http://localhost:5102 | База `events`, порт 5434. CRUD событий, доступные места и результаты запросов на резервирование. |
| Bookings | http://localhost:5103 | База `bookings`, порт 5435. Брони, статусы, отмена и доставка сообщений. |

Каждая база имеет свой контекст и свою начальную миграцию в `Services/<сервис>/Infrastructure/DataAccess/Migrations`. Начальная миграция создаёт **новую** базу: данные из базы монолита не переносятся. Межсервисных внешних ключей и навигационных свойств нет. Users не зависит от других сервисов. Events и Bookings ссылаются на [Contracts](Contracts/BookingMessages.cs), но не на проекты друг друга.

## Локальный запуск

Нужны .NET 10 SDK и Docker. Из корня репозитория:

```powershell
docker compose up -d --wait
dotnet build "Events API.sln"
```

Для локального запуска задайте одинаковый `Jwt__Secret` во всех трёх процессах (не меньше 32 байт), а `Messaging__Key` — в Events и Bookings. Следующий пример для PowerShell создаёт временные ключи и запускает сервисы в трёх окнах с одним окружением:

```powershell
$env:Jwt__Secret = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
$env:Messaging__Key = [guid]::NewGuid().ToString("N")
foreach ($service in "Users", "Events", "Bookings") {
    Start-Process dotnet -ArgumentList "run --no-build --project Services/$service/Presentation"
}
```

Каждый процесс при запуске применяет миграции только к своей базе. Swagger UI доступен на `/swagger` каждого API. Для другого окружения задайте `ConnectionStrings__DefaultConnection` отдельно у каждого сервиса и `Messaging__EventsUrl` у Bookings. При размещении на разных узлах используйте HTTPS для внутреннего транспорта.

## Жизненный цикл брони

1. Клиент получает JWT через `POST /auth/login` у Users. Events и Bookings проверяют токен локально. Для создания события используйте `POST /events` у Events (роль Admin).
2. `POST /events/{eventId}/book` у **Bookings** принимает бронь на одно место и возвращает `202 Accepted` со ссылкой на её статус. Лимит — 10 активных броней на пользователя.
3. Фоновый обработчик Bookings отправляет `reserve-seats` сервису Events. Events одной транзакцией сохраняет результат запроса и обновляет остаток мест. Несуществующее или начавшееся событие и нехватка мест приводят к статусу `Rejected`. Пока Events недоступен, бронь остаётся в ожидании.
4. После положительного ответа Bookings сохраняет статус `Confirmed` и публикует `BookingConfirmed` в топик `booking-confirmed`. Сообщение содержит только идентификаторы брони, события и пользователя, число мест и время подтверждения в UTC. Events помечает резерв подтверждённым без повторного списания мест.
5. `DELETE /bookings/{id}` у Bookings доступен владельцу и администратору. После отмены сообщение `booking-cancelled` возвращает место. Проверить результат можно через `GET /bookings/{id}` у Bookings и `GET /events/{eventId}` у Events.

Транспорт — прямая доставка JSON по HTTP без внешнего брокера. Издатель и подписчик используют одни и те же типы и константы из Contracts. Внутренние маршруты `/internal/messages/{topic}` требуют ключ `X-Messaging-Key`. Необходимость доставки записывается в той же транзакции, что и состояние брони. После сбоя Bookings повторяет отправку; Events распознаёт повторы по идентификатору брони и запоминает отмену, даже если она пришла до запроса места.

## Проверка

```powershell
dotnet test Test/Test.csproj
dotnet test "Integration tests/Integration tests.csproj"
```

Интеграционные тесты используют временный PostgreSQL через Testcontainers; необходим работающий Docker. Сквозной тест проверяет вход, бронь последнего места, отказ следующей брони, отмену и повторное бронирование.
