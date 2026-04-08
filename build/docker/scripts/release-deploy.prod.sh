#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd "$(dirname "$0")" && pwd)"
RELEASE_DIR="$(CDPATH= cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${RELEASE_DIR}/.env.production"
HEALTH_ENDPOINT="/health"
VERSION_ENDPOINT="/version"

compose() {
  docker compose \
    --env-file "$ENV_FILE" \
    -f "${RELEASE_DIR}/docker-compose.yml" \
    -f "${RELEASE_DIR}/docker-compose.prod.yml" \
    "$@"
}

read_env_value() {
  awk -F= -v key="$1" '
    $0 ~ "^[[:space:]]*" key "=" {
      sub(/^[^=]*=/, "", $0)
      print $0
      exit
    }
  ' "$ENV_FILE"
}

require_file() {
  if [ ! -f "$1" ]; then
    echo "\"$1\" was not found."
    exit 1
  fi
}

require_file "$ENV_FILE"
require_file "${RELEASE_DIR}/docker-compose.yml"
require_file "${RELEASE_DIR}/docker-compose.prod.yml"

TAR_COUNT="$(find "$RELEASE_DIR" -maxdepth 1 -type f -name '*.tar' | wc -l | tr -d ' ')"
if [ "$TAR_COUNT" -ne 1 ]; then
  echo "Expected exactly one image tar in \"$RELEASE_DIR\", found $TAR_COUNT."
  exit 1
fi

IMAGE_TAR_FILE="$(find "$RELEASE_DIR" -maxdepth 1 -type f -name '*.tar' | head -n 1)"
API_PORT="$(read_env_value API_PORT)"
HTTPS_PORT="$(read_env_value HTTPS_PORT)"
PUBLIC_BASE_URL="$(read_env_value PUBLIC_BASE_URL)"
FIREBASE_PUSH_ENABLED="$(read_env_value FIREBASE_PUSH_ENABLED)"
DATABASE_AUTO_MIGRATE="$(read_env_value DATABASE_AUTO_MIGRATE)"

if [ -z "${API_PORT}" ]; then
  API_PORT="80"
fi

if [ -z "${HTTPS_PORT}" ]; then
  HTTPS_PORT="443"
fi

if [ -z "${PUBLIC_BASE_URL}" ]; then
  PUBLIC_BASE_URL="https://aktivist.pro"
fi

PUBLIC_BASE_URL="${PUBLIC_BASE_URL%/}"

if [ "${FIREBASE_PUSH_ENABLED:-}" = "true" ] && [ ! -f "${RELEASE_DIR}/secrets/service-account.json" ]; then
  echo "Firebase push is enabled, but \"${RELEASE_DIR}/secrets/service-account.json\" was not found."
  exit 1
fi

if [ -f "${RELEASE_DIR}/secrets/service-account.json" ]; then
  chmod 600 "${RELEASE_DIR}/secrets/service-account.json" || true
fi

chmod 600 "$ENV_FILE" || true

echo "[ldpr_activist_demo][prod] Loading image from \"$IMAGE_TAR_FILE\"..."
docker load -i "$IMAGE_TAR_FILE"

echo "[ldpr_activist_demo][prod] Creating pre-deploy backup..."
"${SCRIPT_DIR}/release-postgres-backup.prod.sh"

if [ "${DATABASE_AUTO_MIGRATE:-false}" != "true" ]; then
  echo "[ldpr_activist_demo][prod] DATABASE_AUTO_MIGRATE=${DATABASE_AUTO_MIGRATE:-false}."
  echo "[ldpr_activist_demo][prod] If this release contains schema changes, run migrations separately before switching traffic."
fi

echo "[ldpr_activist_demo][prod] Starting updated stack without removing volumes..."
compose up -d --remove-orphans postgres redis postgres-backup api nginx

echo "[ldpr_activist_demo][prod] Stack status:"
compose ps

if command -v curl >/dev/null 2>&1; then
  HEALTH_URL="${PUBLIC_BASE_URL}${HEALTH_ENDPOINT}"
  VERSION_URL="${PUBLIC_BASE_URL}${VERSION_ENDPOINT}"
  ATTEMPT=1
  MAX_ATTEMPTS=30

  echo "[ldpr_activist_demo][prod] Waiting for health endpoint: ${HEALTH_URL}"
  echo "[ldpr_activist_demo][prod] Waiting for version endpoint: ${VERSION_URL}"
  while [ "$ATTEMPT" -le "$MAX_ATTEMPTS" ]; do
    if curl -fsS "$HEALTH_URL" >/dev/null 2>&1; then
      VERSION_RESPONSE="$(curl -fsS "$VERSION_URL" 2>/dev/null || true)"
      if [ -n "${VERSION_RESPONSE}" ]; then
        echo "[ldpr_activist_demo][prod] Health check passed."
        echo "[ldpr_activist_demo][prod] Backend version response: ${VERSION_RESPONSE}"
        echo "[ldpr_activist_demo][prod] Release deployed successfully."
        echo "[ldpr_activist_demo][prod] Rollback path: cd into the previous release directory and run ./scripts/release-deploy.prod.sh there."
        exit 0
      fi
    fi

    sleep 2
    ATTEMPT=$((ATTEMPT + 1))
  done

  echo "[ldpr_activist_demo][prod] Health/version check failed."
  echo "[ldpr_activist_demo][prod] Health URL: ${HEALTH_URL}"
  echo "[ldpr_activist_demo][prod] Version URL: ${VERSION_URL}"
  echo "[ldpr_activist_demo][prod] Rollback path: cd into the previous release directory and run ./scripts/release-deploy.prod.sh there."
  exit 1
fi

echo "[ldpr_activist_demo][prod] curl is not installed. Skipping automatic health check."
echo "[ldpr_activist_demo][prod] Verify manually:"
echo "  docker compose --env-file .env.production -f docker-compose.yml -f docker-compose.prod.yml ps"
echo "  curl -i ${PUBLIC_BASE_URL}${HEALTH_ENDPOINT}"
echo "  curl -i ${PUBLIC_BASE_URL}${VERSION_ENDPOINT}"
echo "[ldpr_activist_demo][prod] Rollback path: cd into the previous release directory and run ./scripts/release-deploy.prod.sh there."