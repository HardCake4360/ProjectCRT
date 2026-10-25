"""Publish the verified SC02/SC03 scenes while protecting SC01 and the original models."""
from pathlib import Path
import hashlib,json,re,shutil
root=Path(__file__).resolve().parents[1];copy=root/'tmp/PrologueAuthoringProject';log=root/'Logs/SC23';folder=Path('Assets/Settings/Prologue/SC23')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def guid(p):
    m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M);return m[1] if m else None
def main():
    for name in ['build-status.txt','preview-status.txt','camera-status.txt','runtime-status.txt']:assert (copy/'Logs/SC23'/name).read_text().strip()=='PASS',name
    before=json.loads((log/'before.json').read_text())
    for path,value in before.items():assert sha(root/path)==value,'Live project changed during authoring: '+path
    for path in ['Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity','Assets/Settings/Prologue/SC01/SC01_RoadToLosSimula.asset']:assert sha(copy/path)==before[path],'SC01 changed in authoring copy'
    models=json.loads((root/'Logs/PrologueAssets/preserved-originals.json').read_text())
    for path,value in models.items():assert sha(root/path)==value,path
    rewrites={}
    for directory in [folder,Path('Assets/Editor/Prologue'),Path('Assets/scripts/Prologue')]:
        for src in (copy/directory).rglob('*.meta'):
            dst=root/src.relative_to(copy)
            if dst.exists() and guid(src)!=guid(dst):
                a=Path(str(src)[:-5]);b=Path(str(dst)[:-5]);assert a.is_dir() or sha(a)==sha(b),'Conflicting asset at '+str(b)
                rewrites[guid(src)]=guid(dst)
    paths=[Path('Assets/Scenes/ProjectCRT_SC02_SanRoom.unity'),Path('Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity')]
    outputs=[copy/p for p in paths]+[p for p in (copy/folder).rglob('*') if p.is_file()]
    outputs+=[copy/Path(str(folder)+'.meta')]
    for directory in ['Assets/Editor/Prologue','Assets/scripts/Prologue']:
        for src in (copy/directory).glob('*.cs.meta'):
            if src.name.startswith('SC23') or src.name.startswith('PrologueSanMotor'):outputs.append(src)
    for src in outputs:
        dst=root/src.relative_to(copy);dst.parent.mkdir(parents=True,exist_ok=True)
        if src.suffix=='.meta' and dst.exists():continue
        shutil.copy2(src,dst)
    if rewrites:
        for p in paths+[p.relative_to(root) for p in (root/folder).rglob('*') if p.suffix in {'.asset','.anim','.mat'}]:
            dst=root/p;value=dst.read_text(encoding='utf-8-sig')
            for old,new in rewrites.items():value=value.replace('guid: '+old,'guid: '+new)
            dst.write_text(value,encoding='utf-8')
    shutil.copytree(copy/'Logs/SC23',log,dirs_exist_ok=True)
    for path,value in models.items():assert sha(root/path)==value,path
    for path in ['Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity','Assets/Settings/Prologue/SC01/SC01_RoadToLosSimula.asset']:assert sha(root/path)==before[path]
    result={'status':'PASS','scenes':[str(p) for p in paths],'original_models_unchanged':len(models),'SC01_unchanged':True,'guid_reconciliations':rewrites}
    (log/'publish-result.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
