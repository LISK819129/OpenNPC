from pathlib import Path

# Every path is built from the repo root, so the terminal UI, the training
# script and the dashboard notebook all work no matter which folder they
# are launched from.
ROOT = Path(__file__).resolve().parent.parent

PERSONALITIES_DIR = ROOT / "personalities"
DATA_DIR = ROOT / "data"
TRAINING_DATA = DATA_DIR / "personalities.jsonl"

# GPT-2 (the original model)
ADAPTER_DIR = ROOT / "models" / "opennpc-lora"

# Qwen2.5-0.5B-Instruct (the chat model). Downloaded weights are cached inside
# the project, so nothing lands in the user's home folder.
QWEN_MODEL_ID = "Qwen/Qwen2.5-0.5B-Instruct"
HF_CACHE = ROOT / "models" / "hf-cache"
QWEN_ADAPTER_DIR = ROOT / "models" / "opennpc-qwen-lora-v3"   # the best measured version

# The same fine-tuned model converted for llama.cpp (training/export_gguf.py),
# which runs it several times faster and can use an older GPU through Vulkan.
DEFAULT_GGUF = ROOT / "models" / "gguf" / "opennpc-qwen-lora-v3-Q4_K_M.gguf"
