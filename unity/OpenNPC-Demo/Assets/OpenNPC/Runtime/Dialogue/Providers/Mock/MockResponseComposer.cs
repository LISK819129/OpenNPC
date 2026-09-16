using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenNPC.Personas;

namespace OpenNPC.Dialogue.Mock
{
    /// <summary>
    /// Turns (persona, intent, history) into a line. Content comes from the
    /// persona's own fields; delivery (openers, closers, terseness, formality)
    /// comes from persona.voice. Nothing here is specific to any one NPC.
    /// </summary>
    public sealed class MockResponseComposer
    {
        private static readonly Regex IntroduceRegex =
            new Regex(@"\b(?:my name is|call me|i am called|name's)\s+([a-z][a-z'\-]{1,20})", RegexOptions.IgnoreCase);

        public DialogueResponse Compose(DialogueRequest request)
        {
            Persona p = request.Persona;
            var rng = new System.Random(Seed(p.id, request.PlayerMessage, request.Conversation.Count));
            var voice = new Voice(p.voice, rng);

            if (request.IsOpening)
                return new DialogueResponse(voice.Wrap(Greeting(p, voice), openerChance: 1f, closerChance: 0f),
                                            EmotionFor(p, Intent.Greeting));

            IntentMatch match = IntentClassifier.Classify(request.PlayerMessage, p);
            string content = Content(p, match, request, voice, out bool endConversation);
            bool isFallback = content == null;
            if (isFallback)
                content = voice.Pick(p.voice.fallbacks) ?? "Hm. Not sure what to say to that.";

            bool repeated = !isFallback && IsRepeatable(match.Intent) && AlreadySaid(request.Conversation, content);
            string line = repeated
                ? voice.RepeatPrefix() + " " + content
                : voice.Wrap(content);

            Intent emotionIntent = isFallback ? Intent.Unknown : match.Intent;
            return new DialogueResponse(line, EmotionFor(p, emotionIntent), endConversation: endConversation);
        }

