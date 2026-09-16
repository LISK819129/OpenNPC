import os
import re
import time
from pathlib import Path

import torch
from transformers import GPT2LMHeadModel, GPT2Tokenizer

from opennpc.paths import ADAPTER_DIR, HF_CACHE, QWEN_MODEL_ID


class ModelBackend:
    """Shared shape so backends are swappable.

    chat = False: generate(prompt: str), a plain text-completion model (GPT-2).
    chat = True:  generate(messages: list of {"role", "content"}), a chat model (Qwen).
    """

    chat = False
    name = "model"

    def generate(self, prompt):
        raise NotImplementedError


class GPT2Backend(ModelBackend):
    name = "gpt2"

    def __init__(self, model_name="gpt2"):
        device = "cuda" if torch.cuda.is_available() else "cpu"
        self.device = device
        self.tokenizer = GPT2Tokenizer.from_pretrained(model_name)
        self.model = GPT2LMHeadModel.from_pretrained(model_name).to(device)
        self.model.eval()
        # Filled in by every generate() call, so a UI can show what really
        # happened: token counts and how long generation took.
        self.last_stats = {}

    def generate(self, prompt, max_new_tokens=60):
        input_ids = self.tokenizer.encode(prompt, return_tensors="pt").to(self.device)

        started = time.perf_counter()
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
        seconds = time.perf_counter() - started

        # Decode only the newly generated tokens. Cutting the decoded string
        # at len(prompt) breaks whenever decoding doesn't reproduce the
        # prompt character-for-character.
        new_ids = output_ids[0][input_ids.shape[1]:]
        reply = self.tokenizer.decode(new_ids, skip_special_tokens=True).strip()

        self.last_stats = {
            "device": self.device,
            "prompt_tokens": int(input_ids.shape[1]),
            "new_tokens": int(new_ids.shape[0]),
            "seconds": seconds,
        }

        reply = reply.split("\n")[0].strip()
        return reply if reply else "..."


class GPT2LoRABackend(GPT2Backend):
    """Loads a fine-tuned LoRA adapter on top of the base GPT2Backend.
    Only usable after training/train_lora.py has produced an adapter
    at the given path (default: models/opennpc-lora)."""

    def __init__(self, adapter_path=ADAPTER_DIR):
        adapter_path = Path(adapter_path)
        # Without this check a missing folder makes PEFT try to download
        # "models/opennpc-lora" from the Hugging Face Hub and fail confusingly.
        if not (adapter_path / "adapter_config.json").exists():
            raise FileNotFoundError(
                f"No LoRA adapter at {adapter_path}. Run `python training/train_lora.py` first."
            )
        super().__init__()
        from peft import PeftModel
        self.model = PeftModel.from_pretrained(self.model, str(adapter_path))
        self.model.eval()


class QwenChatBackend(ModelBackend):
    """Qwen2.5-0.5B-Instruct: a small chat model that follows instructions, so the
    whole persona goes into the system prompt. An optional LoRA adapter
    (training/train_qwen_lora.py) fine-tunes its speaking style on top."""

    chat = True

    def __init__(self, adapter_path=None, model_id=QWEN_MODEL_ID, quantize=False, max_new_tokens=36):
        from transformers import AutoModelForCausalLM, AutoTokenizer

        # One thread per physical core: more threads make small models slower, not faster.
        torch.set_num_threads(max(1, (os.cpu_count() or 4) // 2))
        self.max_new_tokens = max_new_tokens

        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.tokenizer = AutoTokenizer.from_pretrained(model_id, cache_dir=HF_CACHE)
        self.model = AutoModelForCausalLM.from_pretrained(
            model_id, cache_dir=HF_CACHE, dtype=torch.float32 if self.device == "cpu" else torch.float16
        ).to(self.device)
        self.name = "qwen2.5-0.5b"
        if adapter_path:
            adapter_path = Path(adapter_path)
            if not (adapter_path / "adapter_config.json").exists():
                raise FileNotFoundError(
                    f"No LoRA adapter at {adapter_path}. Run `python training/train_qwen_lora.py` first."
                )
            from peft import PeftModel
            # merge_and_unload folds the adapter into the weights: same answers,
            # no extra matrix multiplications per token.
            self.model = PeftModel.from_pretrained(self.model, str(adapter_path)).merge_and_unload()
            self.name += "+lora"
        if quantize:
            # int8 weights for every Linear layer: less memory to read per token,
            # which is what makes generation slow on a CPU.
            self.model = torch.ao.quantization.quantize_dynamic(self.model, {torch.nn.Linear}, dtype=torch.qint8)
            self.name += "+int8"
        self.model.eval()
        self.last_stats = {}

    def generate(self, messages, max_new_tokens=None):
        input_ids = self.tokenizer.apply_chat_template(
            messages, add_generation_prompt=True, return_tensors="pt", return_dict=True
        )["input_ids"].to(self.device)
        started = time.perf_counter()
        with torch.no_grad():
            output_ids = self.model.generate(
                input_ids,
                attention_mask=torch.ones_like(input_ids),
                max_new_tokens=max_new_tokens or self.max_new_tokens,
                do_sample=True,
                temperature=0.7,
                top_p=0.9,
                repetition_penalty=1.1,
                pad_token_id=self.tokenizer.eos_token_id,
                # An NPC line is one line: stop as soon as the model starts a new
                # paragraph instead of generating tokens that get thrown away.
                stop_strings=["\n"],
                tokenizer=self.tokenizer,
            )
        seconds = time.perf_counter() - started
        new_ids = output_ids[0][input_ids.shape[1]:]
        reply = self.tokenizer.decode(new_ids, skip_special_tokens=True)
        self.last_stats = {"device": self.device, "prompt_tokens": int(input_ids.shape[1]),
                           "new_tokens": int(new_ids.shape[0]), "seconds": seconds}
        return clean_reply(reply)


def clean_reply(text, max_sentences=2):
    """One spoken line: first paragraph, no wrapping quotes or stage directions,
    at most two sentences (a cut-off sentence at the token limit is dropped)."""
    text = text.strip().split("\n")[0].strip().strip('"').strip()
    text = re.sub(r"\*[^*]*\*|\([^)]*\)", "", text).strip()   # *sighs*, (rolls eyes)
    sentences = re.findall(r"[^.!?]+[.!?]+|[^.!?]+$", text)
    complete = [s.strip() for s in sentences if s.strip() and s.strip()[-1] in ".!?"]
    kept = (complete or [s.strip() for s in sentences])[:max_sentences]
    return " ".join(kept).strip() or "..."
