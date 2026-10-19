<#
.SYNOPSIS
  Restores ShippingERP from a SQL Server .bak file (and optionally an uploads .tar.gz) into the Docker stack.
.DESCRIPTION
  1. Stops the API and the web app (no writes during the restore).
  2. Copies the .bak into the "backups" volume and runs RESTORE DATABASE ... WITH REPLACE.
  3. Optionally replaces the uploads volume with the archive.
  4. Starts the API (migrations newer than the backup are applied at start-up) and the web app.
  THE CURRENT DATABASE IS OVERWRITTEN. Take a backup first (scripts\backup-now.ps1).
.EXAMPLE
  .\scripts\restore.ps1 -BackupFile D:\Backups\ShippingERP-20261015-023000.bak -UploadsArchive D:\Backups\uploads-20261015-023000.tar.gz
#>
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [string]$UploadsArchive,
    [string]$Database = 'ShippingERP'
)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Test-Path $BackupFile)) { throw "Not found: $BackupFile" }
$answer = Read-Host "The database '$Database' will be REPLACED by $BackupFile. Type YES to continue"
if ($answer -ne 'YES') { Write-Host 'Cancelled.'; exit 1 }

$envFile = Get-Content .env | Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' }
$saPassword = ($envFile -replace "^MSSQL_SA_PASSWORD=", '').Trim("'", '"')

Write-Host 'Stopping api and frontend...' -ForegroundColor Cyan
docker compose stop frontend api

$name = Split-Path $BackupFile -Leaf
$folder = (Resolve-Path (Split-Path $BackupFile -Parent)).Path
docker run --rm -v shippingerp_backups:/backups -v "${folder}:/in:ro" alpine:3.20 `
    sh -c "mkdir -p /backups/restore && cp '/in/$name' /backups/restore/ && chown -R 10001:0 /backups/restore"

$sql = @"
IF DB_ID('$Database') IS NOT NULL ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Database] FROM DISK = N'/var/opt/mssql/backup/restore/$name' WITH REPLACE, CHECKSUM, STATS = 10;
ALTER DATABASE [$Database] SET MULTI_USER;
"@
Write-Host 'Restoring...' -ForegroundColor Cyan
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $saPassword -C -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw 'RESTORE failed. Check the message above; the old database is unchanged if the restore did not start.' }

if ($UploadsArchive) {
    if (-not (Test-Path $UploadsArchive)) { throw "Not found: $UploadsArchive" }
    $uName = Split-Path $UploadsArchive -Leaf
    $uFolder = (Resolve-Path (Split-Path $UploadsArchive -Parent)).Path
    Write-Host 'Restoring uploaded files...' -ForegroundColor Cyan
    docker run --rm -v shippingerp_api-uploads:/uploads -v "${uFolder}:/in:ro" alpine:3.20 `
        sh -c "find /uploads -mindepth 1 -delete && tar -xzf '/in/$uName' -C /uploads && chown -R 1654:1654 /uploads"
}

Write-Host 'Starting api and frontend...' -ForegroundColor Cyan
docker compose up -d api frontend
Write-Host 'Restore finished. Check: http://localhost:5080/health/ready and sign in.' -ForegroundColor Green
