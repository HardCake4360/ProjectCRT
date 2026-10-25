"""Publish only verified Rebecca SC03 output, preserving other scenes and original FBXs."""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
AUTHORING = ROOT / 'tmp/PrologueAuthoringProject'
LOG = ROOT / 'Logs/RebeccaAnimationAssets'
MOTIONS = Path('Assets/Settings/Prologue/RebeccaAnimationTest')
SOURCES = Path('Assets/ThirdParty/CharacterAnimations')
SCENE = Path('Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity')
SEQUENCE = Path('Assets/Settings/Prologue/SC23/SC03_ShangrilaReunion.asset')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    match = re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig'), re.M)
    return match[1] if match else None


def invariant_sequence(text):
    # Only Rebecca/bottle clip assignments and reaction wait times may change.
    actor = None
    result = []
    for line in text.splitlines():
        match = re.match(r'    - actorId: (.+)', line)
        if match:
            actor = match[1]
        elif line.startswith('    ') and not line.startswith('      ') and not line.startswith('    -'):
            actor = None
        if actor == 'rebecca_bottle':
            continue
        if actor == 'rebecca' and re.match(r'      (clip|loop):', line):
            continue
        if line.startswith('    waitForActors:'):
            continue
        result.append(line)
    return '\n'.join(result)


def main():
    newest_motion = max(p.stat().st_mtime for p in (AUTHORING / MOTIONS).rglob('*.anim'))
    for name in ('build-status.txt', 'preview-status.txt'):
        status = AUTHORING / 'Logs/RebeccaAnimationAssets' / name
        assert status.read_text().strip() == 'PASS' and status.stat().st_mtime >= newest_motion, name
    runtime = AUTHORING / 'Logs/SC23/runtime-status.txt'
    assert runtime.read_text().strip() == 'PASS' and runtime.stat().st_mtime >= newest_motion
    before = json.loads((LOG / 'before.json').read_text())
    for name, digest in before.items():
        assert sha(ROOT / name) == digest, 'Live project changed: ' + name
        if name not in {SCENE.as_posix(), SEQUENCE.as_posix()}:
            assert sha(AUTHORING / name) == digest, 'Protected authoring asset changed: ' + name
    models = json.loads((ROOT / 'Logs/PrologueAssets/preserved-originals.json').read_text())
    for name, digest in models.items():
        assert sha(ROOT / name) == digest, 'Original model changed: ' + name
    for name, digest in json.loads((LOG / 'source-fbx-checksums.json').read_text()).items():
        assert sha(ROOT / name) == digest and sha(AUTHORING / name) == digest, 'Downloaded source changed: ' + name
    assert invariant_sequence((ROOT / SEQUENCE).read_text(encoding='utf-8-sig')) == invariant_sequence((AUTHORING / SEQUENCE).read_text(encoding='utf-8-sig')), 'Unrelated sequence fields changed'
    for source in (ROOT / 'Assets/Settings/Prologue/SC23/Animation').rglob('*'):
        if source.is_file():
            assert sha(source) == sha(AUTHORING / source.relative_to(ROOT)), 'Original SC23 animation changed'

    outputs = [SCENE, SEQUENCE, Path(str(MOTIONS) + '.meta')]
    outputs += [p.relative_to(AUTHORING) for p in (AUTHORING / MOTIONS).rglob('*') if p.is_file()]
    outputs += [p.relative_to(AUTHORING) for p in (AUTHORING / SOURCES).rglob('*.meta')]
    outputs += [Path(str(SOURCES) + '.meta')]
    outputs += [p.relative_to(AUTHORING) for p in (AUTHORING / 'Assets/Editor/Prologue').glob('Rebecca*.cs.meta') if (ROOT / p.relative_to(AUTHORING).with_suffix('')).exists()]
    rewrites = {}
    for path in outputs:
        if path.suffix == '.meta' and (ROOT / path).exists() and guid(ROOT / path) != guid(AUTHORING / path):
            rewrites[guid(AUTHORING / path)] = guid(ROOT / path)
    for path in outputs:
        source, target = AUTHORING / path, ROOT / path
        target.parent.mkdir(parents=True, exist_ok=True)
        if rewrites and path.suffix in {'.meta', '.asset', '.anim', '.unity'}:
            value = source.read_bytes().decode('utf-8')
            for old, new in rewrites.items():
                value = value.replace('guid: ' + old, 'guid: ' + new)
            target.write_bytes(value.encode('utf-8'))
        else:
            shutil.copy2(source, target)
    shutil.copytree(AUTHORING / 'Logs/RebeccaAnimationAssets', LOG, dirs_exist_ok=True)
    for name in ('runtime-status.txt', 'runtime-validation.txt'):
        shutil.copy2(AUTHORING / 'Logs/SC23' / name, LOG / name)
    previews = LOG / 'Previews'
    previews.mkdir(exist_ok=True)
    for source in (AUTHORING / 'Logs/SC23/Previews').glob('Rebecca-new-*.png'):
        shutil.copy2(source, previews / source.name)
    shutil.copytree(AUTHORING / 'Logs/SC23/RuntimePreviews', LOG / 'RuntimePreviews', dirs_exist_ok=True)
    for name, digest in before.items():
        if name not in {SCENE.as_posix(), SEQUENCE.as_posix()}:
            assert sha(ROOT / name) == digest
    for name, digest in models.items():
        assert sha(ROOT / name) == digest
    result = {'status': 'PASS', 'original_models_unchanged': len(models), 'SC01_SC02_unchanged': True, 'dialogue_camera_other_actor_cues_unchanged': True, 'published_files': len(outputs), 'guid_reconciliations': rewrites}
    (LOG / 'publish-result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
