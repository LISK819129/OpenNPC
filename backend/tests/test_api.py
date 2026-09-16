"""API tests with a stand-in model, so they run in a second without torch."""

import json
import sys
import threading
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from server.api import OpenNPCService, make_handler


class EchoModel:
    device = "cpu"
    chat = False
    name = "echo"

    def __init__(self):
        self.last_stats = {}
        self.prompts = []

    def generate(self, prompt):
        self.prompts.append(prompt)
        self.last_stats = {"prompt_tokens": 10, "new_tokens": 3, "seconds": 0.01}
        return "reply to " + prompt.split("User: ")[1].split("\n")[0]


@pytest.fixture()
def api(tmp_path):
    (tmp_path / "index.html").write_text("<h1>game</h1>")
    (tmp_path / "Build").mkdir()
    (tmp_path / "Build" / "game.wasm.br").write_bytes(b"\x00fake")
    model = EchoModel()
    service = OpenNPCService(model)
    server = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(service, "echo", tmp_path, "*"))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{server.server_port}", model, service
    server.shutdown()


def call(url, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, json.loads(r.read()), r.headers
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read()), e.headers


def test_personalities_are_listed(api):
    base, _, _ = api
    status, data, headers = call(base + "/api/personalities")
    assert status == 200
    assert {p["id"] for p in data["personalities"]} >= {"rude", "friendly", "sarcastic", "paranoid"}
    assert headers["Access-Control-Allow-Origin"] == "*"


def test_chat_uses_the_npcs_personality(api):
    base, model, _ = api
    status, data, _ = call(base + "/api/chat", {"npc_id": "stickman_1", "personality": "Paranoid", "message": "hi"})
    assert status == 200
    assert data["reply"] == "reply to hi" and data["personality"] == "paranoid"
    assert model.prompts[-1] == "Personality: paranoid\nUser: hi\nNPC:"


def test_each_npc_keeps_its_own_conversation(api):
    base, _, service = api
    call(base + "/api/chat", {"npc_id": "a", "personality": "rude", "message": "one"})
    call(base + "/api/chat", {"npc_id": "b", "personality": "friendly", "message": "two"})
    call(base + "/api/chat", {"npc_id": "a", "personality": "rude", "message": "three"})
    assert len(service.npcs["a"].conversation.get_history()) == 4
    assert len(service.npcs["b"].conversation.get_history()) == 2
    assert call(base + "/api/reset", {"npc_id": "a"})[0] == 200
    assert "a" not in service.npcs and "b" in service.npcs


def test_bad_requests_get_clear_errors(api):
    base, _, _ = api
    assert call(base + "/api/chat", {"npc_id": "a", "message": "hi"})[0] == 400
    status, data, _ = call(base + "/api/chat", {"npc_id": "a", "personality": "grumpy", "message": "hi"})
    assert status == 400 and "rude" in data["error"]


def test_webgl_build_is_served_with_compression_headers(api):
    base, _, _ = api
    with urllib.request.urlopen(base + "/Build/game.wasm.br") as r:
        assert r.headers["Content-Type"] == "application/wasm"
        assert r.headers["Content-Encoding"] == "br"
    with urllib.request.urlopen(base + "/") as r:
        assert b"game" in r.read()


RAVI = {"id": "npc_001", "name": "Ravi", "occupation": "Taxi Driver", "mood": "tired",
        "personalityTraits": ["impatient", "sarcastic", "helpful"],
        "speakingStyle": "Short sentences and sarcastic remarks. Helps you anyway."}


def test_dialogue_contract_matches_the_unity_demo(api):
    base, model, _ = api
    body = {"persona": RAVI, "context": {"location": "Market Street"}, "conversation": [],
            "player_message": "What do you think about this city?"}
    status, data, headers = call(base + "/v1/dialogue", body)
    assert status == 200 and data["text"] == "reply to What do you think about this city?"
    assert data["emotion"] and data["model_personality"] == "sarcastic"
    assert headers["Access-Control-Allow-Origin"] == "*"


def test_dialogue_opening_needs_no_message(api):
    base, _, _ = api
    status, data, _ = call(base + "/v1/dialogue", {"persona": RAVI, "context": {"event": "conversation_start"}})
    assert status == 200 and data["text"]


def test_dialogue_rejects_missing_persona_or_message(api):
    base, _, _ = api
    assert call(base + "/v1/dialogue", {"player_message": "hi"})[0] == 400
    assert call(base + "/v1/dialogue", {"persona": RAVI})[0] == 400


