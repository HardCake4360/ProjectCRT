"""Isolated Unity batch authoring copy; never shares its Library with the user's editor."""
from pathlib import Path
import shutil
import json

root=Path(__file__).resolve().parents[1]
copy=root/'tmp/PrologueAuthoringProject'
copy.mkdir(parents=True,exist_ok=True)
for name in ['Assets','Packages','ProjectSettings']:
    shutil.copytree(root/name,copy/name,dirs_exist_ok=True)
(copy/'Logs/PrologueAssets').mkdir(parents=True,exist_ok=True)
(copy/'Logs/PrologueAssets/request.txt').write_text('build')
(root/'Logs/PrologueAssets/authoring-copy.json').write_text(json.dumps({'source':str(root),'copy':str(copy)},indent=2))
print(copy)
