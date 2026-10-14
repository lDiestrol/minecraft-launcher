"""Verify a diagnostic MCBOOT01 bundle using data reads only; never execute payloads."""

import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import struct
import xml.etree.ElementTree as ET
import zipfile


IDENTITY = {
    "schemaVersion": 1,
    "packId": "MinecraftLauncher.BootstrapperDiagnostic",
    "mainExe": "VendorProbe.exe",
    "version": "0.0.1",
    "channel": "dev",
    "velopackVersion": "1.2.161",
}
REQUIRED = {
    "bootstrapper-catalog.json", "helper/Launcher.InstallHelper.exe",
    "helper/Launcher.InstallHelper.dll", "helper/Launcher.Infrastructure.dll",
    "vendor/Setup.exe", "vendor/full.nupkg",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_exact(stream, size):
    data = stream.read(size)
    require(len(data) == size, "Truncated bundle")
    return data


def hash_range(stream, offset, size):
    stream.seek(offset)
    digest = hashlib.sha256()
    while size:
        data = read_exact(stream, min(size, 1024 * 1024))
        digest.update(data)
        size -= len(data)
    return digest.hexdigest()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "Duplicate JSON key")
        result[key] = value
    return result


def safe_name(name):
    require(re.fullmatch(r"[A-Za-z0-9._/-]+", name) is not None, "Unsafe entry name")
    require(".." not in name, "Traversal entry")
    for segment in name.split("/"):
        require(segment and segment != "." and not segment.endswith("."), "Unsafe entry segment")
        stem = segment.split(".")[0].upper()
        require(stem not in {"CON", "PRN", "AUX", "NUL"} and
                re.fullmatch(r"(?:COM|LPT)[1-9]", stem) is None, "Reserved entry name")
    require(name == "bootstrapper-catalog.json" or name.startswith("helper/") or
            name in {"vendor/Setup.exe", "vendor/full.nupkg"}, "Unapproved entry")


