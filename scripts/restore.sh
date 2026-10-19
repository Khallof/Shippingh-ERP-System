#!/bin/sh
# Linux / macOS version of scripts/restore.ps1.
# Usage: ./scripts/restore.sh /path/ShippingERP-YYYYMMDD-HHMMSS.bak [/path/uploads-YYYYMMDD-HHMMSS.tar.gz]
set -eu
cd "$(dirname "$0")/.."
BAK="$1"; UPLOADS="${2:-}"; DB=ShippingERP
[ -f "$BAK" ] || { echo "Not found: $BAK"; exit 1; }
printf "The database %s will be REPLACED. Type YES: " "$DB"; read -r answer; [ "$answer" = "YES" ] || exit 1
SA=$(grep '^MSSQL_SA_PASSWORD=' .env | cut -d= -f2- | sed "s/^'//; s/'$//")
docker compose stop frontend api
NAME=$(basename "$BAK"); DIR=$(cd "$(dirname "$BAK")" && pwd)
docker run --rm -v shippingerp_backups:/backups -v "$DIR:/in:ro" alpine:3.20 \
  sh -c "mkdir -p /backups/restore && cp '/in/$NAME' /backups/restore/ && chown -R 10001:0 /backups/restore"
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA" -C -b -Q \
  "IF DB_ID('$DB') IS NOT NULL ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
   RESTORE DATABASE [$DB] FROM DISK = N'/var/opt/mssql/backup/restore/$NAME' WITH REPLACE, CHECKSUM, STATS = 10;
   ALTER DATABASE [$DB] SET MULTI_USER;"
if [ -n "$UPLOADS" ]; then
  UN=$(basename "$UPLOADS"); UD=$(cd "$(dirname "$UPLOADS")" && pwd)
  docker run --rm -v shippingerp_api-uploads:/uploads -v "$UD:/in:ro" alpine:3.20 \
    sh -c "find /uploads -mindepth 1 -delete && tar -xzf '/in/$UN' -C /uploads && chown -R 1654:1654 /uploads"
fi
docker compose up -d api frontend
echo "Restore finished. Check http://localhost:5080/health/ready"
