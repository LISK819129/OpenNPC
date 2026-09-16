"""Fine-tune Qwen2.5-0.5B-Instruct with LoRA so NPCs stay in character.

    python training/train_qwen_lora.py
    python training/train_qwen_lora.py --personas data/personas_train --epochs 2 --out models/opennpc-qwen-lora-v3

This is supervised fine-tuning (SFT) on chat conversations. Each training row
{"personality", "user", "response"} becomes three messages:

    system     the persona for that personality (the same prompt used when playing)
    user       what the player said
    assistant  the in-character reply

The model is trained to predict ONLY the assistant's tokens. The system and user
tokens are part of the input but carry label -100, which the loss ignores: the
model should learn how to answer, not memorise the questions or its instructions.
"""

import argparse
import json
import random
import sys
from pathlib import Path

import torch
from peft import LoraConfig, get_peft_model
from transformers import AutoModelForCausalLM, AutoTokenizer, Trainer, TrainingArguments

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from opennpc.paths import DATA_DIR, HF_CACHE, PERSONALITIES_DIR, QWEN_ADAPTER_DIR, QWEN_MODEL_ID  # noqa: E402
from opennpc.persona import persona_from_personality, system_prompt  # noqa: E402
from opennpc.personality import Personality  # noqa: E402

IGNORE = -100


# Player questions for persona knowledge and opinions, worded differently from the
# questions the models are tested with ("Where is the train station?", "What do you
# think about this city?"), so the test still measures something new.
QUESTIONS = {
    "train station": ["How do I get to the train station?", "Is the station far from here?"],
    "food": ["I'm starving, any food nearby?", "Where should I grab a bite?"],
    "city": ["How do you like living here?", "Is this a good place to live?"],
    "work": ["How's work going?", "Enjoying your job these days?"],
    "weather": ["Crazy weather lately, huh?", "Do you like this weather?"],
}


def with_style(persona, style_persona):
    """A rich persona (name, background, knowledge...) speaking in a trained style."""
    return {**persona, "personalityTraits": style_persona["personalityTraits"], "mood": "",
            "speakingStyle": style_persona["speakingStyle"]}


def rich_persona_rows(rows, persona_files, style_personas, rng):
    """Teaches the model two things the short built-in personas can't:

    1. Stay in character inside a long, detailed system prompt. Every conversation
       row gets a random rich persona, speaking in that row's style.
    2. Use the persona. For each knowledge fact and opinion, a player question
       whose answer is that fact, in the persona's own words.
    """
    personas = [json.loads(f.read_text(encoding="utf-8")) for f in persona_files]
    out = [{**r, "persona": with_style(rng.choice(personas), style_personas[r["personality"]])} for r in rows]
    for p in personas:
        for item, text in [(k, k.get("fact")) for k in p.get("knowledge", [])] + \
                          [(o, o.get("text")) for o in p.get("opinions", [])]:
            topic = item.get("topic", "")
            for question in QUESTIONS.get(topic, [f"What do you know about {topic}?", f"Tell me about {topic}."]):
                if text:
                    out.append({"persona": p, "user": question, "response": text})
    return out


def encode(tokenizer, row, personas):
    persona = row.get("persona") or personas[row["personality"]]
    messages = [
        {"role": "system", "content": system_prompt(persona, {"location": "Market Street"} if "persona" in row else None)},
        {"role": "user", "content": row["user"]},
        {"role": "assistant", "content": row["response"]},
    ]
    full = tokenizer.apply_chat_template(messages, tokenize=True, return_dict=True)["input_ids"]
    prompt = tokenizer.apply_chat_template(messages[:-1], add_generation_prompt=True, tokenize=True,
                                           return_dict=True)["input_ids"]
    assert full[:len(prompt)] == prompt, "chat template changed the prompt tokens; masking would be wrong"
    labels = [IGNORE] * len(prompt) + full[len(prompt):]   # learn the reply and its end token only
    return {"input_ids": full, "labels": labels}


class PadCollator:
    """Pads a batch to its longest example. Padding is masked out of attention and loss."""

    def __init__(self, pad_id):
        self.pad_id = pad_id

    def __call__(self, batch):
        width = max(len(x["input_ids"]) for x in batch)
        ids, labels, mask = [], [], []
        for x in batch:
            gap = width - len(x["input_ids"])
            ids.append(x["input_ids"] + [self.pad_id] * gap)
            labels.append(x["labels"] + [IGNORE] * gap)
            mask.append([1] * len(x["input_ids"]) + [0] * gap)
        return {"input_ids": torch.tensor(ids), "labels": torch.tensor(labels), "attention_mask": torch.tensor(mask)}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--data", type=Path, default=DATA_DIR / "conversations_v2.jsonl")
    parser.add_argument("--out", type=Path, default=QWEN_ADAPTER_DIR.parent / "opennpc-qwen-lora-new",
                        help="never the default adapter folder, so a new run can't overwrite the best one")
    parser.add_argument("--epochs", type=float, default=3)
    parser.add_argument("--lr", type=float, default=2e-4)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--personas", type=Path,
                        help="folder of rich persona JSON files: train with detailed system prompts and "
                             "knowledge questions, as a game like the Unity demo sends")
    args = parser.parse_args()

    rng = random.Random(args.seed)
    random.seed(args.seed)
    torch.manual_seed(args.seed)
    rows = [json.loads(l) for l in args.data.read_text(encoding="utf-8").splitlines() if l.strip()]
    personas = {p.stem: persona_from_personality(Personality.from_file(p)) for p in PERSONALITIES_DIR.glob("*.json")}
    if args.personas:
        files = sorted(args.personas.glob("*.json"))
        rows = rich_persona_rows(rows, files, personas, rng)
        print(f"Rich personas from {len(files)} files: {len(rows)} rows including knowledge and opinion questions")

    tokenizer = AutoTokenizer.from_pretrained(QWEN_MODEL_ID, cache_dir=HF_CACHE)
    examples = [encode(tokenizer, r, personas) for r in rows]
    random.shuffle(examples)
    learned = sum(sum(1 for t in e["labels"] if t != IGNORE) for e in examples)
    total = sum(len(e["input_ids"]) for e in examples)
    print(f"{len(examples)} conversations, {total} tokens, of which {learned} ({learned / total:.0%}) are replies "
          "the model learns to produce")

    model = AutoModelForCausalLM.from_pretrained(QWEN_MODEL_ID, cache_dir=HF_CACHE, dtype=torch.float32)
    lora = LoraConfig(r=8, lora_alpha=16, lora_dropout=0.05, task_type="CAUSAL_LM",
                      target_modules=["q_proj", "k_proj", "v_proj", "o_proj"])
    model = get_peft_model(model, lora)
    model.print_trainable_parameters()

    training_args = TrainingArguments(
        output_dir=str(args.out),
        per_device_train_batch_size=4,
        num_train_epochs=args.epochs,
        learning_rate=args.lr,
        warmup_steps=5,
        lr_scheduler_type="cosine",
        logging_steps=5,
        save_strategy="no",
        seed=args.seed,
        remove_unused_columns=False,
        report_to=[],
    )
    trainer = Trainer(model=model, args=training_args, train_dataset=examples,
                      data_collator=PadCollator(tokenizer.pad_token_id))
    trainer.train()
    model.save_pretrained(str(args.out))
    losses = [h["loss"] for h in trainer.state.log_history if "loss" in h]
    if losses:
        print(f"Training loss {losses[0]:.2f} -> {losses[-1]:.2f}")
    print(f"LoRA adapter saved to {args.out}")


if __name__ == "__main__":
    main()
