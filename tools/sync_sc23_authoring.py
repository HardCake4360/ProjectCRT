from pathlib import Path
import shutil,json,hashlib,sys
root=Path(__file__).resolve().parents[1]
copy=root/'tmp/PrologueAuthoringProject'
log=root/'Logs/SC23';log.mkdir(parents=True,exist_ok=True)
if not (log/'before.json').exists():
    files=['Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity','Assets/Scenes/ProjectCRT_SC02_SanRoom.unity','Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity','Assets/Settings/Prologue/SC01/SC01_RoadToLosSimula.asset']
    (log/'before.json').write_text(json.dumps({p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in files},indent=2))
count=0
for folder in (['Assets/scripts/Prologue','Assets/Editor/Prologue'] if '--scripts-only' in sys.argv else ['Assets','Packages','ProjectSettings']):
    for source in (root/folder).rglob('*'):
        if not source.is_file():continue
        target=copy/source.relative_to(root)
        if target.exists() and source.stat().st_mtime_ns==target.stat().st_mtime_ns and source.stat().st_size==target.stat().st_size:continue
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target);count+=1
(copy/'Logs/SC23').mkdir(parents=True,exist_ok=True)
print('Synchronized',count,'changed files')
