"""Validate every persona in framework/examples/personas against the schema.

    python scripts/validate_personas.py

Uses the `jsonschema` package when installed; otherwise falls back to a
minimal built-in check of required fields and types so the script still works
on a fresh machine.
"""
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
SCHEMA = ROOT / "framework" / "schemas" / "persona.schema.json"
PERSONAS = ROOT / "framework" / "examples" / "personas"


def minimal_check(schema, persona):
    errors = []
    for key in schema["required"]:
        if key not in persona:
            errors.append(f"missing required field '{key}'")
    for key in persona:
        if key not in schema["properties"]:
            errors.append(f"unknown field '{key}'")
    for key in ("personalityTraits", "likes", "dislikes", "goals", "knowledge", "opinions"):
        if key in persona and not isinstance(persona[key], list):
            errors.append(f"'{key}' must be a list")
    return errors


def main():
    schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
    try:
        import jsonschema
        validator = jsonschema.Draft202012Validator(schema)
        check = lambda p: [e.message for e in validator.iter_errors(p)]
        mode = "jsonschema"
    except ImportError:
        check = lambda p: minimal_check(schema, p)
        mode = "minimal"

    ids, failed = set(), 0
    files = sorted(PERSONAS.glob("*.json"))
    for path in files:
        persona = json.loads(path.read_text(encoding="utf-8"))
        errors = check(persona)
        if persona.get("id") in ids:
            errors.append(f"duplicate id '{persona.get('id')}'")
        ids.add(persona.get("id"))
        status = "ok" if not errors else "FAIL"
        print(f"  [{status}] {path.name:<16} {persona.get('name', '?')}")
        for e in errors:
            print(f"         - {e}")
        failed += bool(errors)

    print(f"{len(files)} personas, {failed} failed ({mode} validation)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