def test_dialogue_is_saved_to_the_log(tmp_path):
    log = tmp_path / "dialogue_log.jsonl"
    service = OpenNPCService(EchoModel(), log)
    service.dialogue({"persona": RAVI, "player_message": "fuck you"})
    service.dialogue({"persona": RAVI, "context": {"event": "conversation_start"}})
    entries = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines()]
    assert [e["user"] for e in entries] == ["fuck you", "(walks up)"]
    assert entries[0]["npc"] == "Ravi" and entries[0]["personality"] == "sarcastic" and entries[0]["response"]


def test_personas_map_to_the_closest_trained_style():
    from server.api import persona_to_personality
    assert persona_to_personality({"personalityTraits": ["cheerful", "curious"], "mood": "happy"}) == "friendly"
    assert persona_to_personality({"personalityTraits": ["suspicious", "quiet"], "mood": "nervous"}) == "paranoid"
    assert persona_to_personality({"personalityTraits": ["grumpy"], "mood": "annoyed"}) == "rude"
    assert persona_to_personality({"personalityTraits": ["mysterious"], "mood": ""}) == "friendly"


class FakeChatModel:
    device = "cpu"
    chat = True
    name = "fake-chat"

    def __init__(self):
        self.last_stats = {"seconds": 0.01}
        self.messages = None

    def generate(self, messages):
        self.messages = messages
        return "Follow the signs. Or pay me."


def test_chat_model_gets_the_whole_persona_and_the_games_history():
    model = FakeChatModel()
    persona = {**RAVI, "knowledge": [{"topic": "station", "fact": "Three streets that way."}]}
    body = {"persona": persona, "context": {"location": "Market Street"},
            "conversation": [{"role": "player", "content": "hi"}, {"role": "npc", "content": "What."}],
            "player_message": "Where is the train station?"}
    reply = OpenNPCService(model).dialogue(body)
    system, *turns = model.messages
    assert system["role"] == "system"
    assert "Ravi" in system["content"] and "- station: Three streets that way." in system["content"]
    assert "Market Street" in system["content"] and "sarcastic" in system["content"]
    assert [(t["role"], t["content"]) for t in turns] == [
        ("user", "hi"), ("assistant", "What."), ("user", "Where is the train station?")]
    assert reply["text"] == "Follow the signs. Or pay me." and reply["model_personality"] == "persona"


def test_replies_are_cleaned_to_a_spoken_line():
    from opennpc.model import clean_reply
    assert clean_reply('"Oh, charming. *rolls eyes* Get lost." And more') == "Oh, charming. Get lost."
    assert clean_reply("Sure! Right away. Third sentence. Fourth") == "Sure! Right away."
    assert clean_reply("") == "..."


def test_npc_remembers_and_answers_the_name_question():
    from opennpc.memory import PlayerMemory, asks_own_name
    memory = PlayerMemory()
    memory.observe("Nice, I'm Atul..nice to meet you")
    assert memory.name == "Atul"
    # Asking is not telling: the question must not overwrite the stored name.
    memory.observe("so what was my name again?")
    assert memory.name == "Atul"
    assert asks_own_name("whats my name") and not asks_own_name("what is your name")
    # If the model answers with its own name anyway, the server corrects it.
    assert memory.correct("whats my name", "My name is Sofia.") == "You're Atul."
    assert memory.correct("whats my name", "You're Atul, of course.") == "You're Atul, of course."
    assert memory.correct("hello", "Hi there!") == "Hi there!"


def test_name_note_is_its_own_message_before_the_player_line():
    from opennpc.memory import PlayerMemory
    from opennpc.prompt import build_chat_messages
    memory = PlayerMemory().observe("I'm Atul")
    messages = build_chat_messages(RAVI, [], "whats my name", player_memory=memory)
    assert messages[-1] == {"role": "user", "content": "whats my name"}
    assert messages[-2]["role"] == "system" and "Atul" in messages[-2]["content"]


def test_npc_answers_its_own_name_question():
    from opennpc.memory import PlayerMemory, asks_npc_name, asks_own_name
    memory = PlayerMemory().observe("My name is Zaid")
    for asked in ("whats your name", "what's your name?", "I asked your good name sir", "who are you",
                  "may i know your name"):
        assert asks_npc_name(asked) and not asks_own_name(asked), asked
    # The two questions never overlap, and neither fires on ordinary chat.
    assert asks_own_name("whats my name") and not asks_npc_name("whats my name")
    assert not asks_npc_name("my name is Zaid") and not asks_npc_name("hello")
    # An NPC answering with the player's name, its job, or nothing is replaced.
    assert memory.correct("whats your name", "My name is Zaid.", "Rahul") == "I'm Rahul."
    assert memory.correct("whats your name", "I am a chef here.", "Rahul") == "I'm Rahul."
    assert memory.correct("whats your name", "Rahul. Nice to meet you.", "Rahul") == "Rahul. Nice to meet you."
