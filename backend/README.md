# OpenNPC backend

Game characters that talk like people with a personality. A taxi driver who's
sarcastic but helpful. A nurse who's kind but blunt. A stranger convinced you were
sent to follow them.

A game sends an NPC's **persona** (who they are, what they know, how they talk) and
the player's line. The server answers in character, **in about 0.14 seconds on a
laptop GPU**, remembers what the player told it, and runs a model fine-tuned so it
never sounds like an AI assistant.

It powers the stickman street in the [OpenNPC Unity demo](../README.md). Any engine
that can make an HTTP request can use it.

> **New to language models?** [docs/how-it-works.md](docs/how-it-works.md) explains
> every step from scratch: tokens, Qwen, prompts, memory, LoRA fine-tuning, GGUF,
> quantization, llama.cpp and Vulkan, using this project's real code and numbers.

## Results

Measured on player lines and personas that were **never in the training data**
(`training/eval_model.py`, `training/eval_personas.py`; every reply is in [`eval/`](eval/)).

| | GPT-2 (first version) | Qwen, no fine-tuning | **OpenNPC (shipped)** |
|---|---|---|---|
| Answers from the persona's own knowledge | can't read it | 90% | **100%** |
| Uses the persona's own opinion | can't read it | 30% | **100%** |
| Breaks character ("I can't assist with that") | 0%* | 37% | **0%** |
| Apologises when insulted | – | 80% | **10%** |
| Seconds per reply (median) | 0.34 | 3.04 | **0.14** |

\* GPT-2 never refused only because it didn't understand enough to; its replies rarely
followed what the player said.

Ravi, a persona the model never trained on, asked "What do you think about this city?":
*"Too many cars. Not enough patience."* That's his opinion from his persona file.

Asked "what's my name?" after "My name is Zaid": *"You're Zaid."* Asked "who are you?":
*"I'm Rahul."*

**Still weak:** some replies to insults are in character but don't make sense ("Great!
Let's make a deal."), gibberish is sometimes read as a name, and beyond names the
0.5B model can lose track of what was said a few turns earlier.

## How it works

```
 game ──POST /v1/dialogue──► server/api.py (Python)
   {persona, context,         │ 1. validate
    conversation,             │ 2. remember the player's name         opennpc/memory.py
    player_message}           │ 3. persona → system prompt,
                              │    only the facts that matter now     opennpc/persona.py
                              │ 4. + last 6 turns + memory note       opennpc/prompt.py
                              │ 5. generate ─────────────────────────► llama-server.exe (C++)
                              │                                        4-bit Qwen on the GPU
                              │ 6. clean, check name answers, log
 speech bubble ◄─────────────┘ {text, emotion, model, seconds}
```

| Piece | What it is |
|---|---|
| **Qwen2.5-0.5B-Instruct** | An open chat model (494M parameters) from Alibaba's Qwen team |
| **LoRA fine-tune (v3)** | 1.08M trained parameters (0.22%) teaching it to stay in character and use persona knowledge |
| **GGUF, Q4_K_M** | The fine-tuned model merged and stored in 4 bits: 398 MB instead of ~2 GB |
| **llama.cpp** | A C++ engine that runs the model, started by the server as `llama-server.exe` |
| **Vulkan** | How llama.cpp uses the GPU; works on older NVIDIA drivers that current CUDA builds reject |
| **Player memory** | Rules that catch "my name is…", remind the NPC, and check answers to "what's my name?" / "what's your name?" |

### Speed (same model, i5-10300H + GTX 1650 Ti)

| Engine | Reading the prompt | Writing | Typical reply |
|---|---|---|---|
| PyTorch, CPU | 222 tokens/s | 10 tokens/s | ~3.8 s |
| llama.cpp, CPU, 4-bit | 146 tokens/s | 53 tokens/s | ~1.5 s |
| **llama.cpp, GPU via Vulkan, 4-bit** | **3,498 tokens/s** | **165 tokens/s** | **~0.14 s** |

## Quick start

### 1. Install

```bash
pip install -r requirements.txt
```

### 2. Get llama.cpp