def verify(bundle, checksums, stub=None):
    bundle, checksums = Path(bundle), Path(checksums)
    require(bundle.name == "MinecraftLauncher-Setup.exe", "Unexpected output filename")
    with bundle.open("rb") as stream:
        total = stream.seek(0, 2)
        require(52 < total <= 9 * 1024**3, "Invalid bundle size")
        actual_hash = hash_range(stream, 0, total)
        lines = checksums.read_text(encoding="utf-8-sig").splitlines()
        require(len(lines) == 1 and
                re.fullmatch(r"[0-9a-fA-F]{64}  MinecraftLauncher-Setup\.exe", lines[0]) is not None,
                "Invalid SHA256SUMS.txt")
        require(lines[0][:64].lower() == actual_hash, "Final EXE SHA-256 mismatch")
        stream.seek(total - 52)
        magic, offset, length, digest = struct.unpack("<8sQI32s", read_exact(stream, 52))
        require(magic == b"MCBOOT01" and 8 <= length <= 1048576 and
                offset + length == total - 52, "Invalid bundle footer")
        stream.seek(offset)
        data = read_exact(stream, length)
        require(hashlib.sha256(data).digest() == digest, "Index SHA-256 mismatch")
        index = io.BytesIO(data)
        schema, count = struct.unpack("<II", read_exact(index, 8))
        require(schema == 1 and 6 <= count <= 1025, "Invalid index schema/count")
        entries, seen, end, payload_size = {}, set(), None, 0
        for _ in range(count):
            name_length, size, position = struct.unpack("<IQQ", read_exact(index, 20))
            entry_hash = read_exact(index, 32).hex()
            require(0 < name_length <= 240 and 0 < size <= 2147483648 and
                    position <= offset and size <= offset - position, "Invalid entry bounds")
            name = read_exact(index, name_length).decode("utf-8", errors="strict")
            safe_name(name)
            require(name.lower() not in seen, "Duplicate entry name")
            require(end is None or position == end, "Noncontiguous or overlapping payload")
            seen.add(name.lower())
            entries[name] = (position, size, entry_hash)
            end = position + size
            payload_size += size
            require(hash_range(stream, position, size) == entry_hash, "Payload SHA-256 mismatch: " + name)
        require(index.tell() == length and end == offset and payload_size <= 8 * 1024**3,
                "Invalid index coverage")
        require(REQUIRED <= entries.keys(), "Required diagnostic payload missing")

        def entry_bytes(name, limit):
            position, size, _ = entries[name]
            require(size <= limit, "Oversized metadata: " + name)
            stream.seek(position)
            return read_exact(stream, size)

        stub_length = next(iter(entries.values()))[0]
        require(64 <= stub_length <= 16 * 1024**2, "Invalid native stub size")
        stream.seek(0)
        native = read_exact(stream, stub_length)
        require(native[:2] == b"MZ", "Missing native PE header")
        pe_offset = struct.unpack_from("<I", native, 60)[0]
        require(pe_offset + 6 <= len(native) and native[pe_offset:pe_offset + 6] == b"PE\0\0\x64\x86",
                "Native stub must be an AMD64 PE")
        require(IDENTITY["packId"].encode("utf-16le") in native and
                "lDiestrol.MinecraftLauncher".encode("utf-16le") not in native,
                "Native bootstrapper is not Diagnostic")
        if stub is not None:
            require(native == Path(stub).read_bytes(), "Bundle differs from the compiled diagnostic stub")
        helper = entry_bytes("helper/Launcher.InstallHelper.dll", 16 * 1024**2)
        require("diagnostic-profile".encode("utf-16le") in helper,
                "Helper lacks the compiled diagnostic profile binding")
        catalog = json.loads(entry_bytes("bootstrapper-catalog.json", 1048576), object_pairs_hook=unique_object)
        require(set(catalog) == set(IDENTITY) | {"updateSha256", "files"}, "Unexpected catalog fields")
        require(all(type(catalog[key]) is type(value) and catalog[key] == value for key, value in IDENTITY.items()),
                "Catalog must use the pinned diagnostic identity")
        require(re.fullmatch(r"[0-9a-fA-F]{64}", catalog["updateSha256"]) is not None,
                "Missing trusted updater SHA-256")
        require(isinstance(catalog["files"], list) and len(catalog["files"]) == count - 1, "Incomplete catalog")
        catalog_names = set()
        for item in catalog["files"]:
            require(set(item) == {"path", "size", "sha256"}, "Unexpected catalog file fields")
            name = item["path"]
            require(name in entries and name != "bootstrapper-catalog.json" and name not in catalog_names,
                    "Unknown or duplicate catalog file")
            _, size, entry_hash = entries[name]
            require(type(item["size"]) is int and item["size"] == size and
                    item["sha256"].lower() == entry_hash, "Catalog/index file mismatch")
            catalog_names.add(name)
        with zipfile.ZipFile(io.BytesIO(entry_bytes("vendor/full.nupkg", 2147483648))) as package:
            manifests = [name for name in package.namelist() if name.endswith(".nuspec")]
            require(len(manifests) == 1 and package.getinfo(manifests[0]).file_size <= 65536,
                    "Invalid package manifest")
            xml = package.read(manifests[0])
            require(b"<!DOCTYPE" not in xml.upper() and b"<!ENTITY" not in xml.upper(), "Unsafe XML declarations")
            metadata = [element for element in ET.fromstring(xml).iter() if element.tag.split("}")[-1] == "metadata"]
            require(len(metadata) == 1, "Missing unique package metadata")
            fields = unique_object([(element.tag.split("}")[-1], element.text or "") for element in metadata[0]])
            for key, expected in {"id": IDENTITY["packId"], "version": "0.0.1", "channel": "dev",
                                  "mainExe": "VendorProbe.exe", "machineArchitecture": "x64"}.items():
                require(fields.get(key) == expected, "Unexpected package identity: " + key)
            require("lib/app/VendorProbe.exe" in package.namelist() and
                    "lib/app/MinecraftLauncher.exe" not in package.namelist(), "Unexpected packaged application")
            if "lib/app/Squirrel.exe" in package.namelist():
                with package.open("lib/app/Squirrel.exe") as updater:
                    require(hashlib.file_digest(updater, "sha256").hexdigest() == catalog["updateSha256"].lower(),
                            "Packaged updater differs from the trusted updater")
    return {"sha256": actual_hash, "size": total, "packId": IDENTITY["packId"], "files": count}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", required=True)
    parser.add_argument("--checksums", required=True)
    parser.add_argument("--stub")
    args = parser.parse_args()
    print(json.dumps(verify(args.bundle, args.checksums, args.stub), sort_keys=True))
