#!/usr/bin/env python3
"""Generate real Jellyfin catalog entries only from successfully built release ZIP files."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path

PLUGINS = [
    ("f8517621-7c60-4e19-9696-67f9b31fa092", "Request Movie", "RequestMovie",
     "Jellyfin movie and series requests, TMDb search and duplicate checks."),
    ("e30f17c1-cd72-4bdc-8225-38d122947719", "NFO Trailer", "Trailer",
     "Display trailers from local NFO metadata on Jellyfin movie details.")
]

def main():
    p = argparse.ArgumentParser()
    for field in ("manifest", "dist", "repository", "tag", "version"):
        p.add_argument("--" + field, required=True)
    args = p.parse_args()
    target = Path(args.manifest)
    catalog = json.loads(target.read_text(encoding="utf-8")) if target.exists() else []
    timestamp = datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")
    for guid, name, slug, description in PLUGINS:
        archive = Path(args.dist) / f"jellyfin-{slug}.zip"
        data = archive.read_bytes()
        entry = next((e for e in catalog if e.get("guid") == guid), None)
        if entry is None:
            entry = {
                "guid": guid, "name": name, "description": description,
                "overview": description, "owner": args.repository.split("/")[0],
                "category": "General", "versions": []
            }
            catalog.append(entry)
        version = {
            "version": args.version,
            "changelog": "Reconstructed Jellyfin 12.4 plugin build",
            "targetAbi": "12.4.0.0",
            "sourceUrl": f"https://github.com/{args.repository}/releases/download/{args.tag}/{archive.name}",
            "checksum": hashlib.md5(data).hexdigest(),
            "timestamp": timestamp
        }
        entry["versions"] = [version] + [v for v in entry["versions"] if v.get("version") != args.version]
    target.write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

if __name__ == "__main__":
    main()
