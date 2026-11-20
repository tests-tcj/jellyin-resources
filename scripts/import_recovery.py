#!/usr/bin/env python3
"""Safely import the two original plugin folders from a Jellyfin recovery TAR."""
import argparse
import hashlib
import json
import pathlib
import tarfile

EXPECTED = {
    "Request Movie_0.1.0.111": "Jellyfin.Plugin.RequestMovie.dll",
    "NFO Trailer_0.1.0.10": "Jellyfin.Plugin.Trailer.dll",
}

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("archive", type=pathlib.Path)
    ap.add_argument("--output", type=pathlib.Path, default=pathlib.Path("recovered"))
    args = ap.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    inventory = []
    with tarfile.open(args.archive, "r:gz") as archive:
        for member in archive.getmembers():
            if not member.isfile():
                continue
            parts = pathlib.PurePosixPath(member.name).parts
            if len(parts) != 2 or parts[0] not in EXPECTED:
                continue
            if parts[1] not in ("meta.json", EXPECTED[parts[0]]):
                continue
            if member.size > 20 * 1024 * 1024:
                raise ValueError("Unexpectedly large archive member: " + member.name)
            stream = archive.extractfile(member)
            if stream is None:
                continue
            raw = stream.read()
            destination = args.output / parts[0] / parts[1]
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(raw)
            if parts[1] == "meta.json":
                metadata = json.loads(raw.decode("utf-8"))
                info = {"plugin": parts[0], "version": metadata.get("version"),
                        "guid": metadata.get("guid"), "status": metadata.get("status")}
            else:
                info = {"plugin": parts[0], "file": parts[1],
                        "size": len(raw), "sha256": hashlib.sha256(raw).hexdigest()}
            inventory.append(info)
    if not inventory:
        raise SystemExit("No expected Jellyfin plugin files found in archive")
    (args.output / "inventory.json").write_text(
        json.dumps(inventory, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(inventory, indent=2, ensure_ascii=False))
    print("Recovered locally to:", args.output.resolve())

if __name__ == "__main__":
    main()
