import os

from opennpc.personality import Personality
from opennpc.conversation import Conversation
from opennpc.npc import NPC
from opennpc.model import GPT2LoRABackend
from opennpc.prompt import build_prompt

PERSONALITY_FILES = {
    "1": "personalities/rude.json",
    "2": "personalities/friendly.json",
    "3": "personalities/sarcastic.json",
    "4": "personalities/paranoid.json",
}


def clear_screen():
    os.system("cls" if os.name == "nt" else "clear")


def center_text(text, width=60):
    return text.center(width)


def choose_personality():
    print(center_text("OPENNPC"))
    print()
    print(center_text("1. Rude"))
    print(center_text("2. Friendly"))
    print(center_text("3. Sarcastic"))
    print(center_text("4. Paranoid"))
    print()
    choice = input(center_text("> Choose a personality (1-4): ").strip() + " ")
    filepath = PERSONALITY_FILES.get(choice, PERSONALITY_FILES["1"])
    return Personality.from_file(filepath)


def run():
    clear_screen()
    personality = choose_personality()

    memory_path = f"data/memory_{personality.name.lower()}.json"
    conversation = Conversation.load(memory_path)

    print(center_text("Loading GPT-2 + LoRA adapter..."))
    model = GPT2LoRABackend()
    npc = NPC(personality, conversation, model, build_prompt)

    clear_screen()
    print(center_text("OPENNPC"))
    print(center_text(f"Personality: {personality.name}"))
    print()

    while True:
        user_text = input(center_text("You: ").strip() + " ")
        if user_text.lower() in ("quit", "exit"):
            conversation.save(memory_path)
            break
        reply = npc.respond(user_text)
        print(center_text(f"NPC: {reply}"))
        print()


if __name__ == "__main__":
    run()