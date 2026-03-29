@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

echo [ldpr_activist_demo] Stopping all services (including postgres-backup) without removing containers...
docker compose --env-file ".env" -f "docker-compose.yml" stop
if errorlevel 1 (
  echo Failed to stop.
  popd
  exit /b 1
)

echo Done.
popd
