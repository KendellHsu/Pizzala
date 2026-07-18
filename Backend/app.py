# ─────────────────────────────────────────────────────────────
# app.py — history site backend. Implements the 4 endpoints from
# Docs/WEB_API_CONTRACT.md by reading SessionLogger's raw JSON dumps
# directly; no separate database.
#
# DATA_DIR resolution: config.json's "data_dir" (relative to this file),
# overridable with the PIZZALA_DATA_DIR env var - that's how Kendell's
# machine points this at the real Application.persistentDataPath instead
# of the repo's sample Data/ folder, with zero code changes.
#
# Run: pip install -r requirements.txt && uvicorn app:app --host 0.0.0.0 --port 8787
# ─────────────────────────────────────────────────────────────
import json
import os
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from fastapi.staticfiles import StaticFiles

BACKEND_DIR = Path(__file__).resolve().parent
SESSION_PREFIX = "session_"
SESSION_SUFFIX = ".json"


def resolve_data_dir() -> Path:
    config_path = BACKEND_DIR / "config.json"
    configured = None
    if config_path.exists():
        with open(config_path, encoding="utf-8") as f:
            configured = json.load(f).get("data_dir")

    data_dir = os.environ.get("PIZZALA_DATA_DIR", configured)
    if not data_dir:
        raise RuntimeError(
            "No DATA_DIR configured - set \"data_dir\" in Backend/config.json "
            "or the PIZZALA_DATA_DIR environment variable."
        )

    path = Path(data_dir)
    if not path.is_absolute():
        path = (BACKEND_DIR / path).resolve()
    return path


DATA_DIR = resolve_data_dir()
SESSIONS_DIR = DATA_DIR / "sessions"
PHOTOS_DIR = DATA_DIR / "photos"
PUBLIC_DIR = BACKEND_DIR / "public"  # front-end's built static files land here

app = FastAPI()
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["GET"],
    allow_headers=["*"],
)


# Contract (WEB_API_CONTRACT.md) specifies error bodies as {"error": "..."},
# not FastAPI's default {"detail": "..."} - keep every HTTPException call
# site using the normal `detail=` kwarg and translate the wire format here.
@app.exception_handler(HTTPException)
async def http_exception_handler(request: Request, exc: HTTPException):
    return JSONResponse(status_code=exc.status_code, content={"error": exc.detail})


# ── session file I/O ──

def session_id_from_filename(filename: str) -> str:
    # "session_20260718_203048_Control.json" -> "20260718_203048_Control".
    # This (not the JSON's own "sessionId" field, which never carries the
    # condition suffix) is what the contract's "id" means - it's what
    # SessionLogger.SaveToDisk() actually names the file.
    stem = filename[len(SESSION_PREFIX):] if filename.startswith(SESSION_PREFIX) else filename
    return stem[: -len(SESSION_SUFFIX)] if stem.endswith(SESSION_SUFFIX) else stem


def read_session_file(path: Path) -> dict:
    # Unity's Encoding.UTF8 writes a BOM; utf-8-sig strips it, and is a
    # harmless no-op for files that don't have one.
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def list_session_files():
    if not SESSIONS_DIR.exists():
        return []
    return sorted(SESSIONS_DIR.glob(f"{SESSION_PREFIX}*{SESSION_SUFFIX}"))


def load_session_by_id(session_id: str):
    path = SESSIONS_DIR / f"{SESSION_PREFIX}{session_id}{SESSION_SUFFIX}"
    if not path.exists():
        return None
    return read_session_file(path)


# ── field normalization (tolerant of pre-1-1/1-2/1-3 session files) ──

def photo_filename(raw_path: str) -> str:
    # Recorded photo paths are absolute paths from whichever machine
    # played the round (e.g. "C:/Users/dexlab/.../photos\\face_x.png") -
    # meaningless on this machine. Only the filename is portable; the
    # actual file is looked up under this backend's own PHOTOS_DIR.
    return Path(raw_path.replace("\\", "/")).name


