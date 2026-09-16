"""OpenNPC HTTP API: lets a game engine (Unity, Godot, a web page) talk to
OpenNPC characters over plain HTTP + JSON.

    python server/api.py                          # fine-tuned Qwen via llama.cpp (GPU if available)
    python server/api.py --model gpt2             # the faster GPT-2 model
    python server/api.py --webgl "path/to/Build"  # also serve a Unity WebGL build

Endpoints
    POST /v1/dialogue        the OpenNPC framework contract the Unity demo speaks:
                             {persona, context, conversation, player_message} -> {text, emotion}
    GET  /health, /api/health -> {"ok": true, "backend": ..., "device": ...}
    GET  /api/personalities  -> {"personalities": [{"id", "name", "description", "traits"}]}
    POST /api/chat           {"npc_id", "personality", "message"}
                             -> {"npc_id", "personality", "reply", "tokens_in", "tokens_out", "seconds"}
    POST /api/reset          {"npc_id"} -> {"ok": true}

Each npc_id keeps its own conversation, so every stickman in a scene is a
separate character. The model is loaded once and shared by all of them.

Serving the WebGL build from this same server (--webgl) is the easy way to
test in a browser: page and API share one origin, so no CORS setup is needed.
"""

import argparse
import json
import mimetypes
import re
import sys
import threading
from collections import OrderedDict
from datetime import datetime
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from opennpc.conversation import Conversation  # noqa: E402
from opennpc.memory import PlayerMemory  # noqa: E402
from opennpc.npc import NPC  # noqa: E402
from opennpc.paths import ADAPTER_DIR, DATA_DIR, DEFAULT_GGUF, QWEN_ADAPTER_DIR  # noqa: E402
from opennpc.personality import load_all  # noqa: E402
from opennpc.prompt import build_chat_messages, build_prompt  # noqa: E402

MAX_MESSAGE_CHARS = 300
MAX_NPCS = 500   # oldest conversations are dropped past this, so memory stays bounded

# Unity WebGL builds are often pre-compressed (.br / .gz). Browsers only
# accept them with the right Content-Encoding and the *inner* file's type.
COMPRESSED = {".br": "br", ".gz": "gzip"}
INNER_TYPES = {".js": "application/javascript", ".wasm": "application/wasm",
               ".data": "application/octet-stream", ".json": "application/json"}


