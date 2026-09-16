from opennpc.memory import PlayerMemory
from opennpc.persona import persona_from_personality
from opennpc.prompt import build_chat_messages


class NPC:
    def __init__(self, personality, conversation, model_backend, prompt_builder, persona=None):
        self.personality = personality
        self.conversation = conversation
        self.model = model_backend
        self.build_prompt = prompt_builder
        # A chat model reads a full persona; the built-in personalities get a simple one.
        self.persona = persona or persona_from_personality(personality)
        self.memory = PlayerMemory()

    def respond(self, user_text):
        if self.model.chat:
            # Chat models see the persona and the recent conversation.
            history = list(self.conversation.get_history())
            self.conversation.add_user_message(user_text)
            self.memory.observe(user_text)
            reply = self.memory.correct(user_text, self.model.generate(
                build_chat_messages(self.persona, history, user_text, player_memory=self.memory)),
                self.persona.get("name"))
        else:
            # GPT-2 was trained on single lines in a fixed text format.
            self.conversation.add_user_message(user_text)
            reply = self.model.generate(self.build_prompt(self.personality, self.conversation))
        self.conversation.add_npc_message(reply)
        return reply
