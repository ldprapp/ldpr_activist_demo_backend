#!/bin/sh
set -eu

INTERVAL_SECONDS="${BACKUP_INTERVAL_SECONDS:-86400}"

trap 'exit 0' TERM INT

/bin/sh /scripts/postgres-backup.sh

while true
do
  sleep "${INTERVAL_SECONDS}"
  /bin/sh /scripts/postgres-backup.sh
done