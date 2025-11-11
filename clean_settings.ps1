# Clean AutoBet - Remove all personal settings and data
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Red
Write-Host "  AutoBet - Cleaning Personal Data" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Red
Write-Host ""

# Paths to clean
$SettingsDir = "$env:APPDATA\AutoBet"
$DesktopLog = "$env:USERPROFILE\Desktop\AutoBet_Log.txt"

Write-Host "Files to be removed:" -ForegroundColor Yellow
Write-Host "  1. Settings folder: $SettingsDir" -ForegroundColor White
Write-Host "  2. Desktop log: $DesktopLog" -ForegroundColor White
Write-Host ""

$confirmation = Read-Host "Continue? (Y/N)"
if ($confirmation -ne 'Y' -and $confirmation -ne 'y') {
    Write-Host "Cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Cleaning..." -ForegroundColor Cyan

# Remove settings folder
if (Test-Path $SettingsDir) {
    Remove-Item $SettingsDir -Recurse -Force
    Write-Host "Removed: $SettingsDir" -ForegroundColor Green
} else {
    Write-Host "Not found: $SettingsDir" -ForegroundColor Gray
}

# Remove desktop log
if (Test-Path $DesktopLog) {
    Remove-Item $DesktopLog -Force
    Write-Host "Removed: $DesktopLog" -ForegroundColor Green
} else {
    Write-Host "Not found: $DesktopLog" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Cleaning completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Application is now clean for distribution" -ForegroundColor Cyan