def normalize_photo(entry) -> dict:
    # Pre-PhotoRecord sessions store customerFacePhotos/environmentPhotos
    # as bare path strings; current sessions store {path, gameTime, caption}.
    if isinstance(entry, str):
        return {"fileName": photo_filename(entry), "gameTime": 0.0, "caption": ""}
    return {
        "fileName": photo_filename(entry.get("path", "")),
        "gameTime": entry.get("gameTime", 0.0) or 0.0,
        "caption": entry.get("caption") or "",
    }


def split_boss_comment(boss_comment: str):
    # session.bossComment (when set) is already "hashtag line\nnote line" -
    # BossCommentService.Deliver() stripped the persona line before saving.
    # Sessions from Control, or from before 1-2, leave this "" / missing.
    if not boss_comment:
        return [], ""
    lines = [l.strip() for l in boss_comment.split("\n") if l.strip()]
    if not lines:
        return [], ""
    hashtags = [tok for tok in lines[0].split() if tok.startswith("#")]
    note = "\n".join(lines[1:])
    return hashtags, note


def pick_evenly(items: list, n: int) -> list:
    # Contract: "最多 6 張，時間上均勻分布，不是只取前 6". Assumes items is
    # already sorted (by gameTime) - spreads n index picks evenly across it.
    if len(items) <= n:
        return items
    if n <= 1:
        return items[:1]
    step = (len(items) - 1) / (n - 1)
    seen_idx = []
    for i in range(n):
        idx = round(i * step)
        if idx not in seen_idx:
            seen_idx.append(idx)
    return [items[i] for i in seen_idx]


def card_image_url(player_card_image: str):
    return f"/photos/{player_card_image}" if player_card_image else None


# ── endpoints ──

@app.get("/api/sessions")
def get_sessions():
    sessions = []
    for file_path in list_session_files():
        try:
            raw = read_session_file(file_path)
        except (OSError, json.JSONDecodeError):
            continue  # corrupt/partial file - skip rather than 500 the whole wall

        summary = raw.get("summary") or {}
        if summary.get("totalThrows", 0) <= 0:
            continue  # "0 投擲的空場次後端會自動濾掉"

        hashtags, _ = split_boss_comment(raw.get("bossComment") or "")
        sessions.append({
            "id": session_id_from_filename(file_path.name),
            "playedAt": raw.get("startedAtIso", ""),
            "cardImage": card_image_url(raw.get("playerCardImage") or ""),
            "hashtags": hashtags,
            "persona": raw.get("playerPersona") or "",
        })

    sessions.sort(key=lambda s: s["playedAt"], reverse=True)
    return {"sessions": sessions}


@app.get("/api/sessions/{session_id}")
def get_session_detail(session_id: str):
    raw = load_session_by_id(session_id)
    if raw is None:
        raise HTTPException(status_code=404, detail="session not found")

    hashtags, note = split_boss_comment(raw.get("bossComment") or "")

    face_photos = [dict(normalize_photo(p), type="customerFace")
                   for p in (raw.get("customerFacePhotos") or [])]
    env_photos = [dict(normalize_photo(p), type="environment")
                  for p in (raw.get("environmentPhotos") or [])]
    # playerFacePhotos deliberately excluded - real player photos never
    # reach the public site, per the contract's privacy note.
    merged = sorted(face_photos + env_photos, key=lambda p: p["gameTime"])
    picked = pick_evenly(merged, 6)

    return {
        "id": session_id,
        "playedAt": raw.get("startedAtIso", ""),
        "card": {
            "image": card_image_url(raw.get("playerCardImage") or ""),
            "hashtags": hashtags,
            "persona": raw.get("playerPersona") or "",
        },
        "bossComment": note,
        "photos": [
            {
                "image": f"/photos/{p['fileName']}",
                "caption": p["caption"],
                "gameTime": p["gameTime"],
                "type": p["type"],
            }
            for p in picked
        ],
    }


if PHOTOS_DIR.exists():
    app.mount("/photos", StaticFiles(directory=str(PHOTOS_DIR)), name="photos")

# Catch-all for the front-end's built static files - must be mounted last,
# since Starlette matches routes in registration order and a "/" mount
# would otherwise swallow every request above it, including /api/*.
if (PUBLIC_DIR / "index.html").exists():
    app.mount("/", StaticFiles(directory=str(PUBLIC_DIR), html=True), name="public")