Download a Windows build from the [llama.cpp releases](https://github.com/ggml-org/llama.cpp/releases)
(tested with `b10993`) and unzip:

| Download | Unzip into | For |
|---|---|---|
| `llama-<build>-bin-win-vulkan-x64.zip` | `tools/llama-vulkan/` | GPU (recommended) |
| `llama-<build>-bin-win-cpu-x64.zip` | `tools/llama-cpu/` | CPU, and the quantize tool |

### 3. Build the model file

```bash
python training/export_gguf.py
```

This merges the fine-tuned adapter into Qwen (downloaded automatically, ~1 GB, into
`models/hf-cache/`), converts it to GGUF and quantizes it to
`models/gguf/opennpc-qwen-lora-v3-Q4_K_M.gguf`. It needs three things from the **same**
llama.cpp release's source code, copied into `tools/`: `convert_hf_to_gguf.py`, the
`conversion/` folder, and `gguf-py/gguf/` (as `tools/gguf/`).

### 4. Run

```bash
python server/api.py
```

The server starts llama.cpp on the GPU and listens on `http://localhost:8787`.

| Option | Effect |
|---|---|
| `--cpu` | Run llama.cpp on the CPU |
| `--gguf PATH` | Use another model file, e.g. a bigger Qwen |
| `--model qwen` | Run the fine-tuned model through PyTorch instead (no llama.cpp needed, ~3–4 s) |
| `--model gpt2` | The original GPT-2 version |
| `--port`, `--host`, `--allow-origin` | Networking and CORS |
| `--webgl DIR` | Also serve a Unity WebGL build from the same address |
| `--no-log` | Don't save conversations |

### 5. Try it

**With the Unity demo:** serve the demo's `web/` folder on port 8000, then open
`http://localhost:8000/?provider=opennpc&endpoint=http://localhost:8787/v1/dialogue&debug=1`,
walk up to anyone and press **E**.

**Without a game:**

```bash
python server/compare_personas.py data/personas_test "Where is the train station?" "fuck you"
```

**In the terminal:** `python main.py`

## HTTP API

| Request | Response |
|---|---|
| `POST /v1/dialogue` `{persona, context, conversation, player_message}` | `{"text", "emotion", "model", "seconds"}` |
| `POST /api/chat` `{"npc_id", "personality", "message"}` | `{"reply", "tokens_in", "tokens_out", "seconds"}` |
| `POST /api/reset` `{"npc_id"}` | `{"ok": true}` |
| `GET /api/personalities` | the four built-in personalities |
| `GET /health` | `{"ok": true, "backend", "device"}` |

`/v1/dialogue` is the OpenNPC framework contract (`framework/schemas/` in the main repo).
Send `context.event = "conversation_start"` for a greeting with no player message.
Every exchange is saved to `data/dialogue_log.jsonl` (git-ignored), so real player input
can be reviewed and turned into training data.

## Fine-tuning

```bash
# Qwen v3: stay in character inside detailed personas, and answer from persona knowledge
python training/train_qwen_lora.py --personas data/personas_train --epochs 2 --out models/opennpc-qwen-lora-v3

# Then rebuild the fast model file
python training/export_gguf.py --adapter models/opennpc-qwen-lora-v3
```

Training uses **LoRA** (the base model stays frozen; small low-rank matrices are trained
beside its attention layers) and **supervised fine-tuning with loss masking** (only the
NPC's reply tokens count toward the loss). It runs on a CPU: v3 took 56 minutes.

| Data | Size | Teaches |
|---|---|---|
| `data/conversations_v2.jsonl` | 208 replies | 52 player lines × 4 styles, each written separately: insults, threats, gibberish, typos, strange questions, attempts to break character, small talk |
| `data/personas_train/` | 7 personas | Staying in character inside detailed prompts, plus 84 questions answered from persona knowledge and opinions |
| `data/personas_test/` | 3 personas | Held out: used only for testing |
| `data/eval_lines.json` | 25 lines | Held out: used only for testing |
| `data/personalities.jsonl` | 800 rows | The original templated GPT-2 data |

The full story, including the train/play mismatch that turned v2 into v3, is in
[docs/how-it-works.md](docs/how-it-works.md#7-fine-tuning-with-lora).

## Evaluation

```bash
# 25 held-out player lines × 4 personalities = 100 replies
python training/eval_model.py --model llamacpp --out eval/llamacpp-v3.md

# detailed personas, including 3 never seen in training
python training/eval_personas.py data/personas_test --gguf models/gguf/opennpc-qwen-lora-v3-Q4_K_M.gguf --out eval/personas.md
```

`eval_model.py` refuses to run if a test line appears in any training file. The automatic
scores are signals, not a verdict: the reports list every reply so you can read them.

## Project layout

```
opennpc/            the core
  model.py            GPT-2 and PyTorch Qwen backends, reply cleaning
  llamacpp.py         the llama.cpp backend (starts and talks to llama-server)
  persona.py          persona → system prompt, relevant-fact selection
  prompt.py           chat messages: system prompt, history, memory note
  memory.py           player memory and name-question checks
server/             HTTP API, persona comparison tool
training/           LoRA fine-tuning, GGUF export, both evaluations
data/               training data, held-out lines and personas
models/             LoRA adapters (base model, GGUF files and merged models are git-ignored)
eval/               evaluation reports with every reply
docs/               how-it-works.md
tests/              API, prompt and memory tests (no model needed): python -m pytest tests
tools/              llama.cpp binaries and converter (git-ignored, see Quick start)
dashboard/          Jupyter notebook for the original GPT-2 version
main.py             terminal chat
```

## Limitations

- **A small model.** 0.5B parameters keeps replies fast, but some replies miss the
  point, and it forgets details other than names over longer conversations. A 1.5B
  model is smarter at ~0.3–0.6 s but would need its own fine-tune to stay in character.
- **Memory covers names only.** Other things a player says rely on the model reading
  the recent turns.
- **Small dataset.** 208 conversations and 7 training personas.
- **Rough automatic scores.** Word-overlap and phrase lists catch obvious failures, not
  subtle ones.
- **Not a safety system.** The prompt forbids swearing back and slurs, and the training
  data never does either, but nothing filters output. Add moderation before exposing it
  to the public.
- **Windows-first setup.** The llama.cpp instructions and paths assume Windows; the
  Python code itself is cross-platform.

## Roadmap

- [ ] Fine-tune Qwen2.5-1.5B with the v3 recipe (smarter, still sub-second on a GPU)
- [ ] Remember more than names (what the player is looking for, promises made)
- [ ] Grow the training data from real play logs
- [ ] Emotion predicted by the model instead of a fixed mapping
- [ ] Host the server so the public web demo can use the real model

## Built with

Qwen2.5-0.5B-Instruct · llama.cpp (Vulkan) · GGUF · PyTorch · Hugging Face Transformers ·
PEFT (LoRA) · Python · pytest · GPT-2 (baseline)
