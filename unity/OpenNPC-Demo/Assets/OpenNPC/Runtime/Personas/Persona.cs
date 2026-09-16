using System;
using System.Collections.Generic;

namespace OpenNPC.Personas
{
    /// <summary>
    /// Everything that makes one NPC a specific person. Mirrors
    /// framework/schemas/persona.schema.json field-for-field so the same JSON
    /// can be loaded here and sent unchanged to a dialogue backend.
    /// Field names are camelCase on purpose: they are the wire names.
    /// </summary>
    [Serializable]
    public sealed class Persona
    {
        public string id;
        public string name;
        public int age;
        public string occupation;
        public List<string> personalityTraits = new List<string>();
        public string mood;
        public string background;
        public string speakingStyle;
        public PersonaVoice voice = new PersonaVoice();
        public List<KnowledgeEntry> knowledge = new List<KnowledgeEntry>();
        public List<OpinionEntry> opinions = new List<OpinionEntry>();
        public List<string> likes = new List<string>();
        public List<string> dislikes = new List<string>();
        public List<string> goals = new List<string>();
        public string source = "authored";

        public bool IsGenerated => source == "generated";

        public bool HasTrait(string trait)
        {
            foreach (string t in personalityTraits)
                if (string.Equals(t, trait, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public override string ToString() => $"{name} ({id}, {occupation})";
    }

    [Serializable]
    public sealed class PersonaVoice
    {
        /// <summary>terse | normal | chatty</summary>
        public string verbosity = "normal";
        /// <summary>casual | neutral | formal</summary>
        public string formality = "neutral";
        public List<string> openers = new List<string>();
        public List<string> closers = new List<string>();
        public List<string> fallbacks = new List<string>();
    }

    [Serializable]
    public sealed class KnowledgeEntry
    {
        public string topic;
        public List<string> keywords = new List<string>();
        public string fact;
    }

    [Serializable]
    public sealed class OpinionEntry
    {
        public string topic;
        public List<string> keywords = new List<string>();
        public string text;
    }

    /// <summary>JsonUtility cannot parse a bare array; persona bundles use this wrapper.</summary>
    [Serializable]
    public sealed class PersonaCollection
    {
        public List<Persona> personas = new List<Persona>();
    }
}
