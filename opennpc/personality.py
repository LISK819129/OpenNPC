import json

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