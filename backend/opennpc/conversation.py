import json
import os

# The saved transcript keeps only the most recent turns, so the file cannot
# grow forever across sessions.
MAX_SAVED_TURNS = 200


class Conversation:
    """Stores the back-and-forth history of the current chat session,
    and can save/load that history to a JSON file so a transcript carries
    over between runs.

    Note: the current GPT-2 adapter was trained on single-turn examples,
    so the prompt only uses the latest user message. The history is kept
    for the transcript and for a future multi-turn model."""

    def __init__(self):
        self.history = []  # list of {"role": "user"/"npc", "text": str}

    def add_user_message(self, text):
        self.history.append({"role": "user", "text": text})

    def add_npc_message(self, text):
        self.history.append({"role": "npc", "text": text})

    def get_history(self):
        return self.history

    def save(self, filepath):
        os.makedirs(os.path.dirname(filepath), exist_ok=True)
        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(self.history[-MAX_SAVED_TURNS:], f, indent=2)

    @classmethod
    def load(cls, filepath):
        conv = cls()
        if os.path.exists(filepath):
            with open(filepath, "r", encoding="utf-8") as f:
                conv.history = json.load(f)
        return conv
