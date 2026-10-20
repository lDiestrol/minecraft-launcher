# PR15 Setup Hook evidence witness. Data-only: no installer launch, signal, cancellation or cleanup.
[CmdletBinding()]
param(
    [ValidateSet('Prepare','Observe')][string]$Mode = 'Prepare',
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Parameter(Mandatory)][string]$ReportDirectory,
    [string]$ExpectedComputerName = 'Windows11TEST',
    [string]$RunId,
    [int]$SetupPid,
    [long]$SetupStartUtcTicks,
    [string]$SetupLog
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PinnedSource = '33f3b5831dfa261949e637332ba44e40249c8ec8'
$PinnedWorkflow = 'de75ddafcb8bc2fdf2537b65ccb6da478c647a56'
$PinnedSha256 = '478f775109bcd49e7fb7811adef54cba6e7137cbd3d3fa798331b8d30003acaa'
$DiagnosticPack = 'MinecraftLauncher.BootstrapperDiagnostic'
$Registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MinecraftLauncher.BootstrapperDiagnostic'
function Require([bool]$Ok,[string]$Reason) { if (-not $Ok) { throw "PRECHECK REJECTED: $Reason" } }
function LocalPath([string]$Path,[string]$Description,[bool]$Directory) {
    Require ($Path -cmatch '^[A-Za-z]:\\' -and $Path -notmatch '[\\/](?:\.|\.\.)[\\/]') "$Description must be a canonical local path"
    $full = [IO.Path]::GetFullPath($Path)
    Require ($full.TrimEnd('\') -ceq $Path.TrimEnd('\')) "$Description is not canonical"
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($full))
    Require ($drive.DriveType -eq [IO.DriveType]::Fixed) "$Description must be on a local fixed volume"
    $item = Get-Item -LiteralPath $full -Force -ErrorAction Stop
    Require ($item.PSIsContainer -eq $Directory) "$Description has the wrong type"
    $cursor = if ($Directory) { $full } else { [IO.Path]::GetDirectoryName($full) }
    while ($cursor) {
        $ancestor = Get-Item -LiteralPath $cursor -Force
        Require (-not (($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) "$Description has a reparse ancestor"
        $parent = [IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
    Require (-not (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) "$Description is a reparse point"
    return $full
}
function PrivateDirectory([string]$Path,[string]$Sid) {
    $acl = Get-Acl -LiteralPath $Path -ErrorAction Stop
    Require ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -eq $Sid) 'Owner of evidence/report differs from current user'
    Require ($acl.AreAccessRulesProtected) 'Inherited DACL in evidence/report namespace'
    $allowed = @($Sid, 'S-1-5-18', 'S-1-5-32-544')
    $full = $false
    foreach ($ace in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
        Require ($ace.IdentityReference.Value -in $allowed -and -not $ace.IsInherited) 'Untrusted evidence/report ACL entry'
        if ($ace.IdentityReference.Value -eq $Sid -and $ace.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow -and
            (($ace.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -eq [Security.AccessControl.FileSystemRights]::FullControl)) { $full = $true }
    }
    Require $full 'Current user must have full control of evidence/report namespace'
}
function Receipt([string]$Path,[string]$Id) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $safe = LocalPath $Path 'Hook receipt' $false
    $entry = Get-Item -LiteralPath $safe
    Require ($entry.Length -gt 0 -and $entry.Length -le 16384) 'Malformed hook receipt size'
    $value = Get-Content -LiteralPath $safe -Raw -Encoding utf8 | ConvertFrom-Json -ErrorAction Stop
    Require ($value.schemaVersion -eq 1 -and $value.runId -ceq $Id -and $value.hookPid -gt 0 -and $value.hookStartUtcTicks -gt 0) 'Hook receipt schema/identity mismatch'
    return $value
}
function LiveIdentity([int]$PidNumber,[long]$Start,[string]$ExpectedExe) {
    Require ($PidNumber -gt 0 -and $Start -gt 0) 'Missing pinned process identity'
    $process = Get-Process -Id $PidNumber -ErrorAction Stop
    Require (-not $process.HasExited -and $process.StartTime.ToUniversalTime().Ticks -eq $Start) 'Process no longer active or PID reused'
    Require ($process.ProcessName -ieq [IO.Path]::GetFileNameWithoutExtension($ExpectedExe)) 'Observed process executable name differs'
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId = $PidNumber" -ErrorAction Stop
    Require ($null -ne $cim -and [IO.Path]::GetFileName($cim.ExecutablePath) -ieq $ExpectedExe) 'Observed process executable path is unavailable'
    $owner = Invoke-CimMethod -InputObject $cim -MethodName GetOwnerSid
    Require ($owner.ReturnValue -eq 0 -and $owner.Sid -eq [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) 'Observed process user SID mismatch'
    return @{ pid=$PidNumber; startUtcTicks=$Start; executable=$ExpectedExe; parentPid=[int]$cim.ParentProcessId; liveAtObservation=$true }
}
function NewRecord([string]$Folder,[hashtable]$Data) {
    $path = Join-Path $Folder ("setup-hook-preflight-$([guid]::NewGuid().ToString('N')).json")
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Data | ConvertTo-Json -Depth 7) + [Environment]::NewLine)
    $stream = [IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    return $path
}

Require ($PSVersionTable.PSVersion.Major -ge 7 -and $IsWindows) 'PowerShell 7 on Windows required'
Require ($ExpectedComputerName -ieq 'Windows11TEST' -and $env:COMPUTERNAME -ieq $ExpectedComputerName) 'Witness limited to Windows11TEST'
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
Require (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 'Use a non-elevated Windows user'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$artifact = LocalPath $ArtifactDirectory 'Artifact directory' $true
$evidence = LocalPath $EvidenceRoot 'Evidence root' $true
$report = LocalPath $ReportDirectory 'Report directory' $true
Require ($evidence -ine $report) 'Report and evidence namespaces must be separate'
foreach ($folder in @($evidence,$report)) {
    PrivateDirectory $folder $sid
    $parent = [IO.Directory]::GetParent($folder)
    Require ($null -ne $parent -and $null -ne $parent.Parent) 'Private parent directory required'
    PrivateDirectory $parent.FullName $sid
}
$bundle = LocalPath (Join-Path $artifact 'MinecraftLauncher-Setup.exe') 'Diagnostic EXE' $false
$checksums = LocalPath (Join-Path $artifact 'SHA256SUMS.txt') 'Checksum manifest' $false
$infoFile = LocalPath (Join-Path $artifact 'build-info.json') 'Build metadata' $false
$names = @(Get-ChildItem -LiteralPath $artifact -Force | ForEach-Object Name | Sort-Object)
Require (($names -join '|') -ceq 'build-info.json|MinecraftLauncher-Setup.exe|SHA256SUMS.txt') 'Unexpected artifact directory contents'
$raw = Get-Content -LiteralPath $infoFile -Raw -Encoding utf8
Require ($raw.Length -gt 0 -and $raw.Length -le 65536) 'Build metadata size rejected'
$info = $raw | ConvertFrom-Json -ErrorAction Stop
Require ($info.sourceCommit -ceq $PinnedSource -and $info.workflowCommit -ceq $PinnedWorkflow) 'Artifact provenance mismatch'
Require ($info.packId -ceq $DiagnosticPack -and $info.mainExe -ceq 'VendorProbe.exe' -and
    $info.version -ceq '0.0.1' -and $info.channel -ceq 'dev' -and $info.velopackVersion -ceq '1.2.161') 'Diagnostic identity mismatch'
Require ($info.executed -ceq $false -and $info.setupSha256 -ceq $PinnedSha256) 'Artifact metadata hash/execution mismatch'
$actual = (Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash.ToLowerInvariant()
Require ($actual -ceq $PinnedSha256) 'Diagnostic EXE SHA256 mismatch'
$hashLines = @(Get-Content -LiteralPath $checksums -Encoding utf8)
Require ($hashLines.Count -eq 1 -and $hashLines[0] -ceq "$PinnedSha256  MinecraftLauncher-Setup.exe") 'Checksum manifest mismatch'
$verifier = Join-Path $PSScriptRoot 'verify-diagnostic-artifact.py'
Require (Test-Path -LiteralPath $verifier -PathType Leaf) 'Trusted static verifier missing'
$verifiedOutput = & python -B $verifier --bundle $bundle --checksums $checksums 2>&1
Require ($LASTEXITCODE -eq 0) 'Static verifier rejected bundle'
$verified = ($verifiedOutput | Out-String) | ConvertFrom-Json -ErrorAction Stop
Require ($verified.sha256 -ceq $PinnedSha256 -and $verified.packId -ceq $DiagnosticPack) 'Static verifier identity mismatch'

$existing = Test-Path -LiteralPath $Registration
$record = @{ schemaVersion=1; utc=[DateTime]::UtcNow.ToString('O'); mode=$Mode; machine=$env:COMPUTERNAME;
    sourceCommit=$PinnedSource; workflowCommit=$PinnedWorkflow; sha256=$PinnedSha256;
    staticArtifactVerified=$true; harnessLaunchedSetup=$false;
    diagnosticRegistrationPresent=[bool]$existing; result='INCONCLUSIVE'; details=@{} }
if ($Mode -eq 'Prepare') {
    Require (-not $existing) 'Existing diagnostic registration detected; aborting clean baseline'
    Require (-not $RunId -and $SetupPid -eq 0 -and $SetupStartUtcTicks -eq 0 -and -not $SetupLog) 'Prepare accepts no live-process inputs'
    Require (@(Get-ChildItem -LiteralPath $evidence -Force).Count -eq 0) 'Evidence directory must be empty for new attempt'
    Require (@(Get-ChildItem -LiteralPath $report -Force).Count -eq 0) 'Report directory must be empty for new attempt'
    $record.result = 'PREPARED_NOT_EXECUTED'
    $record.details = @{ note='VMID 103/PBS and controller must be separately reviewed before running the installer.' }
} else {
    Require ($RunId -cmatch '^[0-9a-f]{32}$') 'Observe needs fresh lowercase GUID N run ID'
    Require ($SetupPid -gt 0 -and $SetupStartUtcTicks -gt 0) 'Observe needs Setup PID and exact UTC start ticks'
    $ready = Receipt (Join-Path $evidence "setup-hook-ready-$RunId.json") $RunId
    $continued = Receipt (Join-Path $evidence "setup-hook-continued-$RunId.json") $RunId
    $timedout = Receipt (Join-Path $evidence "setup-hook-timeout-$RunId.json") $RunId
    Require (-not ($continued -and $timedout)) 'Conflicting terminal receipts'
    Require (-not (($continued -or $timedout) -and $null -eq $ready)) 'Terminal receipt without ready receipt'
    if ($null -ne $ready) {
        Require ($ready.state -ceq 'hook_entered' -and $ready.timeoutMs -eq 20000 -and
            $ready.eventName -ceq "Local\MinecraftLauncher.SetupHook.$RunId") 'Ready receipt contract mismatch'
        foreach ($terminal in @($continued,$timedout)) {
            if ($null -ne $terminal) {
                Require ($terminal.hookPid -eq $ready.hookPid -and $terminal.hookStartUtcTicks -eq $ready.hookStartUtcTicks) 'Hook PID/start mismatch'
            }
        }
        if ($continued) { Require ($continued.state -ceq 'continued') 'Continue state mismatch' }
        if ($timedout) { Require ($timedout.state -ceq 'timeout') 'Timeout state mismatch' }
    }
    $setup = LiveIdentity $SetupPid $SetupStartUtcTicks 'Setup.exe'
    $hook = if ($null -ne $ready) { LiveIdentity ([int]$ready.hookPid) ([long]$ready.hookStartUtcTicks) 'VendorProbe.exe' } else { $null }
    $log = $null
    if ($SetupLog) {
        $path = LocalPath $SetupLog 'Setup log' $false
        $file = Get-Item -LiteralPath $path
        Require ($file.Length -le 2MB) 'Log exceeds size bound'
        $log = @{ bytes=$file.Length; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $record.result = 'INCONCLUSIVE_OBSERVATION'
    $record.details = @{ runId=$RunId; ready=($null -ne $ready); continued=($null -ne $continued);
        timedOut=($null -ne $timedout); setup=$setup; hook=$hook; log=$log;
        note='Point-in-time witness only. Not evidence of parent-child relationship at Cancel or of E2E PASS.' }
}
$out = NewRecord $report $record
Write-Output "NON-DESTRUCTIVE $($record.result): $out"
