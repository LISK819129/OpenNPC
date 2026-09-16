using System.Collections.Generic;
using OpenNPC.Config;
using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using OpenNPC.Dialogue;
using UnityEngine;

namespace OpenNPC.Demo.UI
{
    /// <summary>
    /// Owns every speech bubble. Listens to NPC dialogue events and the player's
    /// messages; NPCs never reference UI.
    /// </summary>
    public sealed class SpeechBubbleLayer : MonoBehaviour
    {
        private readonly Dictionary<NPCDialogue, SpeechBubble> _npcBubbles = new Dictionary<NPCDialogue, SpeechBubble>();
        private UITheme _theme;
        private Camera _camera;
        private ConversationController _conversation;
        private OpenNPCConfig _config;
        private SpeechBubble _playerBubble;
        private Transform _player;

        public void Initialize(UITheme theme, Camera camera, ConversationController conversation, Transform player)
        {
            _theme = theme;
            _camera = camera;
            _conversation = conversation;
            _player = player;
            _config = OpenNPCConfig.Load();

            NPCDialogue.AnyLineSpoken += OnLine;
            NPCDialogue.AnyThinkingChanged += OnThinking;
            _conversation.PlayerSpoke += OnPlayerSpoke;
            _conversation.ConversationEnded += OnConversationEnded;
            _playerBubble = SpeechBubble.Create(transform, theme, "You", inverted: true);
        }

        private void OnDestroy()
        {
            NPCDialogue.AnyLineSpoken -= OnLine;
            NPCDialogue.AnyThinkingChanged -= OnThinking;
            if (_conversation != null)
            {
                _conversation.PlayerSpoke -= OnPlayerSpoke;
                _conversation.ConversationEnded -= OnConversationEnded;
            }
        }

        private SpeechBubble BubbleFor(NPCDialogue npc)
        {
            if (!_npcBubbles.TryGetValue(npc, out SpeechBubble bubble) || bubble == null)
            {
                bubble = SpeechBubble.Create(transform, _theme, npc.Controller.Persona.DisplayName, inverted: false);
                _npcBubbles[npc] = bubble;
            }
            return bubble;
        }

        private void OnThinking(NPCDialogue npc, bool thinking)
        {
            if (thinking)
                BubbleFor(npc).ShowThinking();
        }

        private void OnLine(NPCDialogue npc, DialogueResponse response)
        {
            SpeechBubble bubble = BubbleFor(npc);
            bubble.Say(response.Text, _config.typewriterCharsPerSecond);
            if (_conversation.ActiveNpc != npc.Controller)
                bubble.ExpireAfter(_config.dialogueDuration);
        }

        private void OnPlayerSpoke(string text)
        {
            _playerBubble.Say(text, _config.typewriterCharsPerSecond * 2f);
            _playerBubble.ExpireAfter(Mathf.Min(3f, _config.dialogueDuration));
        }

        private void OnConversationEnded(NPCController npc)
        {
            if (_npcBubbles.TryGetValue(npc.Dialogue, out SpeechBubble bubble))
                bubble.ExpireAfter(_config.dialogueDuration * 0.5f);
            _playerBubble.HideNow();
        }

        private void LateUpdate()
        {
            // In a conversation the two bubbles lean away from each other so they never overlap.
            float playerSide = 0f;
            NPCController active = _conversation.ActiveNpc;
            if (active != null && _player != null)
                playerSide = _player.position.x <= active.transform.position.x ? -1f : 1f;

            foreach (KeyValuePair<NPCDialogue, SpeechBubble> pair in _npcBubbles)
                if (pair.Key != null && pair.Value != null)
                    pair.Value.Follow(_camera, pair.Key.BubbleAnchor, pair.Key.Controller == active ? -playerSide : 0f);
            if (_player != null)
                _playerBubble.Follow(_camera, _player.position + Vector3.up * 2.1f, playerSide);
        }
    }
}
