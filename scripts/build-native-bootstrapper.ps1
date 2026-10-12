[CmdletBinding()]
param([switch]$Diagnostic, [switch]$Tests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere)) { throw 'Microsoft C++ Build Tools are required.' }
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'MSVC x64/x86 tools are missing.' }
$flavor = if ($Diagnostic) { 'diagnostic' } else { 'production' }
$output = Join-Path $repo "artifacts\bootstrapper\native-$flavor"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$source = Join-Path $repo 'native\Launcher.Bootstrapper'
$define = if ($Diagnostic) { '/DINSTALLER_DIAGNOSTIC' } else { '' }
$commands = @"
@echo off
call "$vs\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64
if errorlevel 1 exit /b 1
cd /d "$source"
rc /nologo /fo "$output\bootstrapper.res" bootstrapper.rc
if errorlevel 1 exit /b 1
cl /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /utf-8 $define /Fo"$output\main.obj" main.cpp "$output\bootstrapper.res" user32.lib gdi32.lib shell32.lib ole32.lib uuid.lib advapi32.lib bcrypt.lib comctl32.lib /link /SUBSYSTEM:WINDOWS /MACHINE:X64 /MANIFEST:NO /OUT:"$output\bootstrapper.exe"
if errorlevel 1 exit /b 1
dumpbin /dependents "$output\bootstrapper.exe"
if errorlevel 1 exit /b 1
mt -nologo -inputresource:"$output\bootstrapper.exe";#1 -out:"$output\verified.manifest"
if errorlevel 1 exit /b 1
exit /b 0
"@
if ($Tests) {
    $commands = $commands.Replace('exit /b 0', @"
cl /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /utf-8 $define /Fo"$output\tests.obj" tests.cpp user32.lib shell32.lib ole32.lib uuid.lib advapi32.lib bcrypt.lib /link /SUBSYSTEM:CONSOLE /MACHINE:X64 /OUT:"$output\native-tests.exe"
if errorlevel 1 exit /b 1
cl /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /utf-8 /Fo"$output\lifecycle-tests.obj" lifecycle-tests.cpp user32.lib gdi32.lib shell32.lib ole32.lib uuid.lib advapi32.lib bcrypt.lib comctl32.lib /link /SUBSYSTEM:CONSOLE /MACHINE:X64 /OUT:"$output\native-lifecycle-tests.exe"
if errorlevel 1 exit /b 1
exit /b 0
"@)
}
[System.IO.File]::WriteAllText((Join-Path $output 'build.cmd'), $commands, [System.Text.Encoding]::Default)
& cmd.exe /d /c (Join-Path $output 'build.cmd')
if ($LASTEXITCODE -ne 0) { throw 'Native bootstrapper build failed.' }
Write-Output (Join-Path $output 'bootstrapper.exe')
