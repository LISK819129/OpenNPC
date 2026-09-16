"""Fine-tune a LoRA adapter on GPT-2 so it replies in the trained personalities.

    python training/train_lora.py
    python training/train_lora.py --data data/conversations_v2.jsonl data/personalities.jsonl:200 \\
                                  --out models/opennpc-lora-v2 --epochs 8

--data takes one or more JSONL files of {"personality", "user", "response"} rows.
FILE:N takes a random N rows from that file (seeded), which keeps a large,
repetitive file from drowning out a smaller, better one.
"""

import argparse
import json
import random
import sys
from pathlib import Path

import torch
from datasets import Dataset
from transformers import GPT2LMHeadModel, GPT2Tokenizer, TrainingArguments, Trainer
from peft import LoraConfig, get_peft_model

# Allow `python training/train_lora.py` from any folder.
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from opennpc.paths import ADAPTER_DIR, TRAINING_DATA  # noqa: E402

BLOCK_SIZE = 64  # length of each training chunk, in tokens


def load_rows(spec, rng):
    """'data/x.jsonl' -> every row; 'data/x.jsonl:200' -> 200 random rows."""
    path, _, limit = str(spec).partition(":") if not Path(str(spec)).exists() else (str(spec), "", "")
    rows = [json.loads(line) for line in Path(path).read_text(encoding="utf-8").splitlines() if line.strip()]
    if limit:
        rows = rng.sample(rows, min(int(limit), len(rows)))
    print(f"  {path}: {len(rows)} rows")
    return rows


def build_corpus(rows):
    """Joins every example into ONE long string, each example separated by
    GPT-2's end-of-text token. This matters: every chunk cut later is 100% real
    text, with no padding tokens to confuse the model (that was the bug behind
    an earlier training run)."""
    examples = [
        f"Personality: {row['personality']}\nUser: {row['user']}\nNPC: {row['response']}"
        for row in rows
    ]
    return "<|endoftext|>".join(examples) + "<|endoftext|>"


def chunk_into_blocks(token_ids, block_size):
    """Cuts one long list of token ids into equal-length blocks.
    Any leftover tokens shorter than a full block are dropped —
    with a small dataset this loses very little."""
    total_blocks = len(token_ids) // block_size
    return [token_ids[i * block_size:(i + 1) * block_size] for i in range(total_blocks)]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--data", nargs="+", default=[str(TRAINING_DATA)], help="JSONL files, optionally FILE:N")
    parser.add_argument("--out", type=Path, default=ADAPTER_DIR)
    parser.add_argument("--epochs", type=float, default=3)
    parser.add_argument("--seed", type=int, default=0)
    args = parser.parse_args()

    rng = random.Random(args.seed)
    torch.manual_seed(args.seed)
    print("Training data:")
    rows = [row for spec in args.data for row in load_rows(spec, rng)]
    # Shuffle so every 64-token block mixes personalities and sources instead of
    # holding 200 rude lines in a row.
    rng.shuffle(rows)

    tokenizer = GPT2Tokenizer.from_pretrained("gpt2")
    tokenizer.pad_token = tokenizer.eos_token
    base_model = GPT2LMHeadModel.from_pretrained("gpt2")

    lora_config = LoraConfig(
        r=8,
        lora_alpha=16,
        target_modules=["c_attn"],
        lora_dropout=0.1,
        task_type="CAUSAL_LM",
    )
    model = get_peft_model(base_model, lora_config)

    token_ids = tokenizer.encode(build_corpus(rows))
    blocks = chunk_into_blocks(token_ids, BLOCK_SIZE)
    print(f"{len(rows)} examples, {len(token_ids)} tokens -> {len(blocks)} training blocks of {BLOCK_SIZE} tokens")

    # Every block is fully real text, so input_ids and labels are
    # identical, and there's no padding to mask out.
    dataset = Dataset.from_dict({
        "input_ids": blocks,
        "labels": [b.copy() for b in blocks],
        "attention_mask": [[1] * BLOCK_SIZE for _ in blocks],
    })

    training_args = TrainingArguments(
        output_dir=str(args.out),
        per_device_train_batch_size=4,
        num_train_epochs=args.epochs,
        logging_steps=5,
        save_strategy="no",
        learning_rate=3e-4,
        seed=args.seed,
        fp16=torch.cuda.is_available(),
        report_to=[],
    )

    trainer = Trainer(model=model, args=training_args, train_dataset=dataset)
    trainer.train()

    model.save_pretrained(str(args.out))
    losses = [h["loss"] for h in trainer.state.log_history if "loss" in h]
    if losses:
        print(f"Training loss {losses[0]:.2f} -> {losses[-1]:.2f}")
    print(f"LoRA adapter saved to {args.out}")


if __name__ == "__main__":
    main()
