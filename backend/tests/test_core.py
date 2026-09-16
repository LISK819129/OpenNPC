"""Fast checks that need no model download: they catch broken personality
files, prompt drift away from the training format, and memory save/load."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from opennpc.conversation import MAX_SAVED_TURNS, Conversation
from opennpc.paths import PERSONALITIES_DIR, TRAINING_DATA
from opennpc.personality import Personality, load_all
from opennpc.prompt import build_prompt


def test_every_personality_file_loads():
    files = list(PERSONALITIES_DIR.glob("*.json"))
    assert files
    for path in files:
        p = Personality.from_file(path)
        assert p.name and p.description and p.traits


def test_every_personality_has_training_data():
    trained = {json.loads(line)["personality"] for line in TRAINING_DATA.open(encoding="utf-8")}
    for p in load_all():
        assert p.name.lower() in trained, f"{p.name} has no training examples"


def test_prompt_matches_training_format():
    conv = Conversation()
    conv.add_user_message("hello")
    conv.add_npc_message("go away")
    conv.add_user_message("where is the market?")
    prompt = build_prompt(Personality("Rude", {}, ""), conv)
    # Must be byte-identical to the format built in training/train_lora.py.
    assert prompt == "Personality: rude\nUser: where is the market?\nNPC:"


def test_conversation_save_and_load(tmp_path):
    conv = Conversation()
    conv.add_user_message("hi")
    conv.add_npc_message("hello")
    path = tmp_path / "memory" / "friendly.json"
    conv.save(str(path))
    assert Conversation.load(str(path)).get_history() == conv.get_history()


def test_saved_memory_is_capped(tmp_path):
    conv = Conversation()
    for i in range(MAX_SAVED_TURNS + 50):
        conv.add_user_message(str(i))
    path = tmp_path / "m.json"
    conv.save(str(path))
    loaded = Conversation.load(str(path)).get_history()
    assert len(loaded) == MAX_SAVED_TURNS
    assert loaded[-1]["text"] == str(MAX_SAVED_TURNS + 49)


def test_missing_memory_file_gives_empty_conversation(tmp_path):
    assert Conversation.load(str(tmp_path / "nope.json")).get_history() == []
