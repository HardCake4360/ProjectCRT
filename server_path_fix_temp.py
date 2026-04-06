# Temporary manual copy/paste helper for Local-LLM-AI server path fixes.
# Do NOT import this file directly.
# Copy the snippets below into:
# - virtualEnv/RAG_model/app/server.py
# - virtualEnv/RAG_model/app/persona_store.py
# - virtualEnv/RAG_model/app/chatlog.py


# --------------------------------------------------------------------
# 1) server.py: replace the existing path constants block
# --------------------------------------------------------------------

SERVER_PATH_CONSTANTS = r'''
PDF_NAME = "DatabasePrompt"  # PDF file name
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "data")
PDF_PATH = os.path.join(DATA_DIR, "pdfs", PDF_NAME + ".pdf")
INDEX_DIR = os.path.join(DATA_DIR, "index")
INDEX_PATH = os.path.join(INDEX_DIR, "world")
WORLD_DIR = os.path.join(DATA_DIR, "world")
'''


# --------------------------------------------------------------------
# 2) server.py: after "retriever = Retriever()", add this line once
# --------------------------------------------------------------------

SERVER_INDEX_DIR_CREATE = r'''
os.makedirs(INDEX_DIR, exist_ok=True)
'''


# --------------------------------------------------------------------
# 3) server.py: replace _load_world_chunks()
# --------------------------------------------------------------------

SERVER_LOAD_WORLD_CHUNKS = r'''
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
                    chunks.extend([p for p in text.split("\\n\\n") if p.strip()])

    print(f"[PATH] WORLD_FILES={len(world_files)}")
    for world_file in world_files:
        print(f"[WORLD] {world_file}")

    return chunks
'''


# --------------------------------------------------------------------
# 4) server.py: replace init_index()
# --------------------------------------------------------------------

SERVER_INIT_INDEX = r'''
def init_index():
    print(f"[PATH] BASE_DIR={BASE_DIR}")
    print(f"[PATH] DATA_DIR={DATA_DIR}")
    print(f"[PATH] PDF_PATH={PDF_PATH}")
    print(f"[PATH] INDEX_PATH={INDEX_PATH}")
    print(f"[PATH] WORLD_DIR={WORLD_DIR}")

    if not os.path.exists(INDEX_PATH + ".index"):
        world_chunks = _load_world_chunks()
        if world_chunks:
            print(f"Loaded {len(world_chunks)} world chunks")
            retriever.build_index(world_chunks)
            retriever.save_index(INDEX_PATH)
            print("Index build complete")
        else:
            print("No world documents found")
    else:
        print(f"[PATH] Loading existing index from {INDEX_PATH}.index")
        retriever.load_index(INDEX_PATH)
        print("Existing index loaded")
'''


# --------------------------------------------------------------------
# 5) persona_store.py: replace full file content
# --------------------------------------------------------------------

PERSONA_STORE_FILE = r'''
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
'''


# --------------------------------------------------------------------
# 6) chatlog.py: replace full file content
# --------------------------------------------------------------------

CHATLOG_FILE = r'''
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
        f.write(json.dumps(rec, ensure_ascii=False) + "\\n")

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
'''
