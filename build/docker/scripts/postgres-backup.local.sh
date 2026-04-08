#!/bin/sh
set -eu

export BACKUP_ENVIRONMENT_NAME="${BACKUP_ENVIRONMENT_NAME:-local}"
exec /bin/sh -c "tr -d '\r' < /scripts/postgres-backup.run.sh | /bin/sh"