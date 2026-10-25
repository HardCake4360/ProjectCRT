"""Install the reviewable joint-turn module without replacing existing server logic."""
import argparse
import ast
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
HOOK = '''# Rebecca joint-turn prototype (ProjectCRT).
from npc_joint_turn import register_joint_turn_routes
register_joint_turn_routes(
    app, _require_openai_client, lambda: OPENAI_MODEL, load_persona,
    lambda: OPENAI_REASONING_EFFORT,
)

'''
FILES = ("server.py", "server_test.py", "server_for_experiment.py")


def install(app, apply=False):
    app = Path(app).resolve(strict=True)
    if app.name != "app" or app.parent.name != "RAG_model":
        raise ValueError("Expected the explicitly selected RAG_model/app directory.")
    backup = ROOT / "Logs" / "NpcJointTurn" / "original"
    prepared = []
    for name in FILES:
        path = app / name
        source = path.read_text(encoding="utf-8-sig")
        if "from npc_joint_turn import register_joint_turn_routes" in source:
            print(name, "already registered")
            continue
        marker = 'if __name__ == "__main__":'
        if source.count(marker) != 1:
            raise ValueError(f"{name}: expected one main guard")
        updated = source.replace(marker, HOOK + marker)
        ast.parse(updated)
        prepared.append((path, updated))
    module = ROOT / "tools" / "npc_gateway" / "npc_joint_turn.py"
    ast.parse(module.read_text(encoding="utf-8"))
    if apply:
        backup.mkdir(parents=True, exist_ok=True)
        for path, source in prepared:
            original = backup / path.name
            if not original.exists():
                shutil.copy2(path, original)
            path.write_text(source, encoding="utf-8", newline="\n")
        shutil.copy2(module, app / module.name)
    print("Applied" if apply else "Preview", len(prepared), "server hooks and npc_joint_turn.py; no keys or logs changed.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", required=True)
    parser.add_argument("--apply", action="store_true")
    arguments = parser.parse_args()
    install(arguments.app, arguments.apply)
