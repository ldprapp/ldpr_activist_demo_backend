# LDPR Activist Demo Backend

Демо-бэкенд для проекта **LDPR Activist**, построенный на **.NET 10**, **ASP.NET Core Web API**, **EF Core**, **PostgreSQL**, **Redis** и **Docker**.

Проект организован как одно решение из нескольких прикладных слоёв:

- `LdprActivistDemo.Api` — HTTP API, middleware, startup, DI root.
- `LdprActivistDemo.Application` — прикладная логика, сервисы и контракты между API и persistence-слоем.
- `LdprActivistDemo.Persistence` — `DbContext`, репозитории, Redis/in-memory store, доступ к PostgreSQL.
- `LdprActivistDemo.Contracts` — DTO и публичные API-контракты.
- `LdprActivistDemo.Migrations` — EF Core migrations и SQL-артефакты, связанные со схемой базы данных.

## Структура решения

Корень решения:

- `LDPRActivistDEMO.slnx`

Основные проекты:

- `LdprActivistDemo.Api`
- `LdprActivistDemo.Application`
- `LdprActivistDemo.Contracts`
- `LdprActivistDemo.Migrations`
- `LdprActivistDemo.Persistence`

Инфраструктура запуска и деплоя:

- `build/docker/` — Dockerfile, compose-файлы, `.env`-файлы, `.bat`-скрипты, shell-скрипты бэкапа, nginx-конфиг.

Документация:

- `doc/ldpr_activist_demo_spec.md` — спецификация демо-бэкенда.
- `doc/docker-scripts.md` — описание docker-скриптов проекта.
- `doc/deployment-guide.md` — пошаговая инструкция по деплою release bundle на сервер.
- `doc/runtime-artifacts.md` — бэкапы, логи, runtime-артефакты и работа с ними.

## Основные возможности

На текущий момент репозиторий содержит backend для следующих сценариев:

- регистрация и аутентификация пользователей;
- OTP и password reset;
- справочник регионов и населённых пунктов;
- задания и отправка подтверждений выполнения;
- реферальная система;
- транзакции пользовательских баллов;
- рейтинги пользователей;
- загрузка пользовательских и системных изображений;
- push-уведомления через Firebase;
- rate limiting;
- health/version endpoints;
- production-ready docker-окружение с PostgreSQL, Redis, nginx и сервисом автоматических бэкапов PostgreSQL.

## Быстрый старт для локальной разработки

### Предварительные требования

- .NET SDK 10
- Docker Desktop / Docker Engine
- Docker Compose v2

### Локальный запуск через Docker

1. Перейди в каталог:

   `build/docker`

2. Убедись, что существует файл:

   `.env.local`

   Если его нет — создай из шаблона:

   `.env.local.template`

3. Если в `.env.local` включён Firebase push:

   `FIREBASE_PUSH_ENABLED=true`

   то положи файл:

   `build/docker/secrets/service-account.json`

4. Запусти окружение:

   `docker-up.bat`

5. После успешного старта API будет доступен через nginx reverse proxy:

   `http://localhost:8080`

   Значение порта берётся из:

   `API_PORT` в `.env.local`

### Проверка запуска

Health endpoint:

- `GET /api/v1/health`

Пример:

`http://localhost:8080/api/v1/health`

## Локальные docker-скрипты

В каталоге `build/docker` находятся основные скрипты разработчика:

- `docker-up.bat` — поднять локальное окружение;
- `docker-down.bat` — остановить локальное окружение без удаления контейнеров и volumes;
- `docker-clean.bat` — полный reset локального docker-окружения;
- `docker-backup-now.bat` — вручную запустить локальный PostgreSQL backup;
- `docker-release-image.bat` — собрать production image и сформировать release bundle;
- `docker-backup-now.prod.bat` — вручную выполнить backup для production-конфигурации.

Подробно они описаны в:

- `doc/docker-scripts.md`

## Конфигурация

Основные конфигурационные файлы для Docker:

- `build/docker/.env.local`
- `build/docker/.env.local.template`
- `build/docker/.env.production`
- `build/docker/.env.production.template`

Через них настраиваются:

- PostgreSQL;
- Redis;
- порт nginx;
- auto-migrate;
- rate limiting;
- structured logging;
- Firebase push;
- пути логов и бэкапов;
- имя и тег production image.

Для API docker-compose пробрасывает настройки через переменные окружения в стандартный .NET configuration pipeline.

## Миграции базы данных

За схему базы данных отвечает проект:

- `LdprActivistDemo.Migrations`

В нём находятся:

- EF Core migrations;
- `AppDbContextModelSnapshot`;
- дополнительные SQL-артефакты, например trigger-скрипты.

В локальной среде обычно используется:

- `DATABASE_AUTO_MIGRATE=true`

В production по умолчанию:

- `DATABASE_AUTO_MIGRATE=false`

Это поведение задаётся в соответствующих `.env`-файлах.

## Production / release / deployment

Production release собирается через:

- `build/docker/docker-release-image.bat`

Результат сборки попадает в:

- `build/docker/.release/<tag>`

Внутри release bundle лежат:

- compose-файлы;
- production `.env`;
- nginx-конфиг;
- shell-скрипты;
- docker image в виде `.tar`.

Подробная инструкция по выкладке новой версии на сервер, когда там уже крутится предыдущая версия, описана в:

- `doc/deployment-guide.md`

## Бэкапы, логи и runtime-артефакты

Проект умеет:

- автоматически делать PostgreSQL backups через отдельный контейнер `postgres-backup`;
- писать файловые structured logs в host-каталог;
- хранить release bundle на сервере как отдельный артефакт релиза.

Подробно:

- как сделать backup вручную;
- где лежат backup-файлы;
- как скачать backup с сервера на компьютер;
- где лежат файлы логов;
- какие runtime-артефакты критично сохранять;

описано в:

- `doc/runtime-artifacts.md`

## Дополнительная документация

- `doc/ldpr_activist_demo_spec.md` — спецификация API и предметной модели.
- `doc/docker-scripts.md` — справочник по docker-скриптам.
- `doc/deployment-guide.md` — production deployment guide.
- `doc/runtime-artifacts.md` — бэкапы, логи и прочие runtime-артефакты.