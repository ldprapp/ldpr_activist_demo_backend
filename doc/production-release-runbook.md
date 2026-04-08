# Production release runbook для `ldpr_activist_demo`

Этот документ описывает безопасную выкатку нового production release **без потери текущей PostgreSQL базы данных**.

---

## 1. Почему база данных сохраняется между релизами

Текущая production БД живёт в docker named volume:

```text
pg_data
```

А compose-проект зафиксирован через:

```yaml
name: ldpr_activist_demo
```

Из этого следуют два важных свойства:

1. release-каталог можно менять;
2. PostgreSQL volume при этом остаётся тем же самым, пока оператор не удаляет его вручную.

Именно поэтому новый release нужно выкатывать через:

```bash
docker compose up -d --remove-orphans
```

и нельзя использовать для обычного deploy:

```bash
docker compose down --volumes
```

---

## 2. Что подготовить до выкладки

На сервере должен появиться новый release bundle, например:

```text
/opt/ldpr-activist/releases/prod-2026-04-06-01
```

Внутри него должны быть:

- `docker-compose.yml`
- `docker-compose.prod.yml`
- `.env.production`
- `secrets/service-account.json`
- `nginx/default.conf`
- `scripts/*.sh`
- один docker image tar-файл `*.tar`

Перед запуском желательно ограничить права:

```bash
cd /opt/ldpr-activist/releases/<tag>
chmod 600 .env.production
chmod 600 secrets/service-account.json
```

---

## 3. Безопасная выкатка

Основная команда:

```bash
cd /opt/ldpr-activist/releases/<tag>
./scripts/release-deploy.prod.sh
```

Скрипт делает следующее:

1. загружает docker image из `.tar`;
2. запускает одноразовый pre-deploy backup текущей production БД;
3. поднимает новый stack без удаления named volumes;
4. выполняет health-check `http://127.0.0.1:<API_PORT>/api/v1/health`.

---

## 4. Если нужен только backup

Можно отдельно выполнить одноразовый backup:

```bash
cd /opt/ldpr-activist/releases/<tag>
./scripts/release-postgres-backup.prod.sh
```

Это полезно:

- перед risky-операциями;
- перед ручным изменением `.env.production`;
- перед откатом;
- перед schema migration.

---

## 5. Rollback

Если новый release не прошёл health-check или сломал приложение, rollback делается запуском того же deploy script из предыдущего release-каталога:

```bash
cd /opt/ldpr-activist/releases/<previous-tag>
./scripts/release-deploy.prod.sh
```

Так как БД хранится в том же самом named volume, rollback возвращает предыдущую версию приложения поверх той же базы.

---

## 6. Важное замечание про EF Core migrations

В текущем production `.env.production` установлено:

```text
DATABASE_AUTO_MIGRATE=false
```

Это значит:

- безопасный redeploy **сохраняет** БД;
- но schema changes **не применяются автоматически**.

Если новый release содержит миграции схемы БД, их нужно выполнять отдельным контролируемым шагом.

---

## 7. Что нельзя делать на production при обычном релизе

Нельзя:

- выполнять `docker compose down --volumes`;
- удалять volume `pg_data` вручную;
- запускать локальный destructive cleanup-скрипт по аналогии с `docker-clean.bat`;
- выкатывать release без backup, если это не совсем тривиальная замена.