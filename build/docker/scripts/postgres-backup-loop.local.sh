#!/bin/sh
set -eu

INTERVAL_SECONDS="${BACKUP_INTERVAL_SECONDS:-86400}"

trap 'exit 0' TERM INT

/bin/sh -c "tr -d '\r' < /scripts/postgres-backup.local.sh | /bin/sh"

while true
do
  sleep "${INTERVAL_SECONDS}"
  /bin/sh -c "tr -d '\r' < /scripts/postgres-backup.local.sh | /bin/sh"
done