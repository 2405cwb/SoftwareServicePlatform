$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$legacyModel = Join-Path $root "backend\SoftwareServicePlatform.Api\Models\ClientUpdateCredential.cs"

if (Test-Path $legacyModel)
{
    Remove-Item $legacyModel -Force
    Write-Host "Removed legacy ClientUpdateCredential.cs"
}
else
{
    Write-Host "ClientUpdateCredential.cs does not exist. Nothing to remove."
}

Write-Host ""
Write-Host "Do not delete historical EF Core migration files."
Write-Host "Next run: .\scripts\create-hardening-migration.ps1"
