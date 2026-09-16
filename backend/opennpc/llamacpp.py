"""Run the model through llama.cpp instead of PyTorch.

llama.cpp is a C++ engine for running language models on ordinary hardware. It
reads GGUF files (see training/export_gguf.py), keeps weights in 4 bits, and has
a Vulkan backend that runs on almost any GPU, including older ones that current
CUDA builds of PyTorch no longer support.

Measured here (Qwen2.5-0.5B, Q4_K_M, i5-10300H + GTX 1650 Ti):

    PyTorch CPU        222 tokens/s reading, 10 tokens/s writing
    llama.cpp CPU      146                   53
    llama.cpp Vulkan  3498                  165

This starts `llama-server.exe` as a child process and talks to it over HTTP on
localhost, which keeps the C++ engine out of the Python process and needs no
compiler or Python bindings.
"""

import atexit
import json
import socket
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path

from opennpc.model import ModelBackend, clean_reply
from opennpc.paths import ROOT

TOOLS = ROOT / "tools"


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def pick_build(prefer_gpu=True):
    """The Vulkan build if it's here and a GPU is usable, else the CPU build."""
    vulkan, cpu = TOOLS / "llama-vulkan" / "llama-server.exe", TOOLS / "llama-cpu" / "llama-server.exe"
    if prefer_gpu and vulkan.exists():
        return vulkan, True
    if cpu.exists():
        return cpu, False
    raise FileNotFoundError(
        f"No llama.cpp server in {TOOLS}. Download a llama.cpp release for Windows and unzip it into "
        "tools/llama-cpu (and tools/llama-vulkan for GPU). See docs/how-it-works.md.")


class LlamaCppBackend(ModelBackend):
    chat = True

    def __init__(self, gguf_path, gpu=True, context=4096, threads=4, max_new_tokens=48, port=None):
        gguf_path = Path(gguf_path)
        if not gguf_path.exists():
            raise FileNotFoundError(f"No model file at {gguf_path}. Run `python training/export_gguf.py` first.")
        server, self.gpu = pick_build(gpu)
        self.port = port or free_port()
        self.url = f"http://127.0.0.1:{self.port}"
        self.max_new_tokens = max_new_tokens
        self.last_stats = {}
        self.device = "gpu (vulkan)" if self.gpu else "cpu"
        self.name = f"{gguf_path.stem} (llama.cpp {'vulkan' if self.gpu else 'cpu'})"

        command = [str(server), "-m", str(gguf_path), "--host", "127.0.0.1", "--port", str(self.port),
                   "-c", str(context), "-t", str(threads), "--jinja", "--no-webui"]
        if self.gpu:
            command += ["-ngl", "99"]   # every layer on the GPU; a 0.5B model needs ~0.4 GB
        self.log = open(ROOT / "models" / "llama-server.log", "w", encoding="utf-8", errors="replace")
        self.process = subprocess.Popen(command, stdout=self.log, stderr=subprocess.STDOUT)
        atexit.register(self.close)
        self._wait_until_ready()

    def _wait_until_ready(self, timeout=120):
        deadline = time.time() + timeout
        while time.time() < deadline:
            if self.process.poll() is not None:
                raise RuntimeError(f"llama-server stopped while starting. See models/llama-server.log")
            try:
                with urllib.request.urlopen(f"{self.url}/health", timeout=2) as r:
                    if json.loads(r.read()).get("status") == "ok":
                        return
            except (urllib.error.URLError, OSError, ValueError):
                time.sleep(0.5)
        raise TimeoutError("llama-server did not become ready. See models/llama-server.log")

    def generate(self, messages, max_new_tokens=None):
        body = json.dumps({
            "messages": messages,
            "temperature": 0.7,
            "top_p": 0.9,
            "repeat_penalty": 1.1,
            "max_tokens": max_new_tokens or self.max_new_tokens,
            "stop": ["\n"],          # an NPC line is one line
        }).encode()
        request = urllib.request.Request(f"{self.url}/v1/chat/completions", body,
                                         {"Content-Type": "application/json"})
        started = time.perf_counter()
        with urllib.request.urlopen(request, timeout=120) as response:
            data = json.loads(response.read())
        seconds = time.perf_counter() - started
        usage = data.get("usage", {})
        self.last_stats = {"device": self.device, "prompt_tokens": usage.get("prompt_tokens"),
                           "new_tokens": usage.get("completion_tokens"), "seconds": seconds}
        return clean_reply(data["choices"][0]["message"]["content"])

    def close(self):
        process = getattr(self, "process", None)
        if process and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
        log = getattr(self, "log", None)
        if log and not log.closed:
            log.close()