        // ------------------------------------------------------------------ //
        private static string Content(Persona p, IntentMatch match, DialogueRequest request, Voice v, out bool end)
        {
            end = false;
            switch (match.Intent)
            {
                case Intent.Knowledge:
                case Intent.Opinion:
                    return match.TopicText;

                case Intent.Greeting:
                    return Greeting(p, v);

                case Intent.Farewell:
                    end = true;
                    return v.ByFormality("Later.", "Goodbye.", "Good day to you.");

                case Intent.Thanks:
                    return v.ByFormality("Yeah, sure.", "You're welcome.", "Not at all.");

                case Intent.Name:
                    return v.Terse
                        ? p.name + "."
                        : v.Chatty ? $"I'm {p.name}! What's yours?" : v.ByFormality($"I'm {p.name}.", $"I'm {p.name}.", $"My name is {p.name}.");

                case Intent.Occupation:
                {
                    string job = v.Terse ? p.occupation + "." : $"I'm {Article(p.occupation)} {p.occupation.ToLowerInvariant()}.";
                    string opinion = FindOpinion(p, "work");
                    return opinion != null ? job + " " + opinion : job;
                }

                case Intent.Age:
                    if (p.age <= 0)
                        return null;
                    return v.Terse ? $"{p.age}. Why?" : v.Chatty ? $"{p.age}! Can you believe it?"
                        : v.ByFormality($"I'm {p.age}.", $"I'm {p.age}.", $"I am {p.age} years old.");

                case Intent.Mood:
                {
                    string mood = string.IsNullOrEmpty(p.mood) ? "fine" : p.mood;
                    string line = v.Terse ? Capitalize(mood) + "."
                        : v.Chatty ? $"So {mood}! Honestly!"
                        : v.ByFormality($"Honestly? {Capitalize(mood)}.", $"I'm feeling {mood}.", $"I am rather {mood}, thank you for asking.");
                    return v.Terse || string.IsNullOrEmpty(p.background) ? line : line + " " + FirstSentence(p.background);
                }

                case Intent.Background:
                    return string.IsNullOrEmpty(p.background) ? null : p.background;

                case Intent.Likes:
                    if (p.likes.Count == 0) return null;
                    return v.Terse ? Capitalize(JoinList(p.likes)) + "."
                        : v.Chatty ? $"Ooh, {JoinList(p.likes)}! Obviously!"
                        : v.ByFormality($"I like {JoinList(p.likes)}.", $"I like {JoinList(p.likes)}.", $"I enjoy {JoinList(p.likes)}.");

                case Intent.Dislikes:
                    if (p.dislikes.Count == 0) return null;
                    return v.Terse ? Capitalize(JoinList(p.dislikes)) + "."
                        : v.Chatty ? $"Ugh, {JoinList(p.dislikes)}! Don't get me started!"
                        : v.ByFormality($"Can't stand {JoinList(p.dislikes)}.", $"I don't like {JoinList(p.dislikes)}.", $"I dislike {JoinList(p.dislikes)}.");

                case Intent.Goals:
                    if (p.goals.Count == 0) return null;
                    return v.ByFormality("Honestly? ", "My goal? ", "If you must know: ") + p.goals[0];

                case Intent.IntroducePlayer:
                {
                    string n = match.Value;
                    string reply = v.Terse ? $"{n}. Got it."
                        : v.Chatty ? $"{n}! Love that name!"
                        : v.ByFormality($"Nice to meet you, {n}.", $"Nice to meet you, {n}.", $"A pleasure, {n}.");
                    return MentionedOwnName(request.Conversation, p.name) ? reply : reply + $" I'm {p.name}.";
                }

                case Intent.RecallPlayer:
                {
                    string known = PlayerName(request.Conversation);
                    if (known == null)
                        return v.ByFormality("You never told me.", "I don't think you told me.", "I don't believe you've introduced yourself.");
                    return v.Terse ? $"{known}." : v.ByFormality($"You're {known}.", $"You're {known}, right?", $"You are {known}, if I recall correctly.");
                }

                default:
                    return null;
            }
        }

        private static string Greeting(Persona p, Voice v)
        {
            if (v.Terse) return v.ByFormality("Yeah?", "Yes?", "Can I help you?");
            if (v.Chatty) return v.ByFormality("Hi! What's up?", "Hello there! Lovely to meet you!", "Well, good day to you! How can I help?");
            return v.ByFormality("Hey. What's up?", "Hello. Can I help?", "Good day. How may I help you?");
        }

        private static string EmotionFor(Persona p, Intent intent)
        {
            if (intent == Intent.Unknown) return "confused";
            if (intent == Intent.Thanks || intent == Intent.IntroducePlayer)
                return Any(p, "grumpy", "impatient") ? "neutral" : "happy";
            string mood = (p.mood ?? string.Empty).ToLowerInvariant();
            if (Any(p, "impatient", "sarcastic", "grumpy", "suspicious", "stern")) return "annoyed";
            if (Any(p, "anxious", "nervous") || mood.Contains("lost")) return "nervous";
            if (mood.Contains("tired") || mood.Contains("exhausted") || mood.Contains("sleepy")) return "tired";
            if (Any(p, "cheerful", "optimistic", "warm", "energetic", "hyperactive") || mood.Contains("excited")) return "happy";
            if (Any(p, "cryptic", "dramatic", "nostalgic")) return "thoughtful";
            return "neutral";
        }

        // ------------------------------------------------------------------ //
        private static bool Any(Persona p, params string[] traits)
        {
            foreach (string t in traits)
                if (p.HasTrait(t))
                    return true;
            return false;
        }

        private static string FindOpinion(Persona p, string topic)
        {
            foreach (OpinionEntry o in p.opinions)
                if (o.topic == topic)
                    return o.text;
            return null;
        }

