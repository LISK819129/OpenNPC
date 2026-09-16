"""What an NPC remembers about the player.

A small model can't reliably pick facts out of a transcript. Told "I'm Atul" and
asked "what's my name?", it answers with its own name. So the server extracts the
few facts that matter with plain rules, states them in the NPC's instructions, and
checks the answer to the one question models get backwards.

Memory is per NPC: telling one character your name doesn't tell the whole street.
"""

import re

# "I'm hungry" is not a name. Words that follow "I'm" far more often than a name does.
NOT_NAMES = {
    "a", "an", "the", "here", "there", "new", "lost", "sorry", "fine", "good", "great", "ok", "okay",
    "hungry", "thirsty", "tired", "late", "back", "busy", "ready", "done", "sure", "hurt", "sick", "cold",
    "hot", "happy", "sad", "angry", "scared", "bored", "curious", "just", "not", "so", "very", "really",
    "looking", "going", "coming", "trying", "asking", "wondering", "from", "with", "about", "your", "my",
    "you", "me", "it", "that", "this", "still", "also", "too", "only", "now", "today", "leaving", "staying",
}

NAME_PATTERNS = [
    re.compile(r"\bmy name'?s?\s+(?:is\s+)?([A-Za-z][A-Za-z'-]{1,19})", re.I),
    re.compile(r"\bcall me\s+([A-Za-z][A-Za-z'-]{1,19})", re.I),
    re.compile(r"\bi'?m\s+([A-Za-z][A-Za-z'-]{1,19})\b", re.I),
    re.compile(r"\bi am\s+([A-Za-z][A-Za-z'-]{1,19})\b", re.I),
    re.compile(r"\bthis is\s+([A-Z][A-Za-z'-]{1,19})\b"),
]

# "So what was my name again?" is the player ASKING, not telling.
ASKING = re.compile(r"(what|what's|whats|who|whos|who's|remind|forgot|forget|remember|again|tell me)", re.I)

# The one question small models reliably get backwards: they answer with their own
# name. It's detected so the NPC can be told exactly what is being asked, and the
# answer checked afterwards.
ASKS_OWN_NAME = re.compile(
    r"\b(?:"
    r"what(?:'s|s| is| was| were)?\s+(?:my|mah)\s+names?"      # what's my name / whats my name
    r"|my\s+name\s+again"                                      # so what was my name again?
    r"|(?:remember|recall|know|forgot|forget)\s+my\s+names?"    # do you remember my name?
    r"|who\s+am\s+i"
    r"|say\s+my\s+name"
    r")", re.I)

# "What's your name?" is the mirror question, and small models answer it with the
# player's name, their job, or nothing. It gets the same treatment.
ASKS_NPC_NAME = re.compile(
    r"\b(?:"
    r"what(?:'s|s| is| was| were)?\s+(?:your|ur|his|her|thy)\s+(?:good\s+)?names?"
    r"|who\s+(?:are|r)\s+(?:you|u)\b"
    r"|(?:tell|give)\s+me\s+your\s+names?"
    r"|may\s+i\s+know\s+your\s+name"
    r"|your\s+(?:good\s+)?name\s*\??$"
    r"|i\s+asked\s+your\s+(?:good\s+)?name"
    r")", re.I)


def asks_npc_name(message):
    return bool(ASKS_NPC_NAME.search((message or "").strip()))


def find_player_name(message):
    """The player's name if they just gave it, else None."""
    for pattern in NAME_PATTERNS:
        match = pattern.search(message)
        if match:
            if ASKING.search(message[:match.start()]) or message.rstrip().endswith("?"):
                continue
            name = match.group(1)
            if name.lower() in NOT_NAMES or len(name) < 2:
                continue
            # "i'm atul" counts, but only when the rest of the sentence doesn't
            # turn it into something else ("I'm going", "I'm from Delhi").
            return name[0].upper() + name[1:]
    return None


def asks_own_name(message):
    return bool(ASKS_OWN_NAME.search(message or ""))


class PlayerMemory:
    """Facts one NPC has been told about the player."""

    def __init__(self):
        self.name = None

    def observe(self, message):
        name = find_player_name(message or "")
        if name:
            self.name = name
        return self

    def lines(self, npc_name="you", message=""):
        """The note put in front of the player's line (see opennpc/prompt.py)."""
        if asks_npc_name(message):
            # Without this an NPC answers with its job, or with the player's name.
            return [f"The player is asking what YOUR name is. Your name is {npc_name}. "
                    f"Say your own name, {npc_name}, in your own words."]
        if not self.name:
            return []
        if asks_own_name(message):
            # Spell out what is being asked. A general note isn't enough here.
            return [f"The player is asking you to say THEIR name back to them. Their name is {self.name}. "
                    f"Reply in character and include the word {self.name}. Do not give your own name."]
        # No "the answer is <name>" here: models repeated it as their own name.
        return [f"Note: you are talking to the player, whose name is {self.name}. "
                f"You are still {npc_name}. Use their name if it fits naturally."]

    def correct(self, message, reply, npc_name=None):
        """Last resort for the two questions small models get backwards. If the reply
        doesn't contain the right name, answer the question instead of letting the
        NPC say something wrong."""
        def says(name):
            return bool(name) and bool(re.search(re.escape(name), reply or "", re.I))

        if npc_name and asks_npc_name(message) and not says(npc_name):
            return f"I'm {npc_name}."
        if self.name and asks_own_name(message) and not says(self.name):
            return f"You're {self.name}."
        return reply
