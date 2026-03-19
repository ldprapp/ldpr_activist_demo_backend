@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

echo [ldpr_activist_demo] Cleaning (containers, networks, volumes, local images)...
docker compose -f "docker-compose.yml" down --remove-orphans --volumes --rmi local
if errorlevel 1 (
  echo Failed to clean.
  popd
  exit /b 1
)

if exist ".runtime" (
  rmdir /s /q ".runtime"
)

echo Done.
popd
