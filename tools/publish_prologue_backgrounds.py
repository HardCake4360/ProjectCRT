"""Publish only validated, newly authored scenes/materials from the isolated Unity copy."""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
COPY = ROOT / "tmp/PrologueAuthoringProject"
OUT = ROOT / "Logs/PrologueAssets"
SCENES = [
    "Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity",
    "Assets/Scenes/ProjectCRT_SC02_SanRoom.unity",
    "Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity",
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def publish():
    for name in ["batch-status.txt", "inspect-status.txt"]:
        assert (COPY / "Logs/PrologueAssets" / name).read_text().strip() == "PASS", name
    assert (COPY / "Logs/PrologueAssets/validation.txt").stat().st_mtime >= max((COPY / p).stat().st_mtime for p in SCENES)
    protected = json.loads((OUT / "preserved-originals.json").read_text())
    for rel, expected in protected.items():
        assert digest(ROOT / rel) == expected, "Original model changed: " + rel
    # Never replace a scene that the user has since started authoring.
    for rel in SCENES:
        assert not (ROOT / rel).exists(), "Existing scene: " + rel
    assert not (ROOT / "Assets/Settings/Prologue").exists(), "Existing material folder"
    # Guard GUID consistency before copying any output into the live project.
    metadata = []
    for folder in ["Assets/ThirdParty/Prologue", "Assets/Editor/Prologue", "Assets/scripts/Prologue"]:
        for src in (COPY / folder).rglob("*.meta"):
            dst = ROOT / src.relative_to(COPY)
            if dst.exists():
                a = re.search(r"^guid: (\w+)", src.read_text(), re.M).group(1)
                b = re.search(r"^guid: (\w+)", dst.read_text(), re.M).group(1)
                assert a == b, "GUID conflict: " + str(dst)
            else:
                metadata.append((src, dst))
    # The bootstrap was temporary and must not run in the user's open scene.
    for rel in ["Assets/Editor/PrologueBootstrap.cs", "Assets/Editor/PrologueBootstrap.cs.meta", "Logs/PrologueAssets/request.txt"]:
        target = ROOT / rel
        if target.exists():
            target.unlink()
    for src, dst in metadata:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
    for rel in SCENES:
        shutil.copy2(COPY / rel, ROOT / rel)
        shutil.copy2(COPY / (rel + ".meta"), ROOT / (rel + ".meta"))
    shutil.copytree(COPY / "Assets/Settings/Prologue", ROOT / "Assets/Settings/Prologue")
    shutil.copy2(COPY / "Assets/Settings/Prologue.meta", ROOT / "Assets/Settings/Prologue.meta")
    # Preserve current build settings; append the new scenes without changing order.
    settings = ROOT / "ProjectSettings/EditorBuildSettings.asset"
    text = settings.read_text(encoding="utf-8-sig")
    additions = ""
    for rel in SCENES:
        if "path: " + rel not in text:
            guid = re.search(r"^guid: (\w+)", (ROOT / (rel + ".meta")).read_text(), re.M).group(1)
            additions += f"  - enabled: 1\n    path: {rel}\n    guid: {guid}\n"
    assert "  m_configObjects:" in text
    settings.write_text(text.replace("  m_configObjects:", additions + "  m_configObjects:", 1), encoding="utf-8")
    for name in ["layout-report.txt", "validation.txt", "inspect-status.txt", "batch-status.txt", "current-interior.json"]:
        shutil.copy2(COPY / "Logs/PrologueAssets" / name, OUT / name)
    shutil.copytree(COPY / "Logs/PrologueAssets/Previews", OUT / "Previews", dirs_exist_ok=True)
    for rel, expected in protected.items():
        assert digest(ROOT / rel) == expected, rel
    result = {"scenes": SCENES, "preserved_originals": protected, "missing_metadata_copied": len(metadata), "status": "PASS"}
    (OUT / "publish-result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    publish()
