$serverPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server.py"
$personaStorePath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\persona_store.py"
$chatlogPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\chatlog.py"

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$serverContent = Get-Content -LiteralPath $serverPath -Raw
$serverContent = $serverContent -replace 'PDF_NAME = "DatabasePrompt"  # PDF파일 이름\s*PDF_PATH = "app/data/pdfs/" \+ PDF_NAME \+ "\.pdf"  # PDF 경로\s*INDEX_PATH = "app/data/index/world"\s*WORLD_DIR = "app/data/world"', @'
PDF_NAME = "DatabasePrompt"  # PDF파일 이름
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "data")
PDF_PATH = os.path.join(DATA_DIR, "pdfs", PDF_NAME + ".pdf")  # PDF 경로
INDEX_DIR = os.path.join(DATA_DIR, "index")
INDEX_PATH = os.path.join(INDEX_DIR, "world")
WORLD_DIR = os.path.join(DATA_DIR, "world")
'@
$serverContent = $serverContent -replace 'retriever = Retriever\(\)', @'
retriever = Retriever()
os.makedirs(INDEX_DIR, exist_ok=True)
'@
$serverContent = $serverContent -replace 'def _load_world_chunks\(\):\s*    chunks = \[\]\s*    if not os\.path\.isdir\(WORLD_DIR\):\s*        return chunks\s*    for fname in os\.listdir\(WORLD_DIR\):\s*        path = os\.path\.join\(WORLD_DIR, fname\)\s*        if os\.path\.isfile\(path\) and any\(fname\.lower\(\)\.endswith\(ext\) for ext in \["\.txt", "\.md"\]\):\s*            with open\(path, "r", encoding="utf-8"\) as f:\s*                text = f\.read\(\)\.strip\(\)\s*                if text:\s*                    # 간단히 라인 단위 나누기\s*                    chunks\.extend\(\[p for p in text\.split\("\\n\\n"\) if p\.strip\(\)\]\)\s*    return chunks', @'
def _load_world_chunks():
    chunks = []
    print(f"[PATH] WORLD_DIR={WORLD_DIR}")
    if not os.path.isdir(WORLD_DIR):
        print(f"[WARN] WORLD_DIR not found: {WORLD_DIR}")
        return chunks

    world_files = []
    for fname in os.listdir(WORLD_DIR):
        path = os.path.join(WORLD_DIR, fname)
        if os.path.isfile(path) and any(fname.lower().endswith(ext) for ext in [".txt", ".md"]):
            world_files.append(path)
            with open(path, "r", encoding="utf-8") as f:
                text = f.read().strip()
                if text:
                    chunks.extend([p for p in text.split("\n\n") if p.strip()])

    print(f"[PATH] WORLD_FILES={len(world_files)}")
    for world_file in world_files:
        print(f"[WORLD] {world_file}")
    return chunks
'@
$serverContent = $serverContent -replace 'def init_index\(\):\s*    # 세계관 문서 인덱싱\s*    if not os\.path\.exists\(INDEX_PATH \+ "\.index"\):\s*        world_chunks = _load_world_chunks\(\)\s*        if world_chunks:\s*            print\(f" 세계관 문서 \{len\(world_chunks\)\}개 청크 적재"\)\s*            retriever\.build_index\(world_chunks\)\s*            retriever\.save_index\(INDEX_PATH\)\s*            print\("인덱스 로드 완료"\)\s*        else:\s*            print\("세계관 문서 없음"\)\s*    else:\s*        retriever\.load_index\(INDEX_PATH\)\s*        print\("📚 기존 인덱스 로드 완료"\)', @'
def init_index():
    print(f"[PATH] BASE_DIR={BASE_DIR}")
    print(f"[PATH] DATA_DIR={DATA_DIR}")
    print(f"[PATH] PDF_PATH={PDF_PATH}")
    print(f"[PATH] INDEX_PATH={INDEX_PATH}")
    print(f"[PATH] WORLD_DIR={WORLD_DIR}")

    if not os.path.exists(INDEX_PATH + ".index"):
        world_chunks = _load_world_chunks()
        if world_chunks:
            print(f" 세계관 문서 {len(world_chunks)}개 청크 적재")
            retriever.build_index(world_chunks)
            retriever.save_index(INDEX_PATH)
            print("인덱스 로드 완료")
        else:
            print("세계관 문서 없음")
    else:
        print(f"[PATH] Loading existing index from {INDEX_PATH}.index")
        retriever.load_index(INDEX_PATH)
        print("📚 기존 인덱스 로드 완료")
'@
Write-Utf8NoBom -Path $serverPath -Content $serverContent

$personaStoreContent = @'
import os, json
from functools import lru_cache

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
PERSONA_DIR = os.path.join(BASE_DIR, "data", "personas")

@lru_cache(maxsize=64)
def list_persona_keys():
    if not os.path.isdir(PERSONA_DIR):
        return []
    return sorted([
        os.path.splitext(f)[0]
        for f in os.listdir(PERSONA_DIR)
        if f.endswith(".json")
    ])

@lru_cache(maxsize=128)
def load_persona(key: str) -> dict | None:
    path = os.path.join(PERSONA_DIR, f"{key}.json")
    if not os.path.exists(path):
        return None
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)
'@
Write-Utf8NoBom -Path $personaStorePath -Content $personaStoreContent

$chatlogContent = @'
# chatlog.py
import os, json, time
from typing import List, Dict

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
BASE = os.path.join(BASE_DIR, "data", "chatlogs")
os.makedirs(BASE, exist_ok=True)

def _path(user_id: str) -> str:
    return os.path.join(BASE, f"{user_id}.jsonl")

def append_log(user_id: str, speaker: str, text: str) -> None:
    rec = {"ts": time.time(), "speaker": speaker, "text": text}
    with open(_path(user_id), "a", encoding="utf-8") as f:
        f.write(json.dumps(rec, ensure_ascii=False) + "\n")

def read_log(user_id: str, max_items: int = 200) -> List[Dict]:
    p = _path(user_id)
    if not os.path.exists(p):
        return []
    lines = []
    with open(p, "r", encoding="utf-8") as f:
        for line in f:
            try:
                lines.append(json.loads(line))
            except:
                continue
    return lines[-max_items:]

def reset_log(user_id: str) -> None:
    p = _path(user_id)
    if os.path.exists(p):
        os.remove(p)
    reset_summary(user_id)

def _summary_path(user_id: str) -> str:
    return os.path.join(BASE, f"{user_id}.summary.json")

def write_summary(user_id: str, summary: dict) -> None:
    with open(_summary_path(user_id), "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False)

def read_summary(user_id: str) -> dict | None:
    p = _summary_path(user_id)
    if not os.path.exists(p):
        return None
    try:
        with open(p, "r", encoding="utf-8") as f:
            return json.load(f)
    except:
        return None

def reset_summary(user_id: str) -> None:
    p = _summary_path(user_id)
    if os.path.exists(p):
        os.remove(p)
'@
Write-Utf8NoBom -Path $chatlogPath -Content $chatlogContent

Write-Output "Patched external path handling:"
Write-Output "  server.py"
Write-Output "  persona_store.py"
Write-Output "  chatlog.py"
