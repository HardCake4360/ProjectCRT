"""Download only the selected free uploads from their creators' itch.io endpoints."""
from pathlib import Path
import concurrent.futures
import hashlib
import http.cookiejar
import html
import json
import re
import urllib.parse
import urllib.request
import urllib.error
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LOG = ROOT / 'Logs/PrologueAssets'
DEST = ROOT / 'Assets/ThirdParty/Prologue'
PACKS = [
    ('BricchiCar', 'https://bricchi-games.itch.io/retro-car-with-interior', 'Bricchi Games', 'Creative Commons; commercial use and attribution required; inspect included licence', ['Retro Car1.0.zip']),
    ('ElegantCrowSkyscrapers', 'https://elegantcrow.itch.io/retro-skyscraper-pack', 'Elegant Crow', 'CC0 1.0', None),
    ('McPatoInterior', 'https://mcpato.itch.io/house-interior-psx-assets', 'McPato', 'CC BY 4.0', ['DoriHome.fbx']),
    ('McPatoBar', 'https://mcpato.itch.io/barranco-bar-ps1-environment', 'McPato', 'CC BY 4.0', ['bar_end.fbx']),
    ('FurnitureFree', 'https://heyheythere.itch.io/psx-furniture-free', 'heyheythere', 'CC BY 4.0; creator discloses AI assistance', None),
    ('GGBotCars', 'https://ggbot.itch.io/psx-style-cars', 'GGBotNet', 'CC0 1.0', None),
]

def fetch(pack):
    key, url, author, licence, wanted = pack
    folder = DEST / key / 'Source'; folder.mkdir(parents=True, exist_ok=True)
    archives = LOG / 'Downloads' / key; archives.mkdir(parents=True, exist_ok=True)
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    opener.addheaders = [('User-Agent', 'Mozilla/5.0')]
    def get(address):
        return opener.open(address, timeout=60)
    try: purchase = get(url + '/purchase').read().decode()
    except urllib.error.HTTPError as error:
        if error.code != 404: raise
        purchase = get(url).read().decode()
    meta = re.search(r'<meta[^>]*name="csrf_token"[^>]*>', purchase).group()
    csrf = re.search(r'(?:value|content)="([^"]+)"', meta).group(1)
    def post(address, ref):
        request = urllib.request.Request(address, data=urllib.parse.urlencode({'csrf_token': csrf}).encode(),
            headers={'Referer': ref, 'X-Requested-With': 'XMLHttpRequest'})
        return json.load(get(request))
    download_page = post(url + '/download_url', url + '/purchase')['url']
    listing = get(download_page).read().decode()
    uploads = re.findall(r'data-upload_id="(\d+)".*?<strong\b[^>]*>(.*?)</strong>', listing, re.S)
    selected = [(uid, html.unescape(name)) for uid, name in uploads if wanted is None or html.unescape(name) in wanted]
    if not selected:
        raise RuntimeError(f'{key}: no expected free upload found')
    records = []
    for uid, name in selected:
        if Path(name).name != name: raise RuntimeError('Unexpected file name')
        target = archives / name
        if not target.exists():
            info = post(url + '/file/' + uid, download_page)
            if info.get('external'): raise RuntimeError('External download requires separate inspection')
            with get(info['url']) as response, target.open('wb') as out:
                while True:
                    block = response.read(1024 * 1024)
                    if not block: break
                    out.write(block)
        if zipfile.is_zipfile(target):
            with zipfile.ZipFile(target) as archive:
                for member in archive.infolist():
                    relative = Path(member.filename)
                    output = (folder / relative).resolve()
                    if not output.is_relative_to(folder.resolve()): raise RuntimeError('Unsafe archive path')
                    # Asset files only: never import downloaded code, Unity packages or project files.
                    allowed = {'.fbx','.obj','.mtl','.glb','.gltf','.png','.jpg','.jpeg','.tga','.bmp','.ogg','.wav','.mp3','.txt','.md','.pdf','.license','.html'}
                    if member.is_dir() or relative.suffix.lower() not in allowed: continue
                    if '__MACOSX' in relative.parts or relative.name.startswith('._'): continue
                    output.parent.mkdir(parents=True, exist_ok=True)
                    if not output.exists(): output.write_bytes(archive.read(member))
        elif target.suffix.lower() == '.fbx':
            output = folder / name
            if not output.exists(): output.write_bytes(target.read_bytes())
        else:
            records.append({'file': name, 'status': 'archive retained; conversion needed'})
            continue
        records.append({'file': name, 'bytes': target.stat().st_size, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
    attribution = (f'# {key}\n\nAuthor: {author}\nSource: {url}\nLicence: {licence}\nDownloaded: 2026-10-25 (Asia/Seoul)\n\n'
        'Original model/texture files are retained under Source. ProjectCRT may create derived FBX files, extract embedded textures, '
        'create URP materials, and adapt scale, orientation and scene placement. Keep this notice and the included original licence in distributed credits. '
        'Download archives and checksums are recorded under Logs/PrologueAssets. Downloaded scripts, engine packages and project files were not imported.\n')
    (folder.parent / 'ATTRIBUTION.md').write_text(attribution, encoding='utf-8')
    return {'key': key, 'source': url, 'author': author, 'licence': licence, 'files': records}

if __name__ == '__main__':
    LOG.mkdir(parents=True, exist_ok=True)
    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures = {pool.submit(fetch, pack): pack[0] for pack in PACKS}
        for future in concurrent.futures.as_completed(futures):
            try:
                result = future.result(); results.append(result)
                print(result['key'], 'OK', [f['file'] for f in result['files']], flush=True)
            except Exception as error:
                result = {'key': futures[future], 'error': str(error)}; results.append(result)
                print(result['key'], 'FAILED', str(error), flush=True)
    (LOG / 'downloads.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    if any('error' in result for result in results): raise SystemExit(1)
