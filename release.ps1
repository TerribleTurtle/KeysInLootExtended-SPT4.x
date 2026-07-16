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

Write-Host "Extracting release notes from CHANGELOG.md..."
$notesFile = Join-Path $PWD "temp_release_notes.md"
$releaseNotesArgs = "--generate-notes"

if (Test-Path "CHANGELOG.md") {
    $lines = Get-Content "CHANGELOG.md"
    $inVersion = $false
    $notes = @()
    foreach ($line in $lines) {
        if ($line -match "^## \[([^\]]+)\]") {
            $matchedVersion = $matches[1]
            if ($matchedVersion -eq $version) {
                $inVersion = $true
                continue
            } elseif ($inVersion) {
                break
            }
        }
        if ($inVersion) {
            $notes += $line
        }
    }
    
    $first = 0
    while ($first -lt $notes.Count -and [string]::IsNullOrWhiteSpace($notes[$first])) { $first++ }

    $last = $notes.Count - 1
    while ($last -ge $first -and [string]::IsNullOrWhiteSpace($notes[$last])) { $last-- }

    if ($first -le $last) {
        $notes = $notes[$first..$last]
    } else {
        $notes = @()
    }

    if ($notes.Count -gt 0) {
        $notes | Out-File -FilePath $notesFile -Encoding utf8
        $releaseNotesArgs = "--notes-file `"$notesFile`""
    }
}

Write-Host "Uploading to GitHub Releases..."
gh release view $version 2>$null
if ($LASTEXITCODE -eq 0) {
    if ($Overwrite) {
        Write-Host "Release $version already exists. -Overwrite flag provided. Uploading asset to overwrite..."
        gh release upload $version $zipPath --clobber
        Write-Host "Updating release notes..."
        if ($releaseNotesArgs -match "--notes-file") {
            gh release edit $version --notes-file $notesFile
        }
    } else {
        Write-Error "Release $version already exists. Use the -Overwrite switch to force upload and clobber existing assets."
        exit 1
    }
} else {
    Write-Host "Creating new release $version..."
    $releaseCmd = "gh release create $version $zipPath --title `"$version`" $releaseNotesArgs"
    Invoke-Expression $releaseCmd
}

if (Test-Path $notesFile) {
    Remove-Item $notesFile -Force
}

Write-Host "Successfully pushed $zipName to GitHub."
