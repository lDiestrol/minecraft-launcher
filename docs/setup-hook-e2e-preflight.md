# PR #15 — Setup Hook E2E: non-executing preflight and observer (stage 1)

**Experimental, Draft only.** This code does not authorize a Windows E2E test or prove E2E PASS. It never invokes the bundled EXE, Setup, helper, Cancel or Continue. Only CI static tests and preflight preparation are in scope.

The witness is in `scripts/setup-hook-e2e-harness.ps1`. Its two modes are `Prepare` (artifact/environment inspection) and `Observe` (point-in-time, read-only process/receipt observation after a separately consented installer run). Result classifications are **PREPARED_NOT_EXECUTED** and **INCONCLUSIVE_OBSERVATION**; never PASS.

## Immutable diagnostic artifact

- Source commit: `33f3b5831dfa261949e637332ba44e40249c8ec8` (CI #41 successful).
- Packaging workflow commit: `de75ddafcb8bc2fdf2537b65ccb6da478c647a56` (packaging v2 successful).
- EXE SHA-256: `478f775109bcd49e7fb7811adef54cba6e7137cbd3d3fa798331b8d30003acaa`.
- Only three extracted artifact files are accepted: `MinecraftLauncher-Setup.exe`, `SHA256SUMS.txt`, `build-info.json`. The witness compares every pinned field and calls `verify-diagnostic-artifact.py` **as a data-only verifier**, including the MCBOOT01/catalog/vendor manifest. It does not execute any PE file.

## Required human checks before any live test

Before proceeding to Proxmox VM **103**, named `Windows11TEST` in PVE, independently confirm the VM and its usable PBS backup/snapshot, isolation of target App and GameData, and that no other diagnostic installation or active installer exists. The **actual Windows hostname is `DESKTOP-HUC2CMF`**, not `Windows11TEST`. The guest SMBIOS UUID **`a06e6a51-dbad-46bb-856d-c006f38896dd`** was separately observed both in `qm config 103` and via `Get-CimInstance Win32_ComputerSystemProduct`. The harness requires BOTH the pinned hostname and UUID from CIM before reading the diagnostic artifact. Hostname or SMBIOS UUID alone is not cryptographic host attestation; cloned/spoofed SMBIOS data and stale PBS copies remain outside its proof. Do not touch AWG, RDP, networking, production launcher, real GameData, or other installer Jobs.

Use Windows PowerShell 7 (`pwsh`) under a **non-elevated** account inside PVE `Windows11TEST`, whose Windows computer name is `DESKTOP-HUC2CMF`. Provide pre-existing, separate folders for evidence and reports inside an explicitly reviewed private parent. The **owner must equal the current SID**, with a protected DACL allowing only the user, SYSTEM and Administrators; no reparse ancestors or removable/UNC drives. This tool never creates those directories or changes ACLs. `Prepare` requires empty evidence/report folders and no diagnostic HKCU uninstall registration.

Example future **data-only** preflight command after manual preparation (not approval to launch EXE):

```powershell
pwsh -NoProfile -File .\scripts\setup-hook-e2e-harness.ps1 -Mode Prepare -ArtifactDirectory 'C:\SetupHookLab\private\artifact' -EvidenceRoot 'C:\SetupHookLab\private\evidence' -ReportDirectory 'C:\SetupHookLab\private\reports'
```

Each successful invocation writes only one new UTF-8 JSON report via `FileMode.CreateNew` in the approved private report directory, never modifies an existing receipt or registration. The report records the guest SMBIOS UUID and expected PVE VMID as provenance **claims, not remote PVE attestation**. `Prepare` requires an empty report folder: use a fresh report folder instead of the Work investigation folder that already contains `REPORT.md` and `result.json`. Failures leave the state unchanged.

## Observer contract (only after *separate* live-install permission)

Use `-Mode Observe -RunId <lowercase-GUID-N> -SetupPid <PID> -SetupStartUtcTicks <ticks>` alongside the same artifact/evidence/report arguments. It checks the receipt schema, event name, 20s timeout, contradictory terminal receipts, live Setup.exe and VendorProbe.exe PID/start ticks, process user SID and executable name. Optional `-SetupLog` reports the hash and length of an existing bounded log, never raw text.

An observed `setup-hook-ready` receipt **does not establish active Setup at the future Cancel instant**. A live E2E controller must additionally prove process ancestry/command line, GUI button handler identity, Job lifecycle, correct cancellation outcome and absence of false completion. This stage-1 witness never opens the installer Job (additional handles can change KILL_ON_JOB_CLOSE behavior), sends a named event, clicks a GUI control, or terminates a process. If a process is gone, suspicious, or receipts conflict, it fails closed. **There is no automation of installation, merge, release, cleanup or recovery in this stage.**

CI only parses and tests the non-executing negative path. A real Windows E2E is not performed by GitHub Actions.
