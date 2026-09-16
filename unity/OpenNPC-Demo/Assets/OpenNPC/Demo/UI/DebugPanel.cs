using System.Collections.Generic;
using System.Text;
using OpenNPC.Config;
using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using OpenNPC.Dialogue;
using OpenNPC.Personas;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNPC.Demo.UI
{
    /// <summary>
    /// Developer view (TAB): which NPC, which persona, which state, which provider,
    /// and the request moving through the pipeline. Monochrome and monospace on
    /// purpose — a tool, not a HUD.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        private const float StageHold = 0.16f;
        private static readonly PipelineStage[] Stages =
        {
            PipelineStage.PlayerMessage, PipelineStage.Persona, PipelineStage.Context,
            PipelineStage.Provider, PipelineStage.Response, PipelineStage.Npc,
        };
        private static readonly string[] StageLabels =
            { "PLAYER MESSAGE", "PERSONA", "CONTEXT", "DIALOGUE PROVIDER", "RESPONSE", "NPC" };

        private UITheme _theme;
        private Text _text;
        private ConversationController _conversation;
        private PlayerInteractor _interactor;
        private OpenNPCConfig _config;
        private DialogueManager _manager;
        private readonly Queue<PipelineStage> _stageQueue = new Queue<PipelineStage>();
        private PipelineStage _shownStage = PipelineStage.Idle;
        private float _stageTimer;
        private float _refresh;
        private float _fps;
        private NPCController _lastSpeaker;
        private readonly StringBuilder _sb = new StringBuilder(2048);

        public bool IsOpen => gameObject.activeSelf;

        public void Initialize(UITheme theme, ConversationController conversation, PlayerInteractor interactor)
        {
            _theme = theme;
            _conversation = conversation;
            _interactor = interactor;
            _config = OpenNPCConfig.Load();

            var root = (RectTransform)transform;
            UIFactory.Anchor(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(560f, 850f));
            Image back = UIFactory.Box("Back", root, theme.debugBackground);
            UIFactory.Stretch(back.rectTransform);
            _text = UIFactory.Label("Text", root, theme.mono, theme.smallSize, theme.debugText);
            UIFactory.Stretch(_text.rectTransform, 24f);

            NPCDialogue.AnyLineSpoken += OnLine;
            // Subscribe now, not in Update: a hidden panel must still track the pipeline.
            _manager = DialogueManager.Instance;
            if (_manager != null)
                _manager.StageChanged += OnStage;
            gameObject.SetActive(_config.debugPanelOnStart);
        }

        private void OnDestroy()
        {
            NPCDialogue.AnyLineSpoken -= OnLine;
            if (_manager != null) _manager.StageChanged -= OnStage;
        }

        public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

        private void OnLine(NPCDialogue npc, DialogueResponse response) => _lastSpeaker = npc.Controller;

        private void OnStage(PipelineStage stage)
        {
            if (isActiveAndEnabled)
                _stageQueue.Enqueue(stage);
            else
                _shownStage = stage;   // no animation while hidden, just the latest state
        }

        private void Update()
        {
            if (_manager == null && DialogueManager.Instance != null)
            {
                _manager = DialogueManager.Instance;
                _manager.StageChanged += OnStage;
            }
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);

            // Hold each stage long enough to be seen; the provider stage naturally lasts the latency.
            _stageTimer -= Time.unscaledDeltaTime;
            if (_stageTimer <= 0f && _stageQueue.Count > 0)
            {
                _shownStage = _stageQueue.Dequeue();
                _stageTimer = StageHold;
            }

            _refresh -= Time.unscaledDeltaTime;
            if (_refresh > 0f)
                return;
            _refresh = 0.1f;
            Rebuild();
        }

        private NPCController Focus()
        {
            if (_conversation.ActiveNpc != null) return _conversation.ActiveNpc;
            if (_interactor.Target != null) return _interactor.Target.Controller;
            return _lastSpeaker;
        }

        private void Rebuild()
        {
            string dim = UIFactory.Hex(_theme.debugDim);
            string bright = UIFactory.Hex(_theme.white);
            _sb.Clear();
            void Rule() => _sb.Append("<color=").Append(dim).Append(">────────────────────────────────────────</color>\n");
            void Label(string l) => _sb.Append("<color=").Append(dim).Append('>').Append(l).Append("</color>\n");

            _sb.Append("<b>OPENNPC DEBUG</b>").Append("<color=").Append(dim).Append(">   TAB to close</color>\n");
            Rule();

            NPCController npc = Focus();
            Persona p = npc != null ? npc.Persona.Persona : null;
            if (p == null)
            {
                Label("NPC");
                _sb.Append("— walk up to someone —\n");
            }
            else
            {
                Label("NPC"); _sb.Append(p.name).Append("   <color=").Append(dim).Append('>').Append(p.id).Append("</color>\n\n");
                Label("PERSONA"); _sb.Append(p.occupation).Append(p.IsGenerated ? "  (generated)" : "").Append("\n\n");
                Label("TRAITS");
                _sb.Append(string.Join("  ·  ", p.personalityTraits)).Append("\n\n");
                Label("MOOD"); _sb.Append(p.mood).Append("\n\n");
                Label("STATE"); _sb.Append(npc.State).Append("   ").Append(npc.Movement.CurrentSpeed.ToString("0.00")).Append(" m/s\n\n");
                Label("MEMORY"); _sb.Append(npc.Dialogue.History.Count).Append(" turns in context (")
                                   .Append(npc.Dialogue.History.TotalRecorded).Append(" total)\n\n");
            }
            Label("DIALOGUE PROVIDER");
            _sb.Append(_manager != null ? _manager.Provider.Name : "(none)").Append('\n');
            if (_config.provider == DialogueProviderType.OpenNPC)
                _sb.Append("<color=").Append(dim).Append('>').Append(_config.openNpcEndpoint).Append("</color>\n");
            Rule();

            if (_config.showPipeline)
            {
                Label("PIPELINE");
                int active = System.Array.IndexOf(Stages, _shownStage);
                bool waiting = _shownStage == PipelineStage.Provider;
                for (int i = 0; i < Stages.Length; i++)
                {
                    bool on = i == active;
                    bool done = active >= 0 && i < active;
                    string colour = on ? bright : done ? UIFactory.Hex(_theme.debugText) : dim;
                    _sb.Append("<color=").Append(colour).Append('>')
                       .Append(on ? " ▶ " : done ? " ✓ " : "   ")
                       .Append(on ? "<b>" : "").Append(StageLabels[i]).Append(on ? "</b>" : "");
                    if (on && waiting)
                        _sb.Append(new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3));
                    _sb.Append("</color>\n");
                    if (i < Stages.Length - 1)
                        _sb.Append("<color=").Append(dim).Append(">      │</color>\n");
                }
                if (_manager != null && _manager.LastRequest != null)
                {
                    _sb.Append('\n');
                    Label("LAST REQUEST");
                    DialogueRequest r = _manager.LastRequest;
                    _sb.Append("persona       ").Append(r.Persona.id).Append('\n')
                       .Append("event         ").Append(r.Context.dialogueEvent ?? "message").Append('\n')
                       .Append("history       ").Append(r.Conversation.Count).Append(" turns\n")
                       .Append("player_msg    \"").Append(Trim(r.PlayerMessage, 26)).Append("\"\n");
                    if (_manager.LastResponse != null)
                        _sb.Append("emotion       ").Append(_manager.LastResponse.Emotion).Append('\n')
                           .Append("latency       ").Append((_manager.LastLatency * 1000f).ToString("0")).Append(" ms\n");
                }
                Rule();
            }
            _sb.Append("<color=").Append(dim).Append('>')
               .Append("npcs ").Append(NPCInteraction.All.Count)
               .Append("   requests ").Append(_manager != null ? _manager.RequestCount : 0)
               .Append("   fps ").Append(_fps.ToString("0")).Append("</color>");
            _text.text = _sb.ToString();
            // Grow with the content so nothing spills past the panel.
            var root = (RectTransform)transform;
            float wanted = UIFactory.Measure(_text, _text.text, 512f).y + 56f;
            root.sizeDelta = new Vector2(root.sizeDelta.x, Mathf.Min(wanted, 1016f));
        }

        private static string Trim(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
