#!/bin/sh
set -eu

ENVIRONMENT_NAME="${BACKUP_ENVIRONMENT_NAME:-unknown}"
BACKUP_ROOT="${BACKUP_ROOT_PATH_CONTAINER:-/var/backups/postgres}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-7}"
FILE_PREFIX="${BACKUP_FILE_PREFIX:-postgres}"

DAILY_DIR="${BACKUP_ROOT}/daily"
GLOBALS_DIR="${BACKUP_ROOT}/globals"

mkdir -p "${DAILY_DIR}" "${GLOBALS_DIR}"

TIMESTAMP="$(date -u +%Y-%m-%dT%H-%M-%SZ)"

DB_DUMP_TMP="${DAILY_DIR}/${FILE_PREFIX}_${TIMESTAMP}.dump.tmp"
DB_DUMP_FINAL="${DAILY_DIR}/${FILE_PREFIX}_${TIMESTAMP}.dump"

GLOBALS_DUMP_TMP="${GLOBALS_DIR}/${FILE_PREFIX}_globals_${TIMESTAMP}.sql.tmp"
GLOBALS_DUMP_FINAL="${GLOBALS_DIR}/${FILE_PREFIX}_globals_${TIMESTAMP}.sql"

echo "[$(date -u +%FT%TZ)] [${ENVIRONMENT_NAME}] Starting PostgreSQL backup..."

pg_dump \
  --host="${PGHOST}" \
  --port="${PGPORT}" \
  --username="${PGUSER}" \
  --dbname="${PGDATABASE}" \
  --format=custom \
  --file="${DB_DUMP_TMP}"

pg_dumpall \
  --host="${PGHOST}" \
  --port="${PGPORT}" \
  --username="${PGUSER}" \
  --globals-only \
  > "${GLOBALS_DUMP_TMP}"

mv "${DB_DUMP_TMP}" "${DB_DUMP_FINAL}"
mv "${GLOBALS_DUMP_TMP}" "${GLOBALS_DUMP_FINAL}"

find "${DAILY_DIR}" -type f -name '*.dump' -mtime +"${RETENTION_DAYS}" -delete
find "${GLOBALS_DIR}" -type f -name '*.sql' -mtime +"${RETENTION_DAYS}" -delete

echo "[$(date -u +%FT%TZ)] [${ENVIRONMENT_NAME}] Backup completed."
echo "[$(date -u +%FT%TZ)] [${ENVIRONMENT_NAME}] DB dump: ${DB_DUMP_FINAL}"
echo "[$(date -u +%FT%TZ)] [${ENVIRONMENT_NAME}] Globals dump: ${GLOBALS_DUMP_FINAL}"