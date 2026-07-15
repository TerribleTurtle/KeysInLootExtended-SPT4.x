param()
$ErrorActionPreference = "Stop"

Write-Host "Building project in Release mode..."
dotnet build .\KeysInLootExtended\KeysInLootExtended.csproj -c Release

$zipPath = Join-Path $PWD "KeysInLootExtended-2.0.0.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating zip archive..."
Compress-Archive -Path ".\dist\user" -DestinationPath $zipPath -Force
Write-Host "Release packaged successfully to $zipPath"