class OpenNPCService:
    def __init__(self, model, log_path=None):
        self.model = model
        # Every exchange is appended here (JSON lines), so odd things players type
        # can be reviewed and turned into training data. None disables logging.
        self.log_path = log_path
        self.personalities = {p.name.lower(): p for p in load_all()}
        self.npcs = OrderedDict()          # npc_id -> NPC
        self.memories = OrderedDict()      # npc_id -> PlayerMemory: what this NPC was told
        self.lock = threading.Lock()       # one generation at a time: the model isn't thread-safe

    def list_personalities(self):
        return [{"id": pid, "name": p.name, "description": p.description, "traits": p.traits}
                for pid, p in self.personalities.items()]

    def chat(self, npc_id, personality_id, message):
        personality = self.personalities.get(personality_id.lower())
        if personality is None:
            raise ValueError(f"Unknown personality '{personality_id}'. "
                             f"Choose one of: {', '.join(self.personalities)}")
        with self.lock:
            npc = self.npcs.get(npc_id)
            if npc is None or npc.personality is not personality:
                npc = NPC(personality, Conversation(), self.model, build_prompt)
                self.npcs[npc_id] = npc
                while len(self.npcs) > MAX_NPCS:
                    self.npcs.popitem(last=False)
            self.npcs.move_to_end(npc_id)
            reply = npc.respond(message)
            stats = dict(self.model.last_stats)
        return {"npc_id": npc_id, "personality": personality.name.lower(), "reply": reply,
                "tokens_in": stats.get("prompt_tokens"), "tokens_out": stats.get("new_tokens"),
                "seconds": round(stats.get("seconds", 0.0), 3)}

    def reset(self, npc_id):
        with self.lock:
            self.npcs.pop(npc_id, None)

    def dialogue(self, body):
        """The framework contract (framework/schemas/dialogue-*.schema.json).

        Chat model (Qwen): the whole persona becomes the system prompt and the
        conversation the client sends becomes the chat history, so knowledge,
        opinions and earlier turns all shape the reply.

        GPT-2: it only knows four speaking styles, so the persona's traits, mood and
        speaking style pick the closest one; the rest of the persona can't reach it."""
        persona = body["persona"]
        context = body.get("context") or {}
        style = persona_to_personality(persona)
        opening = context.get("event") == "conversation_start"
        message = "Hello." if opening else str(body["player_message"]).strip()[:MAX_MESSAGE_CHARS]
        npc_id = str(persona.get("id") or persona.get("name"))[:64]
        if self.model.chat:
            history = [t for t in body.get("conversation") or [] if isinstance(t, dict)]
            memory = self.memories.get(npc_id) or PlayerMemory()
            if not opening:
                memory.observe(message)
            self.memories[npc_id] = memory
            self.memories.move_to_end(npc_id)
            while len(self.memories) > MAX_NPCS:
                self.memories.popitem(last=False)
            with self.lock:
                text = self.model.generate(build_chat_messages(persona, history, message, context, player_memory=memory))
                seconds = self.model.last_stats.get("seconds", 0.0)
            text = memory.correct(message, text, persona.get("name"))
            result = {"reply": text, "seconds": round(seconds, 3)}
            used = "persona"
        else:
            result = self.chat(npc_id, style, message)
            used = style
        reply = {"text": result["reply"], "emotion": EMOTION[style], "model": self.model.name,
                 "model_personality": used, "seconds": result["seconds"]}
        if self.log_path:
            entry = {"time": datetime.now().isoformat(timespec="seconds"), "npc": persona.get("name"),
                     "personality": style, "user": "(walks up)" if opening else message, "response": reply["text"]}
            with self.lock, open(self.log_path, "a", encoding="utf-8") as f:
                f.write(json.dumps(entry, ensure_ascii=False) + "\n")
        return reply


# Words in a persona that point at each trained speaking style.
STYLE_WORDS = {
    "rude": {"rude", "impatient", "grumpy", "gruff", "irritable", "angry", "hostile", "dismissive", "blunt",
             "short", "tempered", "annoyed", "arrogant", "stressed", "stern", "curt"},
    "sarcastic": {"sarcastic", "sardonic", "cynical", "dry", "ironic", "witty", "snarky", "mocking", "deadpan"},
    "paranoid": {"paranoid", "suspicious", "nervous", "anxious", "jumpy", "secretive", "distrustful", "wary",
                 "fearful", "scared", "worried", "shifty", "cryptic"},
    "friendly": {"friendly", "cheerful", "kind", "warm", "curious", "helpful", "calm", "patient", "polite",
                 "gentle", "happy", "optimistic", "chatty", "caring", "thoughtful", "content"},
}
EMOTION = {"rude": "annoyed", "sarcastic": "neutral", "paranoid": "suspicious", "friendly": "happy"}


def persona_to_personality(persona):
    def words(text):
        return re.findall(r"[a-z]+", str(text).lower())   # "dry-humoured" -> dry, humoured

    traits = [w for t in persona.get("personalityTraits", []) for w in words(t)]
    others = words(persona.get("mood", "")) + words(persona.get("speakingStyle", ""))
    scores = {style: 0.0 for style in STYLE_WORDS}
    for group, weight in ((traits, 2.0), (others, 1.0)):   # personality traits count double
        for word in group:
            for style, vocab in STYLE_WORDS.items():
                if word in vocab:
                    scores[style] += weight
    # On a tie the more distinctive style wins: "kind but blunt" should sound blunt.
    order = ["sarcastic", "rude", "paranoid", "friendly"]
    best = max(order, key=lambda s: (scores[s], -order.index(s)))
    return best if scores[best] > 0 else "friendly"


