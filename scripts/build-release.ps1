[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'stable')]
    [string]$Channel,

    [switch]$KeepPreviousReleases
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw "Version '$Version' is not a supported semantic package version."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishRoot = Join-Path $artifactsRoot 'publish'
$publishDir = Join-Path $publishRoot $Version
$releaseRoot = Join-Path $artifactsRoot 'releases'
$releaseDir = Join-Path $releaseRoot $Channel
$project = Join-Path $repoRoot 'src\Launcher.App\Launcher.App.csproj'
$solution = Join-Path $repoRoot 'MinecraftLauncher.sln'
$mainExe = 'MinecraftLauncher.exe'
$iconPath = Join-Path $repoRoot 'src\Launcher.App\Assets\Launcher.ico'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK 10 is required.'
}

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot '.config\dotnet-tools.json'))) {
    throw 'Repository-local dotnet tool manifest is missing.'
}

if (-not (Test-Path -LiteralPath $iconPath)) {
    throw "Launcher icon is missing: $iconPath"
}

New-Item -ItemType Directory -Force -Path $publishRoot, $releaseRoot | Out-Null
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (-not $KeepPreviousReleases -and (Test-Path -LiteralPath $releaseDir)) {
    Remove-Item -LiteralPath $releaseDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir, $releaseDir | Out-Null

Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet restore $solution -r win-x64
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet publish $project `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $publishDir `
        -p:Version=$Version `
        -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    $exePath = Join-Path $publishDir $mainExe
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Published main executable is missing: $exePath"
    }

    $publishedVersion = (Get-Item -LiteralPath $exePath).VersionInfo.ProductVersion
    if ($publishedVersion -ne $Version) {
        throw "Published version '$publishedVersion' does not match package version '$Version'."
    }

    $notesPath = Join-Path $repoRoot "docs\release-notes\$Version.md"
    $vpkArguments = @(
        'vpk', 'pack',
        '--packId', 'lDiestrol.MinecraftLauncher',
        '--packVersion', $Version,
        '--packDir', $publishDir,
        '--mainExe', $mainExe,
        '--packTitle', 'Minecraft Launcher',
        '--packAuthors', 'lDiestrol',
        '--icon', $iconPath,
        '--channel', $Channel,
        '--runtime', 'win-x64',
        '--outputDir', $releaseDir
    )
    if (Test-Path -LiteralPath $notesPath) {
        $vpkArguments += @('--releaseNotes', $notesPath)
    }

    & dotnet @vpkArguments
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed.' }

    $requiredArtifacts = @(
        (Get-ChildItem -LiteralPath $releaseDir -Filter '*-Setup.exe' -File),
        (Get-ChildItem -LiteralPath $releaseDir -Filter '*-full.nupkg' -File),
        (Get-ChildItem -LiteralPath $releaseDir -Filter '*-Portable.zip' -File),
        (Get-Item -LiteralPath (Join-Path $releaseDir "releases.$Channel.json") -ErrorAction SilentlyContinue)
    ) | Where-Object { $null -ne $_ }

    if ($requiredArtifacts.Count -lt 4) {
        throw 'Velopack did not create Setup, full package, portable ZIP, and release index.'
    }

    $finalizeFeedScript = Join-Path $repoRoot 'scripts\finalize-github-release-feed.ps1'
    & $finalizeFeedScript -ReleaseDirectory $releaseDir -Version $Version -Channel $Channel

    $hashFile = Join-Path $releaseDir 'SHA256SUMS.txt'
    Write-Host "Release created: version=$Version channel=$Channel"
    Get-Content -LiteralPath $hashFile |
        ForEach-Object { $_ -replace '^[0-9a-f]{64}  ', '' } |
        ForEach-Object { Get-Item -LiteralPath (Join-Path $releaseDir $_) } |
        Select-Object Name, Length, FullName |
        Format-Table -AutoSize
}
finally {
    Pop-Location
}
