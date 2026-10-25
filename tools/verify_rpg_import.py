"""Audit the imported baseline, source preservation, GUIDs and asset references."""

import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CUSTOMIZED = {
    "Assets/NoSleep/Runtime/Core/NoSleepBootstrap.cs",
    "Assets/NoSleep/Runtime/UI/GameUI.cs",
    "Assets/NoSleep/Runtime/UI/GameUI.Progress.cs",
    # RPG is disabled by an explicit opt-in compilation constraint (2026-10-21).
    "Assets/NoSleep/Runtime/NoSleep.Runtime.asmdef",
    "Assets/NoSleep/Editor/NoSleep.Editor.asmdef",
    "Assets/NoSleep/Tests/Editor/NoSleep.Tests.asmdef",
    "Assets/NoSleep/Tests/PlayMode/NoSleep.PlayMode.Tests.asmdef",
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifest = json.loads((ROOT / "Docs/NoSleep/IMPORT_MANIFEST.json").read_text(encoding="utf-8"))
    source = Path(manifest["source"])
    errors = []
    unchanged = 0
    for entry in manifest["files"]:
        relative = entry["path"]
        destination = ROOT / relative
        if not destination.is_file():
            errors.append(f"Missing imported file: {relative}")
        elif relative not in CUSTOMIZED:
            if sha(destination) != entry["sha256"]:
                errors.append(f"Unexpected imported change: {relative}")
            else:
                unchanged += 1
        original = source / relative if relative.startswith("Assets/") else source / "Docs" / Path(relative).name
        if source.exists() and (not original.is_file() or sha(original) != entry["sha256"]):
            errors.append(f"Source changed since import: {original}")
    asset_guids = defaultdict(list)
    known = set()
    for folder in [ROOT / "Assets", ROOT / "Packages", ROOT / "Library/PackageCache"]:
        if not folder.exists():
            continue
        for meta in folder.rglob("*.meta"):
            match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8-sig"), re.M)
            if match:
                known.add(match[1])
                if meta.is_relative_to(ROOT / "Assets"):
                    asset_guids[match[1]].append(meta.relative_to(ROOT).as_posix())
    for guid, paths in asset_guids.items():
        if len(paths) > 1:
            errors.append(f"Duplicate Assets GUID {guid}: {paths}")
    refs = 0
    for asset in (ROOT / "Assets/NoSleep").rglob("*"):
        if not asset.is_file() or asset.suffix not in {".unity", ".prefab", ".asset", ".mat"}:
            continue
        for guid in set(re.findall(r"guid: ([0-9a-f]{32})", asset.read_text(encoding="utf-8-sig"))):
            # Unity's built-in assets use zero-prefixed reserved GUIDs.
            if guid.startswith("0000000000000000"):
                continue
            refs += 1
            if guid not in known:
                errors.append(f"Unresolved asset GUID {guid}: {asset.relative_to(ROOT)}")
    for asset in (ROOT / "Assets/NoSleep").rglob("*"):
        if asset.suffix != ".meta" and not Path(str(asset) + ".meta").is_file():
            errors.append(f"Missing .meta: {asset.relative_to(ROOT)}")
    if errors:
        for error in errors:
            print(error)
        raise SystemExit(1)
    print(f"PASS: {unchanged} imported files unchanged; {len(CUSTOMIZED)} documented adaptations.")
    print(f"PASS: {len(asset_guids)} unique Assets GUIDs; {refs} serialized references resolved; all imported assets have .meta files.")
    print("PASS: original source matches every imported baseline hash." if source.exists() else "Source unavailable; baseline audit only.")


if __name__ == "__main__":
    main()
