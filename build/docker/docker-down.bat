@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

set "ENV_FILE=.env.local"
set "COMPOSE_FILES=-f docker-compose.yml -f docker-compose.local.yml"

if not exist "%ENV_FILE%" (
  echo "%CD%\%ENV_FILE%" was not found.
  echo Create it from ".env.local.template" and run again.
  popd
  exit /b 1
)

echo [ldpr_activist_demo][local] Stopping all services (including postgres-backup) without removing containers...
docker compose --env-file "%ENV_FILE%" %COMPOSE_FILES% stop
if errorlevel 1 (
  echo Failed to stop.
  popd
  exit /b 1
)

echo Done.
popd
