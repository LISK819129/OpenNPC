using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNPC.Demo.UI
{
    /// <summary>"[E] TALK TO RAVI" hanging above the nearest talkable NPC.</summary>
    public sealed class InteractionPrompt : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _canvas;
        private CanvasGroup _group;
        private Text _label;
        private Camera _camera;
        private PlayerInteractor _interactor;
        private ConversationController _conversation;
        private UITheme _theme;
        private IInteractable _shown;

        public void Initialize(UITheme theme, Camera camera, PlayerInteractor interactor, ConversationController conversation)
        {
            _theme = theme;
            _camera = camera;
            _interactor = interactor;
            _conversation = conversation;
            _root = (RectTransform)transform;
            _canvas = (RectTransform)_root.parent;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0f);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            UIFactory.Stretch(UIFactory.Box("Back", _root, theme.ink).rectTransform);
            _label = UIFactory.Label("Label", _root, theme.monoBold, theme.smallSize + 2, theme.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_label.rectTransform);
        }

        private void LateUpdate()
        {
            NPCInteraction target = _conversation.InConversation ? null : _interactor.Target;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target != null ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (target == null)
                return;

            if (!ReferenceEquals(target, _shown))
            {
                _shown = target;
                _label.text = $"[E]  TALK TO {target.DisplayName.ToUpperInvariant()}";
                float w = UIFactory.Measure(_label, _label.text, 600f).x + 28f;
                _root.sizeDelta = new Vector2(w, _theme.smallSize + 20f);
            }

            Vector3 screen = _camera.WorldToScreenPoint(target.PromptAnchor);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 local);
            _root.anchoredPosition = local + new Vector2(0f, 10f);
        }
    }
}
