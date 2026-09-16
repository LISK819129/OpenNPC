import json

from opennpc.paths import PERSONALITIES_DIR


class Personality:
    """Holds a personality's traits and description, loaded from JSON."""

    def __init__(self, name, traits, description):
        self.name = name
        self.traits = traits
        self.description = description

    @classmethod
    def from_file(cls, filepath):
        with open(filepath, "r", encoding="utf-8") as f:
            data = json.load(f)
        return cls(
            name=data["name"],
            traits=data["traits"],
            description=data["description"],
        )

    def __repr__(self):
        return f"Personality({self.name})"


def load_all():
    """Every personality in personalities/, sorted by name. Adding a new
    personality is just dropping a JSON file in that folder."""
    return sorted(
        (Personality.from_file(p) for p in PERSONALITIES_DIR.glob("*.json")),
        key=lambda p: p.name,
    )
