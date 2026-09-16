import argparse

from ui.terminal_ui import run

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Chat with an OpenNPC character in the terminal.")
    parser.add_argument("--model", choices=["gpt2", "qwen"], default="qwen",
                        help="gpt2: fast, four speaking styles. qwen: follows the persona, slower")
    parser.add_argument("--no-adapter", action="store_true", help="use the base model without fine-tuning")
    args = parser.parse_args()
    run(args.model, use_adapter=not args.no_adapter)
