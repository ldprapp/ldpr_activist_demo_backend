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

for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%ENV_FILE%") do (
  if not "%%A"=="" set "%%A=%%B"
)

docker --version >nul 2>&1
if errorlevel 1 (
  echo Docker is not installed or not available in PATH.
  popd
  exit /b 1
)

docker compose version >nul 2>&1
if errorlevel 1 (
  echo Docker Compose v2 is not available. Update Docker Desktop / Docker Engine.
  popd
  exit /b 1
)

if /I "%STRUCTURED_LOGGING_FILES_ENABLED%"=="true" if defined STRUCTURED_LOGGING_FILES_ROOT_PATH_HOST (
  if not exist "%STRUCTURED_LOGGING_FILES_ROOT_PATH_HOST%" mkdir "%STRUCTURED_LOGGING_FILES_ROOT_PATH_HOST%" >nul 2>&1
)

if defined DB_BACKUP_ROOT_PATH_HOST (
  if not exist "%DB_BACKUP_ROOT_PATH_HOST%" mkdir "%DB_BACKUP_ROOT_PATH_HOST%" >nul 2>&1
)

if not exist "secrets" (
  mkdir "secrets" >nul 2>&1
)

if not defined FIREBASE_PUSH_ENABLED (
  set "FIREBASE_PUSH_ENABLED=false"
)

echo Firebase push enabled: %FIREBASE_PUSH_ENABLED%

if /I "%FIREBASE_PUSH_ENABLED%"=="true" (
  if not exist "secrets\service-account.json" (
    echo Firebase push is enabled, but "%CD%\secrets\service-account.json" was not found.
    echo Put your Firebase service account json there and run again.
    popd
    exit /b 1
  )
  if "%FIREBASE_PUSH_PROJECT_ID%"=="" (
    echo Firebase push is enabled, but FIREBASE_PUSH_PROJECT_ID is empty in %ENV_FILE%
    popd
    exit /b 1
  )
)

echo [ldpr_activist_demo][local] Starting...
docker compose --env-file "%ENV_FILE%" %COMPOSE_FILES% up -d postgres redis postgres-backup
if errorlevel 1 (
  echo Failed to start infrastructure.
  popd
  exit /b 1
)

docker compose --env-file "%ENV_FILE%" %COMPOSE_FILES% up -d --build api nginx
if errorlevel 1 (
  echo Failed to start.
  popd
  exit /b 1
)

if not defined API_PORT (
  for /f "usebackq delims=" %%P in (`docker compose --env-file "%ENV_FILE%" %COMPOSE_FILES% port nginx 80 2^>nul`) do (
    set "API_PORT=%%P"
  )
  for /f "tokens=2 delims=:" %%A in ("%API_PORT%") do set "API_PORT=%%A"
)

echo Done. Local API should be available on http://localhost:%API_PORT% via nginx reverse proxy.
popd
