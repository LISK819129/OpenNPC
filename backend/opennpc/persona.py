"""Personas for the chat model.

A persona is a dict in the OpenNPC framework format (framework/schemas/
persona.schema.json): name, occupation, personalityTraits, mood, speakingStyle,
background, knowledge, opinions, likes, dislikes. The chat model reads it as
its instructions, so every field can shape the reply, not just four styles.
"""

import re


def persona_from_personality(personality):
    """Turns one of the built-in personalities (personalities/*.json) into a persona."""
    return {
        "name": "a local",
        "occupation": "townsperson",
        "personalityTraits": [personality.name.lower()],
        "mood": "",
        "speakingStyle": personality.description,
    }


def relevant(items, message, limit=2):
    """The persona entries worth putting in the prompt for this message.

    A persona can carry a dozen facts and opinions. Sending all of them makes the
    prompt long (slower on a CPU) and buries the one that answers the question, so
    entries are matched on their topic and keywords, and only the best few are
    sent. With no message (a greeting), the first couple go in."""
    if not items:
        return []
    if not message:
        return items[:limit]
    words = set(re.findall(r"[a-z']+", message.lower()))
    scored = []
    for item in items:
        keys = set(re.findall(r"[a-z']+", " ".join([item.get("topic", "")] + item.get("keywords", [])).lower()))
        scored.append((len(words & keys), item))
    best = [item for score, item in sorted(scored, key=lambda x: -x[0]) if score]
    return best[:limit] if best else items[:1]


def system_prompt(persona, context=None, player_memory=None, message=None):
    p = persona
    lines = [
        f"You are {p.get('name', 'a character')}, a character in a video game. You are talking to the player "
        "on the street. Stay in character at all times.",
    ]
    who = ", ".join(x for x in [p.get("occupation"), f"age {p['age']}" if p.get("age") else ""] if x)
    if who:
        lines.append(f"You are: {who}.")
    if p.get("personalityTraits"):
        lines.append(f"Your personality: {', '.join(p['personalityTraits'])}.")
    if p.get("mood"):
        lines.append(f"Your mood right now: {p['mood']}.")
    if p.get("speakingStyle"):
        lines.append(f"How you talk: {p['speakingStyle']}")
    if p.get("background"):
        lines.append(f"Background: {p['background']}")
    # Each fact keeps its topic: "Three streets that way." means nothing unless
    # the model knows it answers "where is the train station".
    facts = [f"- {k.get('topic', 'fact')}: {k['fact']}" for k in relevant(p.get("knowledge", []), message)
             if k.get("fact")]
    if facts:
        lines.append("Things you know:\n" + "\n".join(facts))
    opinions = [f"- {o.get('topic', 'opinion')}: {o['text']}" for o in relevant(p.get("opinions", []), message)
                if o.get("text")]
    if opinions:
        lines.append("Your opinions:\n" + "\n".join(opinions))
    if p.get("likes"):
        lines.append(f"You like: {', '.join(p['likes'])}.")
    if p.get("dislikes"):
        lines.append(f"You dislike: {', '.join(p['dislikes'])}.")
    if context and context.get("location"):
        lines.append(f"You are at: {context['location']}.")
    lines.append(
        "Rules: reply with one or two short spoken sentences, as this character. Never say you are an AI, a "
        "model or a game character. When the player says \"my name is...\" or \"I'm...\", that is the "
        "player's name, never yours. If the player is rude, insults you, types gibberish or says something "
        "strange, react the way this character would. Never swear back and never use slurs."
    )
    return "\n".join(lines)
