@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

if exist ".env" (
  for /f "usebackq eol=# tokens=1,* delims==" %%A in (".env") do (
    if not "%%A"=="" set "%%A=%%B"
  )
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

echo [ldpr_activist_demo] Starting...
docker compose --env-file ".env" -f "docker-compose.yml" up -d postgres redis
if errorlevel 1 (
  echo Failed to start infrastructure.
  popd
  exit /b 1
)

docker compose --env-file ".env" -f "docker-compose.yml" up -d --build api
if errorlevel 1 (
  echo Failed to start.
  popd
  exit /b 1
)

if not defined API_PORT (
  for /f "usebackq delims=" %%P in (`docker compose --env-file ".env" -f "docker-compose.yml" port api 8080 2^>nul`) do (
    set "API_PORT=%%P"
  )
  for /f "tokens=2 delims=:" %%A in ("%API_PORT%") do set "API_PORT=%%A"
)

echo Done. API should be available on http://localhost:%API_PORT%
popd
