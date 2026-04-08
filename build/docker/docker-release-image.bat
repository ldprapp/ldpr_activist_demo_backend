@echo off
setlocal EnableExtensions

pushd "%~dp0" || exit /b 1

set "ENV_FILE=.env.production"
set "FIREBASE_SECRET_SOURCE=secrets\service-account.json"
set "FIREBASE_SECRET_TARGET="

if not exist "%ENV_FILE%" (
  echo "%CD%\%ENV_FILE%" was not found.
  echo Create it from ".env.production.template" and fill real production values.
  popd
  exit /b 1
)

for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%ENV_FILE%") do (
  if not "%%A"=="" set "%%A=%%B"
)

if not "%~1"=="" (
  set "API_IMAGE_TAG=%~1"
)

if "%API_IMAGE_NAME%"=="" (
  echo API_IMAGE_NAME is empty in %ENV_FILE%
  popd
  exit /b 1
)

if "%API_IMAGE_TAG%"=="" (
  echo API_IMAGE_TAG is empty in %ENV_FILE%
  popd
  exit /b 1
)

if "%DOTNET_VERSION%"=="" (
  echo DOTNET_VERSION is empty in %ENV_FILE%
  popd
  exit /b 1
)

if "%APP_PROJECT%"=="" (
  echo APP_PROJECT is empty in %ENV_FILE%
  popd
  exit /b 1
)

set "IMAGE_REF=%API_IMAGE_NAME%:%API_IMAGE_TAG%"
set "RELEASE_ROOT=.release"
set "RELEASE_DIR=%RELEASE_ROOT%\%API_IMAGE_TAG%"
set "IMAGE_TAR_FILE=%RELEASE_DIR%\%API_IMAGE_NAME%_%API_IMAGE_TAG%.tar"
set "FIREBASE_SECRET_TARGET=%RELEASE_DIR%\secrets\service-account.json"

if not exist "%RELEASE_DIR%" mkdir "%RELEASE_DIR%" >nul 2>&1
if not exist "%RELEASE_DIR%\nginx" mkdir "%RELEASE_DIR%\nginx" >nul 2>&1
if not exist "%RELEASE_DIR%\scripts" mkdir "%RELEASE_DIR%\scripts" >nul 2>&1
if not exist "%RELEASE_DIR%\secrets" mkdir "%RELEASE_DIR%\secrets" >nul 2>&1

if not defined FIREBASE_PUSH_ENABLED (
  set "FIREBASE_PUSH_ENABLED=false"
)

if /I "%FIREBASE_PUSH_ENABLED%"=="true" (
  if not exist "%FIREBASE_SECRET_SOURCE%" (
    echo Firebase push is enabled, but "%CD%\%FIREBASE_SECRET_SOURCE%" was not found.
    echo Put production Firebase service-account.json there and run again.
    popd
    exit /b 1
  )
)

echo [ldpr_activist_demo][release] Building production image "%IMAGE_REF%"...
docker build -f "Dockerfile" --build-arg DOTNET_VERSION=%DOTNET_VERSION% --build-arg APP_PROJECT=%APP_PROJECT% -t "%IMAGE_REF%" ../..
if errorlevel 1 (
  echo Failed to build production image.
  popd
  exit /b 1
)

echo [ldpr_activist_demo][release] Saving image to "%IMAGE_TAR_FILE%"...
docker save -o "%IMAGE_TAR_FILE%" "%IMAGE_REF%"
if errorlevel 1 (
  echo Failed to save image tar.
  popd
  exit /b 1
)

copy /y "docker-compose.yml" "%RELEASE_DIR%\docker-compose.yml" >nul
copy /y "docker-compose.prod.yml" "%RELEASE_DIR%\docker-compose.prod.yml" >nul
copy /y ".env.production" "%RELEASE_DIR%\.env.production" >nul
copy /y ".env.production.template" "%RELEASE_DIR%\.env.production.template" >nul
copy /y "nginx\default.conf" "%RELEASE_DIR%\nginx\default.conf" >nul
xcopy /y /i "scripts\*.sh" "%RELEASE_DIR%\scripts\" >nul

echo [ldpr_activist_demo][release] Synchronizing copied production env files with resolved image reference...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "$releaseDir = Join-Path (Get-Location) '%RELEASE_DIR%';" ^
  "$envFiles = @(" ^
  "  (Join-Path $releaseDir '.env.production')," ^
  "  (Join-Path $releaseDir '.env.production.template')" ^
  ");" ^
  "$enc = New-Object System.Text.UTF8Encoding($false);" ^
  "foreach ($file in $envFiles) {" ^
  "  $content = [System.IO.File]::ReadAllText($file);" ^
  "  $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^API_IMAGE_NAME=.*$', 'API_IMAGE_NAME=%API_IMAGE_NAME%');" ^
  "  $content = [System.Text.RegularExpressions.Regex]::Replace($content, '(?m)^API_IMAGE_TAG=.*$', 'API_IMAGE_TAG=%API_IMAGE_TAG%');" ^
  "  $content = $content -replace \"`r`n\", \"`n\";" ^
  "  $content = $content -replace \"`r\", \"`n\";" ^
  "  [System.IO.File]::WriteAllText($file, $content, $enc);" ^
  "}"
if errorlevel 1 (
  echo Failed to synchronize copied env files.
  popd
  exit /b 1
)

echo [ldpr_activist_demo][release] Normalizing Linux text files to UTF-8 without BOM and LF line endings...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "$releaseDir = Join-Path (Get-Location) '%RELEASE_DIR%';" ^
  "$files = @(" ^
  "  (Join-Path $releaseDir 'docker-compose.yml')," ^
  "  (Join-Path $releaseDir 'docker-compose.prod.yml')," ^
  "  (Join-Path $releaseDir '.env.production')," ^
  "  (Join-Path $releaseDir '.env.production.template')," ^
  "  (Join-Path $releaseDir 'nginx\default.conf')" ^
  ");" ^
  "$files += Get-ChildItem -Path (Join-Path $releaseDir 'scripts') -Filter '*.sh' | Select-Object -ExpandProperty FullName;" ^
  "$enc = New-Object System.Text.UTF8Encoding($false);" ^
  "foreach ($file in $files) {" ^
  "  $content = [System.IO.File]::ReadAllText($file);" ^
  "  $content = $content -replace \"`r`n\", \"`n\";" ^
  "  $content = $content -replace \"`r\", \"`n\";" ^
  "  [System.IO.File]::WriteAllText($file, $content, $enc);" ^
  "}"
if errorlevel 1 (
  echo Failed to normalize Linux text files in release bundle.
  popd
  exit /b 1
)

echo [ldpr_activist_demo][release] Done.
echo Release bundle:
echo   %CD%\%RELEASE_DIR%
echo.
echo Image tar:
echo   %CD%\%IMAGE_TAR_FILE%
echo.
if exist "%FIREBASE_SECRET_SOURCE%" (
  copy /y "%FIREBASE_SECRET_SOURCE%" "%FIREBASE_SECRET_TARGET%" >nul
  if errorlevel 1 (
    echo Failed to copy Firebase service-account.json into release bundle.
    popd
    exit /b 1
  )
)

if exist "%FIREBASE_SECRET_TARGET%" (
  echo Firebase service-account.json copied to:
  echo   %CD%\%FIREBASE_SECRET_TARGET%
  echo.
) else (
  echo Firebase service-account.json was not copied because source file was not found.
  echo.
)
echo Production docker compose on server must use:
echo   --env-file .env.production
echo   -f docker-compose.yml -f docker-compose.prod.yml

popd