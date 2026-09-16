using System;
using System.Collections.Generic;
using OpenNPC.Personas;

namespace OpenNPC.Dialogue
{
    public enum ConversationRole
    {
        Player,
        Npc,
    }

    [Serializable]
    public struct ConversationTurn
    {
        public ConversationRole role;
        public string content;

        public ConversationTurn(ConversationRole role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    /// <summary>The situation a conversation happens in. Engines send what they know.</summary>
    [Serializable]
    public sealed class DialogueContext
    {
        public string location;
        public string timeOfDay;
        public string npcState;
        public string npcMood;
        public List<string> nearby = new List<string>();
        public string sessionId;

        /// <summary>
        /// Non-message triggers. "conversation_start" asks the NPC to open the
        /// conversation (playerMessage is empty); null for a normal turn.
        /// </summary>
        public string dialogueEvent;
    }

    /// <summary>
    /// Everything a provider needs to produce one NPC line. Mirrors
    /// framework/schemas/dialogue-request.schema.json.
    /// </summary>
    public sealed class DialogueRequest
    {
        public const string EventConversationStart = "conversation_start";

        public Persona Persona { get; }
        public DialogueContext Context { get; }
        public IReadOnlyList<ConversationTurn> Conversation { get; }
        public string PlayerMessage { get; }

        public bool IsOpening => Context?.dialogueEvent == EventConversationStart;

        public DialogueRequest(Persona persona, DialogueContext context,
                               IReadOnlyList<ConversationTurn> conversation, string playerMessage)
        {
            Persona = persona ?? throw new ArgumentNullException(nameof(persona));
            Context = context ?? new DialogueContext();
            Conversation = conversation ?? Array.Empty<ConversationTurn>();
            PlayerMessage = playerMessage ?? string.Empty;
        }
    }

    /// <summary>Mirrors framework/schemas/dialogue-response.schema.json.</summary>
    public sealed class DialogueResponse
    {
        public string Text { get; }
        public string Emotion { get; }
        public string Mood { get; }
        public bool EndConversation { get; }

        /// <summary>True when the provider could not produce a real answer (network, config …).</summary>
        public bool IsError { get; }

        public DialogueResponse(string text, string emotion = "neutral", string mood = null,
                                bool endConversation = false, bool isError = false)
        {
            Text = text ?? string.Empty;
            Emotion = string.IsNullOrEmpty(emotion) ? "neutral" : emotion;
            Mood = mood;
            EndConversation = endConversation;
            IsError = isError;
        }

        public static DialogueResponse Error(string message) =>
            new DialogueResponse(message, "confused", isError: true);
    }
}
