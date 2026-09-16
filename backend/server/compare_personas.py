"""Ask every persona the same questions and print their answers side by side.

    python server/compare_personas.py path/to/personas "What do you think about this city?"
    python server/compare_personas.py path/to/personas "Hi" "Where is the train station?" --server http://localhost:8787

Sends real requests to a running OpenNPC server (python server/api.py), exactly as
the Unity demo does, so this shows what players would see without walking to
every NPC in the game.
"""

import argparse
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path


def ask(server, persona, message):
    body = json.dumps({"persona": persona, "context": {"location": "Market Street"}, "conversation": [],
                       "player_message": message}).encode("utf-8")
    req = urllib.request.Request(server.rstrip("/") + "/v1/dialogue", data=body,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("personas", type=Path, help="folder of persona .json files, or one file")
    parser.add_argument("questions", nargs="+")
    parser.add_argument("--server", default="http://localhost:8787")
    args = parser.parse_args()

    files = sorted(args.personas.glob("*.json")) if args.personas.is_dir() else [args.personas]
    personas = [json.loads(f.read_text(encoding="utf-8")) for f in files]
    if not personas:
        sys.exit(f"No persona files in {args.personas}")

    for question in args.questions:
        print(f'\nPlayer: "{question}"\n')
        for p in personas:
            try:
                reply = ask(args.server, p, question)
            except urllib.error.URLError as e:
                sys.exit(f"Can't reach {args.server} ({e.reason}). Start it with: python server/api.py")
            traits = ", ".join(p.get("personalityTraits", []))
            print(f"  {p['name']:<12} {'(' + traits + ')':<42} [{reply.get('model_personality', '?'):<9}] "
                  f"{reply['text']}")


if __name__ == "__main__":
    main()
