# Diagnostic Velopack Setup hook barrier — experimental

**Scope:** diagnostic VendorProbe only. Branch is based on PR #14 HEAD `32cfb2e`; do not merge it into the production branch. The production Launcher, coordinator, installation backend, native GUI and release scripts are not changed. This is not a release approval or a Windows E2E PASS.

## Mechanism

`VendorProbe.exe` registers `OnAfterInstallFastCallback` before `VelopackApp.Run()`. The callback runs inside a real Velopack installation hook after the installer has started and reached the hook phase. Without the two environment variables below it does nothing, so ordinary diagnostic installs remain unchanged.

The harness may set BOTH environment variables **only for the new, consented diagnostic GUI process**:

- `LAUNCHER_DIAGNOSTIC_SETUP_BARRIER_ROOT`: canonical pre-existing local directory, outside disposable install App/GameData, with reviewed protected ACL and no reparse ancestors; unique for each attempt. This is where hook JSON receipts are written with `CreateNew`.
- `LAUNCHER_DIAGNOSTIC_SETUP_BARRIER_ID`: a freshly generated lowercase 32-character GUID without hyphens.

The diagnostic native GUI inherits environment; its helper inherits the GUI environment; the helper starts Setup with inherited environment; Velopack's install hook may inherit those variables. **This chain must be independently verified with a packaged artifact before any destructive E2E.** The present commit does not inject any options into the production helper or change its process launch.

The hook creates `Local\\MinecraftLauncher.SetupHook.<id>` with exclusive new-name policy and then writes `setup-hook-ready-<id>.json` (schema 1; hook PID, UTC start ticks, event name, 20s deadline). It waits up to 20s for Continue. On a signal it writes `setup-hook-continued-<id>.json`; on timeout it writes `setup-hook-timeout-<id>.json` and throws. Receipts and event identity are per-run and must be treated as local private diagnostic data. A ready receipt is evidence that a real install hook started, **not by itself proof that Setup is still active at GUI Cancel**. Verify Setup process handle, identity, parent, command line and absence of completion immediately before Cancel; classify races as INCONCLUSIVE.

This bounded hook has a substantially narrower observable phase than a generic 'during Setup' point. Velopack imposes a ~30-second fast-hook watchdog, so the 20s local limit is deliberate. Do not use an indefinite wait or infer success from a timeout/termination.

## Limits and safety gates

1. No packaging is enabled in PR #14 CI by this branch. A new hash-pinned diagnostic artifact requires separate explicit authorization to build and distribute. The previous source `6496570` EXE cannot exercise this hook.
2. Do not run the hook on VM 103 until the new artifact passes CI, integrity verification, and the harness identity/namespace/ACL review.
3. On timeout, Velopack's reaction to a throwing hook is not a claimed guarantee of failure; inspect installed App, HKCU and durable completion independently.
4. An observer opening the installer Job changes `KILL_ON_JOB_CLOSE` lifetime. Avoid an extra Job handle during the real scenario. Neither this hook nor its unit tests opens a Job.
5. Do not terminate/setup/close any production process; only GUI Cancel of the explicitly isolated diagnostic run is in scope after separate consent. Never touch AWG, VPN, DNS, routes, RDP, firewall, PBS, genuine Minecraft or GameData.
6. A real Windows E2E must establish that the diagnostic install hook executes and can publish the ready receipt; unit tests test only the gate itself.
7. Do not use the barrier receipt folder without reviewing its ACL; this experimental implementation validates absolute local path and reparse ancestors but does **not** audit DACL or ownership internally. Harness MUST enforce ACL and reject unsafe namespace before running anything.

## Local CI coverage

The Core.Tests project links the diagnostic-only source. Cases exercise opt-in, malformed IDs and paths, bounded timeout, Continue, durable receipts, and duplicate run rejection. These are component tests, not an installer process E2E. CI packaging remains disabled.

## Review-before-merge checklist

- Validate the SDK 1.2.161 callback signature and hook invocation on actual Windows runner.
- Test environment propagation to install hook and the diagnostic opt-in contract.
- Verify hook output directory ACL/owner, no-reparse and identity assumptions.
- Decide whether the timeout exception is an acceptable experimental failure path.
- Confirm no production binary or packaged output includes barrier source.
- Repeat independent safety review before a dedicated diagnostic build and live VM E2E.
