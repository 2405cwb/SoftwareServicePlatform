param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
$PackageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Test-Path $ProjectRoot)) {
    throw "项目目录不存在: $ProjectRoot"
}

Write-Host "目标项目: $ProjectRoot" -ForegroundColor Cyan
Write-Host "开始覆盖 backend / forntend ..." -ForegroundColor Cyan

Copy-Item -Path (Join-Path $PackageRoot "backend\*") -Destination (Join-Path $ProjectRoot "backend") -Recurse -Force
Copy-Item -Path (Join-Path $PackageRoot "forntend\*") -Destination (Join-Path $ProjectRoot "forntend") -Recurse -Force

Write-Host "覆盖完成。" -ForegroundColor Green
Write-Host "请执行 dotnet build 和 npm run build。" -ForegroundColor Yellow
