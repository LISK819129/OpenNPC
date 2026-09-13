# OpenNPC

## What this is

OpenNPC is a small, open-source framework for building AI-driven NPCs (non-player characters) that talk with a personality. You pick a personality — Rude, Friendly, Sarcastic, or Paranoid — and chat with it in a terminal or a live Jupyter dashboard. The NPC's replies come from GPT-2, fine-tuned with a technique called LoRA so it leans into each personality's tone.

## Why this was made

This is a learning project. It was built step by step to understand, hands-on, how an AI-powered NPC framework actually works under the hood — how personality data, conversation memory, prompt building, and a language model fit together, and how fine-tuning (LoRA) changes a model's behavior. It's not a polished product — it's a way of learning AI/ML and software engineering by building something real, one small piece at a time.

## This is a basic version

Please keep expectations realistic:
- The model is GPT-2 (124M parameters) — small and dated by today's standards.
- Replies can be short, repetitive, or occasionally not make sense.
- It can connect to what you say on a surface level (echoing back the topic), but it can't reason deeply about complex or novel remarks — that needs a much bigger model.
- There's no real long-term memory, no voice, no graphics — just text.

This is intentional. The goal was to understand each piece, not to build something feature-complete.

## How to run it

1. Open a terminal in the project folder.
2. Create and activate a virtual environment:
   ```
   python -m venv .venv
   .venv\Scripts\activate
   ```
3. Install the requirements:
   ```
   pip install -r requirements.txt
   ```
4. Run it:
   ```
   python main.py
   ```
5. Pick a personality (1–4) and start chatting. Type `quit` or `exit` to leave.

First run downloads GPT-2's model files once (about 500MB) — after that it's cached and loads normally.

## Optional: live dashboard

There's also a Jupyter notebook (`dashboard/OpenNPC_Live_Dashboard.ipynb`) that shows the same chat in a 4-panel live view — a network diagram, conversation history, a live process log, and a terminal-style output. Open it with `jupyter notebook` and run each cell top to bottom.
