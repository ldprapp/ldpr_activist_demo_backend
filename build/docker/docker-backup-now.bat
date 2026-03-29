@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

echo [ldpr_activist_demo] Running one-off PostgreSQL backup...
docker compose --env-file ".env" -f "docker-compose.yml" exec postgres-backup /bin/sh /scripts/postgres-backup.sh
if errorlevel 1 (
  echo Backup failed.
  popd
  exit /b 1
)

echo Backup completed.
popd