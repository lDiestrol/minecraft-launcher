"""Synthetic packaging verification tests. No executable is ever started."""

import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
import zipfile


spec = importlib.util.spec_from_file_location(
    "diagnostic_verifier", Path(__file__).resolve().parents[1] / "scripts/verify-diagnostic-artifact.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class DiagnosticArtifactTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="diagnostic-artifact-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.bundle = self.root / "MinecraftLauncher-Setup.exe"
        self.checksums = self.root / "SHA256SUMS.txt"
        self.stub = self.root / "bootstrapper.exe"

    def checksum(self):
        digest = hashlib.sha256(self.bundle.read_bytes()).hexdigest()
        self.checksums.write_text(digest + "  MinecraftLauncher-Setup.exe\n", encoding="utf-8")

    def create(self, catalog_changes=None, native_marker=None, helper_marker="diagnostic-profile",
               package_product=None, extra_name=None, manifest_version="0.0.1"):
        native = bytearray(128)
        native[:2] = b"MZ"
        struct.pack_into("<I", native, 60, 64)
        native[64:70] = b"PE\0\0\x64\x86"
        native += (native_marker or verifier.IDENTITY["packId"]).encode("utf-16le")
        package_data = io.BytesIO()
        product = package_product or verifier.IDENTITY["packId"]
        updater = b"synthetic-updater"
        with zipfile.ZipFile(package_data, "w") as package:
            package.writestr("Diagnostic.nuspec", f"<package><metadata><id>{product}</id>"
                             f"<version>{manifest_version}</version><channel>dev</channel><mainExe>VendorProbe.exe</mainExe>"
                             "<machineArchitecture>x64</machineArchitecture></metadata></package>")
            package.writestr("lib/app/VendorProbe.exe", b"synthetic-host")
            package.writestr("lib/app/Squirrel.exe", updater)
        files = {
            "helper/Launcher.InstallHelper.exe": b"synthetic-apphost",
            "helper/Launcher.InstallHelper.dll": helper_marker.encode("utf-16le"),
            "helper/Launcher.Infrastructure.dll": b"synthetic-infrastructure",
            "vendor/Setup.exe": b"synthetic-vendor-setup",
            "vendor/full.nupkg": package_data.getvalue(),
        }
        if extra_name:
            files[extra_name] = b"synthetic-extra"
        catalog = dict(verifier.IDENTITY, updateSha256=hashlib.sha256(updater).hexdigest(), files=[
            {"path": name, "size": len(data), "sha256": hashlib.sha256(data).hexdigest()}
            for name, data in files.items()])
        catalog.update(catalog_changes or {})
        files["bootstrapper-catalog.json"] = json.dumps(catalog).encode("utf-8")
        data, index = bytearray(native), bytearray(struct.pack("<II", 1, len(files)))
        for name, payload in files.items():
            encoded = name.encode("utf-8")
            index += struct.pack("<IQQ", len(encoded), len(payload), len(data))
            index += hashlib.sha256(payload).digest() + encoded
            data += payload
        offset = len(data)
        data += index + struct.pack("<8sQI32s", b"MCBOOT01", offset, len(index), hashlib.sha256(index).digest())
        self.bundle.write_bytes(data)
        self.stub.write_bytes(native)
        self.checksum()

    def verify(self):
        return verifier.verify(self.bundle, self.checksums, self.stub)

    def test_valid_diagnostic_bundle(self):
        self.create()
        result = self.verify()
        self.assertEqual(result["packId"], verifier.IDENTITY["packId"])
        self.assertEqual(result["sha256"], self.checksums.read_text()[:64])

    def test_checksum_is_required_and_exact(self):
        for line in ("", "0" * 64 + "  MinecraftLauncher-Setup.exe\n", "0" * 64 + "  Setup.exe\n"):
            with self.subTest(line=line):
                self.create()
                self.checksums.write_text(line)
                with self.assertRaises(ValueError):
                    self.verify()

    def test_outer_checksum_cannot_hide_index_or_payload_corruption(self):
        for position in (130, -53):
            with self.subTest(position=position):
                self.create()
                data = bytearray(self.bundle.read_bytes())
                data[position] ^= 1
                self.bundle.write_bytes(data)
                self.checksum()
                with self.assertRaises(ValueError):
                    self.verify()

    def test_native_bootstrapper_must_be_diagnostic(self):
        self.create(native_marker="lDiestrol.MinecraftLauncher")
        with self.assertRaisesRegex(ValueError, "Native bootstrapper"):
            self.verify()

    def test_native_stub_must_match_compiler_output(self):
        self.create()
        self.stub.write_bytes(self.stub.read_bytes() + b"changed")
        with self.assertRaisesRegex(ValueError, "compiled diagnostic stub"):
            self.verify()

    def test_helper_must_have_compiled_diagnostic_binding(self):
        self.create(helper_marker="production-helper")
        with self.assertRaisesRegex(ValueError, "Helper lacks"):
            self.verify()

    def test_catalog_identity_is_pinned(self):
        for changes in ({"packId": "lDiestrol.MinecraftLauncher"}, {"mainExe": "MinecraftLauncher.exe"},
                        {"version": "0.6.0"}, {"channel": "stable"}, {"velopackVersion": "1.2.158"},
                        {"schemaVersion": True}):
            with self.subTest(changes=changes):
                self.create(catalog_changes=changes)
                with self.assertRaisesRegex(ValueError, "pinned diagnostic identity"):
                    self.verify()

    def test_catalog_must_cover_and_match_every_file(self):
        for changes in ({"files": []}, {"unknown": True}, {"updateSha256": "0" * 64}):
            with self.subTest(changes=changes):
                self.create(catalog_changes=changes)
                with self.assertRaises(ValueError):
                    self.verify()

    def test_package_identity_is_pinned(self):
        self.create(package_product="lDiestrol.MinecraftLauncher")
        with self.assertRaisesRegex(ValueError, "package identity"):
            self.verify()
        self.create(manifest_version="0.6.0")
        with self.assertRaisesRegex(ValueError, "package identity"):
            self.verify()

    def test_paths_cannot_escape_or_alias_another_file(self):
        for name in ("helper/../evil.dll", "helper/CON.dll", "helper/launcher.installhelper.dll", "vendor/other.exe"):
            with self.subTest(name=name):
                self.create(extra_name=name)
                with self.assertRaises(ValueError):
                    self.verify()

    def test_truncated_footer_is_rejected(self):
        self.create()
        self.bundle.write_bytes(self.bundle.read_bytes()[:-1])
        self.checksum()
        with self.assertRaises(ValueError):
            self.verify()


if __name__ == "__main__":
    unittest.main()
