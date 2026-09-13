class NPC:
    def __init__(self, personality, conversation, model_backend, prompt_builder):
        self.personality = personality
        self.conversation = conversation
        self.model = model_backend
        self.build_prompt = prompt_builder

    def respond(self, user_text):
        self.conversation.add_user_message(user_text)
        prompt = self.build_prompt(self.personality, self.conversation)
        reply = self.model.generate(prompt)
        self.conversation.add_npc_message(reply)
        return reply