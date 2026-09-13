import json
import torch
from datasets import Dataset
from transformers import GPT2LMHeadModel, GPT2Tokenizer, TrainingArguments, Trainer
from peft import LoraConfig, get_peft_model

BLOCK_SIZE = 64  # length of each training chunk, in tokens


def build_corpus(path):
    """Reads personalities.jsonl and joins every example into ONE long
    string, each example separated by GPT-2's end-of-text token. This
    matters: it means every chunk we later cut is 100% real text, with
    no padding tokens to accidentally confuse the model (that was the
    bug behind the last training run)."""
    lines = []
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            row = json.loads(line)
            example = (
                f"Personality: {row['personality']}\n"
                f"User: {row['user']}\n"
                f"NPC: {row['response']}"
            )
            lines.append(example)
    return "<|endoftext|>".join(lines) + "<|endoftext|>"


def chunk_into_blocks(token_ids, block_size):
    """Cuts one long list of token ids into equal-length blocks.
    Any leftover tokens shorter than a full block are dropped —
    with a small dataset this loses very little."""
    total_blocks = len(token_ids) // block_size
    blocks = []
    for i in range(total_blocks):
        start = i * block_size
        blocks.append(token_ids[start:start + block_size])
    return blocks


def main():
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

    corpus = build_corpus("data/personalities.jsonl")
    token_ids = tokenizer.encode(corpus)
    blocks = chunk_into_blocks(token_ids, BLOCK_SIZE)

    print(f"Corpus tokens: {len(token_ids)} -> {len(blocks)} training blocks of {BLOCK_SIZE} tokens")

    # Every block is fully real text, so input_ids and labels are
    # identical, and there's no padding to mask out.
    dataset = Dataset.from_dict({
        "input_ids": blocks,
        "labels": [b.copy() for b in blocks],
        "attention_mask": [[1] * BLOCK_SIZE for _ in blocks],
    })

    args = TrainingArguments(
        output_dir="models/opennpc-lora",
        per_device_train_batch_size=4,
        num_train_epochs=3,
        logging_steps=2,
        save_strategy="epoch",
        learning_rate=3e-4,
        fp16=torch.cuda.is_available(),
    )

    trainer = Trainer(model=model, args=args, train_dataset=dataset)
    trainer.train()

    model.save_pretrained("models/opennpc-lora")
    print("LoRA adapter saved to models/opennpc-lora")


if __name__ == "__main__":
    main()