# Runtime artifacts: бэкапы, логи и release-артефакты проекта `ldpr_activist_demo`

Этот документ описывает, какие артефакты появляются по мере жизни проекта, где они лежат, как ими пользоваться и как их забирать с production-сервера.

---

## 1. Какие артефакты создаются

У проекта есть несколько основных классов артефактов:

1. release bundles;
2. docker image tar-файлы;
3. PostgreSQL backups;
4. файлы логов приложения;
5. runtime secrets.

---

## 2. Release bundles

### 2.1. Где создаются локально

При выполнении:

```bat
cd build\docker
docker-release-image.bat <tag>
```

создаётся каталог:

```text
build/docker/.release/<tag>
```

Пример:

```text
build/docker/.release/prod-2026-04-05-03
```

### 2.2. Что внутри

Обычно внутри лежат:

- `docker-compose.yml`
- `docker-compose.prod.yml`
- `.env.production`
- `.env.production.template`
- `nginx/default.conf`
- `scripts/*.sh`
- `ldpr-activist-demo-api_<tag>.tar`

Начиная с безопасной production-схемы выкатки, в `scripts/*.sh` также входят server-side утилиты:

- `scripts/release-postgres-backup.prod.sh`
- `scripts/release-deploy.prod.sh`

Они запускаются уже на сервере из конкретного release-каталога и позволяют:

- сделать pre-deploy backup;
- развернуть новый release поверх существующих named volumes;
- сохранить текущую production БД.

### 2.3. Где хранятся на сервере

Релизы загружаются на сервер в:

```text
/opt/ldpr-activist/releases/<tag>
```

### 2.4. Как новый release использует старую БД

Production PostgreSQL данные живут не в release-каталоге, а в docker named volume `pg_data`.

Так как в compose зафиксировано имя проекта:

`yaml
name: ldpr_activist_demo
`

новый release из другого каталога продолжает работать с тем же volume.

Именно поэтому безопасная выкатка должна использовать:

`bash
docker compose up -d --remove-orphans
`

а не destructive-команды вида:

`bash
docker compose down --volumes
`

---

## 3. PostgreSQL backups

## 3.1. Как устроены бэкапы

В проекте есть отдельный сервис:

```text
postgres-backup
```

Он запускается вместе с окружением и периодически делает бэкапы PostgreSQL.

Частота и retention настраиваются через переменные:

- `DB_BACKUP_INTERVAL_SECONDS`
- `DB_BACKUP_RETENTION_DAYS`
- `DB_BACKUP_FILE_PREFIX`
- `DB_BACKUP_ROOT_PATH_HOST`
- `DB_BACKUP_ROOT_PATH_CONTAINER`

### 3.2. Локальный путь для бэкапов

Из `.env.local`:

```text
DB_BACKUP_ROOT_PATH_HOST=C:\ProgramData\ldpr_activist\db_backup
```

Там будут появляться каталоги:

```text
daily
globals
```

### 3.3. Production-путь для бэкапов

Из `.env.production`:

```text
DB_BACKUP_ROOT_PATH_HOST=/var/backups/ldpr_activist/postgres
```

На production-сервере структура будет такой:

```text
/var/backups/ldpr_activist/postgres/daily
/var/backups/ldpr_activist/postgres/globals
```

### 3.4. Какие файлы создаются

Основная база:

```text
daily/<prefix>_<timestamp>.dump
```

Пример:

```text
/var/backups/ldpr_activist/postgres/daily/ldpr_activist_2026-04-05T18-30-00Z.dump
```

Глобальные объекты PostgreSQL:

```text
globals/<prefix>_globals_<timestamp>.sql
```

Пример:

```text
/var/backups/ldpr_activist/postgres/globals/ldpr_activist_globals_2026-04-05T18-30-00Z.sql
```

### 3.5. Как сделать бэкап вручную локально

```bat
cd build\docker
docker-backup-now.bat
```

### 3.6. Как сделать бэкап вручную для production-конфигурации

Локально:

```bat
cd build\docker
docker-backup-now.prod.bat
```

На сервере вручную можно выполнить compose-команду из каталога релиза:

```bash
docker compose --env-file .env.production -f docker-compose.yml -f docker-compose.prod.yml exec postgres-backup /bin/sh -c "tr -d '\r' < /scripts/postgres-backup.prod.sh | /bin/sh"
```

### 3.7. Как посмотреть, что бэкап действительно создался

На сервере:

```bash
ls -la /var/backups/ldpr_activist/postgres/daily
ls -la /var/backups/ldpr_activist/postgres/globals
```

Или по логам backup-контейнера:

```bash
docker logs ldpr_activist_demo-postgres-backup-1 --tail 200
```

---

## 4. Как скачать бэкап БД на компьютер

### 4.1. Через FileZilla / WinSCP / MobaXterm

Подключиться к серверу по SFTP и скачать файл из:

```text
/var/backups/ldpr_activist/postgres/daily
```

