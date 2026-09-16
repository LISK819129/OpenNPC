import os

from opennpc.personality import load_all
from opennpc.conversation import Conversation
from opennpc.npc import NPC
from opennpc.model import GPT2Backend, GPT2LoRABackend, QwenChatBackend
from opennpc.paths import DATA_DIR, QWEN_ADAPTER_DIR
from opennpc.prompt import build_prompt


def load_model(model_name, use_adapter):
    if model_name == "qwen":
        adapter = QWEN_ADAPTER_DIR if use_adapter and QWEN_ADAPTER_DIR.exists() else None
        print(center_text("Loading Qwen2.5-0.5B" + (" + LoRA adapter..." if adapter else "...")))
        return QwenChatBackend(adapter)
    print(center_text("Loading GPT-2" + (" + LoRA adapter..." if use_adapter else "...")))
    return GPT2LoRABackend() if use_adapter else GPT2Backend()


def clear_screen():
    os.system("cls" if os.name == "nt" else "clear")


def center_text(text, width=60):
    return text.center(width)


def choose_personality():
    personalities = load_all()
    print(center_text("OPENNPC"))
    print()
    for i, p in enumerate(personalities, start=1):
        print(center_text(f"{i}. {p.name}"))
    print()
    while True:
        choice = input(center_text(f"> Choose a personality (1-{len(personalities)}): ").rstrip() + " ").strip()
        if choice.isdigit() and 1 <= int(choice) <= len(personalities):
            return personalities[int(choice) - 1]
        print(center_text("Please type one of the numbers above."))


def run(model_name="gpt2", use_adapter=True):
    clear_screen()
    personality = choose_personality()

    memory_path = DATA_DIR / f"memory_{personality.name.lower()}.json"
    conversation = Conversation.load(memory_path)

    model = load_model(model_name, use_adapter)
    npc = NPC(personality, conversation, model, build_prompt)

    clear_screen()
    print(center_text("OPENNPC"))
    print(center_text(f"Personality: {personality.name}"))
    print(center_text("(type quit to exit)"))
    print()

    try:
        while True:
            user_text = input(center_text("You: ").rstrip() + " ").strip()
            if not user_text:
                continue
            if user_text.lower() in ("quit", "exit"):
                break
            reply = npc.respond(user_text)
            print(center_text(f"NPC: {reply}"))
            print()
    except (KeyboardInterrupt, EOFError):
        print()
    finally:
        # Saved on quit, Ctrl+C or a closed input stream alike.
        conversation.save(memory_path)


if __name__ == "__main__":
    run()
