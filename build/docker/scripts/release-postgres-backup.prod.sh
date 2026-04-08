#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd "$(dirname "$0")" && pwd)"
RELEASE_DIR="$(CDPATH= cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${RELEASE_DIR}/.env.production"

compose() {
  docker compose \
    --env-file "$ENV_FILE" \
    -f "${RELEASE_DIR}/docker-compose.yml" \
    -f "${RELEASE_DIR}/docker-compose.prod.yml" \
    "$@"
}

if [ ! -f "$ENV_FILE" ]; then
  echo "\"$ENV_FILE\" was not found."
  exit 1
fi

if [ ! -f "${RELEASE_DIR}/docker-compose.yml" ]; then
  echo "\"${RELEASE_DIR}/docker-compose.yml\" was not found."
  exit 1
fi

if [ ! -f "${RELEASE_DIR}/docker-compose.prod.yml" ]; then
  echo "\"${RELEASE_DIR}/docker-compose.prod.yml\" was not found."
  exit 1
fi

echo "[ldpr_activist_demo][prod] Ensuring PostgreSQL is running..."
compose up -d postgres

echo "[ldpr_activist_demo][prod] Running one-off PostgreSQL backup..."
compose run --rm --no-deps --entrypoint /bin/sh postgres-backup -c "tr -d '\r' < /scripts/postgres-backup.prod.sh | /bin/sh"

echo "[ldpr_activist_demo][prod] Backup completed."