def validate_dialogue(body):
    if not isinstance(body, dict):
        return "body must be a JSON object"
    persona = body.get("persona")
    if not isinstance(persona, dict):
        return "persona is required"
    if not (persona.get("id") or persona.get("name")):
        return "persona.id or persona.name is required"
    opening = (body.get("context") or {}).get("event") == "conversation_start"
    if not opening and not str(body.get("player_message") or "").strip():
        return "player_message is required (unless context.event is conversation_start)"
    return None


def make_handler(service, backend_name, webgl_dir, allow_origin):
    class Handler(SimpleHTTPRequestHandler):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, directory=str(webgl_dir) if webgl_dir else None, **kwargs)

        # --- API -----------------------------------------------------------------
        def do_OPTIONS(self):
            self.send_response(204)
            self.send_cors()
            self.end_headers()

        def do_GET(self):
            if self.path in ("/api/health", "/health"):
                return self.send_json({"ok": True, "backend": backend_name, "device": service.model.device})
            if self.path == "/api/personalities":
                return self.send_json({"personalities": service.list_personalities()})
            if self.path.startswith(("/api/", "/v1/")):
                return self.send_json({"error": "Not found"}, 404)
            if webgl_dir is None:
                return self.send_json({"error": "No WebGL build is being served. Start with --webgl PATH."}, 404)
            return super().do_GET()

        def do_POST(self):
            try:
                length = int(self.headers.get("Content-Length", 0))
                body = json.loads(self.rfile.read(length) or b"{}")
            except (ValueError, json.JSONDecodeError):
                return self.send_json({"error": "Body must be JSON"}, 400)

            if self.path == "/v1/dialogue":
                problem = validate_dialogue(body)
                if problem:
                    return self.send_json({"error": problem}, 400)
                reply = service.dialogue(body)
                said = body.get("player_message") or "(walks up)"
                sys.stderr.write(f"{str(body['persona'].get('name', '?')):<9} [{reply['model_personality']}] "
                                 f"<- {said!r} -> {reply['text']!r} ({reply['seconds']}s)\n")
                return self.send_json(reply)
            if self.path == "/api/chat":
                npc_id = str(body.get("npc_id", "")).strip()[:64]
                message = str(body.get("message", "")).strip()[:MAX_MESSAGE_CHARS]
                personality = str(body.get("personality", "")).strip()
                if not npc_id or not message or not personality:
                    return self.send_json({"error": "npc_id, personality and message are required"}, 400)
                try:
                    return self.send_json(service.chat(npc_id, personality, message))
                except ValueError as e:
                    return self.send_json({"error": str(e)}, 400)
            if self.path == "/api/reset":
                service.reset(str(body.get("npc_id", "")))
                return self.send_json({"ok": True})
            return self.send_json({"error": "Not found"}, 404)

        # --- helpers --------------------------------------------------------------
        def send_cors(self):
            self.send_header("Access-Control-Allow-Origin", allow_origin)
            self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type, Accept")
            # Lets a page hosted on the internet (the GitHub Pages demo) call a server on
            # the visitor's own machine; newer Chrome versions ask for this before allowing it.
            self.send_header("Access-Control-Allow-Private-Network", "true")

        def send_json(self, data, status=200):
            payload = json.dumps(data).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(payload)))
            self.send_cors()
            self.end_headers()
            self.wfile.write(payload)

        def guess_type(self, path):
            suffix = Path(path).suffix
            if suffix in COMPRESSED:
                inner = Path(path[: -len(suffix)]).suffix
                return INNER_TYPES.get(inner, mimetypes.guess_type(path[: -len(suffix)])[0] or "application/octet-stream")
            return INNER_TYPES.get(suffix) or super().guess_type(path)

        def end_headers(self):
            suffix = Path(self.path.split("?")[0]).suffix
            if suffix in COMPRESSED and not self.path.startswith(("/api/", "/v1/")):
                self.send_header("Content-Encoding", COMPRESSED[suffix])
            super().end_headers()

        def log_message(self, fmt, *args):
            pass  # dialogue lines are logged above; per-file request lines would drown them

    return Handler


