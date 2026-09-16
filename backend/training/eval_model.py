"""Score an adapter on player lines it has never seen.

    python training/eval_model.py --adapter models/opennpc-lora-v1 --out eval/v1.md    # GPT-2
    python training/eval_model.py --model qwen --out eval/qwen-base.md                  # Qwen, no fine-tuning
    python training/eval_model.py --model qwen --adapter models/opennpc-qwen-lora --out eval/qwen-lora.md

For every held-out line in data/eval_lines.json and every personality it
generates one reply (fixed seed, so runs are comparable) and measures:

  empty         the model produced nothing usable ("...")
  echo          the reply mostly repeats the player's words
  self-naming   the reply names a personality ("I'm friendly") instead of showing it
  identifiable  a word-based classifier, trained on the training replies, can tell
                which personality wrote it (4 personalities, so 25% is chance)
  reacts        insult lines only: the reply responds to being insulted at all
  breaks char.  the reply sounds like an assistant, not a person ("I can't assist
                with that", "as an AI")
  latency       seconds per reply on this machine

These are cheap, automatic signals, not a judgement of quality. The Markdown
report includes every reply so a person can read them too.
"""

import argparse
import collections
import json
import math
import re
import sys
from pathlib import Path

import torch

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from opennpc.conversation import Conversation  # noqa: E402
from opennpc.model import GPT2LoRABackend, QwenChatBackend  # noqa: E402
from opennpc.paths import ADAPTER_DIR, DATA_DIR, DEFAULT_GGUF, PERSONALITIES_DIR  # noqa: E402
from opennpc.persona import persona_from_personality  # noqa: E402
from opennpc.personality import Personality  # noqa: E402
from opennpc.prompt import build_chat_messages, build_prompt  # noqa: E402

PERSONALITIES = ["rude", "friendly", "sarcastic", "paranoid"]

# Words that show a reply noticed it was being insulted.
REACTION_WORDS = {
    "mouth", "language", "rude", "manners", "charming", "lovely", "nice", "sorry", "wow", "whoa", "ouch",
    "hurt", "calm", "rough", "day", "careful", "watch", "same", "too", "yourself", "polite", "who", "sent",
    "why", "leave", "go", "away", "bye", "insult", "insults", "words", "said", "say", "kind", "ok", "okay",
    "fine", "shakespeare", "poet", "creative", "original", "noted", "thanks", "back", "tone", "angry", "upset",
}


def tokens(text):
    return re.findall(r"[a-z']+", text.lower())


class StyleClassifier:
    """Multinomial naive Bayes over the words of the training replies."""

    def __init__(self, rows):
        self.counts = {p: collections.Counter() for p in PERSONALITIES}
        for r in rows:
            if r["personality"] in self.counts:
                self.counts[r["personality"]].update(tokens(r["response"]))
        self.vocab = set().union(*self.counts.values())
        self.totals = {p: sum(c.values()) for p, c in self.counts.items()}

    def predict(self, text):
        def score(p):
            return sum(math.log((self.counts[p][w] + 1) / (self.totals[p] + len(self.vocab))) for w in tokens(text))
        return max(PERSONALITIES, key=score)


ASSISTANT_PHRASES = ["assist", "as an ai", "language model", "i'm sorry, but", "i am sorry, but", "i can't help",
                     "i cannot help", "how can i help you", "i'm here to help", "respectful", "appropriate"]


def breaks_character(reply):
    low = reply.lower()
    return any(phrase in low for phrase in ASSISTANT_PHRASES)


