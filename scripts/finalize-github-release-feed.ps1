[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'stable')]
    [string]$Channel
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw "Version '$Version' is not a supported semantic package version."
}

$releaseDir = [System.IO.Path]::GetFullPath($ReleaseDirectory)
if (-not (Test-Path -LiteralPath $releaseDir -PathType Container)) {
    throw "Release directory does not exist: $releaseDir"
}

$feedPath = Join-Path $releaseDir "releases.$Channel.json"
if (-not (Test-Path -LiteralPath $feedPath -PathType Leaf)) {
    throw "Release feed is missing: $feedPath"
}

$feed = Get-Content -Raw -LiteralPath $feedPath | ConvertFrom-Json
$currentAssets = @($feed.Assets | Where-Object { $_.Version.ToString() -eq $Version })
if ($currentAssets.Count -eq 0) {
    throw "Release feed contains no assets for version $Version."
}

$fullAssets = @($currentAssets | Where-Object { $_.Type -eq 'Full' })
$deltaAssets = @($currentAssets | Where-Object { $_.Type -eq 'Delta' })
if ($fullAssets.Count -ne 1) {
    throw "Release feed must contain exactly one full package for version $Version."
}
if ($deltaAssets.Count -gt 1) {
    throw "Release feed contains more than one delta package for version $Version."
}

$duplicateFiles = @($currentAssets | Group-Object FileName | Where-Object { $_.Count -gt 1 })
if ($duplicateFiles.Count -ne 0) {
    throw "Release feed contains duplicate file names for version $Version."
}

foreach ($asset in $currentAssets) {
    $assetPath = Join-Path $releaseDir $asset.FileName
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "Release asset is missing: $($asset.FileName)"
    }

    $file = Get-Item -LiteralPath $assetPath
    if ($file.Length -ne [long]$asset.Size) {
        throw "Release asset size mismatch: $($asset.FileName)"
    }

    $sha256 = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash
    if ($sha256 -ne $asset.SHA256) {
        throw "Release asset SHA-256 mismatch: $($asset.FileName)"
    }

    $sha1 = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA1).Hash
    if ($sha1 -ne $asset.SHA1) {
        throw "Release asset SHA-1 mismatch: $($asset.FileName)"
    }
}

$publishedFeed = [ordered]@{ Assets = $currentAssets }
$json = $publishedFeed | ConvertTo-Json -Depth 20 -Compress
[System.IO.File]::WriteAllText($feedPath, $json, [System.Text.UTF8Encoding]::new($false))

$publishFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($asset in $currentAssets) {
    [void]$publishFiles.Add($asset.FileName)
}

$requiredPatterns = @(
    '*-Setup.exe',
    '*-Portable.zip'
)
foreach ($pattern in $requiredPatterns) {
    $matches = @(Get-ChildItem -LiteralPath $releaseDir -Filter $pattern -File)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one release file matching '$pattern'."
    }
    [void]$publishFiles.Add($matches[0].Name)
}

$optionalIndexFiles = @(
    "assets.$Channel.json"
)
foreach ($name in $optionalIndexFiles) {
    if (Test-Path -LiteralPath (Join-Path $releaseDir $name) -PathType Leaf) {
        [void]$publishFiles.Add($name)
    }
}
[void]$publishFiles.Add((Split-Path -Leaf $feedPath))

$hashFile = Join-Path $releaseDir 'SHA256SUMS.txt'
$hashLines = $publishFiles |
    Sort-Object |
    ForEach-Object {
        $path = Join-Path $releaseDir $_
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $_"
    }
[System.IO.File]::WriteAllLines($hashFile, $hashLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "GitHub release feed finalized: version=$Version channel=$Channel assets=$($currentAssets.Count)"
