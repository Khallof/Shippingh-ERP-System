#!/bin/sh
# ShippingERP scheduled backup (runs inside the "backup" container, profile "backup").
#  - Full SQL Server backup (COMPRESSION, CHECKSUM) written by SQL Server into the shared "backups" volume.
#  - RESTORE VERIFYONLY on the new file.
#  - tar.gz of the uploads volume (proof-of-delivery signatures / photos, shipment attachments).
#  - Deletes backups older than BACKUP_RETENTION_DAYS.
# Schedule: every day at BACKUP_TIME (HH:MM, container time zone TZ). Set BACKUP_RUN_ON_START=true for an immediate run.
set -eu

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
DB="${DB_NAME:-ShippingERP}"
DIR=/backups                      # this container
SERVER_DIR=/var/opt/mssql/backup  # the same volume inside the sqlserver container
RETENTION="${BACKUP_RETENTION_DAYS:-14}"
TIME="${BACKUP_TIME:-02:30}"

run_backup() {
  stamp=$(date +%Y%m%d-%H%M%S)
  name="${DB}-${stamp}.bak"
  file="$SERVER_DIR/db/$name"
  mkdir -p "$DIR/db" "$DIR/uploads"
  # SQL Server runs as user mssql (10001) and writes the .bak file itself.
  chown 10001:0 "$DIR/db"
  echo "[$(date -Iseconds)] backup $DB -> $DIR/db/$name"
  "$SQLCMD" -S "${SQL_HOST:-sqlserver},1433" -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q \
    "BACKUP DATABASE [$DB] TO DISK = N'$file' WITH COMPRESSION, CHECKSUM, INIT, STATS = 25;
     RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM;"
  echo "[$(date -Iseconds)] uploads -> $DIR/uploads/uploads-${stamp}.tar.gz"
  tar -czf "$DIR/uploads/uploads-${stamp}.tar.gz" -C /uploads .
  find "$DIR/db" -name '*.bak' -mtime +"$RETENTION" -print -delete
  find "$DIR/uploads" -name '*.tar.gz' -mtime +"$RETENTION" -print -delete
  echo "[$(date -Iseconds)] done"
}

if [ "${1:-}" = "now" ]; then
  run_backup
  exit 0
fi

[ "${BACKUP_RUN_ON_START:-false}" = "true" ] && run_backup

while true; do
  now=$(date +%s)
  next=$(date -d "$(date +%F) $TIME" +%s 2>/dev/null || date -d "$TIME" +%s)
  [ "$next" -le "$now" ] && next=$((next + 86400))
  echo "[$(date -Iseconds)] next backup at $(date -d "@$next" -Iseconds)"
  sleep $((next - now))
  run_backup || echo "[$(date -Iseconds)] BACKUP FAILED" >&2
done
