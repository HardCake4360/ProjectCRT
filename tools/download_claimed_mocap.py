from pathlib import Path
import hashlib,html,json,re,urllib.request,urllib.parse,zipfile
ROOT=Path(__file__).resolve().parents[1];LOG=ROOT/'Logs/RebeccaAnimationAssets';DEST=ROOT/'Assets/ThirdParty/CharacterAnimations'
if __name__=='__main__':
    for number,label in [(4,'CrowdReactions'),(2,'Cleaning')]:
        listing=(LOG/f'mocap_pack{number}-download-page.html').read_text(encoding='utf-8')
        page=json.loads(html.unescape(re.search(r'data-page="([^"]+)"',listing,re.S)[1]));items=page['props']['content']['content_items']
        folder=DEST/('AndersonRohr'+label);source=folder/'Source';source.mkdir(parents=True,exist_ok=True)
        records=[]
        for item in items:
            if str(item.get('extension','')).lower()!='zip':continue
            name=item['file_name']+'.zip';assert Path(name).name==name;archive=LOG/'Downloads'/label/name;archive.parent.mkdir(parents=True,exist_ok=True)
            raw_url=item.get('download_url') or item.get('url');assert raw_url;url=urllib.parse.urljoin('https://gumroad.com',raw_url);assert url.startswith('https://')
            if not archive.exists():
                req=urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'})
                with urllib.request.urlopen(req,timeout=90) as incoming,archive.open('wb') as output:
                    while True:
                        data=incoming.read(1024*1024)
                        if not data:break
                        output.write(data)
            with zipfile.ZipFile(archive) as package:
                for member in package.infolist():
                    path=Path(member.filename);target=(source/path).resolve()
                    if not target.is_relative_to(source.resolve()):raise RuntimeError('Unsafe archive member')
                    if member.is_dir() or path.suffix.lower() not in {'.fbx','.txt','.pdf','.md','.license'}:continue
                    chosen={'16_Scratch_Neck_iclone.fbx','21_Holding_Cup_Nodding_iclone.fbx','22_Pointing_And_Laughing_iclone.fbx'} if number==4 else {'06_Cleaning_Glass_With_Cloth_01_iclone.fbx','07_Cleaning_Glass_With_Cloth_02_iclone.fbx'}
                    if path.suffix.lower()=='.fbx' and path.name not in chosen:continue
                    if '__MACOSX' in path.parts or path.name.startswith('._'):continue
                    target.parent.mkdir(parents=True,exist_ok=True)
                    if not target.exists():target.write_bytes(package.read(member))
            records.append({'file':name,'bytes':archive.stat().st_size,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest()})
        (folder/'ATTRIBUTION.md').write_text(f'# Anderson Rohr — {label}\n\nCreator: Anderson Rohr\nSource: https://andersonrohr.gumroad.com/l/mocap_pack{number}\nDownloaded: 2026-10-25 (Asia/Seoul)\n\nFree for personal and commercial projects. Attribution not required. Do not redistribute, resell or repackage as a standalone product. The project uses adapted animation data on its existing characters.\n',encoding='utf-8')
        (LOG/(label+'-download.json')).write_text(json.dumps(records,indent=2),encoding='utf-8');print(label,'downloaded',records,'FBX files',len(list(source.rglob('*.fbx'))),flush=True)
