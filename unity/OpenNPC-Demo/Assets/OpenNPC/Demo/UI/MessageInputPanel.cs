using System;
using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenNPC.Demo.UI
{
    /// <summary>
    /// The typing line at the bottom of the screen during a conversation.
    /// ENTER sends; ESC leaves; P (with an empty box) opens the persona inspector.
    /// </summary>
    public sealed class MessageInputPanel : MonoBehaviour
    {
        private ConversationController _conversation;
        private InputField _field;
        private Text _title;
        private CanvasGroup _group;
        private bool _refocus;

        /// <summary>Raised when the player presses P with an empty message box.</summary>
        public event Action PersonaHotkey;
        /// <summary>Raised when the player presses TAB inside the box.</summary>
        public event Action DebugHotkey;

        public InputField Field => _field;

        public void Initialize(UITheme theme, ConversationController conversation)
        {
            _conversation = conversation;
            var root = (RectTransform)transform;
            UIFactory.Anchor(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(980f, 128f));
            _group = gameObject.AddComponent<CanvasGroup>();

            _title = UIFactory.Label("Title", root, theme.display, theme.nameSize, theme.ink, TextAnchor.LowerLeft);
            UIFactory.Anchor(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(980f, 36f));

            (Image border, Image fill) = UIFactory.BorderedBox("Box", root, theme.ink, theme.white, theme.border);
            UIFactory.Anchor(border.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(980f, 58f));
            fill.raycastTarget = true;

            Text text = UIFactory.Label("Text", fill.transform, theme.mono, theme.bodySize, theme.ink, TextAnchor.MiddleLeft);
            UIFactory.Stretch(text.rectTransform, 0f);
            text.rectTransform.offsetMin = new Vector2(18f, 0f);
            text.rectTransform.offsetMax = new Vector2(-18f, 0f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;

            Text placeholder = UIFactory.Label("Placeholder", fill.transform, theme.mono, theme.bodySize, theme.muted, TextAnchor.MiddleLeft);
            UIFactory.Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(18f, 0f);
            placeholder.text = "Say something…  e.g. What do you think about this city?";

            _field = fill.gameObject.AddComponent<InputField>();
            _field.textComponent = text;
            _field.placeholder = placeholder;
            _field.lineType = InputField.LineType.SingleLine;
            _field.characterLimit = 200;
            _field.caretWidth = 3;
            _field.customCaretColor = true;
            _field.caretColor = theme.ink;
            _field.selectionColor = new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0.25f);
            _field.onValidateInput = Validate;
            _field.onEndEdit.AddListener(OnEndEdit);

            Text hint = UIFactory.Label("Hint", root, theme.mono, theme.smallSize, theme.ink, TextAnchor.UpperLeft);
            UIFactory.Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(980f, 26f));
            hint.text = "ENTER  send     ESC  leave     P  persona     TAB  debug";

            conversation.ConversationStarted += OnStarted;
            conversation.ConversationEnded += OnEnded;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_conversation == null) return;
            _conversation.ConversationStarted -= OnStarted;
            _conversation.ConversationEnded -= OnEnded;
        }

        private char Validate(string current, int index, char added)
        {
            if (added == '\t')
            {
                DebugHotkey?.Invoke();
                return '\0';
            }
            if ((added == 'p' || added == 'P') && string.IsNullOrEmpty(current))
            {
                PersonaHotkey?.Invoke();
                return '\0';
            }
            return added;
        }

        private void OnStarted(NPCController npc)
        {
            gameObject.SetActive(true);
            _title.text = "TALKING TO " + npc.Persona.DisplayName.ToUpperInvariant();
            _field.text = string.Empty;
            _refocus = true;
        }

        private void OnEnded(NPCController npc)
        {
            _field.DeactivateInputField();
            _conversation.TextInputFocused = false;
            gameObject.SetActive(false);
        }

        private void OnEndEdit(string value)
        {
            Keyboard k = Keyboard.current;
            bool enter = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
            if (enter && !string.IsNullOrWhiteSpace(value))
            {
                _conversation.Send(value);
                _field.text = string.Empty;
            }
            if (_conversation.InConversation)
                _refocus = true;
        }

        /// <summary>Used by the autopilot and tests.</summary>
        public void Submit(string message)
        {
            _conversation.Send(message);
            _field.text = string.Empty;
        }

        private void Update()
        {
            _conversation.TextInputFocused = _field.isFocused;
            if (_refocus && _conversation.InConversation)
            {
                _refocus = false;
                EventSystem.current?.SetSelectedGameObject(_field.gameObject);
                _field.ActivateInputField();
            }
        }
    }
}
