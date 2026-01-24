# LDPR Activist Backend

Бэкенд-платформа (микросервисы на .NET 10) для проекта LDPR Activist.

## Состав сервисов

Репозиторий содержит несколько сервисов, каждый реализован “пачкой” проектов (API / Contracts / Client / Infrastructure / Domain):

- **Identity Service** (`src/ldpractivist.identity.service`)
  - Аутентификация/авторизация, JWT, учетные записи, refresh-сессии, аудит-события.
  - В текущем коде реализован endpoint: `GET /health`.

- **Organization Service** (`src/ldpractivist.organization.service`)
  - Доменные данные организаций (каркас проектов присутствует).

- **Points Service** (`src/ldpractivist.points.service`)
  - Доменные данные “точек/пунктов” (каркас проектов присутствует).

- **Tasks Service** (`src/ldpractivist.tasks.service`)
  - Доменные данные задач (каркас проектов присутствует).
  - Примечание: в текущем дереве есть опечатка в названии Contracts-проекта: `contrtacts` (как в папках/сборках).

- **ldpractivist.service** (`src/ldpractivist.service`)
  - Общий сервис/публичное API (каркас проектов присутствует).

## Структура решения

Корень: `src/LDPRActivist.slnx`

Типовая структура одного сервиса:

- `*.service.api.prj` — ASP.NET Core API (эндпоинты, middleware, DI root).
- `*.service.prj` — доменная логика/ядро сервиса.
- `*.service.infrastructure.prj` — инфраструктура (EF Core, Postgres, Redis, внешние интеграции).
- `*.service.contracts.prj` — DTO/контракты и JSON source generation (если нужно).
- `*.service.client.prj` — клиентская библиотека для вызова сервиса из других сервисов.

Общий код:
- `src/ldpractivist.common` — общие соглашения/утилиты (в т.ч. соглашения по внешним конфигам).

Production-артефакты:
- `src/production/**` — docker-compose, Dockerfile, шаблоны конфигов/SQL.

Скрипты разработчика:
- `build/docker/*.bat` — удобные команды для поднятия/остановки docker окружения (локально).
- `src/tools/dev/Generate-PostgresInit-Identity.ps1` — генерация init-скриптов для Postgres (Identity).

## Быстрый старт (локальная разработка)

### Предварительные требования

- .NET SDK 10
- Docker + Docker Compose (если поднимаешь Postgres/Redis в контейнерах)

### Запуск Identity API

1) Убедись, что доступен Postgres и Redis.
   - Пример dev-конфига в `src/ldpractivist.identity.service/ldpractivist.identity.service.api.prj/appsettings.Development.json`:
     - Postgres: `Host=localhost;Port=5433;Database=ldpr_identity;Username=ldpr_identity_user;Password=identity_dev_password`
     - Redis: `localhost:6379`

2) Обязательно задай JWT настройки (иначе сервис упадёт при старте из-за валидации опций):
   - `Jwt:Issuer`
   - `Jwt:Audience`
   - `Jwt:SigningKey` (минимум 32 байта UTF-8)
   - `Jwt:AccessTokenMinutes` (1..1440)
   - `Jwt:RefreshTokenDays` (1..365)

   Удобнее всего задать через внешний конфиг (см. раздел “Конфигурация” ниже) или переменные окружения.

3) Запуск:
   - из папки `src/ldpractivist.identity.service/ldpractivist.identity.service.api.prj`:
     - `dotnet run`

4) Проверка:
   - `GET http://localhost:5101/health`

## Конфигурация (важно)

Identity Service подхватывает внешний конфиг-файл:
- имя файла: `ldpractivist.identity.service.config.json`
- переменная окружения с путём к директории: `LDPR_CONFIG_DIR`
- стандартные директории (см. `LdprIdentityConfigConventions` в common)

Приоритет источников конфигурации:
- `appsettings*.json` (в проекте API)
- внешний JSON-конфиг
- переменные окружения
- аргументы командной строки

Подробно: см. `docs/configuration.md`.

## Миграции (Identity)

Используется `dotnet-ef` (см. `src/dotnet-tools.json`).

Подробная инструкция (создание/применение): см. `docs/migrations-identity.md`.

## Production / Docker

В `src/production/**` лежат готовые заготовки для деплоя:
- `ldpractivist.infra/Docker/docker-compose.yml` + шаблоны SQL/паролей
- папки сервисов: `*/Config` и `*/Docker`

Подробно: см. `docs/deployment.md`.