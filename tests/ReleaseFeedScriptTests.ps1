$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $repoRoot 'scripts\finalize-github-release-feed.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('MinecraftLauncherReleaseFeedTests-' + [Guid]::NewGuid().ToString('N'))

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        throw $Message
    }
}

function New-TestAsset([string]$Directory, [string]$Name, [string]$Content) {
    $path = Join-Path $Directory $Name
    [System.IO.File]::WriteAllText($path, $Content, [System.Text.UTF8Encoding]::new($false))
    $file = Get-Item -LiteralPath $path
    return [ordered]@{
        PackageId = 'lDiestrol.MinecraftLauncher'
        Version = if ($Name -like '*0.5.0*') { '0.5.0' } else { '0.6.0' }
        Type = if ($Name -like '*delta*') { 'Delta' } else { 'Full' }
        FileName = $Name
        SHA1 = (Get-FileHash -LiteralPath $path -Algorithm SHA1).Hash
        SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        Size = $file.Length
    }
}

New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    $oldAsset = New-TestAsset $tempRoot 'lDiestrol.MinecraftLauncher-0.5.0-stable-full.nupkg' 'old-full'
    $fullAsset = New-TestAsset $tempRoot 'lDiestrol.MinecraftLauncher-0.6.0-stable-full.nupkg' 'new-full'
    $deltaAsset = New-TestAsset $tempRoot 'lDiestrol.MinecraftLauncher-0.6.0-stable-delta.nupkg' 'new-delta'
    [System.IO.File]::WriteAllText((Join-Path $tempRoot 'lDiestrol.MinecraftLauncher-stable-Setup.exe'), 'setup')
    [System.IO.File]::WriteAllText((Join-Path $tempRoot 'lDiestrol.MinecraftLauncher-stable-Portable.zip'), 'portable')
    [System.IO.File]::WriteAllText((Join-Path $tempRoot 'assets.stable.json'), '{}')
    [System.IO.File]::WriteAllText((Join-Path $tempRoot 'RELEASES-stable'), 'legacy')
    @{ Assets = @($fullAsset, $deltaAsset, $oldAsset) } |
        ConvertTo-Json -Depth 10 -Compress |
        Set-Content -LiteralPath (Join-Path $tempRoot 'releases.stable.json') -Encoding utf8NoBOM

    & $scriptPath -ReleaseDirectory $tempRoot -Version '0.6.0' -Channel stable

    $feed = Get-Content -Raw -LiteralPath (Join-Path $tempRoot 'releases.stable.json') | ConvertFrom-Json
    Assert-True ($feed.Assets.Count -eq 2) 'Published feed must contain exactly current full and delta assets.'
    Assert-True (@($feed.Assets | Where-Object Version -ne '0.6.0').Count -eq 0) 'Published feed contains a historical version.'
    Assert-True (@($feed.Assets | Where-Object Type -eq 'Full').Count -eq 1) 'Published feed must contain one full package.'
    Assert-True (@($feed.Assets | Where-Object Type -eq 'Delta').Count -eq 1) 'Published feed must contain one delta package.'

    $hashLines = Get-Content -LiteralPath (Join-Path $tempRoot 'SHA256SUMS.txt')
    Assert-True (-not ($hashLines -match '0\.5\.0')) 'Hash manifest must not publish the historical full package.'
    Assert-True (-not ($hashLines -match '^.+  RELEASES-stable$')) 'Hash manifest must not publish the legacy index with historical package references.'
    Assert-True (@($hashLines -match '0\.6\.0-stable-full\.nupkg').Count -eq 1) 'Current full package is missing from hash manifest.'
    Assert-True (@($hashLines -match '0\.6\.0-stable-delta\.nupkg').Count -eq 1) 'Current delta package is missing from hash manifest.'

    foreach ($line in $hashLines) {
        Assert-True ($line -match '^([0-9a-f]{64})  (.+)$') "Invalid hash manifest line: $line"
        $expectedHash = $Matches[1]
        $path = Join-Path $tempRoot $Matches[2]
        Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Manifest file is missing: $path"
        $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        Assert-True ($actualHash -eq $expectedHash) "Manifest hash mismatch: $path"
    }

    Write-Host 'Release feed script tests passed.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
