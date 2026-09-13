import torch
from transformers import GPT2LMHeadModel, GPT2Tokenizer


class ModelBackend:
    """Shared shape so backends are swappable later (GPT2Backend, GPT2LoRABackend, etc.)"""

    def generate(self, prompt):
        raise NotImplementedError


class GPT2Backend(ModelBackend):
    def __init__(self, model_name="gpt2"):
        device = "cuda" if torch.cuda.is_available() else "cpu"
        self.device = device
        self.tokenizer = GPT2Tokenizer.from_pretrained(model_name)
        self.model = GPT2LMHeadModel.from_pretrained(model_name).to(device)
        self.model.eval()

    def generate(self, prompt, max_new_tokens=60):
        input_ids = self.tokenizer.encode(prompt, return_tensors="pt").to(self.device)

        with torch.no_grad():
            output_ids = self.model.generate(
                input_ids,
                max_new_tokens=max_new_tokens,
                do_sample=True,
                temperature=0.8,
                top_k=50,
                top_p=0.9,
                pad_token_id=self.tokenizer.eos_token_id,
            )

        full_text = self.tokenizer.decode(output_ids[0], skip_special_tokens=True)
        new_text = full_text[len(prompt):]
        reply = new_text.strip()

        if not reply:
            return "..."

        reply = reply.split("\n")[0].strip()
        return reply if reply else "..."


class GPT2LoRABackend(GPT2Backend):
    """Loads a fine-tuned LoRA adapter on top of the base GPT2Backend.
    Only usable after training/train_lora.py has produced an adapter
    at the given path (default: models/opennpc-lora)."""

    def __init__(self, adapter_path="models/opennpc-lora"):
        super().__init__()
        from peft import PeftModel
        self.model = PeftModel.from_pretrained(self.model, adapter_path)