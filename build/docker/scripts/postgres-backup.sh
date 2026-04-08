#!/bin/sh
set -eu

exec /bin/sh -c "tr -d '\r' < /scripts/postgres-backup.local.sh | /bin/sh"