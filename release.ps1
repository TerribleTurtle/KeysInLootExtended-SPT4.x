param(
    [switch]$Overwrite,
    [switch]$PackageOnly,
    [string]$TagName
)
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    $repoRoot = (Get-Location).ProviderPath
}

$csprojPath = [System.IO.Path]::Combine($repoRoot, "KeysInLootExtended", "KeysInLootExtended.csproj")
$testCsprojPath = [System.IO.Path]::Combine($repoRoot, "KeysInLootExtended.Tests", "KeysInLootExtended.Tests.csproj")
$pluginPath = [System.IO.Path]::Combine($repoRoot, "KeysInLootExtended", "KeysInLootExtendedPlugin.cs")
$changelogPath = [System.IO.Path]::Combine($repoRoot, "CHANGELOG.md")
$distSptPath = [System.IO.Path]::Combine($repoRoot, "dist", "SPT")

Write-Host "Parsing version from $csprojPath..."
[xml]$project = Get-Content -LiteralPath $csprojPath -Raw -Encoding UTF8
$versionNode = $project.SelectSingleNode("/Project/PropertyGroup/Version")
$version = if ($null -ne $versionNode) { $versionNode.InnerText.Trim() } else { "" }
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not find <Version> in $csprojPath"
}

Write-Host "Verifying ModMetadata.Version in $pluginPath..."
$pluginSource = Get-Content -LiteralPath $pluginPath -Raw -Encoding UTF8
if ($pluginSource -notmatch 'SemanticVersioning\.Version\s+Version\s*\{[^}]*\}\s*=\s*(?:new\s+SemanticVersioning\.Version|new)\(\s*"([^"]+)"\s*\)') {
    throw "Could not parse ModMetadata.Version in $pluginPath"
}
$metadataVersion = $matches[1].Trim()
if ($metadataVersion -ne $version) {
    throw "Version mismatch: KeysInLootExtended.csproj <Version> is '$version', whereas ModMetadata.Version in KeysInLootExtendedPlugin.cs is '$metadataVersion'."
}

$releaseTag = if ([string]::IsNullOrWhiteSpace($TagName)) { $version } else { $TagName.Trim() }
$normalizedTag = if ($releaseTag.StartsWith("v")) { $releaseTag.Substring(1) } else { $releaseTag }
if ($normalizedTag -ne $version) {
    throw "Release tag '$releaseTag' does not match project <Version> '$version'."
}

Write-Host "Running test suite in Release mode..."
& dotnet @("test", $testCsprojPath, "-c", "Release")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
}

Write-Host "Building mod in Release mode for version $version..."
if (Test-Path -LiteralPath $distSptPath) {
    Remove-Item -LiteralPath $distSptPath -Recurse -Force
}
& dotnet @("build", $csprojPath, "-c", "Release", "--no-incremental")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $distSptPath)) {
    throw "Expected build output directory '$distSptPath' was not created."
}

$distSptFull = [System.IO.Path]::GetFullPath($distSptPath)
$stagePrefix = $distSptFull.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$stagedFiles = @(Get-ChildItem -LiteralPath $distSptFull -File -Recurse | Sort-Object FullName)

$forbiddenFiles = @($stagedFiles | Where-Object {
    $_.Name -like "*.pdb" -or
    $_.Name -like "*.deps.json" -or
    $_.Name -like "*.cs"
})
if ($forbiddenFiles.Count -gt 0) {
    $badList = ($forbiddenFiles | ForEach-Object { $_.FullName }) -join ", "
    throw "Forbidden debug/dependency/source files staged in dist/SPT: $badList"
}

$entryPairs = @()
$locationFileCount = 0
$hasDll = $false
$hasConfig = $false

