"""Read-only observer/verifier; optional explicit continue signals a session event, never writes files.

This tool does not launch the GUI or click Cancel. An observer-ready exit is not an E2E PASS.
"""
import argparse
import ctypes
import json
import os
from pathlib import Path
import re
import stat
import time

MANUAL_CANCEL_MINIMUM_MS = 10000

CANCEL_SEQUENCE = (
    "install_requested", "extraction_started", "payload_bytes_written", "barrier_reached",
    "gui_cancel", "worker_observes_cancellation", "extraction_aborted", "helper_not_started",
)
FIELDS = {"schemaVersion", "runId", "sessionNonce", "sequence", "event", "utcFileTimeTicks",
          "monotonicMs", "pid", "processStartFileTimeTicks", "bytesWritten", "totalBytes"}


def identifier(value):
    if not isinstance(value, str) or re.fullmatch(r"[0-9a-f]{32}", value) is None:
        raise ValueError("Expected a fresh 32 lowercase hex identifier")
    return value


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate journal field")
        result[key] = value
    return result


def read_events(root):
    root = Path(root).absolute()
    # Reject symlink/reparse ancestors as well as journal leaves. No creation or repair.
    for item in (root, *root.parents):
        info = item.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400:
            raise ValueError("Linked evidence path rejected")
    events = []
    for index in range(1, 11):
        path = root / f"extraction-{index:02}.json"
        try:
            info = path.lstat()
        except FileNotFoundError:
            if any((root / f"extraction-{later:02}.json").exists() for later in range(index + 1, 11)):
                raise ValueError("Journal event missing")
            break
        if not stat.S_ISREG(info.st_mode) or getattr(info, "st_file_attributes", 0) & 0x400 or not 0 < info.st_size <= 16384:
            raise ValueError("Unsafe journal leaf or size")
        with path.open("rb") as stream:
            data = stream.read(16385)
        if not 0 < len(data) <= 16384:
            raise ValueError("Invalid journal size")
        events.append(json.loads(data.decode("utf-8"), object_pairs_hook=unique_object))
    return events


def validate(events, run_id, nonce=None, complete=True, pid=None, start=None):
    identifier(run_id)
    if nonce is not None:
        identifier(nonce)
    expected_count = 8 if complete else 4
    if len(events) != expected_count:
        raise ValueError("Missing/extra events; no E2E PASS")
    if any(not isinstance(event, dict) for event in events):
        raise ValueError("Journal entries must be objects")
    first = events[0]
    session = identifier(first.get("sessionNonce"))
    if nonce is not None and session != nonce:
        raise ValueError("Stale session nonce")
    last_mono = -1
    for index, event in enumerate(events, 1):
        extra = {"continueEvent", "timeoutMs"} if index == 4 else set()
        if set(event) != FIELDS | extra:
            raise ValueError("Unknown/missing journal fields")
        for field in FIELDS - {"runId", "sessionNonce", "event"}:
            if type(event[field]) is not int or not 0 <= event[field] <= 0xFFFFFFFFFFFFFFFF:
                raise ValueError("Non-integer or out-of-range journal field")
        if event["schemaVersion"] != 1 or event["sequence"] != index or event["event"] != CANCEL_SEQUENCE[index - 1]:
            raise ValueError("Journal sequence violation")
        if event["runId"] != run_id or event["sessionNonce"] != session:
            raise ValueError("Stale run/session")
        if event["utcFileTimeTicks"] <= 0 or event["monotonicMs"] < last_mono:
            raise ValueError("Invalid timestamps/order")
        last_mono = event["monotonicMs"]
        if not 0 < event["pid"] <= 0xFFFFFFFF or event["processStartFileTimeTicks"] <= 0:
            raise ValueError("Invalid process identity")
        for field in ("pid", "processStartFileTimeTicks", "totalBytes"):
            if event[field] != first[field]:
                raise ValueError("Process/payload identity changed")
        if not 0 < event["totalBytes"] <= 8589934592 or event["bytesWritten"] != (0 if index < 3 else events[2]["bytesWritten"]):
            raise ValueError("Invalid partial payload evidence")
        if index >= 3 and not (0 < event["bytesWritten"] <= 65536 and event["bytesWritten"] < event["totalBytes"]):
            raise ValueError("Payload not partial at barrier")
    if (pid is not None and first["pid"] != pid) or (start is not None and first["processStartFileTimeTicks"] != start):
        raise ValueError("Unexpected GUI process identity")
    barrier = events[3]
    expected_name = f"Local\\MinecraftLauncher.Extraction.{run_id}.{session}"
    if barrier["continueEvent"] != expected_name or type(barrier["timeoutMs"]) is not int or barrier["timeoutMs"] != 30000:
        raise ValueError("Invalid control event/timeout")
    if complete and events[4]["monotonicMs"] - barrier["monotonicMs"] > barrier["timeoutMs"]:
        raise ValueError("GUI Cancel occurred after the barrier deadline")
    return barrier


