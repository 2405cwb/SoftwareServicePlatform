$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$api = Join-Path $root "backend\SoftwareServicePlatform.Api"

if (-not (Test-Path $api))
{
    throw "Backend project directory not found: $api"
}

Set-Location $api

Write-Host "Creating EF Core migration: AddPlatformHardening"

dotnet ef migrations add AddPlatformHardening

if ($LASTEXITCODE -ne 0)
{
    throw "dotnet ef migrations add failed."
}

Write-Host ""
Write-Host "Building backend..."

dotnet build

if ($LASTEXITCODE -ne 0)
{
    throw "dotnet build failed."
}

Write-Host ""
Write-Host "Done."
Write-Host "Check the new migration contains:"
Write-Host "1. CreateTable AuditLogs"
Write-Host "2. CreateTable SystemEventLogs"
Write-Host "3. AddColumn MaxDeviceCount"
Write-Host "4. AddColumn Remark"
Write-Host "5. DropTable ClientUpdateCredentials"
