[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReleaseDirectory,
    [Parameter(Mandatory = $true)][string]$Version,
    [ValidateSet('dev', 'stable')][string]$Channel = 'dev',
    [switch]$Diagnostic
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 (pwsh) is required for controlled SHA-256 packaging.' }
Set-StrictMode -Version Latest
$repo = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $repo '.config\dotnet-tools.json') -Raw | ConvertFrom-Json
if ($manifest.tools.vpk.version -ne '1.2.161') { throw 'SDK/vpk 1.2.161 is required.' }
[xml]$sdkProject = Get-Content -LiteralPath (Join-Path $repo 'src\Launcher.Infrastructure\Launcher.Infrastructure.csproj') -Raw
$sdkReferences = @($sdkProject.SelectNodes("/Project/ItemGroup/PackageReference[@Include='Velopack']"))
if ($sdkReferences.Count -ne 1 -or $sdkReferences[0].GetAttribute('Version') -ne '1.2.161') {
    throw 'The helper infrastructure SDK must match vpk 1.2.161.'
}
$identity = if ($Diagnostic) { 'MinecraftLauncher.BootstrapperDiagnostic' } else { 'lDiestrol.MinecraftLauncher' }
$mainExe = if ($Diagnostic) { 'VendorProbe.exe' } else { 'MinecraftLauncher.exe' }
$release = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$setup = Join-Path $release "$identity-$Channel-Setup.exe"
$package = Join-Path $release "$identity-$Version-$Channel-full.nupkg"
$portable = Join-Path $release "$identity-$Channel-Portable.zip"
foreach ($file in @($setup, $package, $portable)) { if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing vpk output: $file" } }
$setupStream = [System.IO.File]::OpenRead($setup)
try {
    if ($setupStream.Length -gt 52) {
        $setupStream.Position = $setupStream.Length - 52
        $marker = [byte[]]::new(8); $null = $setupStream.Read($marker, 0, 8)
        if ([System.Text.Encoding]::ASCII.GetString($marker) -eq 'MCBOOT01') { throw 'Refusing to wrap an already wrapped GUI. Supply fresh vpk vendor output.' }
    }
} finally { $setupStream.Dispose() }
# Validate release identity rather than trusting the caller's version/channel strings.
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $nuspec = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
    if ($nuspec.Count -ne 1) { throw 'Expected exactly one package manifest.' }
    $reader = [System.IO.StreamReader]::new($nuspec[0].Open())
    try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $xml.package.metadata
    if ($metadata.id -ne $identity -or $metadata.version -ne $Version -or $metadata.channel -ne $Channel -or
        $metadata.mainExe -ne $mainExe -or $metadata.machineArchitecture -ne 'x64') { throw 'Package identity mismatch.' }
} finally { $archive.Dispose() }
$token = [guid]::NewGuid().ToString('N')
$work = Join-Path $repo "artifacts\bootstrapper\bundles\$token"
$payload = Join-Path $work 'payload'
$helper = Join-Path $payload 'helper'
$vendor = Join-Path $payload 'vendor'
New-Item -ItemType Directory -Path $helper, $vendor | Out-Null
$helperProject = Join-Path $repo 'src\Launcher.InstallHelper\Launcher.InstallHelper.csproj'
& dotnet publish $helperProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:InstallerDiagnostic=$($Diagnostic.IsPresent.ToString().ToLowerInvariant()) -o $helper
if ($LASTEXITCODE -ne 0) { throw 'Self-contained helper publish failed.' }
Copy-Item -LiteralPath $setup -Destination (Join-Path $vendor 'Setup.exe')
Copy-Item -LiteralPath $package -Destination (Join-Path $vendor 'full.nupkg')
# Portable Update.exe is the same vpk build's actual updater, including icon customization.
# Never guess its hash from an absent entry in the public full nupkg.
$archive = [System.IO.Compression.ZipFile]::OpenRead($portable)
try {
    $entry = $archive.GetEntry('Update.exe'); if (!$entry) { throw 'Portable output is missing Update.exe.' }
    $stream = $entry.Open()
    try { $updateHash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
} finally { $archive.Dispose() }
$catalogFiles = @()
foreach ($file in (Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName)) {
    $relative = [System.IO.Path]::GetRelativePath($payload, $file.FullName).Replace('\', '/')
    if ($relative -notmatch '^(helper/[A-Za-z0-9._/-]+|vendor/(Setup\.exe|full\.nupkg))$' -or
        $relative.Contains('..') -or $file.Length -le 0 -or $file.Length -gt 2147483648 -or
        $file.Attributes.HasFlag([System.IO.FileAttributes]::ReparsePoint)) { throw 'Unapproved payload file.' }
    $catalogFiles += @{ path = $relative; size = $file.Length; sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash }
}
if ($catalogFiles.Count -lt 3 -or $catalogFiles.Count -gt 1024 -or
    ($catalogFiles | Measure-Object -Property size -Sum).Sum -gt 8589934592) { throw 'Invalid payload count or total size.' }
$catalog = @{ schemaVersion = 1; packId = $identity; mainExe = $mainExe; version = $Version; channel = $Channel;
    velopackVersion = '1.2.161'; updateSha256 = $updateHash; files = $catalogFiles }
$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $payload 'bootstrapper-catalog.json'), ($catalog | ConvertTo-Json -Depth 5), $utf8)
& (Join-Path $PSScriptRoot 'build-native-bootstrapper.ps1') -Diagnostic:$Diagnostic
$flavor = if ($Diagnostic) { 'diagnostic' } else { 'production' }
$stub = Join-Path $repo "artifacts\bootstrapper\native-$flavor\bootstrapper.exe"
$output = Join-Path $work 'MinecraftLauncher-Setup.exe'
$destination = [System.IO.File]::Open($output, [System.IO.FileMode]::CreateNew)
$index = [System.IO.MemoryStream]::new()
$indexWriter = [System.IO.BinaryWriter]::new($index, $utf8, $true)
try {
    $source = [System.IO.File]::OpenRead($stub); try { $source.CopyTo($destination) } finally { $source.Dispose() }
    $files = @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName)
    $indexWriter.Write([uint32]1); $indexWriter.Write([uint32]$files.Count)
    foreach ($file in $files) {
        $relative = [System.IO.Path]::GetRelativePath($payload, $file.FullName).Replace('\', '/')
        $name = $utf8.GetBytes($relative)
        $indexWriter.Write([uint32]$name.Length); $indexWriter.Write([uint64]$file.Length); $indexWriter.Write([uint64]$destination.Position)
        $indexWriter.Write([Convert]::FromHexString((Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash)); $indexWriter.Write($name)
        $source = [System.IO.File]::OpenRead($file.FullName); try { $source.CopyTo($destination) } finally { $source.Dispose() }
    }
    $indexWriter.Flush(); $indexBytes = $index.ToArray(); $indexOffset = $destination.Position; $destination.Write($indexBytes)
    $footer = [System.IO.BinaryWriter]::new($destination, $utf8, $true)
    try {
        $footer.Write($utf8.GetBytes('MCBOOT01')); $footer.Write([uint64]$indexOffset); $footer.Write([uint32]$indexBytes.Length)
        $footer.Write([System.Security.Cryptography.SHA256]::HashData($indexBytes)); $footer.Flush(); $destination.Flush($true)
    } finally { $footer.Dispose() }
} finally { $destination.Dispose(); $indexWriter.Dispose(); $index.Dispose() }
[System.IO.File]::WriteAllText((Join-Path $work 'SHA256SUMS.txt'),
    (Get-FileHash -Algorithm SHA256 -LiteralPath $output).Hash.ToLowerInvariant() + '  MinecraftLauncher-Setup.exe' + "`r`n", $utf8)
Write-Output "Bootstrapper: $output"
Write-Output "Trusted catalog: $(Join-Path $payload 'bootstrapper-catalog.json')"
Write-Output $output
