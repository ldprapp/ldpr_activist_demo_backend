@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

set "RESET_PASSWORD=LDPR_RESET_2026"

echo.
echo [ldpr_activist_demo] WARNING: full docker reset requested.
echo This will remove:
echo   - all project containers
echo   - project network
echo   - ALL docker compose volumes ^(including PostgreSQL and Redis data^)
echo   - locally built images
echo   - local ".runtime" directory
echo.

choice /C YN /N /M "Are you sure you want to continue? [Y/N]: "
if errorlevel 2 (
  echo Cancelled.
  popd
  exit /b 0
)

set "RESET_PASSWORD_INPUT="
set /p "RESET_PASSWORD_INPUT=Enter reset password: "

if not "%RESET_PASSWORD_INPUT%"=="%RESET_PASSWORD%" (
  echo Invalid password. Cleanup cancelled.
  popd
  exit /b 1
)

echo [ldpr_activist_demo] Cleaning (containers, networks, volumes, local images)...
docker compose --env-file ".env" -f "docker-compose.yml" down --remove-orphans --volumes --rmi local
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