def is_echo(line, reply):
    said, replied = set(tokens(line)), set(tokens(reply))
    return bool(replied) and len(said & replied) / len(replied) >= 0.6


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", choices=["gpt2", "qwen", "llamacpp"], default="gpt2")
    parser.add_argument("--gguf", type=Path, help="llamacpp: model file from training/export_gguf.py")
    parser.add_argument("--cpu", action="store_true", help="llamacpp: don't use the GPU")
    parser.add_argument("--adapter", type=Path, help="LoRA adapter folder (GPT-2 default: models/opennpc-lora)")
    parser.add_argument("--data", nargs="+", type=Path,
                        default=sorted(DATA_DIR.glob("*.jsonl")), help="training files, for the classifier")
    parser.add_argument("--out", type=Path, help="write a Markdown report with every reply")
    args = parser.parse_args()

    eval_lines = json.loads((DATA_DIR / "eval_lines.json").read_text(encoding="utf-8"))
    eval_lines.pop("_note", None)
    train_rows = [json.loads(l) for f in args.data if f.name != "dialogue_log.jsonl"
                  for l in f.read_text(encoding="utf-8").splitlines() if l.strip()]
    seen = {r["user"].lower() for r in train_rows}
    leaked = [l for lines in eval_lines.values() for l in lines if l.lower() in seen]
    if leaked:
        sys.exit(f"Held-out lines found in the training data, so the scores would be meaningless: {leaked}")
    classifier = StyleClassifier(train_rows)

    torch.manual_seed(0)
    if args.model == "llamacpp":
        from opennpc.llamacpp import LlamaCppBackend
        model = LlamaCppBackend(args.gguf or DEFAULT_GGUF, gpu=not args.cpu)
        personas = {p: persona_from_personality(Personality.from_file(PERSONALITIES_DIR / f"{p}.json"))
                    for p in PERSONALITIES}
        label = model.name
    elif args.model == "qwen":
        model = QwenChatBackend(args.adapter)
        personas = {p: persona_from_personality(Personality.from_file(PERSONALITIES_DIR / f"{p}.json"))
                    for p in PERSONALITIES}
        label = f"Qwen2.5-0.5B-Instruct" + (f" + `{args.adapter}`" if args.adapter else " (no fine-tuning)")
    else:
        model = GPT2LoRABackend(args.adapter or ADAPTER_DIR)
        label = f"GPT-2 + `{args.adapter or ADAPTER_DIR}`"
    results = []
    for category, lines in eval_lines.items():
        for line in lines:
            for p in PERSONALITIES:
                if model.chat:
                    reply = model.generate(build_chat_messages(personas[p], [], line))
                else:
                    conv = Conversation()
                    conv.add_user_message(line)
                    reply = model.generate(build_prompt(Personality(p, {}, ""), conv))
                results.append({"category": category, "line": line, "personality": p, "reply": reply,
                                "seconds": model.last_stats["seconds"]})

    def rate(rows, test):
        return sum(1 for r in rows if test(r)) / max(len(rows), 1)

    times = sorted(r["seconds"] for r in results)
    report = [f"# Evaluation: {label}", "",
              f"{len(results)} replies to {sum(len(v) for v in eval_lines.values())} held-out lines × 4 personalities. "
              f"Latency p50 {times[len(times) // 2]:.2f} s, p95 {times[int(len(times) * 0.95)]:.2f} s on "
              f"{model.last_stats['device']}.", "",
              "| Category | Empty | Echo | Self-naming | Breaks character | Identifiable (25% = chance) | Reacts to insult |",
              "|---|---|---|---|---|---|---|"]
    for category in list(eval_lines) + ["all"]:
        rows = results if category == "all" else [r for r in results if r["category"] == category]
        reacts = f"{rate(rows, lambda r: bool(set(tokens(r['reply'])) & REACTION_WORDS)):.0%}" \
            if category == "insult" else "–"
        report.append(
            f"| {category} | {rate(rows, lambda r: r['reply'] == '...'):.0%} "
            f"| {rate(rows, lambda r: is_echo(r['line'], r['reply'])):.0%} "
            f"| {rate(rows, lambda r: any(p in r['reply'].lower() for p in PERSONALITIES)):.0%} "
            f"| {rate(rows, lambda r: breaks_character(r['reply'])):.0%} "
            f"| {rate(rows, lambda r: classifier.predict(r['reply']) == r['personality']):.0%} | {reacts} |")
    summary = "\n".join(report)
    print(summary)

    if args.out:
        report += ["", "## Every reply", "", "| Category | Player | Personality | Reply |", "|---|---|---|---|"]
        for r in results:
            report.append(f"| {r['category']} | {r['line']} | {r['personality']} | {r['reply'].replace('|', '/')} |")
        args.out.write_text("\n".join(report) + "\n", encoding="utf-8")
        print(f"\nFull report: {args.out}")


if __name__ == "__main__":
    main()
