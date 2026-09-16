using UnityEngine;
using UnityEngine.UI;

namespace OpenNPC.Demo.UI
{
    /// <summary>
    /// A speech bubble in the world: ink border, paper-white fill, the speaker's
    /// name on a small tab and a straight ink line down to their head. Sizes itself
    /// to the text, reveals it typewriter-style, follows a world anchor.
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        private const float TailLength = 34f;
        private const float ScreenMargin = 16f;

        private UITheme _theme;
        private RectTransform _root;
        private RectTransform _canvas;
        private RectTransform _tail;
        private CanvasGroup _group;
        private Text _body;
        private Text _nameLabel;
        private RectTransform _nameTab;

        private string _fullText = string.Empty;
        private float _revealed;
        private float _charsPerSecond;
        private bool _thinking;
        private float _expiresAt;   // hidden until first Say / ShowThinking
        private float _alpha;

        public bool Visible => _alpha > 0.01f;
        public bool IsTyping => !_thinking && _revealed < _fullText.Length;

        public static SpeechBubble Create(Transform parent, UITheme theme, string speaker, bool inverted)
        {
            RectTransform root = UIFactory.Rect("Bubble_" + speaker, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            var bubble = root.gameObject.AddComponent<SpeechBubble>();
            bubble.Build(theme, speaker, inverted, root);
            return bubble;
        }

        private void Build(UITheme theme, string speaker, bool inverted, RectTransform root)
        {
            _theme = theme;
            _root = root;
            _canvas = (RectTransform)root.parent;
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            Color fill = inverted ? theme.ink : theme.white;
            Color text = inverted ? theme.white : theme.ink;

            _tail = UIFactory.Box("Tail", root, theme.ink).rectTransform;
            _tail.anchorMin = _tail.anchorMax = new Vector2(0.5f, 0f);
            _tail.pivot = new Vector2(0.5f, 1f);
            _tail.sizeDelta = new Vector2(theme.border, TailLength);

            (Image border, Image _) = UIFactory.BorderedBox("Frame", root, theme.ink, fill, theme.border);
            UIFactory.Stretch(border.rectTransform);

            _body = UIFactory.Label("Text", root, theme.mono, theme.bodySize, text);
            UIFactory.Stretch(_body.rectTransform);
            _body.rectTransform.offsetMin = new Vector2(theme.bubblePadding.x, theme.bubblePadding.y);
            _body.rectTransform.offsetMax = new Vector2(-theme.bubblePadding.x, -theme.bubblePadding.y);

            _nameTab = UIFactory.Box("NameTab", root, theme.ink).rectTransform;
            _nameTab.anchorMin = _nameTab.anchorMax = new Vector2(0f, 1f);
            _nameTab.pivot = new Vector2(0f, 0f);
            _nameTab.anchoredPosition = new Vector2(0f, -theme.border);
            _nameLabel = UIFactory.Label("Name", _nameTab, theme.display, theme.nameSize - 6, theme.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_nameLabel.rectTransform);
            SetSpeaker(speaker);
        }

        public void SetSpeaker(string speaker)
        {
            _nameLabel.text = speaker.ToUpperInvariant();
            float w = UIFactory.Measure(_nameLabel, _nameLabel.text, 400f).x + 18f;
            _nameTab.sizeDelta = new Vector2(w, _theme.nameSize);
        }

        public void ShowThinking()
        {
            _thinking = true;
            _fullText = "· · ·";
            Resize("· · ·");
            _expiresAt = float.PositiveInfinity;
        }

        public void Say(string text, float charsPerSecond)
        {
            _thinking = false;
            _fullText = text ?? string.Empty;
            _revealed = 0f;
            _charsPerSecond = charsPerSecond;
            Resize(_fullText);
            _body.text = string.Empty;
            _expiresAt = float.PositiveInfinity;
        }

        /// <summary>Fade out after <paramref name="seconds"/> (after typing finishes).</summary>
        public void ExpireAfter(float seconds)
        {
            float typingLeft = _charsPerSecond > 0f ? (_fullText.Length - _revealed) / _charsPerSecond : 0f;
            _expiresAt = Time.time + typingLeft + seconds;
        }

        public void HideNow() => _expiresAt = 0f;

        private void Resize(string content)
        {
            Vector2 size = UIFactory.Measure(_body, content, _theme.bubbleMaxWidth);
            _root.sizeDelta = new Vector2(Mathf.Max(size.x, 64f) + _theme.bubblePadding.x * 2f,
                                          size.y + _theme.bubblePadding.y * 2f);
        }

        /// <summary>Call every frame from the layer with the world anchor's screen point.</summary>
        public void Follow(Camera camera, Vector3 worldAnchor, float sideBias = 0f)
        {
            Vector3 screen = camera.WorldToScreenPoint(worldAnchor);
            bool inFront = screen.z > 0f;
            float targetAlpha = inFront && Time.time < _expiresAt ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, targetAlpha, Time.unscaledDeltaTime * 5f);
            _group.alpha = _alpha;
            if (!inFront)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 local);
            Rect bounds = _canvas.rect;
            float halfW = _root.sizeDelta.x * 0.5f;
            float biased = local.x + sideBias * (halfW - 26f);
            float x = Mathf.Clamp(biased, bounds.xMin + halfW + ScreenMargin, bounds.xMax - halfW - ScreenMargin);
            float y = Mathf.Min(local.y + TailLength, bounds.yMax - _root.sizeDelta.y - ScreenMargin - _theme.nameSize);
            _root.anchoredPosition = new Vector2(x, y);
            _tail.anchoredPosition = new Vector2(Mathf.Clamp(local.x - x, -halfW + 12f, halfW - 12f), 0f);
            _tail.sizeDelta = new Vector2(_theme.border, Mathf.Max(8f, y - local.y));
        }

        private void Update()
        {
            if (_thinking)
            {
                int dots = 1 + (int)(Time.unscaledTime * 3f) % 3;
                _body.text = dots == 1 ? "·" : dots == 2 ? "· ·" : "· · ·";
                return;
            }
            if (_revealed < _fullText.Length)
            {
                _revealed = Mathf.Min(_fullText.Length, _revealed + _charsPerSecond * Time.unscaledDeltaTime);
                _body.text = _fullText.Substring(0, (int)_revealed);
            }
        }
    }
}
