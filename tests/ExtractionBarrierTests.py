"""Synthetic journal/controller regressions; no GUI, installation or Windows evidence reads."""
import copy
import importlib.util
import json
import io
from pathlib import Path
import tempfile
import unittest
from unittest import mock
from ctypes import wintypes

spec = importlib.util.spec_from_file_location("extraction_barrier", Path(__file__).resolve().parents[1] / "scripts/extraction-barrier.py")
barrier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(barrier)
RUN, NONCE = "a" * 32, "b" * 32


def journal():
    events = []
    for index, name in enumerate(barrier.CANCEL_SEQUENCE, 1):
        event = dict(schemaVersion=1, runId=RUN, sessionNonce=NONCE, sequence=index, event=name,
                     utcFileTimeTicks=100 + index, monotonicMs=index, pid=12, processStartFileTimeTicks=99,
                     bytesWritten=0 if index < 3 else 32, totalBytes=100)
        if index == 4:
            event.update(continueEvent=f"Local\\MinecraftLauncher.Extraction.{RUN}.{NONCE}", timeoutMs=30000)
        events.append(event)
    return events


class FakeWindowsAPI:
    def __init__(self):
        self.alive = True
        self.start = 99
        self.ticks = 10
        self.opens = []
        self.closed = []
        self.signals = []
        self.on_clock = lambda: None
        self.on_open_event = lambda: None

    def OpenProcess(self, rights, inherit, pid):
        self.opens.append((rights, inherit, pid))
        return 42

    def GetProcessTimes(self, handle, created, exited, kernel, user):
        created._obj.dwLowDateTime = self.start
        created._obj.dwHighDateTime = 0
        return True

    def WaitForSingleObject(self, handle, timeout):
        return 258 if self.alive else 0

    def GetTickCount64(self):
        self.on_clock()
        return self.ticks

    def CloseHandle(self, handle):
        self.closed.append(handle)

    def OpenEventW(self, rights, inherit, name):
        self.on_open_event()
        return 43

    def SetEvent(self, handle):
        self.signals.append(handle)
        return True


