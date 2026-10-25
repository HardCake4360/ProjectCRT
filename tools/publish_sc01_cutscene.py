"""Apply the verified SC01 cutscene only; preserve other scene files and original models."""
from pathlib import Path
import hashlib,json,re,shutil

ROOT=Path(__file__).resolve().parents[1]
COPY=ROOT/'tmp/PrologueAuthoringProject'
LOG=ROOT/'Logs/SC01'
SCENE=Path('Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity')
FOLDER=Path('Assets/Settings/Prologue/SC01')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path):
    match=re.search(r'^guid: (\w+)',path.read_text(encoding='utf-8-sig'),re.M)
    return match.group(1) if match else None

def main():
    for name in ['build-status.txt','preview-status.txt','runtime-status.txt']:
        assert (COPY/'Logs/SC01'/name).read_text().strip()=='PASS',name
    before=json.loads((LOG/'before.json').read_text())
    assert sha(ROOT/SCENE)==before['scene_sha256'],'SC01 changed in the live project during authoring; preserve that edit.'
    for rel in ['Assets/Scenes/ProjectCRT_SC02_SanRoom.unity','Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity']:
        assert sha(ROOT/rel)==sha(COPY/rel),'An unrelated scene changed: '+rel
    originals=json.loads((ROOT/'Logs/PrologueAssets/preserved-originals.json').read_text())
    for rel,value in originals.items():assert sha(ROOT/rel)==value,rel
    rewrites={}
    for directory in [FOLDER,Path('Assets/Editor/Prologue'),Path('Assets/scripts/Prologue')]:
        for src in (COPY/directory).rglob('*.meta'):
            dst=ROOT/src.relative_to(COPY)
            if dst.exists() and guid(src)!=guid(dst):
                asset=Path(str(dst)[:-5]);source_asset=Path(str(src)[:-5])
                assert asset.exists() and source_asset.exists() and (asset.is_dir() or sha(asset)==sha(source_asset)),'Changed asset at GUID conflict: '+str(asset)
                rewrites[guid(src)]=guid(dst)
    for src in (COPY/FOLDER).rglob('*'):
        if not src.is_file():continue
        dst=ROOT/src.relative_to(COPY);dst.parent.mkdir(parents=True,exist_ok=True)
        if src.suffix=='.meta' and dst.exists() and guid(src)!=guid(dst):continue
        shutil.copy2(src,dst)
    folder_meta=Path(str(FOLDER)+'.meta')
    if not (ROOT/folder_meta).exists():shutil.copy2(COPY/folder_meta,ROOT/folder_meta)
    for directory in ['Assets/Editor/Prologue','Assets/scripts/Prologue']:
        for src in (COPY/directory).glob('SC01*.meta'):
            dst=ROOT/src.relative_to(COPY)
            if not dst.exists():shutil.copy2(src,dst)
    shutil.copy2(COPY/SCENE,ROOT/SCENE)
    if rewrites:
        for dst in [ROOT/SCENE]+[p for p in (ROOT/FOLDER).rglob('*') if p.suffix in {'.asset','.anim','.mat'}]:
            text=dst.read_text(encoding='utf-8-sig')
            for old,new in rewrites.items():text=text.replace('guid: '+old,'guid: '+new)
            dst.write_text(text,encoding='utf-8')
    shutil.copytree(COPY/'Logs/SC01',LOG,dirs_exist_ok=True)
    for rel,value in originals.items():assert sha(ROOT/rel)==value,rel
    result={'status':'PASS','scene':str(SCENE),'sequence':str(FOLDER/'SC01_RoadToLosSimula.asset'),'original_models_unchanged':len(originals),'guid_reconciliations':rewrites,'sc02_sc03_unchanged':True}
    (LOG/'publish-result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2))
if __name__=='__main__':main()
