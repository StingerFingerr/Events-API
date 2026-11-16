FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG SERVICE
WORKDIR /src
COPY Contracts/Contracts.csproj Contracts/
COPY Services/${SERVICE}/Domain/*.csproj Services/${SERVICE}/Domain/
COPY Services/${SERVICE}/Application/*.csproj Services/${SERVICE}/Application/
COPY Services/${SERVICE}/Infrastructure/*.csproj Services/${SERVICE}/Infrastructure/
COPY Services/${SERVICE}/Presentation/*.csproj Services/${SERVICE}/Presentation/
RUN dotnet restore "Services/${SERVICE}/Presentation/${SERVICE}.Presentation.csproj"
COPY Contracts/ Contracts/
COPY Configuration/ Configuration/
COPY Services/${SERVICE}/ Services/${SERVICE}/
RUN dotnet publish "Services/${SERVICE}/Presentation/${SERVICE}.Presentation.csproj" -c Release --no-restore -o /out -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
ARG SERVICE
WORKDIR /app
COPY --from=build /out/ ./
RUN printf '#!/bin/sh\nexec dotnet /app/%s.Presentation.dll "$@"\n' "$SERVICE" > /app/start && chmod +x /app/start
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["/app/start"]
