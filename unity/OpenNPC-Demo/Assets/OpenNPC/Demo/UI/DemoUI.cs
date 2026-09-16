using OpenNPC.Config;
using OpenNPC.Demo.CameraRig;
using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using OpenNPC.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenNPC.Demo.UI
{
    /// <summary>
    /// Builds the whole screen-space UI at startup from a UITheme and wires it to
    /// the gameplay objects. Scene references live here and nowhere else.
    /// </summary>
    public sealed class DemoUI : MonoBehaviour
    {
        [SerializeField] private UITheme theme;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SideScrollCamera cameraRig;
        [SerializeField] private ConversationController conversation;
        [SerializeField] private PlayerInteractor interactor;

        public DebugPanel Debug { get; private set; }
        public PersonaInspector Inspector { get; private set; }
        public MessageInputPanel Input { get; private set; }

        private void Start()
        {
            if (theme == null || worldCamera == null || conversation == null || interactor == null)
            {
                UnityEngine.Debug.LogError("[OpenNPC] DemoUI is missing references.");
                enabled = false;
                return;
            }

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            var bubbles = UIFactory.Rect("Bubbles", root).gameObject.AddComponent<SpeechBubbleLayer>();
            UIFactory.Stretch((RectTransform)bubbles.transform);
            bubbles.Initialize(theme, worldCamera, conversation, conversation.transform);

            UIFactory.Rect("Prompt", root).gameObject.AddComponent<InteractionPrompt>()
                     .Initialize(theme, worldCamera, interactor, conversation);

            BuildHud(root);

            Input = UIFactory.Rect("MessageInput", root).gameObject.AddComponent<MessageInputPanel>();
            Input.Initialize(theme, conversation);
            Input.PersonaHotkey += TogglePersona;
            Input.DebugHotkey += () => Debug.Toggle();

            Inspector = UIFactory.Rect("PersonaInspector", root).gameObject.AddComponent<PersonaInspector>();
            Inspector.Initialize(theme);

            Debug = UIFactory.Rect("DebugPanel", root).gameObject.AddComponent<DebugPanel>();
            Debug.Initialize(theme, conversation, interactor);

            conversation.ConversationStarted += npc => cameraRig?.SetConversationFocus(npc.transform.position);
            conversation.ConversationEnded += npc =>
            {
                cameraRig?.SetConversationFocus(null);
                Inspector.Hide();
            };
            NPCDialogue.AnyLineSpoken += OnLine;
        }

        private void OnDestroy() => NPCDialogue.AnyLineSpoken -= OnLine;

        private void OnLine(NPCDialogue npc, DialogueResponse response)
        {
            if (Inspector != null)
                Inspector.Refresh(npc.Controller);
        }

        private void BuildHud(Transform root)
        {
            // Paper backings keep the HUD readable over the line-drawn street.
            Image titleBack = UIFactory.Box("TitleBack", root, theme.paper);
            UIFactory.Anchor(titleBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(560f, 112f));
            Image controlsBack = UIFactory.Box("ControlsBack", root, theme.paper);
            UIFactory.Anchor(controlsBack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 22f), new Vector2(660f, 40f));
            Text title = UIFactory.Label("Title", root, theme.display, theme.headingSize + 8, theme.ink);
            UIFactory.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(600f, 76f));
            title.text = "OPENNPC";

            OpenNPCConfig config = OpenNPCConfig.Load();
            Text subtitle = UIFactory.Label("Subtitle", root, theme.mono, theme.smallSize, theme.ink);
            UIFactory.Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -96f), new Vector2(900f, 26f));
            subtitle.text = $"interactive demo  ·  {config.locationName.ToLowerInvariant()}  ·  provider: {config.provider.ToString().ToLowerInvariant()}";

            Text controls = UIFactory.Label("Controls", root, theme.mono, theme.smallSize, theme.ink, TextAnchor.LowerLeft);
            UIFactory.Anchor(controls.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 30f), new Vector2(900f, 26f));
            controls.text = "WASD / ARROWS  move     E  talk     TAB  debug     P  persona";
            conversation.ConversationStarted += _ => controls.enabled = controlsBack.enabled = false;
            conversation.ConversationEnded += _ => controls.enabled = controlsBack.enabled = true;
        }

        private void TogglePersona()
        {
            NPCController npc = conversation.ActiveNpc != null ? conversation.ActiveNpc
                : interactor.Target != null ? interactor.Target.Controller : null;
            Inspector.Toggle(npc);
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || Debug == null)
                return;
            bool typing = conversation.TextInputFocused;
            if (k.tabKey.wasPressedThisFrame && !typing)
                Debug.Toggle();
            if (k.pKey.wasPressedThisFrame && !typing)
                TogglePersona();
        }
    }
}
