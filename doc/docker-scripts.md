# Docker-скрипты проекта `ldpr_activist_demo`

Этот документ описывает локальные и release-скрипты из каталога `build/docker`, их назначение, порядок использования и важные ограничения.

---

## 1. Общая структура

Основные файлы и каталоги:

- `build/docker/.env.local` — локальная конфигурация разработки.
- `build/docker/.env.local.template` — шаблон локальной конфигурации.
- `build/docker/.env.production` — production-конфигурация для сборки release bundle и запуска на сервере.
- `build/docker/.env.production.template` — production-шаблон.
- `build/docker/docker-compose.yml` — общий compose-файл.
- `build/docker/docker-compose.local.yml` — локальное наложение для разработки.
- `build/docker/docker-compose.prod.yml` — production-наложение для сервера.
- `build/docker/docker-up.bat` — локальный запуск.
- `build/docker/docker-down.bat` — локальная остановка.
- `build/docker/docker-clean.bat` — полный локальный reset docker-окружения.
- `build/docker/docker-backup-now.bat` — ручной локальный запуск бэкапа PostgreSQL.
- `build/docker/docker-backup-now.prod.bat` — ручной production-запуск бэкапа PostgreSQL.
- `build/docker/docker-release-image.bat` — сборка production image и формирование release bundle.
- `build/docker/scripts/release-postgres-backup.prod.sh` — server-side одноразовый backup из release bundle.
- `build/docker/scripts/release-deploy.prod.sh` — server-side безопасный deploy нового release без удаления volumes.

---

## 2. Какие `.env` используются

### 2.1. Локальная разработка

Для локальной разработки используются:

- `build/docker/.env.local`
- `build/docker/docker-compose.yml`
- `build/docker/docker-compose.local.yml`

Именно на них завязаны:

- `docker-up.bat`
- `docker-down.bat`
- `docker-clean.bat`
- `docker-backup-now.bat`

### 2.2. Production / release

Для подготовки production-релиза используются:

- `build/docker/.env.production`
- `build/docker/docker-compose.yml`
- `build/docker/docker-compose.prod.yml`

На них завязаны:

- `docker-release-image.bat`
- `docker-backup-now.prod.bat`

---

## 3. Локальные скрипты

### 3.1. `docker-up.bat`

Назначение:

- проверяет наличие Docker и Docker Compose v2;
- читает `build/docker/.env.local`;
- создаёт локальные host-директории для логов и бэкапов, если они включены в конфигурации;
- проверяет наличие `build/docker/secrets/service-account.json`, если `FIREBASE_PUSH_ENABLED=true`;
- запускает:
  - `postgres`
  - `redis`
  - `postgres-backup`
- затем собирает и поднимает:
  - `api`
  - `nginx`

Команда запуска:

```bat
cd build\docker
docker-up.bat
```

После успешного старта API должен быть доступен по адресу:

```text
http://localhost:<API_PORT>
```

Значение `<API_PORT>` берётся из `.env.local`, по умолчанию:

```text
8080
```

### 3.2. `docker-down.bat`

Назначение:

- останавливает все локальные сервисы проекта;
- контейнеры не удаляет;
- volumes не удаляет;
- образы не удаляет.

Команда:

```bat
cd build\docker
docker-down.bat
```

Это безопасная остановка, когда нужно просто погасить локальное окружение.

### 3.3. `docker-clean.bat`

Назначение:

- делает полный локальный reset окружения;
- удаляет контейнеры проекта;
- удаляет network проекта;
- удаляет volumes проекта, включая PostgreSQL и Redis data;
- удаляет локально собранные образы.

Скрипт защищён:

- сначала спрашивает подтверждение `Y/N`;
- затем требует ввод пароля.

Команда:

```bat
cd build\docker
docker-clean.bat
```

Использовать только если действительно нужно полностью пересоздать локальное окружение.

### 3.4. `docker-backup-now.bat`

Назначение:

- вручную запускает одноразовый бэкап PostgreSQL в локальном окружении;
- использует уже работающий сервис `postgres-backup`.

Команда:

```bat
cd build\docker
docker-backup-now.bat
```

Бэкап будет сохранён в локальный host-каталог, заданный в `.env.local`:

```text
DB_BACKUP_ROOT_PATH_HOST=C:\ProgramData\ldpr_activist\db_backup
```

---

## 4. Release / production-скрипты

### 4.1. `docker-release-image.bat`

Назначение:

- читает `build/docker/.env.production`;
- собирает production image;
- сохраняет image в `.tar`;
- формирует release bundle в каталоге:

```text
build/docker/.release/<API_IMAGE_TAG>
```

