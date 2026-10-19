"""Import NoSleep assets without replacing existing assets or their GUIDs."""

import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    args = parser.parse_args()
    target = Path(__file__).resolve().parents[1]
    source = args.source.resolve()
    assets = source / "Assets" / "NoSleep"
    if source == target or not assets.is_dir():
        raise SystemExit("Expected a separate Unity project containing Assets/NoSleep.")
    sources = sorted(assets.rglob("*"))
    sources = [path for path in sources if path.is_file()]
    sources.append(source / "Assets" / "NoSleep.meta")
    copies = [(path, target / path.relative_to(source)) for path in sources]
    docs = sorted((source / "Docs").glob("*.md"))
    copies.extend((path, target / "Docs" / "NoSleep" / path.name) for path in docs)
    # Preflight the entire import before making any changes.
    existing_guids = {}
    for meta in (target / "Assets").rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8-sig"), re.M)
        if match:
            existing_guids[match[1]] = meta.resolve()
    for src, dst in copies:
        if dst.exists() and digest(src) != digest(dst):
            raise SystemExit(f"Preserving modified destination: {dst}")
        if src.suffix == ".meta":
            match = re.search(r"^guid: ([0-9a-f]{32})$", src.read_text(encoding="utf-8-sig"), re.M)
            if match and match[1] in existing_guids and existing_guids[match[1]] != dst.resolve():
                raise SystemExit(f"GUID collision: {src} -> {existing_guids[match[1]]}")
    for src, dst in copies:
        dst.parent.mkdir(parents=True, exist_ok=True)
        if not dst.exists():
            shutil.copy2(src, dst)
        if digest(src) != digest(dst):
            raise SystemExit(f"Copy verification failed: {dst}")
    manifest = {
        "source": str(source),
        "importedOn": "2026-10-09 (Asia/Seoul)",
        "unityVersion": "6000.0.32f1",
        "note": "Hashes describe the imported baseline before ProjectCRT adaptations.",
        "files": [{"path": dst.relative_to(target).as_posix(), "sha256": digest(src)} for src, dst in copies],
    }
    report = target / "Docs" / "NoSleep" / "IMPORT_MANIFEST.json"
    report.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Imported and hash-verified {len(sources)} asset files and {len(docs)} documents; no GUID collisions.")


if __name__ == "__main__":
    main()