class ExtractionBarrierTests(unittest.TestCase):
    def run_live_main(self, api, mode="wait", after_read=None):
        reads = 0

        def read(root):
            nonlocal reads
            reads += 1
            if after_read:
                after_read(reads)
            return journal()[:4]

        argv = ["extraction-barrier.py", mode, "unused-owned-evidence", "--run-id", RUN,
                "--pid", "12", "--process-start-ticks", "99"]
        if mode == "continue":
            argv += ["--nonce", NONCE]
        output = io.StringIO()
        with mock.patch.object(barrier, "windows_api", return_value=(api, wintypes)), \
             mock.patch.object(barrier, "read_events", side_effect=read), \
             mock.patch("sys.argv", argv), mock.patch("sys.stdout", output):
            try:
                barrier.main()
            except ValueError:
                self.assertNotIn("BARRIER OBSERVED", output.getvalue())
                raise
        return output.getvalue()

    def test_observer_rechecks_identity_liveness_and_manual_margin(self):
        api = FakeWindowsAPI()
        api.ticks = 20004  # Exactly 10s remain after barrier timestamp 4.
        output = self.run_live_main(api)
        self.assertIn("BARRIER OBSERVED", output)
        self.assertIn("10000ms", output)
        self.assertEqual(len(api.opens), 2)  # Before polling and again after a fresh journal read.
        self.assertTrue(all(pid == 12 for _, _, pid in api.opens))
        self.assertEqual(api.closed, [42, 42])

    def test_observer_process_exits_or_changes_identity_after_valid_prefix(self):
        for field, value in [("alive", False), ("start", 100)]:
            api = FakeWindowsAPI()

            def race(reads):
                if reads == 2:
                    setattr(api, field, value)

            with self.subTest(field=field), self.assertRaises(ValueError):
                self.run_live_main(api, after_read=race)
            self.assertEqual(len(api.opens), 2)
            self.assertEqual(api.closed, [42, 42])

    def test_observer_rejects_expired_future_and_insufficient_window(self):
        for ticks in [3, 20005, 30004, 30005]:
            api = FakeWindowsAPI()
            with self.subTest(ticks=ticks), self.assertRaises(ValueError):
                self.run_live_main(api, after_read=lambda count: setattr(api, "ticks", ticks) if count == 2 else None)
            self.assertEqual(api.closed, [42, 42])

    def test_observer_rechecks_liveness_after_clock_query(self):
        api = FakeWindowsAPI()
        api.on_clock = lambda: setattr(api, "alive", False)
        with self.assertRaises(ValueError):
            self.run_live_main(api)
        self.assertEqual(api.closed, [42, 42])

    def test_observer_rejects_worker_progress_after_poll(self):
        api = FakeWindowsAPI()
        output = io.StringIO()
        with mock.patch.object(barrier, "windows_api", return_value=(api, wintypes)), \
             mock.patch.object(barrier, "read_events", side_effect=[journal()[:4], journal()[:5]]), \
             mock.patch("sys.argv", ["tool", "wait", "unused", "--run-id", RUN, "--pid", "12", "--process-start-ticks", "99"]), \
             mock.patch("sys.stdout", output), self.assertRaises(ValueError):
            barrier.main()
        self.assertNotIn("BARRIER OBSERVED", output.getvalue())
        self.assertEqual(api.closed, [42])

    def test_continue_rechecks_after_open_before_any_signal(self):
        for field, value in [("alive", False), ("ticks", 30004), ("start", 100)]:
            api = FakeWindowsAPI()
            api.on_open_event = lambda: setattr(api, field, value)
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.run_live_main(api, mode="continue")
            self.assertEqual(api.signals, [])
            self.assertIn(43, api.closed)

    def test_exact_cancel_and_observer_prefix(self):
        barrier.validate(journal(), RUN, NONCE, pid=12, start=99)
        barrier.validate(journal()[:4], RUN, NONCE, complete=False)

    def test_each_missing_event_fails(self):
        for index in range(8):
            events = journal()
            events.pop(index)
            with self.subTest(index=index), self.assertRaises(ValueError):
                barrier.validate(events, RUN, NONCE)

    def test_reorder_duplicate_extra_fail(self):
        cases = [journal() + [journal()[-1]], list(reversed(journal()))]
        duplicate = journal()
        duplicate[5] = copy.deepcopy(duplicate[4])
        cases.append(duplicate)
        for events in cases:
            with self.assertRaises(ValueError):
                barrier.validate(events, RUN, NONCE)

    def test_stale_run_nonce_process(self):
        for kwargs in [dict(run_id="c" * 32), dict(run_id=RUN, nonce="c" * 32),
                       dict(run_id=RUN, pid=13), dict(run_id=RUN, start=100)]:
            with self.subTest(kwargs=kwargs), self.assertRaises(ValueError):
                barrier.validate(journal(), **kwargs)
        for field in ["runId", "sessionNonce", "pid", "processStartFileTimeTicks", "totalBytes"]:
            events = journal()
            events[5][field] = "c" * 32 if isinstance(events[5][field], str) else 999
            with self.subTest(field=field), self.assertRaises(ValueError):
                barrier.validate(events, RUN, NONCE)

    def test_invalid_values_and_strict_schema(self):
        faults = [(0, "sequence", True), (0, "pid", 0), (2, "bytesWritten", 0), (2, "bytesWritten", 100),
                  (3, "timeoutMs", True), (3, "continueEvent", "other"), (5, "monotonicMs", 0),
                  (6, "bytesWritten", 33), (7, "utcFileTimeTicks", 0), (0, "unknown", "field"),
                  (0, "monotonicMs", -1), (0, "utcFileTimeTicks", 2**64), (0, "pid", 2**32)]
        for index, field, value in faults:
            events = journal()
            events[index][field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                barrier.validate(events, RUN, NONCE)

    def test_late_cancel_fails(self):
        events = journal()
        for index in range(4, 8):
            events[index]["monotonicMs"] += 30000
        with self.assertRaises(ValueError):
            barrier.validate(events, RUN, NONCE)

    def test_only_gui_cancel_sequence_passes(self):
        for name in ["stop_flag_set", "wm_close", "barrier_failed", "barrier_continue", "extraction_completed"]:
            events = journal()
            events[4]["event"] = name
            with self.subTest(name=name), self.assertRaises(ValueError):
                barrier.validate(events, RUN, NONCE)

    def test_utc_clock_adjustment_is_not_monotonic_order(self):
        events = journal()
        events[5]["utcFileTimeTicks"] = 1
        barrier.validate(events, RUN, NONCE)

    def test_reader_and_holes_collision_limits_links(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for index, event in enumerate(journal(), 1):
                (root / f"extraction-{index:02}.json").write_text(json.dumps(event))
            barrier.validate(barrier.read_events(root), RUN, NONCE)
            (root / "extraction-02.json").unlink()
            with self.assertRaises(ValueError):
                barrier.read_events(root)
            path = root / "extraction-02.json"
            path.write_text('{' + '"duplicate":1,"duplicate":2}')
            with self.assertRaises(ValueError):
                barrier.read_events(root)
            path.write_text(" " * 16385)
            with self.assertRaises(ValueError):
                barrier.read_events(root)
            path.unlink()
            path.symlink_to(root / "extraction-01.json")
            with self.assertRaises(ValueError):
                barrier.read_events(root)

    def test_absent_extra_or_partial_is_never_pass(self):
        for events in [[], journal()[:4], journal()[:7], [None] * 8, [[]] * 8]:
            with self.assertRaises(ValueError):
                barrier.validate(events, RUN, NONCE)
        with self.assertRaises(ValueError):
            barrier.identifier("../escape")


if __name__ == "__main__":
    unittest.main()
