# OpenNPC framework

Engine-agnostic pieces. Everything a game engine and a dialogue backend need to agree on.

| Path | What |
|---|---|
| `../schemas/persona.schema.json` | a persona: identity, personality, voice, knowledge, opinions, goals |
| `../schemas/dialogue-request.schema.json` | engine → provider: persona, context, conversation, player_message |
| `../schemas/dialogue-response.schema.json` | provider → engine: text, emotion, optional mood / end_conversation |
| `../examples/personas/` | ten authored personas used by the demo |
| `../examples/backend-stub/` | a runnable server implementing the contract (stub or real model) |

Validate personas: `python scripts/validate_personas.py` (uses `jsonschema` if installed).

## Design rules

1. **Personas are data.** Behaviour differences come from persona fields, never from NPC-specific code.
2. **Providers are replaceable.** The engine sends the full persona; each provider uses what it can.
3. **Short-term memory travels with the request; long-term memory lives behind the provider.**
4. **Secrets never live in clients.** Game builds talk to a backend you control.
5. **Never fake answers.** On failure, return an explicit error — the demo shows it as such.

See [`../../docs/architecture.md`](../../docs/architecture.md) for the full picture.
