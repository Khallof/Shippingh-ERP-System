<#
.SYNOPSIS
  Runs one ShippingERP backup now (SQL Server full backup + uploads archive) and copies it to a Windows folder.
.EXAMPLE
  .\scripts\backup-now.ps1 -Destination D:\ShippingERP-Backups
#>
param(
    [string]$Destination = "$PSScriptRoot\..\backups"
)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

Write-Host 'Running the backup container once...' -ForegroundColor Cyan
docker compose --profile backup run --rm --entrypoint /bin/sh backup /scripts/backup.sh now
if ($LASTEXITCODE -ne 0) { throw 'Backup failed.' }

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$Destination = (Resolve-Path $Destination).Path

# Copy the newest files out of the "backups" volume (off-site copy: put this folder on another disk / cloud).
docker run --rm -v shippingerp_backups:/backups -v "${Destination}:/out" alpine:3.20 `
    sh -c 'cp -u /backups/db/*.bak /out/ 2>/dev/null; cp -u /backups/uploads/*.tar.gz /out/ 2>/dev/null; ls -lh /out | tail -n 6'

Write-Host "Backups copied to $Destination" -ForegroundColor Green