def windows_api():
    if os.name != "nt":
        raise ValueError("Live control requires Windows; only offline verification is portable")
    from ctypes import wintypes
    api = ctypes.WinDLL("kernel32", use_last_error=True)
    api.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    api.OpenProcess.restype = wintypes.HANDLE
    api.GetProcessTimes.argtypes = [wintypes.HANDLE, *([ctypes.POINTER(wintypes.FILETIME)] * 4)]
    api.GetProcessTimes.restype = wintypes.BOOL
    api.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    api.WaitForSingleObject.restype = wintypes.DWORD
    api.OpenEventW.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.LPCWSTR]
    api.OpenEventW.restype = wintypes.HANDLE
    api.SetEvent.argtypes = [wintypes.HANDLE]
    api.SetEvent.restype = wintypes.BOOL
    api.GetTickCount64.argtypes = []
    api.GetTickCount64.restype = ctypes.c_ulonglong
    api.CloseHandle.argtypes = [wintypes.HANDLE]
    api.CloseHandle.restype = wintypes.BOOL
    return api, wintypes


def live_process(api, types, pid, start):
    process = api.OpenProcess(0x1000 | 0x100000, False, pid)  # query-limited + synchronize, no termination rights
    if not process:
        raise ValueError("GUI process unavailable")
    try:
        created, exited, kernel, user = (types.FILETIME() for _ in range(4))
        if not api.GetProcessTimes(process, ctypes.byref(created), ctypes.byref(exited), ctypes.byref(kernel), ctypes.byref(user)):
            raise ValueError("GUI process time unavailable")
        ticks = created.dwLowDateTime | created.dwHighDateTime << 32
        if ticks != start or api.WaitForSingleObject(process, 0) != 258:
            raise ValueError("Stale/exited GUI process")
        return process
    except Exception:
        api.CloseHandle(process)
        raise


def confirm_live_barrier(api, types, root, run_id, nonce, pid, start, minimum_ms):
    # Re-read after the polling result: the worker may have advanced during observation.
    barrier = validate(read_events(root), run_id, nonce, complete=False, pid=pid, start=start)
    checked = live_process(api, types, pid, start)  # Fresh PID/start-time + liveness check.
    try:
        # Journal monotonicMs is Windows GetTickCount64, not Python's clock epoch.
        elapsed = api.GetTickCount64() - barrier["monotonicMs"]
        remaining = barrier["timeoutMs"] - elapsed
        if elapsed < 0 or remaining <= 0 or remaining < minimum_ms:
            raise ValueError("Barrier expired or insufficient time for the requested action")
        if api.WaitForSingleObject(checked, 0) != 258:
            raise ValueError("GUI exited during barrier confirmation")
        return barrier, remaining
    finally:
        api.CloseHandle(checked)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("wait", "verify", "continue"))
    parser.add_argument("evidence", type=Path)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--nonce")
    parser.add_argument("--pid", type=int)
    parser.add_argument("--process-start-ticks", type=int)
    args = parser.parse_args()
    identifier(args.run_id)
    if args.mode == "verify":
        if args.nonce is None:
            parser.error("verify requires --nonce captured by the observer")
        validate(read_events(args.evidence), args.run_id, args.nonce, pid=args.pid, start=args.process_start_ticks)
        print("PASS: complete extraction Cancel journal; independently check exit codes, absent helper processes and preserved state")
        return
    if args.pid is None or args.process_start_ticks is None:
        parser.error("live observation/control requires --pid and --process-start-ticks from Start-Process")
    if args.mode == "continue" and args.nonce is None:
        parser.error("continue requires --nonce captured by the observer")
    api, types = windows_api()
    process = live_process(api, types, args.pid, args.process_start_ticks)
    try:
        deadline = time.monotonic() + 35
        while True:
            try:
                events = read_events(args.evidence)
            except (FileNotFoundError, json.JSONDecodeError):
                events = []  # Writer may still hold a newly created file; never accept it as a signal.
            except PermissionError:
                events = []  # CREATE_NEW writer holds exclusive sharing until flush/close.
            if len(events) >= 4:
                barrier = validate(events, args.run_id, args.nonce, complete=False, pid=args.pid, start=args.process_start_ticks)
                break
            if time.monotonic() >= deadline or api.WaitForSingleObject(process, 0) != 258:
                raise ValueError("No valid barrier from live GUI; FAIL")
            time.sleep(0.025)  # Poll for a durable event, never guess an extraction duration.
        barrier, remaining = confirm_live_barrier(
            api, types, args.evidence, args.run_id, barrier["sessionNonce"], args.pid,
            args.process_start_ticks, MANUAL_CANCEL_MINIMUM_MS if args.mode == "wait" else 0)
        if args.mode == "wait":
            print(f"BARRIER OBSERVED, not E2E PASS. Session nonce: {barrier['sessionNonce']}. Remaining at check: {remaining}ms. Click Cancel in GUI PID {args.pid} now.")
        else:
            event = api.OpenEventW(2, False, barrier["continueEvent"])
            if not event:
                raise ValueError("Session control event unavailable")
            try:
                # Opening a handle can consume time; never signal an expired/exited session.
                confirm_live_barrier(api, types, args.evidence, args.run_id, barrier["sessionNonce"],
                                     args.pid, args.process_start_ticks, 0)
                if not api.SetEvent(event):
                    raise ValueError("Continue signal failed")
            finally:
                api.CloseHandle(event)
            print("Continue sent: extraction-only negative/control case; cannot PASS the Cancel sequence and cannot start helper")
    finally:
        api.CloseHandle(process)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, TypeError, KeyError) as error:
        raise SystemExit(f"FAIL: {error}") from error
