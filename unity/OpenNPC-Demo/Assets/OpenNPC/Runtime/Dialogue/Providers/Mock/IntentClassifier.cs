using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OpenNPC.Personas;

namespace OpenNPC.Dialogue.Mock
{
    public enum Intent
    {
        Unknown,
        Greeting,
        Farewell,
        Thanks,
        Name,
        Occupation,
        Age,
        Mood,
        Background,
        Likes,
        Dislikes,
        Goals,
        IntroducePlayer,
        RecallPlayer,
        Knowledge,
        Opinion,
    }

    public readonly struct IntentMatch
    {
        public readonly Intent Intent;
        /// <summary>Knowledge or opinion entry text when Intent is Knowledge/Opinion.</summary>
        public readonly string TopicText;
        public readonly string Topic;
        /// <summary>Player name for IntroducePlayer.</summary>
        public readonly string Value;

        public IntentMatch(Intent intent, string topic = null, string topicText = null, string value = null)
        {
            Intent = intent;
            Topic = topic;
            TopicText = topicText;
            Value = value;
        }
    }

    /// <summary>
    /// Keyword intent detection for the mock provider. Deliberately simple and
    /// transparent: its job is to show that the SAME message routed through
    /// DIFFERENT personas gives different answers, not to understand language.
    /// Topic questions are matched against the persona's own knowledge and
    /// opinions, so what an NPC can talk about is data, not code.
    /// </summary>
    public static class IntentClassifier
    {
        private static readonly Regex IntroduceRegex =
            new Regex(@"\b(?:my name is|call me|i am called|name's)\s+([a-z][a-z'\-]{1,20})", RegexOptions.IgnoreCase);

        private static readonly string[] OpinionCues =
            { "think", "opinion", "feel about", "thoughts", "how do you find", "like about", "do you like", "view on" };
        private static readonly string[] KnowledgeCues =
            { "where", "how do i get", "how to get", "way to", "directions", "find", "know", "recommend", "best", "good place" };

        private static readonly (Intent intent, string[] phrases)[] Generic =
        {
            (Intent.RecallPlayer, new[] { "what s my name", "what is my name", "remember my name", "remember me", "who am i" }),
            (Intent.Farewell, new[] { "bye", "goodbye", "good bye", "see you", "see ya", "cya", "gotta go", "have to go", "farewell", "later" }),
            (Intent.Thanks, new[] { "thanks", "thank you", "thank u", "cheers", "ty", "appreciate it" }),
            (Intent.Mood, new[] { "how are you", "how s it going", "how are things", "how do you feel", "feeling", "your mood", "you ok", "you okay", "are you ok", "are you okay", "how you doing", "what s up", "whats up" }),
            (Intent.Name, new[] { "your name", "who are you", "what are you called", "who r u" }),
            (Intent.Occupation, new[] { "what do you do", "your job", "for a living", "occupation", "do you work", "what is your work", "profession" }),
            (Intent.Age, new[] { "how old", "your age" }),
            (Intent.Background, new[] { "about yourself", "about you", "your story", "background", "where are you from", "your life", "your past" }),
            (Intent.Dislikes, new[] { "hate", "dislike", "don t like", "dont like", "annoy", "pet peeve", "can t stand" }),
            (Intent.Likes, new[] { "what do you like", "favorite", "favourite", "enjoy", "hobby", "hobbies", "do you love", "into" }),
            (Intent.Goals, new[] { "dream", "goal", "plans", "future", "wish", "ambition", "want to do", "want in life" }),
            (Intent.Greeting, new[] { "hi", "hello", "hey", "yo", "hiya", "sup", "good morning", "good evening", "good afternoon", "howdy", "namaste", "hola" }),
        };

        public static string Normalize(string message)
        {
            var sb = new StringBuilder(message.Length + 2);
            sb.Append(' ');
            bool lastSpace = true;
            foreach (char raw in message.ToLowerInvariant())
            {
                char c = char.IsLetterOrDigit(raw) ? raw : ' ';
                if (c == ' ' && lastSpace)
                    continue;
                sb.Append(c);
                lastSpace = c == ' ';
            }
            if (!lastSpace)
                sb.Append(' ');
            return sb.ToString();
        }

        private static bool Has(string normalized, string phrase) =>
            normalized.Contains(" " + phrase + " ");

        private static bool HasAny(string normalized, IEnumerable<string> phrases)
        {
            foreach (string p in phrases)
                if (Has(normalized, p))
                    return true;
            return false;
        }

        public static IntentMatch Classify(string message, Persona persona)
        {
            if (string.IsNullOrWhiteSpace(message))
                return new IntentMatch(Intent.Unknown);

            Match intro = IntroduceRegex.Match(message);
            if (intro.Success)
            {
                string n = intro.Groups[1].Value;
                return new IntentMatch(Intent.IntroducePlayer, value: char.ToUpperInvariant(n[0]) + n.Substring(1));
            }

            string text = Normalize(message);
            int words = text.Trim().Split(' ').Length;

            // Short social messages first, so "hi!" is never read as a topic.
            if (words <= 3)
            {
                foreach (Intent social in new[] { Intent.RecallPlayer, Intent.Farewell, Intent.Thanks, Intent.Greeting })
                    if (HasAny(text, PhrasesFor(social)))
                        return new IntentMatch(social);
            }

            IntentMatch topic = MatchTopic(text, persona);
            if (topic.Intent != Intent.Unknown)
                return topic;

            foreach ((Intent intent, string[] phrases) in Generic)
                if (HasAny(text, phrases))
                    return new IntentMatch(intent);

            return new IntentMatch(Intent.Unknown);
        }

        private static string[] PhrasesFor(Intent intent)
        {
            foreach ((Intent i, string[] p) in Generic)
                if (i == intent)
                    return p;
            return System.Array.Empty<string>();
        }

        private static IntentMatch MatchTopic(string text, Persona persona)
        {
            int Score(string topicName, List<string> keywords)
            {
                int s = 0;
                if (!string.IsNullOrEmpty(topicName) && text.Contains(" " + topicName.ToLowerInvariant() + " "))
                    s += 2;
                if (keywords != null)
                    foreach (string k in keywords)
                        if (Has(text, k.ToLowerInvariant()))
                            s++;
                return s;
            }

            KnowledgeEntry bestFact = null;
            int factScore = 0;
            foreach (KnowledgeEntry k in persona.knowledge)
            {
                int s = Score(k.topic, k.keywords);
                if (s > factScore) { factScore = s; bestFact = k; }
            }

            OpinionEntry bestOpinion = null;
            int opinionScore = 0;
            foreach (OpinionEntry o in persona.opinions)
            {
                int s = Score(o.topic, o.keywords);
                if (s > opinionScore) { opinionScore = s; bestOpinion = o; }
            }

            if (factScore == 0 && opinionScore == 0)
                return new IntentMatch(Intent.Unknown);

            bool wantsOpinion = HasAny(text, OpinionCues);
            bool wantsFact = HasAny(text, KnowledgeCues);

            if (bestOpinion != null && (wantsOpinion || bestFact == null || opinionScore > factScore) && !(wantsFact && !wantsOpinion && bestFact != null))
                return new IntentMatch(Intent.Opinion, bestOpinion.topic, bestOpinion.text);
            if (bestFact != null)
                return new IntentMatch(Intent.Knowledge, bestFact.topic, bestFact.fact);
            return new IntentMatch(Intent.Opinion, bestOpinion.topic, bestOpinion.text);
        }
    }
}
