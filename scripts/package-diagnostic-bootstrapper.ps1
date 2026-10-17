[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$work = Join-Path $repo ('artifacts\bootstrapper\diagnostic-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $work 'publish'
$feed = Join-Path $work 'feed'
New-Item -ItemType Directory -Path $work | Out-Null
& dotnet publish (Join-Path $repo 'tools\Launcher.InstallerDiagnosticHost\Launcher.InstallerDiagnosticHost.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:Version=0.0.1 -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Diagnostic host publish failed.' }
Push-Location $repo
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    & dotnet vpk pack --packId MinecraftLauncher.BootstrapperDiagnostic --packVersion 0.0.1 --packDir $publish --mainExe VendorProbe.exe --packTitle 'Minecraft Launcher Diagnostic' --packAuthors Diagnostic --channel dev --runtime win-x64 --shortcuts None --outputDir $feed
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostic vpk packaging failed.' }
    & (Join-Path $PSScriptRoot 'build-bootstrapper.ps1') -ReleaseDirectory $feed -Version 0.0.1 -Channel dev -Diagnostic
} finally { Pop-Location }
