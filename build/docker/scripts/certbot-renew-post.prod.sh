#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd "$(dirname "$0")" && pwd)"
RELEASE_DIR="$(CDPATH= cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${RELEASE_DIR}/.env.production"

docker compose \
  --env-file "$ENV_FILE" \
  -f "${RELEASE_DIR}/docker-compose.yml" \
  -f "${RELEASE_DIR}/docker-compose.prod.yml" \
  up -d nginx