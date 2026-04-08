@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

set "ENV_FILE=.env.production"
set "COMPOSE_FILES=-f docker-compose.yml -f docker-compose.prod.yml"

if not exist "%ENV_FILE%" (
  echo "%CD%\%ENV_FILE%" was not found.
  echo Create it from ".env.production.template" and fill real production values.
  popd
  exit /b 1
)

for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%ENV_FILE%") do (
  if not "%%A"=="" set "%%A=%%B"
)

if not defined POSTGRES_BACKUP_SCRIPT_PATH (
  set "POSTGRES_BACKUP_SCRIPT_PATH=/scripts/postgres-backup.prod.sh"
)

echo [ldpr_activist_demo][prod] Running one-off PostgreSQL backup...
docker compose --env-file "%ENV_FILE%" %COMPOSE_FILES% exec postgres-backup /bin/sh -c "tr -d '\r' ^< \"%POSTGRES_BACKUP_SCRIPT_PATH%\" ^| /bin/sh"
if errorlevel 1 (
  echo Backup failed.
  popd
  exit /b 1
)

echo Backup completed.
popd