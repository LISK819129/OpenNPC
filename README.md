<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="branding/gif/opennpc-logo-dark.gif">
    <source media="(prefers-color-scheme: light)" srcset="branding/gif/opennpc-logo.gif">
    <img src="branding/gif/opennpc-logo.gif" alt="OpenNPC — black stickman NPCs walking behind the OpenNPC wordmark" width="720">
  </picture>
</h1>

<p align="center"><b>Personality-driven NPC dialogue.</b><br>
NPCs aren't just dialogue trees. They're people with personalities.</p>

<p align="center">
  <a href="#demo">Demo</a> ·
  <a href="docs/architecture.md">Architecture</a> ·
  <a href="#persona-system">Personas</a> ·
  <a href="#dialogue-providers">Providers</a> ·
  <a href="#ai-backend">AI backend</a> ·
  <a href="web/">Web demo</a>
</p>

---

**OpenNPC** is an open-source framework for creating NPCs whose conversations and
behaviour are driven by **personality, persona, mood, background, knowledge, context,
memory** and **AI/ML language models** — so they feel like individual people rather
than one chatbot wearing many hats.

This repository contains the framework contract (JSON schemas), a playable Unity
demonstration, the web page that hosts it, the **AI backend** (a fine-tuned language model
that answers as each NPC), and the Blender pipeline that produced the whole visual identity.

> **Status: working, measured, early.** The persona and dialogue contracts, the Unity
> integration layer, an offline mock provider and the AI backend all work together. The
> backend runs a fine-tuned Qwen2.5 model through llama.cpp and replies in about **0.14 s**
> on a laptop GPU. Its limits are measured and documented in [`backend/`](backend/README.md).

## Demo

<p align="center">
  <img src="docs/images/demo-street.jpg" width="49%" alt="The OpenNPC street, full of stickman NPCs">
  <img src="docs/images/demo-debug-persona.jpg" width="49%" alt="Talking to Daniel with the persona inspector and debug pipeline open">
</p>

Walk up to anyone, press **E**, and ask *“What do you think about this city?”*.
Then ask someone else the same thing.

| NPC | Persona | Reply (offline `MockDialogueProvider`) |
|---|---|---|
| **Ravi** | taxi driver · impatient, sarcastic | “Too many cars. Not enough patience. I fit right in.” |
| **Maya** | barista · cheerful, curious | “I love it! Everyone's in a hurry but everyone's going somewhere, you know?” |
| **Daniel** | retired train conductor · calm, precise | “It has become faster and louder. But it still runs, and I respect anything that still runs.” |
| **Priya** | night-shift nurse · kind, blunt | “It's a good city. I just mostly see it at 3 a.m., when it's bleeding a bit.” |

**Same question + different persona = different answer.** Nothing in the NPC code is
specific to any of them — the difference is entirely in their persona files.

**With the AI backend** the replies are generated, not templated. Real replies from the
fine-tuned model, including personas it never saw during training:

| Player | NPC | Reply |
|---|---|---|
| "What do you think about this city?" | Ravi *(never in training)* | "Too many cars. Not enough patience." |
| "Where is the train station?" | Anjali | "Two options. North on foot, eight minutes." |
| "My name is Zaid" … "whats my name" | Rahul | "You're Zaid." |
| "who are you" | Rahul | "I'm Rahul." |

| Key | Action |
|---|---|
| `WASD` / arrows | move |
| `E` | talk to the nearest NPC |
| `Enter` | send a message |
| `Esc` | leave the conversation |
| `Tab` | developer debug panel (state, provider, live pipeline) |
| `P` | persona inspector |

