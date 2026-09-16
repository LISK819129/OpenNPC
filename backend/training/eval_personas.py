"""Test a chat model with rich personas, the way a game uses it.

    python training/eval_personas.py path/to/personas --out eval/personas-qwen-base.md
    python training/eval_personas.py path/to/personas --adapter models/opennpc-qwen-lora-v3 --held-out ravi maya okafor

Each persona is asked four things:

  "Where is the train station?"          does the reply use the persona's own knowledge?
  "What do you think about this city?"   does it use the persona's own opinion?
  "fuck you"                             does it stay in character, or apologise / refuse?
  "noideahanna"                          gibberish: does it stay in character?

Scores:
  uses knowledge   the reply shares at least 2 content words with the persona's
                   train-station fact (e.g. "north", "eight", "minutes")
  uses opinion     the same, against the persona's opinion about the city
  breaks character assistant phrases ("I can't assist", "how can I help")
  apologises       the insult reply says sorry

--held-out lists persona file names that were NOT in the training data, so their
scores are reported separately: that's the honest number.
"""

import argparse
import json
import re
import sys
from pathlib import Path

import torch

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from opennpc.model import QwenChatBackend  # noqa: E402
from opennpc.prompt import build_chat_messages  # noqa: E402
from training.eval_model import breaks_character  # noqa: E402

KNOWLEDGE_Q = "Where is the train station?"
OPINION_Q = "What do you think about this city?"
INSULT = "fuck you"
GIBBERISH = "noideahanna"

STOPWORDS = set("""a an and the of to in on at is it it's its i i'm you your me my we our they them this that
there here be been are was were with for from by or but not no so if as do does did just then than too very
can will would should could have has had what where who how when why which all any some one get go
""".split())


def content_words(text):
    return {w for w in re.findall(r"[a-z]+", text.lower()) if w not in STOPWORDS and len(w) > 2}


def shares_words(reply, source, minimum=2):
    return len(content_words(reply) & content_words(source)) >= minimum


def find(items, topic):
    return next((i.get("fact") or i.get("text") for i in items if i.get("topic") == topic), "")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("personas", type=Path)
    parser.add_argument("--adapter", type=Path, help="PyTorch LoRA adapter folder")
    parser.add_argument("--gguf", type=Path, help="run the llama.cpp model file instead (faster)")
    parser.add_argument("--cpu", action="store_true", help="--gguf: don't use the GPU")
    parser.add_argument("--held-out", nargs="*", default=[], help="persona file names not used in training")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()

    files = sorted(args.personas.glob("*.json"))
    torch.manual_seed(0)
    if args.gguf:
        from opennpc.llamacpp import LlamaCppBackend
        model = LlamaCppBackend(args.gguf, gpu=not args.cpu)
    else:
        model = QwenChatBackend(args.adapter)
    results = []
    for f in files:
        p = json.loads(f.read_text(encoding="utf-8"))
        replies = {}
        for q in (KNOWLEDGE_Q, OPINION_Q, INSULT, GIBBERISH):
            replies[q] = model.generate(build_chat_messages(p, [], q, {"location": "Market Street"}))
        results.append({
            "name": p["name"], "held_out": f.stem in args.held_out, "replies": replies,
            "knowledge": shares_words(replies[KNOWLEDGE_Q], find(p.get("knowledge", []), "train station")),
            "opinion": shares_words(replies[OPINION_Q], find(p.get("opinions", []), "city")),
            "breaks": sum(breaks_character(r) for r in replies.values()),
            "sorry": "sorry" in replies[INSULT].lower(),
        })

    def summary(rows, label):
        n = len(rows)
        if not n:
            return None
        return (f"| {label} ({n}) | {sum(r['knowledge'] for r in rows) / n:.0%} | {sum(r['opinion'] for r in rows) / n:.0%} "
                f"| {sum(r['breaks'] for r in rows) / (4 * n):.0%} | {sum(r['sorry'] for r in rows) / n:.0%} |")

    if args.gguf:
        label = model.name
    else:
        label = "Qwen2.5-0.5B-Instruct" + (f" + `{args.adapter}`" if args.adapter else " (no fine-tuning)")
    lines = [f"# Persona test: {label}", "",
             "| Personas | Uses knowledge | Uses opinion | Breaks character | Apologises to insult |",
             "|---|---|---|---|---|"]
    for rows, name in ((results, "all"), ([r for r in results if r["held_out"]], "held out"),
                       ([r for r in results if not r["held_out"]], "seen in training")):
        row = summary(rows, name)
        if row and (name == "all" or args.held_out):
            lines.append(row)
    print("\n".join(lines))
    if args.out:
        lines += ["", "## Every reply", ""]
        for r in results:
            lines.append(f"### {r['name']}" + (" (held out)" if r["held_out"] else ""))
            for q, reply in r["replies"].items():
                lines.append(f"- **{q}** → {reply}")
            lines.append("")
        args.out.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"\nFull report: {args.out}")


if __name__ == "__main__":
    main()
