$serverPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server.py"
$content = Get-Content -LiteralPath $serverPath -Raw

$oldHeader = @'
PDF_NAME = "DatabasePrompt"  # PDF파일 이름
PDF_PATH = "app/data/pdfs/" + PDF_NAME + ".pdf"  # PDF 경로
INDEX_PATH = "app/data/index/world"
WORLD_DIR = "app/data/world"

DEFAULT_SIGNAL = {
'@

$newHeader = @'
PDF_NAME = "DatabasePrompt"  # PDF파일 이름
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "data")
PDF_PATH = os.path.join(DATA_DIR, "pdfs", PDF_NAME + ".pdf")  # PDF 경로
INDEX_DIR = os.path.join(DATA_DIR, "index")
INDEX_PATH = os.path.join(INDEX_DIR, "world")
WORLD_DIR = os.path.join(DATA_DIR, "world")

DEFAULT_SIGNAL = {
'@

$content = $content.Replace($oldHeader, $newHeader)
$content = $content.Replace("retriever = Retriever()`r`nos.makedirs(INDEX_DIR, exist_ok=True)", "retriever = Retriever()`r`nos.makedirs(INDEX_DIR, exist_ok=True)")
$content = $content.Replace("retriever = Retriever()`nos.makedirs(INDEX_DIR, exist_ok=True)", "retriever = Retriever()`nos.makedirs(INDEX_DIR, exist_ok=True)")
$content = $content.Replace("retriever = Retriever()", "retriever = Retriever()`r`nos.makedirs(INDEX_DIR, exist_ok=True)")

$oldLoadWorld = @'
def _load_world_chunks():
    chunks = []
    if not os.path.isdir(WORLD_DIR):
        return chunks
    for fname in os.listdir(WORLD_DIR):
        path = os.path.join(WORLD_DIR, fname)
        if os.path.isfile(path) and any(fname.lower().endswith(ext) for ext in [".txt", ".md"]):
            with open(path, "r", encoding="utf-8") as f:
                text = f.read().strip()
                if text:
                    # 간단히 라인 단위 나누기
                    chunks.extend([p for p in text.split("\n\n") if p.strip()])
    return chunks
'@

$newLoadWorld = @'
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

$content = $content.Replace($oldLoadWorld, $newLoadWorld)

$oldInitIndex = @'
def init_index():
    # 세계관 문서 인덱싱
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
        retriever.load_index(INDEX_PATH)
        print("📚 기존 인덱스 로드 완료")
'@

$newInitIndex = @'
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

$content = $content.Replace($oldInitIndex, $newInitIndex)

$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($serverPath, $content, $encoding)
Write-Output "server.py path fixes applied"
