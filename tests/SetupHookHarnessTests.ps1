# CI safety gate: syntax + static contracts + fail-closed negative case. Does NOT execute installer.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$script = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/setup-hook-e2e-harness.ps1'
$tokens = $null; $parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($script,[ref]$tokens,[ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw "Harness parser errors: $($parseErrors | Out-String)" }
$content = Get-Content -LiteralPath $script -Raw
foreach ($token in @('PREPARED_NOT_EXECUTED','INCONCLUSIVE_OBSERVATION','PrivateDirectory','LiveIdentity',
 'setup-hook-ready-','setup-hook-continued-','setup-hook-timeout-','AreAccessRulesProtected','GetOwnerSid',
 'FileMode]::CreateNew','verify-diagnostic-artifact.py','33f3b5831dfa261949e637332ba44e40249c8ec8')) {
    if (-not $content.Contains($token)) { throw "Missing fail-closed invariant: $token" }
}
foreach ($token in @('Start-Process','Stop-Process','TerminateProcess','SetEvent(', 'OpenEvent(',
 'Remove-Item','Invoke-Expression','Invoke-RestMethod','Invoke-WebRequest')) {
    if ($content.Contains($token)) { throw "Forbidden side effect in harness: $token" }
}
$root = Join-Path $env:RUNNER_TEMP ("SetupHookHarnessNegative-$([guid]::NewGuid().ToString('N'))")
New-Item -ItemType Directory -Path $root | Out-Null
try {
    $rejected = $false
    try {
        & $script -Mode Prepare -ArtifactDirectory $root -EvidenceRoot $root -ReportDirectory $root -ExpectedComputerName 'Windows11TEST' *> $null
    } catch { $rejected = $true }
    if (-not $rejected) { throw 'Harness accepted an invalid runner or invalid diagnostic artifact' }
    if (@(Get-ChildItem -LiteralPath $root -Force).Count -gt 0) { throw 'Rejected attempt wrote evidence' }
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
Write-Host 'PASS: static harness contracts, parser and negative path; no Setup.exe launched'
