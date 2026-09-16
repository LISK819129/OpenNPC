from opennpc.persona import system_prompt


def build_chat_messages(persona, history, player_message, context=None, max_turns=6, player_memory=None):
    """Messages for a chat model: the persona as the system prompt, then recent
    turns, then the player's new line.

    history: turns as {"role": "user"|"player"|"npc", "text"|"content": ...},
    oldest first, NOT including player_message.
    """
    messages = [{"role": "system", "content": system_prompt(persona, context, None, player_message)}]
    for turn in history[-max_turns:]:
        text = turn.get("text", turn.get("content", ""))
        role = "assistant" if turn.get("role") == "npc" else "user"
        if text:
            messages.append({"role": role, "content": text})
    # Facts about the player go in their own note just before the player speaks.
    # Buried in a long system prompt, a small model reads straight past them.
    reminders = player_memory.lines(persona.get("name", "you"), player_message) if player_memory else []
    if reminders:
        messages.append({"role": "system", "content": " ".join(reminders)})
    messages.append({"role": "user", "content": player_message})
    return messages


def build_prompt(personality, conversation):
    """Builds a prompt matching the exact format used in training
    (training/train_lora.py): 'Personality: X\\nUser: Y\\nNPC:'
    Keeping this format identical to training is what lets the LoRA
    adapter's learned patterns actually apply at inference time.
    """
    history = conversation.get_history()

    # Find the most recent user message to respond to
    last_user_text = ""
    for turn in reversed(history):
        if turn["role"] == "user":
            last_user_text = turn["text"]
            break

    prompt = (
        f"Personality: {personality.name.lower()}\n"
        f"User: {last_user_text}\n"
        f"NPC:"
    )
    return prompt