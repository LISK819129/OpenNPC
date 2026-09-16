using System;
using OpenNPC.Demo.NPC;
using OpenNPC.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenNPC.Demo.Player
{
    /// <summary>
    /// Owns the flow of one conversation: E to start, type + ENTER to send, ESC to
    /// leave. Talks to the NPC through its components only; UI observes the events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConversationController : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerInteractor interactor;

        public NPCController ActiveNpc { get; private set; }
        public bool InConversation => ActiveNpc != null;

        public event Action<NPCController> ConversationStarted;
        public event Action<NPCController> ConversationEnded;
        /// <summary>The player sent a line (text).</summary>
        public event Action<string> PlayerSpoke;

        /// <summary>
        /// UI sets this while a text field has keyboard focus so E/ESC are not
        /// stolen from typing.
        /// </summary>
        public bool TextInputFocused { get; set; }

        private OpenNPC.Config.OpenNPCConfig _config;

        private void Awake()
        {
            _config = OpenNPC.Config.OpenNPCConfig.Load();
            if (player == null) player = GetComponent<PlayerController>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null)
                return;
            if (!InConversation)
            {
                if (k.eKey.wasPressedThisFrame && interactor.Target != null)
                    Begin(interactor.Target.Controller);
            }
            else if (k.escapeKey.wasPressedThisFrame)
            {
                End();
            }
        }

        public void Begin(NPCController npc)
        {
            if (npc == null || InConversation || !npc.Interaction.CanInteract)
                return;
            ActiveNpc = npc;
            player.InputEnabled = false;
            StartCoroutine(StepToTalkingDistance(npc));
            npc.BeginConversation(player.transform);
            ConversationStarted?.Invoke(npc);
            npc.Dialogue.Open(OnNpcReply);
        }

        public void Send(string message)
        {
            if (!InConversation || string.IsNullOrWhiteSpace(message))
                return;
            PlayerSpoke?.Invoke(message.Trim());
            ActiveNpc.Dialogue.Say(message, OnNpcReply);
        }

        public void End()
        {
            if (!InConversation)
                return;
            NPCController npc = ActiveNpc;
            ActiveNpc = null;
            npc.EndConversation(player.transform);
            player.InputEnabled = true;
            ConversationEnded?.Invoke(npc);
        }

        /// <summary>Stand beside the NPC, not inside it, then face it.</summary>
        private System.Collections.IEnumerator StepToTalkingDistance(NPCController npc)
        {
            const float spacing = 0.95f;
            Vector3 start = player.transform.position;
            float side = start.x <= npc.transform.position.x ? -1f : 1f;
            Vector3 goal = new Vector3(npc.transform.position.x + side * spacing, 0f,
                                       Mathf.Lerp(start.z, npc.transform.position.z, 0.6f));
            for (float t = 0f; t < 1f && InConversation; t += Time.deltaTime / 0.3f)
            {
                player.transform.position = Vector3.Lerp(start, goal, Mathf.SmoothStep(0f, 1f, t));
                player.Face(npc.transform.position);
                yield return null;
            }
            if (InConversation)
                player.Face(npc.transform.position);
        }

        private void OnNpcReply(DialogueResponse response)
        {
            if (!InConversation)
                return;
            ActiveNpc.SpeakFor(1.2f + response.Text.Length / Mathf.Max(1f, _config.typewriterCharsPerSecond));
            if (response.EndConversation)
                Invoke(nameof(End), 1.6f);
        }
    }
}
