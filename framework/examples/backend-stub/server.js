#!/usr/bin/env node
/*
 * OpenNPC backend stub — the server side of the integration boundary.
 *
 *   node framework/examples/backend-stub/server.js
 *   → POST http://localhost:8787/v1/dialogue   (framework/schemas/dialogue-request.schema.json)
 *
 * Two modes, chosen by environment variables:
 *
 *   STUB (default)   Replies "[stub] <name> heard: …". Proves the Unity ↔ backend wiring
 *                    works without pretending to be intelligent.
 *
 *   MODEL            Set OPENNPC_MODEL_URL (an OpenAI-compatible /v1/chat/completions endpoint)
 *                    and OPENNPC_MODEL. Optional OPENNPC_MODEL_KEY. Works with Ollama
 *                    (http://localhost:11434/v1/chat/completions), LM Studio, vLLM or a hosted API.
 *
 * The API key lives in this process only. The Unity/WebGL client never sees it.
 * No dependencies: Node 18+ (global fetch).
 */
"use strict";

const http = require("http");

const PORT = Number(process.env.PORT || 8787);
const MODEL_URL = process.env.OPENNPC_MODEL_URL || "";
const MODEL = process.env.OPENNPC_MODEL || "";
const MODEL_KEY = process.env.OPENNPC_MODEL_KEY || "";
const ALLOWED_ORIGIN = process.env.OPENNPC_ALLOWED_ORIGIN || "*";   // set to your site in production
const MAX_BODY = 64 * 1024;

// ------------------------------------------------------------------ prompt
function buildMessages(req) {
  const p = req.persona;
  const facts = (p.knowledge || []).map((k) => `- ${k.topic}: ${k.fact}`).join("\n");
  const opinions = (p.opinions || []).map((o) => `- ${o.topic}: ${o.text}`).join("\n");
  const system = [
    `You are ${p.name}, an NPC in a game. Stay in character. Never mention being an AI or a game.`,
    `Occupation: ${p.occupation}${p.age ? `, age ${p.age}` : ""}.`,
    `Personality traits: ${(p.personalityTraits || []).join(", ")}. Current mood: ${p.mood}.`,
    p.background ? `Background: ${p.background}` : "",
    `Speaking style: ${p.speakingStyle}`,
    facts ? `Things you know (you may be wrong about details, like a real person):\n${facts}` : "",
    opinions ? `Your opinions:\n${opinions}` : "",
    p.likes?.length ? `Likes: ${p.likes.join(", ")}.` : "",
    p.dislikes?.length ? `Dislikes: ${p.dislikes.join(", ")}.` : "",
    req.context?.location ? `You are at: ${req.context.location}.` : "",
    `Reply with 1-3 short spoken sentences. Respond ONLY with JSON: {"text": "...", "emotion": "neutral|happy|annoyed|sad|confused|excited|nervous|tired|thoughtful"}`,
  ].filter(Boolean).join("\n");

  const messages = [{ role: "system", content: system }];
  for (const turn of req.conversation || []) {
    messages.push({ role: turn.role === "player" ? "user" : "assistant", content: turn.content });
  }
  if (req.context?.event === "conversation_start") {
    messages.push({ role: "user", content: "(The player walks up to you.)" });
  } else {
    messages.push({ role: "user", content: req.player_message });
  }
  return messages;
}

async function askModel(req) {
  const res = await fetch(MODEL_URL, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(MODEL_KEY ? { Authorization: `Bearer ${MODEL_KEY}` } : {}),
    },
    body: JSON.stringify({ model: MODEL, messages: buildMessages(req), temperature: 0.8, max_tokens: 160 }),
    signal: AbortSignal.timeout(20000),
  });
  if (!res.ok) throw new Error(`model endpoint ${res.status}`);
  const data = await res.json();
  const raw = data.choices?.[0]?.message?.content?.trim() || "";
  try {
    const parsed = JSON.parse(raw.replace(/^```(json)?|```$/g, "").trim());
    if (typeof parsed.text === "string" && parsed.text) return { text: parsed.text, emotion: parsed.emotion || "neutral" };
  } catch { /* model ignored the JSON instruction: use the text as-is */ }
  return { text: raw || "…", emotion: "neutral" };
}

function stubReply(req) {
  const name = req.persona.name;
  if (req.context?.event === "conversation_start") return { text: `[stub] ${name} notices you.`, emotion: "neutral" };
  return { text: `[stub] ${name} (${req.persona.mood}) heard: "${req.player_message}"`, emotion: "neutral" };
}

// ------------------------------------------------------------------ validation
function validate(body) {
  if (!body || typeof body !== "object") return "body must be a JSON object";
  const p = body.persona;
  if (!p || typeof p !== "object") return "persona is required";
  for (const f of ["id", "name", "occupation", "mood", "speakingStyle"]) {
    if (typeof p[f] !== "string" || !p[f]) return `persona.${f} is required`;
  }
  const opening = body.context?.event === "conversation_start";
  if (!opening && (typeof body.player_message !== "string" || !body.player_message.trim())) {
    return "player_message is required (unless context.event is conversation_start)";
  }
  if (body.conversation && !Array.isArray(body.conversation)) return "conversation must be an array";
  return null;
}

// ------------------------------------------------------------------ server
function send(res, status, obj) {
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Access-Control-Allow-Origin": ALLOWED_ORIGIN,
    "Access-Control-Allow-Methods": "POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type, Accept",
  });
  res.end(JSON.stringify(obj));
}

const server = http.createServer((req, res) => {
  if (req.method === "OPTIONS") return send(res, 204, {});
  if (req.method === "GET" && req.url === "/health") return send(res, 200, { ok: true, mode: MODEL_URL ? "model" : "stub" });
  if (req.method !== "POST" || req.url !== "/v1/dialogue") return send(res, 404, { error: "POST /v1/dialogue" });

  let size = 0;
  const chunks = [];
  req.on("data", (c) => {
    size += c.length;
    if (size > MAX_BODY) { send(res, 413, { error: "request too large" }); req.destroy(); return; }
    chunks.push(c);
  });
  req.on("end", async () => {
    let body;
    try { body = JSON.parse(Buffer.concat(chunks).toString("utf8")); }
    catch { return send(res, 400, { error: "invalid JSON" }); }
    const problem = validate(body);
    if (problem) return send(res, 400, { error: problem });
    try {
      const reply = MODEL_URL ? await askModel(body) : stubReply(body);
      console.log(`${body.persona.name.padEnd(10)} ← ${JSON.stringify(body.player_message ?? body.context?.event)}  → ${JSON.stringify(reply.text)}`);
      send(res, 200, reply);
    } catch (e) {
      console.error("model error:", e.message);
      send(res, 502, { error: "model unavailable" });   // Unity shows an error line; nothing is faked
    }
  });
});

if (require.main === module) {
  server.listen(PORT, () => {
    console.log(`OpenNPC backend stub on http://localhost:${PORT}/v1/dialogue  (mode: ${MODEL_URL ? `model ${MODEL}` : "stub"})`);
  });
}

module.exports = { buildMessages, validate, stubReply, server };
