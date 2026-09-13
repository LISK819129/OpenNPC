import json
import os


class Conversation:
    """Stores the back-and-forth history of the current chat session,
    and can save/load that history to a JSON file for basic persistent
    memory across runs."""

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
            json.dump(self.history, f, indent=2)

    @classmethod
    def load(cls, filepath):
        conv = cls()
        if os.path.exists(filepath):
            with open(filepath, "r", encoding="utf-8") as f:
                conv.history = json.load(f)
        return conv