В release bundle попадают:

- `docker-compose.yml`
- `docker-compose.prod.yml`
- `.env.production`
- `.env.production.template`
- `nginx/default.conf`
- `scripts/*.sh`
- `*.tar` с docker image

Запуск:

```bat
cd build\docker
docker-release-image.bat prod-2026-04-05-01
```

Если тег не передать параметром, будет использован `API_IMAGE_TAG` из `.env.production`.

### 4.2. `docker-backup-now.prod.bat`

Назначение:

- запускает одноразовый PostgreSQL backup в production-конфигурации;
- использует `build/docker/.env.production`.

Команда:

```bat
cd build\docker
docker-backup-now.prod.bat
```

Этот скрипт обычно полезен, если нужно заранее проверить backup logic до релиза или воспроизвести production-сценарий локально.

### 4.3. scripts/release-postgres-backup.prod.sh

Назначение:

- запускается уже на production-сервере из распакованного release bundle;
- поднимает postgres, если он ещё не запущен;
- выполняет одноразовый PostgreSQL backup без остановки приложения;
- не удаляет контейнеры, volumes и образы.

Запуск на сервере:

bash +cd /opt/ldpr-activist/releases/<tag> +./scripts/release-postgres-backup.prod.sh +

### 4.4. scripts/release-deploy.prod.sh

Назначение:

- запускается уже на production-сервере из распакованного release bundle;
- загружает .tar нового image;
- делает pre-deploy backup текущей production БД;
- поднимает новый stack через docker compose up -d --remove-orphans;
- не удаляет named volumes, поэтому текущая БД сохраняется;
- выполняет health-check после запуска.

Запуск на сервере:

bash +cd /opt/ldpr-activist/releases/<tag> +chmod 600 .env.production +chmod 600 secrets/service-account.json +./scripts/release-deploy.prod.sh +

Подробный пошаговый сценарий production-выкатки и rollback описан отдельно в doc/production-release-runbook.md.

---

## 5. Compose-файлы

### 5.1. `docker-compose.yml`

Это базовый compose-файл. Он описывает:

- `postgres`
- `redis`
- `postgres-backup`
- `api`
- `nginx`

В нём же:

- пробрасываются переменные окружения в API;
- задаются volumes для логов и бэкапов;
- подключается `./secrets` как read-only каталог для Firebase service account;
- задаётся фиксированное имя compose-проекта:

```yaml
name: ldpr_activist_demo
```

### 5.2. `docker-compose.local.yml`

Локальное наложение:

- публикует `postgres` и `redis` только на `127.0.0.1`;
- собирает `api` из локального `Dockerfile`;
- публикует `nginx` на `127.0.0.1:${API_PORT}`.

### 5.3. `docker-compose.prod.yml`

Production-наложение:

- не собирает `api`, а использует уже загруженный docker image:

```yaml
image: ${API_IMAGE_NAME}:${API_IMAGE_TAG}
```

- публикует `nginx` наружу на `${API_PORT}:80`.

---

## 6. Важные замечания

### 6.0. Почему production БД сохраняется между релизами

Текущая PostgreSQL база хранится в named volume pg_data, а не внутри release-каталога.

Дополнительно в базовом compose-файле зафиксировано:

yaml +name: ldpr_activist_demo +

Поэтому новый release из другого каталога продолжает использовать тот же compose-project и тот же volume, пока оператор не выполняет destructive-команды, например:

- docker compose down --volumes
- docker volume rm ...

### 6.1. Секреты

Файл:

```text
build/docker/secrets/service-account.json
```

не должен коммититься в репозиторий и должен храниться отдельно для локальной машины и production-хоста.

### 6.2. `.env.local` и `.env.production`

Эти файлы являются рабочими конфигурациями окружения.  
Шаблоны:

- `.env.local.template`
- `.env.production.template`

можно хранить в Git, а реальные `.env` — нет.

### 6.3. CRLF в shell-скриптах

Production shell-скрипты запускаются через `tr -d '\r'`, поэтому даже если файл приехал на сервер с Windows line endings, он всё равно должен отработать.

Тем не менее перед production-запуском рекомендуется дополнительно выполнить:

```bash
sed -i 's/\r$//' scripts/*.sh
```

Это снижает количество трудноуловимых проблем при ручной отладке на сервере.

### 6.4. Миграции БД

В текущем production .env.production используется:

text +DATABASE_AUTO_MIGRATE=false +

Это означает, что сохранение volume само по себе не применяет schema changes. Если новый release содержит EF Core migrations, их нужно выполнить отдельно контролируемым способом до переключения или одновременно с ним.