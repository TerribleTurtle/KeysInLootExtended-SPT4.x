param()

$ErrorActionPreference = "Stop"

$zipName = "KeysInLootExtended-2.0.0.zip"
if (Test-Path $zipName) {
    Remove-Item $zipName -Force
}

Write-Host "Running release.ps1..."
if (-not (Test-Path "release.ps1")) {
    Write-Error "release.ps1 does not exist!"
    exit 1
}

.\release.ps1

if (-not (Test-Path $zipName)) {
    Write-Error "Zip file $zipName was not created!"
    exit 1
}

Write-Host "Zip file created. Verifying contents..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $zipName).Path)

$hasDll = $false
$hasConfig = $false
$hasSource = $false

foreach ($entry in $zip.Entries) {
    if ($entry.FullName -match "user/mods/KeysInLootExtended/KeysInLootExtended.dll") {
        $hasDll = $true
    }
    if ($entry.FullName -match "user/mods/KeysInLootExtended/config.jsonc") {
        $hasConfig = $true
    }
    if ($entry.FullName -match "\.cs$" -or $entry.FullName -match "\.csproj$") {
        $hasSource = $true
    }
}

$zip.Dispose()

if (-not $hasDll) {
    Write-Error "Zip is missing the compiled DLL!"
    exit 1
}

if (-not $hasConfig) {
    Write-Error "Zip is missing config.jsonc!"
    exit 1
}

if ($hasSource) {
    Write-Error "Zip incorrectly contains source code files (.cs / .csproj)!"
    exit 1
}

Write-Host "Test Passed: Zip is correctly packaged."
exit 0
