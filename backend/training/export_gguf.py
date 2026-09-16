"""Turn a fine-tuned model into a quantized GGUF file for llama.cpp.

    python training/export_gguf.py                       # the default adapter -> Q4_K_M
    python training/export_gguf.py --adapter models/opennpc-qwen-lora-v2 --quant Q5_K_M

Three steps:

1. **Merge.** LoRA keeps its learned matrices separate from the base model.
   merge_and_unload() folds them into the weights, giving one ordinary model.
2. **Convert.** llama.cpp uses its own file format, GGUF: the weights plus the
   tokenizer and settings in a single file it can memory-map.
3. **Quantize.** Weights are stored in 4 bits instead of 32 (Q4_K_M). The file
   drops from about 1 GB to 400 MB and generation gets several times faster on a
   CPU, because speed here is limited by how much memory is read per token.

Needs the llama.cpp tools in tools/llama-cpu (see docs/how-it-works.md).
"""

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

import torch

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from opennpc.paths import HF_CACHE, QWEN_ADAPTER_DIR, QWEN_MODEL_ID, ROOT  # noqa: E402

TOOLS = ROOT / "tools"   # llama.cpp binaries, convert_hf_to_gguf.py, conversion/ and gguf/


def merge(adapter: Path, out: Path):
    from peft import PeftModel
    from transformers import AutoModelForCausalLM, AutoTokenizer

    print(f"1/3 merging {adapter.name} into {QWEN_MODEL_ID}...")
    model = AutoModelForCausalLM.from_pretrained(QWEN_MODEL_ID, cache_dir=HF_CACHE, dtype=torch.float16)
    model = PeftModel.from_pretrained(model, str(adapter)).merge_and_unload()
    model.save_pretrained(out)
    AutoTokenizer.from_pretrained(QWEN_MODEL_ID, cache_dir=HF_CACHE).save_pretrained(out)
    # save_pretrained writes only the fast tokenizer.json; the GGUF converter also
    # wants the raw BPE files, or it guesses a different tokenizer format and fails.
    snapshot = next(iter((HF_CACHE / f"models--{QWEN_MODEL_ID.replace('/', '--')}" / "snapshots").glob("*")), None)
    for name in ("vocab.json", "merges.txt"):
        if snapshot and (snapshot / name).exists() and not (out / name).exists():
            shutil.copy(snapshot / name, out / name)
    print(f"    merged model -> {out}")


def convert(merged: Path, gguf: Path):
    script = TOOLS / "convert_hf_to_gguf.py"
    if not script.exists():
        sys.exit(f"{script} not found. See docs/how-it-works.md for the two files to copy from the "
                 "llama.cpp release you unzipped into tools/.")
    print("2/3 converting to GGUF (16-bit)...")
    # The converter and the gguf library must come from the same llama.cpp release,
    # so tools/ goes first on the import path instead of any pip-installed gguf.
    env = {**os.environ, "PYTHONPATH": str(TOOLS)}
    subprocess.run([sys.executable, str(script), str(merged), "--outfile", str(gguf), "--outtype", "f16"],
                   check=True, env=env)


def quantize(source: Path, target: Path, quant: str):
    exe = TOOLS / "llama-cpu" / "llama-quantize.exe"
    if not exe.exists():
        sys.exit(f"{exe} not found. Unzip the llama.cpp release into tools/llama-cpu first.")
    print(f"3/3 quantizing to {quant}...")
    subprocess.run([str(exe), str(source), str(target), quant], check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--adapter", type=Path, default=QWEN_ADAPTER_DIR)
    parser.add_argument("--quant", default="Q4_K_M", help="Q4_K_M (default), Q5_K_M, Q8_0...")
    parser.add_argument("--out", type=Path, help="final .gguf path (default: models/gguf/<adapter>-<quant>.gguf)")
    parser.add_argument("--keep-intermediate", action="store_true")
    args = parser.parse_args()

    gguf_dir = ROOT / "models" / "gguf"
    gguf_dir.mkdir(parents=True, exist_ok=True)
    merged = ROOT / "models" / f"{args.adapter.name}-merged"
    f16 = gguf_dir / f"{args.adapter.name}-f16.gguf"
    out = args.out or gguf_dir / f"{args.adapter.name}-{args.quant}.gguf"

    merge(args.adapter, merged)
    convert(merged, f16)
    quantize(f16, out, args.quant)

    if not args.keep_intermediate:
        shutil.rmtree(merged, ignore_errors=True)
        f16.unlink(missing_ok=True)
    print(f"\nDone: {out}  ({out.stat().st_size / 1e6:.0f} MB)")
    print(f"Run it with:  python server/api.py --model llamacpp --gguf {out}")


if __name__ == "__main__":
    main()
