$ErrorActionPreference = "Stop"
Write-Host "AutoBet Installer Creator" -ForegroundColor Cyan
Write-Host ""
$ProjectRoot = $PSScriptRoot
$PublishFolder = Join-Path $ProjectRoot "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"
$OutputFolder = Join-Path $ProjectRoot "Installer"
$PortableZip = Join-Path $OutputFolder "AutoBet_Portable.zip"
if (Test-Path $OutputFolder) { Remove-Item $OutputFolder -Recurse -Force }
New-Item -ItemType Directory -Path $OutputFolder | Out-Null
Write-Host "Created: $OutputFolder" -ForegroundColor Green
if (-not (Test-Path $PublishFolder)) {
    Write-Host "ERROR: Publish folder not found!" -ForegroundColor Red
    exit 1
}
Write-Host "Creating ZIP..." -ForegroundColor Yellow
Compress-Archive -Path "$PublishFolder\*" -DestinationPath $PortableZip -Force
$zipSize = (Get-Item $PortableZip).Length / 1MB
Write-Host "Done: $PortableZip ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green
Write-Host ""
explorer $OutputFolder
