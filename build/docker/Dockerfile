# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG APP_PROJECT

WORKDIR /src

# Копируем весь репозиторий (просто и надёжно для демо)
COPY . .

# Защитная проверка, чтобы не собирать "пустоту"
RUN test -n "${APP_PROJECT}" || (echo "APP_PROJECT build-arg is required" && exit 1)

RUN dotnet restore "${APP_PROJECT}"
RUN dotnet publish "${APP_PROJECT}" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

COPY --from=build /app/publish .

# Начиная с .NET 8 официальные образы по умолчанию слушают 8080 (мы фиксируем это явно)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Имя dll задаём через env (из docker-compose/.env)
ENV APP_DLL=""
ENTRYPOINT ["sh", "-c", "dotnet \"$APP_DLL\""]
