param(
    [switch]$Overwrite
)
$ErrorActionPreference = "Stop"

$csprojPath = ".\KeysInLootExtended\KeysInLootExtended.csproj"

Write-Host "Parsing version from $csprojPath..."
[xml]$project = Get-Content $csprojPath
$version = $project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    Write-Error "Could not find <Version> in csproj"
    exit 1
}

$zipName = "KeysInLootExtended-$version.zip"
$zipPath = Join-Path $PWD $zipName

Write-Host "Building project in Release mode for version $version..."
dotnet build $csprojPath -c Release

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating zip archive..."
Compress-Archive -Path ".\dist\SPT" -DestinationPath $zipPath -Force
Write-Host "Release packaged successfully to $zipPath"

Write-Host "Uploading to GitHub Releases..."
gh release view $version 2>$null
if ($LASTEXITCODE -eq 0) {
    if ($Overwrite) {
        Write-Host "Release $version already exists. -Overwrite flag provided. Uploading asset to overwrite..."
        gh release upload $version $zipPath --clobber
    } else {
        Write-Error "Release $version already exists. Use the -Overwrite switch to force upload and clobber existing assets."
        exit 1
    }
} else {
    Write-Host "Creating new release $version..."
    gh release create $version $zipPath --title "Release $version" --generate-notes
}
Write-Host "Successfully pushed $zipName to GitHub."
