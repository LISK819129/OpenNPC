using System;
using OpenNPC.Config;
using OpenNPC.Dialogue;
using UnityEngine;

namespace OpenNPC.Demo.NPC
{
    /// <summary>
    /// One NPC's side of a conversation: owns its ConversationHistory, builds
    /// requests from persona + context + history, and announces lines. Contains no
    /// dialogue content and no knowledge of which provider answers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCDialogue : MonoBehaviour
    {
        /// <summary>Any NPC said a line (speaker, response). UI listens to this.</summary>
        public static event Action<NPCDialogue, DialogueResponse> AnyLineSpoken;
        /// <summary>Any NPC started (true) or stopped (false) waiting for its provider.</summary>
        public static event Action<NPCDialogue, bool> AnyThinkingChanged;

        /// <summary>One id per play session, so a backend can group requests.</summary>
        private static readonly string SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);

        [SerializeField] private float bubbleHeight = 2.35f;

        private NPCController _controller;
        private ConversationHistory _history;
        private OpenNPCConfig _config;
        private int _pending;

        public ConversationHistory History => _history;
        public bool IsThinking => _pending > 0;
        public NPCController Controller => _controller;
        public Vector3 BubbleAnchor => transform.position + Vector3.up * bubbleHeight * transform.lossyScale.y;
        public DialogueResponse LastResponse { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<NPCController>();
            _config = OpenNPCConfig.Load();
            _history = new ConversationHistory(_config.historyTurns);
        }

        /// <summary>Ask the NPC to open the conversation.</summary>
        public void Open(Action<DialogueResponse> onReply = null) =>
            Send(null, DialogueRequest.EventConversationStart, onReply);

        /// <summary>Player says something to this NPC.</summary>
        public void Say(string playerMessage, Action<DialogueResponse> onReply = null)
        {
            if (string.IsNullOrWhiteSpace(playerMessage))
                return;
            Send(playerMessage.Trim(), null, onReply);
        }

        private void Send(string message, string dialogueEvent, Action<DialogueResponse> onReply)
        {
            DialogueManager manager = DialogueManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[OpenNPC] No DialogueManager in the scene.");
                return;
            }

            var context = new DialogueContext
            {
                location = _config.locationName,
                timeOfDay = "afternoon",
                npcState = _controller.State.ToString(),
                npcMood = _controller.Persona.Persona.mood,
                sessionId = SessionId,
                dialogueEvent = dialogueEvent,
            };
            var request = new DialogueRequest(_controller.Persona.Persona, context, _history.Snapshot(), message);
            if (message != null)
                _history.Add(ConversationRole.Player, message);

            _pending++;
            AnyThinkingChanged?.Invoke(this, true);
            manager.Request(request, response =>
            {
                _pending = Mathf.Max(0, _pending - 1);
                AnyThinkingChanged?.Invoke(this, IsThinking);
                if (this == null)
                    return;
                if (!response.IsError)
                    _history.Add(ConversationRole.Npc, response.Text);
                _controller.Persona.SetMood(response.Mood);
                LastResponse = response;
                AnyLineSpoken?.Invoke(this, response);
                onReply?.Invoke(response);
            });
        }
    }
}
