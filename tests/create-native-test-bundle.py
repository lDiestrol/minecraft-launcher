"""Data-only MCBOOT01 fixture. Contains no runnable helper, vendor or PE stub."""
import hashlib
from pathlib import Path
import struct
import sys


def create(path):
    files = {
        "bootstrapper-catalog.json": b"{}",
        "helper/Launcher.InstallHelper.exe": b"NONEXECUTABLE TEST FIXTURE",
        "vendor/Setup.exe": b"NONEXECUTABLE TEST FIXTURE",
        # Large enough to exercise cancellation between index-ordered entries.
        "vendor/full.nupkg": b"fixture" * (4 * 1024 * 1024),
    }
    data = bytearray(b"DATA ONLY - NEVER EXECUTE\n")
    index = bytearray(struct.pack("<II", 1, len(files)))
    for name, payload in files.items():
        encoded = name.encode("ascii")
        index += struct.pack("<IQQ", len(encoded), len(payload), len(data))
        index += hashlib.sha256(payload).digest() + encoded
        data += payload
    offset = len(data)
    data += index + struct.pack("<8sQI32s", b"MCBOOT01", offset, len(index), hashlib.sha256(index).digest())
    with Path(path).open("xb") as output:
        output.write(data)


if __name__ == "__main__":
    create(sys.argv[1])