Run it: [`web/`](web/README.md) (browser) or `scripts/build-desktop.sh` (Windows player).
To talk to the real model, see [Play with the AI backend](#play-with-the-ai-backend).

## Architecture

```
Player ─► InteractionSystem ─► NPC ─► Persona + Context + Memory
                                              │
                                        DialogueManager
                                              │
                                      IDialogueProvider
                              ┌───────────────┼────────────────┐
                        MockProvider     Local LLM        OpenNPC backend  (backend/)
                         (offline)    (your provider)   HTTP → Python → llama.cpp
                                                         fine-tuned Qwen2.5 on the GPU
```

An NPC never knows where its words come from. Full write-up: [`docs/architecture.md`](docs/architecture.md).

```
OpenNPC/
├── framework/     schemas (persona, dialogue request/response), example personas, backend stub
├── unity/         OpenNPC-Demo — Unity 6 project (runtime layer + demo layer + editor pipeline)
├── web/           landing page + WebGL host (index.html, style.css, script.js)
├── backend/       the AI: fine-tuned model, HTTP server, training, evaluation, docs
├── blender/       stickman rig/animation scripts, .blend source, FBX export
├── branding/      logo scene, rendered previews, GIF / MP4 / PNG outputs
├── scripts/       one-command builds
└── docs/          architecture, blender pipeline, unity demo, web demo
```

## Persona System

A persona is plain data validated by [`framework/schemas/persona.schema.json`](framework/schemas/persona.schema.json):

```json
{
  "id": "npc_001",
  "name": "Ravi",
  "age": 42,
  "occupation": "Taxi Driver",
  "personalityTraits": ["impatient", "sarcastic", "helpful"],
  "mood": "tired",
  "background": "Has driven around the city for more than 15 years.",
  "speakingStyle": "Short sentences and sarcastic remarks.",
  "voice": { "verbosity": "terse", "formality": "casual" },
  "knowledge": [{ "topic": "train station", "keywords": ["station"], "fact": "Three streets that way." }],
  "opinions": [{ "topic": "city", "keywords": ["city"], "text": "Too many cars. Not enough patience." }],
  "likes": ["tea", "old movies"],
  "dislikes": ["traffic", "small talk"],
  "goals": ["Retire before the next road works finish."]
}
```

Ten authored personas live in [`framework/examples/personas/`](framework/examples/personas)
(Ravi, Maya, Daniel, Anjali, Arjun, Priya, Mrs. Okafor, Leo, Sam, Hana). The rest of the
street is filled by a deterministic `PersonaGenerator`, so no two NPCs share a mind.

**Create an NPC:** add a JSON file to `framework/examples/personas/`, run
`python scripts/validate_personas.py`, then `scripts/generate-assets.sh` (or the Unity
menu *OpenNPC → Run Full Pipeline*). Authored personas spawn near the player first.

## Dialogue Providers

```csharp
public interface IDialogueProvider
{
    string Name { get; }
    void Generate(DialogueRequest request, Action<DialogueResponse> onComplete);
}
```

| Provider | Status | Notes |
|---|---|---|
| `MockDialogueProvider` | ✅ working | Offline. Intent keywords + the persona's own knowledge, opinions and voice. Remembers the player's name and notices repeated questions. Not an AI, and doesn't pretend to be. |
| `OpenNPCDialogueProvider` | ✅ working | HTTP client for the documented contract. Talks to the [AI backend](#ai-backend) in `backend/`, or to any server implementing the contract. |
| Your provider | — | Implement the interface, return it from `DialogueManager.CreateProvider`. |

**Or connect your own model** (Ollama, LM Studio, vLLM, a hosted API) through the Node stub, without writing C#:

```bash
OPENNPC_MODEL_URL=http://localhost:11434/v1/chat/completions OPENNPC_MODEL=llama3.1 node framework/examples/backend-stub/server.js
```

Then open the web demo with `?provider=opennpc&endpoint=http://localhost:8787/v1/dialogue`,
or set `provider = OpenNPC` in `OpenNPCConfig`.

> **Never put API keys in Unity.** A WebGL build is downloaded by every visitor; anything
> in it is public. The client talks to *your* server, and the server holds the key.

## AI Backend

[`backend/`](backend/README.md) is the server behind `OpenNPCDialogueProvider`: it turns a
persona and a conversation into an in-character reply.

- **Model:** Qwen2.5-0.5B-Instruct, fine-tuned with **LoRA** on hand-written conversations
  (insults, gibberish, strange questions, small talk) and on detailed personas, so NPCs stay
  in character and answer from their own knowledge.
- **Speed:** merged, converted to **GGUF**, quantized to 4 bits (398 MB) and run by
  **llama.cpp** on the GPU through **Vulkan**: about 0.14 s per reply on a GTX 1650 Ti, down
  from 3.8 s with PyTorch on the CPU.
- **Memory:** remembers what the player tells each NPC (names today) and checks answers to
  "what's my name?" and "what's your name?".
- **Measured:** on personas and player lines never seen in training, 100% of answers use the
  persona's own knowledge and 0% break character. Every reply is in
  [`backend/eval/`](backend/eval).

| | First version (GPT-2) | Qwen, no fine-tuning | **Shipped** |
|---|---|---|---|
| Answers from the persona's knowledge | can't read it | 90% | **100%** |
| Breaks character ("I can't assist with that") | 0%* | 37% | **0%** |
| Seconds per reply | 0.34 | 3.04 | **0.14** |

\* GPT-2 never refused only because it didn't understand enough to.

**New to language models?** [`backend/docs/how-it-works.md`](backend/docs/how-it-works.md)
explains every step from scratch: tokens, Qwen, prompts, memory, LoRA, GGUF, quantization,
llama.cpp and Vulkan.

## Unity Integration

`unity/OpenNPC-Demo` · Unity **6000.6** · URP (unlit, no shadows, no post) · Input System.

- **Runtime layer** (`OpenNPC.Runtime`): personas, `IDialogueProvider`, providers, `DialogueManager`, `ConversationHistory`, `OpenNPCConfig`. No demo code.
- **Demo layer** (`OpenNPC.Demo`): `NPCController`, `NPCPersona`, `NPCMovement`, `NPCInteraction`, `NPCDialogue`, `NPCAnimation`, player, population, camera, UI.
- **Editor pipeline** (`OpenNPC.Editor`): regenerates materials, animator, `NPC.prefab`, the street scene, and runs `DemoVerify` (35 checks).

Everything configurable lives in one asset: `Assets/OpenNPC/Resources/OpenNPC/OpenNPCConfig`
(spawn count, speed range, lanes, interaction radius, dialogue duration, provider, endpoint, debug).
Details: [`docs/unity-demo.md`](docs/unity-demo.md).

## Web Demo

[`web/`](web) is a static page: the wordmark with walkers behind it, the Unity WebGL
player (loading %, fullscreen, controls), the persona comparison and the pipeline. Any
static host works (GitHub Pages included — the build uses gzip with a JS decompression
fallback). Details: [`docs/web-demo.md`](docs/web-demo.md).

## Installation

Requirements: **Unity 6000.6** (+ WebGL Build Support for the browser build),
**Blender 5.2** (only to regenerate art), **Python 3.10+** with Pillow/NumPy (logo media),
**ffmpeg**, **Node 18+** (backend stub only).

```bash
git clone <this repo> && cd OpenNPC
python scripts/validate_personas.py
python -m http.server 8000 -d web        # page; the demo needs a WebGL build in web/demo/
```

Open `unity/OpenNPC-Demo` in Unity Hub and run *OpenNPC → Run Full Pipeline*, then open
`Assets/OpenNPC/Scenes/OpenNPC_Street` and press Play.

### Play with the AI backend

The web demo uses the offline mock unless you point it at a backend. To talk to the real model:

1. **Set up the backend once** (Python 3.10+; a GPU is optional): follow
   [`backend/README.md` → Quick start](backend/README.md#quick-start). It installs the Python
   packages, downloads llama.cpp and builds the 398 MB model file.
2. **Start it:**
   ```bash
   python backend/server/api.py
   ```
3. **Serve the page** in a second terminal:
   ```bash
   python -m http.server 8000 -d web
   ```
4. **Open**
   `http://localhost:8000/?provider=opennpc&endpoint=http://localhost:8787/v1/dialogue&debug=1`,
   walk up to anyone and press **E**. The debug panel (Tab) shows `OpenNPCDialogueProvider`
   and each reply's latency.

The hosted page on GitHub Pages works the same way: add the same `?provider=opennpc&endpoint=...`
to its address while the backend runs on your machine. The browser may ask permission for the
page to reach `localhost`.

## Development

| Command | Does |
|---|---|
| `scripts/generate-assets.sh` | Blender stickman → FBX → Unity sync → prefabs → scene → verify |
| `scripts/build-logo.sh [--preview]` | logo scene → frames → GIF / WebP / MP4 / PNG |
| `scripts/build-desktop.sh` | Windows player + in-build autopilot playtest with screenshots |
| `scripts/build-web.sh [--serve]` | WebGL build → `web/demo/` |
| `scripts/unity.sh <Method>` | any editor step headless (e.g. `OpenNPC.EditorTools.DemoVerify.Run`) |

Tool paths default to this project's machine; override with `UNITY=` and `BLENDER=`.

## Roadmap

- [x] Persona + dialogue JSON schemas
- [x] Unity integration layer with swappable providers
- [x] Offline mock provider, conversation history, debug + persona tools
- [x] Blender identity pipeline, logo, WebGL demo host
- [x] AI backend: fine-tuned model, persona prompts, fact retrieval, player-name memory
- [x] Fast local inference: GGUF + llama.cpp on the GPU through Vulkan
- [x] Evaluation harness on held-out lines and held-out personas
- [ ] Long-term memory beyond names, relationship state
- [ ] A larger fine-tuned model (Qwen2.5-1.5B)
- [ ] Hosted backend so the public web demo can use the real model
- [ ] Emotion → animation mapping, mood drift over time
- [ ] NPC-to-NPC conversations
- [ ] Godot and Unreal integration layers against the same schemas

## Contributing

Issues and PRs welcome. Good first contributions: new personas (validated by the
schema), a new `IDialogueProvider`, or a provider test. Keep framework code
engine-agnostic, keep secrets out of clients, and run `DemoVerify` before sending Unity changes.

## License

MIT — see [LICENSE](LICENSE). Bundled fonts keep their own licenses: Bebas Neue (SIL OFL 1.1),
DejaVu Sans Mono (Bitstream Vera/DejaVu).