или:

```text
/var/backups/ldpr_activist/postgres/globals
```

### 4.2. Через `scp`

С Linux/macOS/WSL:

```bash
scp root@<server-ip>:/var/backups/ldpr_activist/postgres/daily/ldpr_activist_2026-04-05T18-30-00Z.dump .
```

Для globals dump:

```bash
scp root@<server-ip>:/var/backups/ldpr_activist/postgres/globals/ldpr_activist_globals_2026-04-05T18-30-00Z.sql .
```

### 4.3. Через PowerShell + OpenSSH

Если `scp` доступен в Windows:

```powershell
scp root@<server-ip>:/var/backups/ldpr_activist/postgres/daily/ldpr_activist_2026-04-05T18-30-00Z.dump C:\Users\<User>\Downloads\
```

---

## 5. Файлы логов

### 5.1. Локальные пути логов

Из `.env.local`:

```text
STRUCTURED_LOGGING_FILES_ROOT_PATH_HOST=C:\ProgramData\ldpr_activist\logs
```

### 5.2. Production-путь логов

Из `.env.production`:

```text
STRUCTURED_LOGGING_FILES_ROOT_PATH_HOST=/var/log/ldpr_activist/api
```

Именно здесь API пишет file-based structured logs, если:

```text
STRUCTURED_LOGGING_FILES_ENABLED=true
```

### 5.3. Как посмотреть логи на сервере

Список файлов:

```bash
ls -la /var/log/ldpr_activist/api
```

Просмотр конца файла:

```bash
tail -n 100 /var/log/ldpr_activist/api/<имя_файла>
```

Непрерывное наблюдение:

```bash
tail -f /var/log/ldpr_activist/api/<имя_файла>
```

### 5.4. Когда смотреть именно docker logs

Для проблем раннего старта контейнера удобнее смотреть stdout/stderr контейнера:

```bash
docker logs ldpr_activist_demo-api-1 --tail 200
docker logs ldpr_activist_demo-nginx-1 --tail 200
docker logs ldpr_activist_demo-postgres-backup-1 --tail 200
```

Если контейнер падает до нормальной инициализации файлового логгера, именно `docker logs` будут основным источником диагностики.

---

## 6. Runtime secrets

### 6.1. Firebase service account

В локальном окружении ожидается файл:

```text
build/docker/secrets/service-account.json
```

В release bundle на сервере:

```text
/opt/ldpr-activist/releases/<tag>/secrets/service-account.json
```

Этот файл монтируется в контейнер API как:

```text
/run/secrets/firebase/service-account.json
```

### 6.2. Важное правило

Реальные секреты:

- не должны попадать в Git;
- должны храниться отдельно для локальной среды и production;
- должны иметь ограниченные права доступа.

Для production рекомендуется:

```bash
chmod 600 secrets/service-account.json
chmod 600 .env.production
```

---

## 7. Полезные команды эксплуатации

### 7.1. Проверить контейнеры

```bash
docker compose --env-file .env.production -f docker-compose.yml -f docker-compose.prod.yml ps
```

### 7.2. Проверить health endpoint

```bash
curl -i http://127.0.0.1/api/v1/health
curl -i http://aktivist.pro/api/v1/health
```

### 7.3. Проверить свежие бэкапы

```bash
ls -lah /var/backups/ldpr_activist/postgres/daily | tail
ls -lah /var/backups/ldpr_activist/postgres/globals | tail
```

### 7.4. Проверить свежие логи

```bash
ls -lah /var/log/ldpr_activist/api
docker logs ldpr_activist_demo-api-1 --tail 200
```

---

## 8. Что считать критичными артефактами для сохранения

Минимальный набор, который нельзя терять:

1. PostgreSQL volumes или PostgreSQL dumps;
2. `.env.production`;
3. `service-account.json`;
4. актуальный release bundle или хотя бы image tag + `.tar`;
5. application logs за период инцидента, если идёт расследование ошибки.

Если нужно быстро восстановиться после проблемы, на практике самые важные сущности — это:

- дамп БД;
- production `.env`;
- production secrets;
- docker image конкретного релиза.

## 9. Production release и rollback

Рекомендуемый путь выкатки нового релиза на сервере:

`bash
cd /opt/ldpr-activist/releases/<tag>
chmod 600 .env.production
chmod 600 secrets/service-account.json
./scripts/release-deploy.prod.sh
`

Что делает этот сценарий:

1. загружает docker image из `.tar`;
2. делает pre-deploy backup текущей production БД;
3. поднимает новый stack **без удаления named volumes**;
4. проверяет health endpoint.

Rollback выполняется так же просто: нужно перейти в каталог предыдущего релиза и запустить тот же скрипт:

`bash
cd /opt/ldpr-activist/releases/<previous-tag>
./scripts/release-deploy.prod.sh
`

Подробный runbook см. в `doc/production-release-runbook.md`.