def main():
    parser = argparse.ArgumentParser(description="OpenNPC HTTP API")
    parser.add_argument("--host", default="127.0.0.1", help="use 0.0.0.0 to allow other devices on your network")
    parser.add_argument("--port", type=int, default=8787, help="8787 is the endpoint the OpenNPC Unity demo expects")
    parser.add_argument("--webgl", type=Path, help="Unity WebGL build folder to serve at /")
    parser.add_argument("--allow-origin", default="*",
                        help="CORS origin allowed to call the API (set your demo site's URL in production)")
    parser.add_argument("--model", choices=["llamacpp", "qwen", "gpt2"], default="llamacpp",
                        help="llamacpp (default): the fine-tuned Qwen through llama.cpp, fastest. "
                             "qwen: the same model through PyTorch. gpt2: the old small model")
    parser.add_argument("--gguf", type=Path, default=DEFAULT_GGUF,
                        help="llamacpp: the model file made by training/export_gguf.py")
    parser.add_argument("--cpu", action="store_true", help="llamacpp: don't use the GPU")
    parser.add_argument("--adapter", type=Path,
                        help="LoRA adapter folder. Defaults: models/opennpc-qwen-lora-v3 (qwen), "
                             "models/opennpc-lora (gpt2)")
    parser.add_argument("--no-adapter", action="store_true", help="run the base model without fine-tuning")
    parser.add_argument("--no-log", action="store_true", help="don't save conversations to data/dialogue_log.jsonl")
    args = parser.parse_args()

    if args.webgl and not (args.webgl / "index.html").exists():
        sys.exit(f"{args.webgl} has no index.html. Point --webgl at the folder Unity's WebGL build produced.")

    from opennpc.model import GPT2Backend, GPT2LoRABackend, QwenChatBackend
    print("Loading model...")
    if args.model == "llamacpp":
        from opennpc.llamacpp import LlamaCppBackend
        model = LlamaCppBackend(args.gguf, gpu=not args.cpu)
        backend_name = model.name
    elif args.model == "qwen":
        adapter = None if args.no_adapter else (args.adapter or (QWEN_ADAPTER_DIR if QWEN_ADAPTER_DIR.exists() else None))
        model = QwenChatBackend(adapter)
        backend_name = "Qwen2.5-0.5B-Instruct" + (f" + LoRA adapter ({Path(adapter).name})" if adapter else "")
    elif args.no_adapter:
        model, backend_name = GPT2Backend(), "GPT-2 (base)"
    else:
        try:
            adapter = args.adapter or ADAPTER_DIR
            model, backend_name = GPT2LoRABackend(adapter), f"GPT-2 + LoRA adapter ({Path(adapter).name})"
        except FileNotFoundError as e:
            print(e)
            model, backend_name = GPT2Backend(), "GPT-2 (base)"

    log_path = None if args.no_log else DATA_DIR / "dialogue_log.jsonl"
    service = OpenNPCService(model, log_path)
    if log_path:
        print(f"Saving conversations to {log_path}")
    handler = make_handler(service, backend_name, args.webgl.resolve() if args.webgl else None, args.allow_origin)
    server = ThreadingHTTPServer((args.host, args.port), handler)
    print(f"OpenNPC API ready ({backend_name} on {model.device})", flush=True)
    print(f"  Unity/web demo:  POST http://{args.host}:{args.port}/v1/dialogue", flush=True)
    print(f"  Simple chat:     POST http://{args.host}:{args.port}/api/chat", flush=True)
    print(f"Personalities: {', '.join(service.personalities)}")
    if args.webgl:
        print(f"Serving WebGL build: http://{args.host}:{args.port}/")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    main()
