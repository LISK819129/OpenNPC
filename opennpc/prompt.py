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