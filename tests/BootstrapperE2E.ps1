[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Bootstrapper,
    [string[]]$Volumes = @('C:\', 'D:\'),
    [switch]$UninstallAndReinstall
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 is required.' }
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [System.Security.Principal.WindowsPrincipal]::new($identity)
if ($principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run in an ordinary Windows session/VM. Do not disable UAC or change token checks.'
}
$repo = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $Bootstrapper).Path
# Refuse to execute a production bootstrapper: inspect the hash-pinned embedded catalog first.
$file = [System.IO.File]::OpenRead($source)
$reader = [System.IO.BinaryReader]::new($file)
try {
    $file.Position = $file.Length - 52
    if ([System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(8)) -ne 'MCBOOT01') { throw 'Not a bootstrapper bundle.' }
    $offset = $reader.ReadUInt64(); $length = $reader.ReadUInt32(); $hash = $reader.ReadBytes(32)
    if ($length -gt 1048576 -or $offset + $length -ne $file.Length - 52) { throw 'Invalid bundle index.' }
    $file.Position = $offset; $indexBytes = $reader.ReadBytes($length)
    if (![System.Linq.Enumerable]::SequenceEqual[byte]($hash, [System.Security.Cryptography.SHA256]::HashData($indexBytes))) { throw 'Index hash mismatch.' }
    $indexStream = [System.IO.MemoryStream]::new($indexBytes)
    $index = [System.IO.BinaryReader]::new($indexStream)
    try {
        if ($index.ReadUInt32() -ne 1) { throw 'Unknown bundle schema.' }
        $count = $index.ReadUInt32(); if ($count -lt 4 -or $count -gt 1025) { throw 'Invalid bundle count.' }
        $catalog = $null
        for ($i = 0; $i -lt $count; $i++) {
            $nameLength = $index.ReadUInt32(); $size = $index.ReadUInt64(); $position = $index.ReadUInt64(); $digest = $index.ReadBytes(32)
            if ($nameLength -gt 240 -or $size -gt 2147483648 -or $position + $size -gt $offset) { throw 'Invalid entry.' }
            $name = [System.Text.Encoding]::UTF8.GetString($index.ReadBytes($nameLength))
            if ($name -eq 'bootstrapper-catalog.json') {
                if ($size -gt 1048576 -or $catalog) { throw 'Invalid catalog.' }
                $file.Position = $position; $data = $reader.ReadBytes($size)
                if (![System.Linq.Enumerable]::SequenceEqual[byte]($digest, [System.Security.Cryptography.SHA256]::HashData($data))) { throw 'Catalog hash mismatch.' }
                $catalog = [System.Text.Encoding]::UTF8.GetString($data) | ConvertFrom-Json
            }
        }
        if (!$catalog -or $catalog.packId -ne 'MinecraftLauncher.BootstrapperDiagnostic' -or $catalog.mainExe -ne 'VendorProbe.exe') {
            throw 'Only the compiled diagnostic product is permitted in this harness.'
        }
    } finally { $index.Dispose(); $indexStream.Dispose() }
} finally { $reader.Dispose(); $file.Dispose() }
$registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MinecraftLauncher.BootstrapperDiagnostic'
if (Test-Path -LiteralPath $registration) { throw 'Another diagnostic installation exists. Nothing was changed.' }
$token = [guid]::NewGuid().ToString('N')
$report = Join-Path $repo "artifacts\bootstrapper\e2e-$token"
New-Item -ItemType Directory -Path $report | Out-Null
$results = @()
foreach ($volume in $Volumes) {
    if ($volume -notmatch '^[A-Za-z]:\\$' -or !(Test-Path -LiteralPath $volume)) { throw 'Expected an existing local volume root.' }
    $disk = [System.IO.DriveInfo]::new($volume)
    if ($disk.DriveType -ne [System.IO.DriveType]::Fixed -or $disk.AvailableFreeSpace -lt 2GB) { throw 'A fixed disposable test volume with 2 GiB free is required.' }
    if (Test-Path -LiteralPath $registration) { throw 'Uninstall the preceding diagnostic case before testing another volume.' }
    $case = Join-Path $report $volume.Substring(0, 1)
    New-Item -ItemType Directory -Path $case | Out-Null
    $exe = Join-Path $case 'MinecraftLauncher-Setup.exe'; Copy-Item -LiteralPath $source -Destination $exe
    $base = Join-Path $volume "LauncherBootstrapperLab-$token\Игры с пробелами"
    if (Test-Path -LiteralPath $base) { throw 'Test target must not already exist.' }
    $app = Join-Path $base 'App'; $game = Join-Path $base 'GameData'
    $control = Join-Path $case 'diagnostic-profile\MinecraftLauncherControl'
    function Invoke-Gui([string]$label) {
        $images = Join-Path $case $label
        $process = Start-Process -FilePath $exe -ArgumentList "--smoke `"$base`" `"$images`"" -WindowStyle Normal -PassThru
        if (!$process.WaitForExit(180000)) { throw 'Diagnostic GUI did not finish; retain artifacts, do not remove an intent.' }
        if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $images 'success.bmp'))) { throw 'GUI reported failure; inspect screenshots and helper.log.' }
        $installed = Get-ItemProperty -LiteralPath $registration
        if ($installed.InstallLocation.TrimEnd('\') -ne $app) { throw 'Unexpected diagnostic registry App path.' }
        $locator = Get-Content -LiteralPath (Join-Path $control 'storage-root.json') -Raw | ConvertFrom-Json
        if ($locator.gameDataRoot -ne $game) { throw 'Unexpected GameData locator.' }
        if (!(Test-Path -LiteralPath (Join-Path $control 'installation-completion\installation-intent.json')) -or
            !(Test-Path -LiteralPath (Join-Path $app 'current\launch.marker'))) { throw 'Completion or installed-app launch was not proved.' }
        $update = Join-Path $app 'Update.exe'
        if ((Get-FileHash -LiteralPath $update).Hash -ne $catalog.updateSha256) { throw 'Actual Update.exe differs from the pinned vendor output.' }
    }
    function Invoke-DiagnosticUninstall {
        $registered = Get-ItemProperty -LiteralPath $registration
        if ($registered.InstallLocation.TrimEnd('\') -ne $app -or !(Test-Path -LiteralPath (Join-Path $control 'installation-record\installation-intent.json'))) {
            throw 'Refusing to uninstall an unowned diagnostic App.'
        }
        $update = Join-Path $app 'Update.exe'
        if ((Get-FileHash -LiteralPath $update).Hash -ne $catalog.updateSha256) { throw 'Untrusted updater; no uninstall performed.' }
        $process = Start-Process -FilePath $update -ArgumentList 'uninstall --silent' -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or (Test-Path -LiteralPath $registration)) { throw 'Diagnostic uninstall failed.' }
    }
    Invoke-Gui 'clean'
    $sentinel = Join-Path $game 'world-preservation.sentinel'; [System.IO.File]::WriteAllText($sentinel, "Isolated world sentinel $token")
    $worldHash = (Get-FileHash -LiteralPath $sentinel).Hash; $locatorHash = (Get-FileHash -LiteralPath (Join-Path $control 'storage-root.json')).Hash
    Invoke-Gui 'retry'
    if ($UninstallAndReinstall) { Invoke-DiagnosticUninstall; Invoke-Gui 'reinstall' }
    if ((Get-FileHash -LiteralPath $sentinel).Hash -ne $worldHash -or (Get-FileHash -LiteralPath (Join-Path $control 'storage-root.json')).Hash -ne $locatorHash) {
        throw 'Preservation check failed; retain all evidence.'
    }
    $results += @{ base = $base; control = $control; worldHash = $worldHash; locatorHash = $locatorHash;
        updateVersion = (Get-Item -LiteralPath (Join-Path $app 'Update.exe')).VersionInfo.FileVersion; screenshots = $case }
    if ($UninstallAndReinstall) {
        Invoke-DiagnosticUninstall
        if ((Get-FileHash -LiteralPath $sentinel).Hash -ne $worldHash -or (Get-FileHash -LiteralPath (Join-Path $control 'storage-root.json')).Hash -ne $locatorHash) {
            throw 'Final uninstall changed GameData or Control; retain evidence.'
        }
    }
    elseif ($Volumes.Count -gt 1) { throw 'Use -UninstallAndReinstall to release the diagnostic registration between volumes.' }
}
$results | ConvertTo-Json -Depth 5 | Out-File -LiteralPath (Join-Path $report 'results.json') -Encoding utf8
Write-Output "E2E evidence retained: $report. GameData and Control were not deleted."