foreach ($file in $stagedFiles) {
    if (-not $file.FullName.StartsWith($stagePrefix, [System.StringComparison]::Ordinal)) {
        throw "Staged file '$($file.FullName)' is outside '$stagePrefix'."
    }
    $relPosix = $file.FullName.Substring($stagePrefix.Length).Replace('\', '/')
    if ($relPosix -eq "user/mods/KeysInLootExtended/KeysInLootExtended.dll") {
        $hasDll = $true
    } elseif ($relPosix -eq "user/mods/KeysInLootExtended/config.jsonc") {
        $hasConfig = $true
    } elseif ($relPosix -match '^user/mods/KeysInLootExtended/locations/[^/]+\.jsonc$') {
        $locationFileCount++
    } else {
        throw "Unexpected file staged in dist/SPT: '$relPosix'"
    }
    $entryPairs += [PSCustomObject]@{
        FullName  = $file.FullName
        EntryName = $relPosix
    }
}

if (-not $hasDll -or -not $hasConfig -or $locationFileCount -lt 13) {
    throw "Incomplete staging tree in dist/SPT (dll=$hasDll, config=$hasConfig, locations=$locationFileCount)."
}

$zipName = "KeysInLootExtended-$version.zip"
$zipPath = [System.IO.Path]::Combine($repoRoot, $zipName)
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Write-Host "Creating deterministic POSIX zip archive at $zipPath..."
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zipStream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
try {
    $archive = [System.IO.Compression.ZipArchive]::new($zipStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($pair in $entryPairs) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $pair.FullName,
                $pair.EntryName,
                [System.IO.Compression.CompressionLevel]::Optimal
            )
        }
    } finally {
        $archive.Dispose()
    }
} finally {
    $zipStream.Dispose()
}

$verifyArchive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    if ($verifyArchive.Entries.Count -ne $entryPairs.Count) {
        throw "Zip verification failed: expected $($entryPairs.Count) entries, got $($verifyArchive.Entries.Count)."
    }
    foreach ($entry in $verifyArchive.Entries) {
        if ($entry.FullName.Contains('\')) {
            throw "Zip verification failed: backslash found in archive entry '$($entry.FullName)'."
        }
    }
} finally {
    $verifyArchive.Dispose()
}

Write-Host "Release packaged and verified at $zipPath ($($entryPairs.Count) POSIX entries)."

if ($PackageOnly) {
    Write-Host "PackageOnly switch set; skipping GitHub Release upload."
    exit 0
}

$tempDir = if (-not [string]::IsNullOrWhiteSpace($env:TEMP)) { $env:TEMP } else { [System.IO.Path]::GetTempPath() }
$notesFile = [System.IO.Path]::Combine($tempDir, "KeysInLootExtended_release_notes_${version}_$([Guid]::NewGuid().ToString('N')).md")
$hasCustomNotes = $false

try {
    Write-Host "Extracting release notes from CHANGELOG.md..."
    if (Test-Path -LiteralPath $changelogPath) {
        $lines = Get-Content -LiteralPath $changelogPath -Encoding UTF8
        $inVersion = $false
        $notes = @()
        foreach ($line in $lines) {
            if ($line -match "^## \[([^\]]+)\]") {
                $matchedVersion = $matches[1].Trim()
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
            $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
            [System.IO.File]::WriteAllLines($notesFile, [string[]]$notes, $utf8NoBom)
            $hasCustomNotes = $true
        }
    }

    Write-Host "Checking GitHub Releases for tag '$releaseTag'..."
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & gh @("release", "view", $releaseTag) *> $null
        $releaseExists = ($LASTEXITCODE -eq 0)
    } finally {
        $ErrorActionPreference = $prevEap
    }

    if ($releaseExists) {
        if (-not $Overwrite) {
            throw "Release '$releaseTag' already exists. Pass -Overwrite to clobber existing release assets."
        }
        Write-Host "Release '$releaseTag' already exists and -Overwrite is set. Uploading asset..."
        & gh @("release", "upload", $releaseTag, $zipPath, "--clobber")
        if ($LASTEXITCODE -ne 0) {
            throw "gh release upload failed with exit code $LASTEXITCODE"
        }
        if ($hasCustomNotes) {
            Write-Host "Updating release notes for '$releaseTag'..."
            & gh @("release", "edit", $releaseTag, "--notes-file", $notesFile)
            if ($LASTEXITCODE -ne 0) {
                throw "gh release edit failed with exit code $LASTEXITCODE"
            }
        }
    } else {
        Write-Host "Creating new GitHub Release '$releaseTag'..."
        $ghCreateArgs = @("release", "create", $releaseTag, $zipPath, "--title", $releaseTag)
        if ($hasCustomNotes) {
            $ghCreateArgs += @("--notes-file", $notesFile)
        } else {
            $ghCreateArgs += @("--generate-notes")
        }
        & gh @ghCreateArgs
        if ($LASTEXITCODE -ne 0) {
            throw "gh release create failed with exit code $LASTEXITCODE"
        }
    }

    Write-Host "Successfully published $zipName to GitHub Release '$releaseTag'."
} finally {
    if (Test-Path -LiteralPath $notesFile) {
        Remove-Item -LiteralPath $notesFile -Force -ErrorAction SilentlyContinue
    }
}