        private static bool AlreadySaid(IReadOnlyList<ConversationTurn> history, string content)
        {
            string probe = content.Length > 40 ? content.Substring(0, 40) : content;
            foreach (ConversationTurn t in history)
                if (t.role == ConversationRole.Npc && t.content.Contains(probe))
                    return true;
            return false;
        }

        private static bool MentionedOwnName(IReadOnlyList<ConversationTurn> history, string name)
        {
            foreach (ConversationTurn t in history)
                if (t.role == ConversationRole.Npc && t.content.Contains(name))
                    return true;
            return false;
        }

        private static string PlayerName(IReadOnlyList<ConversationTurn> history)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].role != ConversationRole.Player)
                    continue;
                Match m = IntroduceRegex.Match(history[i].content);
                if (m.Success)
                {
                    string n = m.Groups[1].Value;
                    return char.ToUpperInvariant(n[0]) + n.Substring(1);
                }
            }
            return null;
        }

        private static string JoinList(List<string> items)
        {
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + " and " + items[items.Count - 1];
        }

        private static string Article(string noun) =>
            noun.Length > 0 && "aeiouAEIOU".IndexOf(noun[0]) >= 0 ? "an" : "a";

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Only informational answers can be "repeated"; social turns and name recall cannot.</summary>
        private static bool IsRepeatable(Intent intent)
        {
            switch (intent)
            {
                case Intent.Knowledge:
                case Intent.Opinion:
                case Intent.Occupation:
                case Intent.Background:
                case Intent.Likes:
                case Intent.Dislikes:
                case Intent.Goals:
                case Intent.Age:
                    return true;
                default:
                    return false;
            }
        }

        private static string FirstSentence(string s)
        {
            int dot = s.IndexOf(". ");
            return dot > 0 ? s.Substring(0, dot + 1) : s;
        }

        /// <summary>FNV-1a: stable across Mono, IL2CPP and WebGL, unlike string.GetHashCode.</summary>
        private static int Seed(string a, string b, int n)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in a ?? string.Empty) { h ^= c; h *= 16777619; }
                foreach (char c in b ?? string.Empty) { h ^= c; h *= 16777619; }
                h ^= (uint)n; h *= 16777619;
                return (int)(h & 0x7fffffff);
            }
        }

        // ------------------------------------------------------------------ //
        private sealed class Voice
        {
            private readonly PersonaVoice _v;
            private readonly System.Random _rng;

            public Voice(PersonaVoice v, System.Random rng)
            {
                _v = v ?? new PersonaVoice();
                _rng = rng;
            }

            public bool Terse => _v.verbosity == "terse";
            public bool Chatty => _v.verbosity == "chatty";

            public string ByFormality(string casual, string neutral, string formal) =>
                _v.formality == "casual" ? casual : _v.formality == "formal" ? formal : neutral;

            public string Pick(List<string> options)
            {
                if (options == null || options.Count == 0)
                    return null;
                return options[_rng.Next(options.Count)];
            }

            public string RepeatPrefix() =>
                Terse ? "Told you." : ByFormality("Like I said:", "As I said:", "As I mentioned:");

            public string Wrap(string content, float openerChance = -1f, float closerChance = -1f)
            {
                if (openerChance < 0f) openerChance = Terse ? 0.3f : Chatty ? 0.75f : 0.45f;
                if (closerChance < 0f) closerChance = Terse ? 0.25f : Chatty ? 0.6f : 0.35f;
                string opener = _rng.NextDouble() < openerChance ? Pick(_v.openers) : null;
                string closer = _rng.NextDouble() < closerChance ? Pick(_v.closers) : null;
                string line = content;
                if (!string.IsNullOrEmpty(opener) && !content.StartsWith(opener))
                    line = opener + " " + line;
                if (!string.IsNullOrEmpty(closer))
                    line = line + " " + closer;
                return line;
            }
        }
    }
}
