"""Fetch creator-hosted free animation assets; retain archives and provenance."""
from pathlib import Path
import importlib.util,json,re,urllib.request
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('prologue_download',ROOT/'tools/download_prologue_assets.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
module.LOG=ROOT/'Logs/RebeccaAnimationAssets';module.DEST=ROOT/'Assets/ThirdParty/CharacterAnimations'
module.LOG.mkdir(parents=True,exist_ok=True)
if __name__=='__main__':
    results=[]
    for pack in [
        ('QuaterniusUAL1','https://quaternius.itch.io/universal-animation-library','Quaternius','CC0 1.0',['Universal Animation Library[Standard].zip']),
        ('QuaterniusUAL2','https://quaternius.itch.io/universal-animation-library-2','Quaternius','CC0 1.0',['Universal Animation Library 2[Standard].zip'])
    ]:
        result=module.fetch(pack);results.append(result);print(json.dumps(result,indent=2),flush=True)
    (module.LOG/'quaternius-download.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    for slug in ['mocap_pack4','mocap_pack2']:
        try:
            req=urllib.request.Request('https://andersonrohr.gumroad.com/l/'+slug,headers={'User-Agent':'Mozilla/5.0'})
            content=urllib.request.urlopen(req,timeout=40).read().decode();(module.LOG/(slug+'-public-page.html')).write_text(content,encoding='utf-8')
            print(slug,'public page bytes',len(content),'data blocks',re.findall(r'<script[^>]+(?:type|id)=[^>]+>',content)[-7:],flush=True)
        except Exception as error:print(slug,type(error).__name__,str(error),flush=True)
