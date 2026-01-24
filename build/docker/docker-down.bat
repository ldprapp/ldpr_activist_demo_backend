@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

echo [ldpr_activist_demo] Stopping...
docker compose -f "docker-compose.yml" down
if errorlevel 1 (
  echo Failed to stop.
  popd
  exit /b 1
)

echo Done.
popd
