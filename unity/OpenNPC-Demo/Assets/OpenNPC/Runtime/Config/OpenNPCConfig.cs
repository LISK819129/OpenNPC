using System;
using UnityEngine;

namespace OpenNPC.Config
{
    public enum DialogueProviderType
    {
        /// <summary>Offline, persona-driven templates. No network, no keys.</summary>
        Mock,
        /// <summary>HTTP JSON to an OpenNPC backend (see docs/web-demo.md).</summary>
        OpenNPC,
    }

    /// <summary>
    /// The single place to tune the demo. Lives at Resources/OpenNPC/OpenNPCConfig.
    /// In WebGL builds the provider and endpoint can also be overridden from the page
    /// URL: <c>?provider=opennpc&amp;endpoint=https://your-server/v1/dialogue</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "OpenNPC/Config", fileName = "OpenNPCConfig")]
    public sealed class OpenNPCConfig : ScriptableObject
    {
        public const string ResourcePath = "OpenNPC/OpenNPCConfig";

        [Header("Population")]
        [Tooltip("NPCs on the walkable street (interactable).")]
        [Range(1, 40)] public int npcCount = 18;
        [Tooltip("Extra NPCs on the far pavement. They have personas but are out of reach.")]
        [Range(0, 20)] public int backgroundNpcCount = 7;
        [Tooltip("Walking speed range in m/s. Clips blend Walk_Slow / Walk / Walk_Fast inside this.")]
        public Vector2 walkSpeedRange = new Vector2(0.75f, 2.3f);
        [Tooltip("Street length the population walks along, centred on x = 0.")]
        public float streetLength = 48f;
        [Tooltip("Depths (z) of the walkable lanes. More lanes = lower density.")]
        public float[] laneDepths = { 0.6f, 1.5f, 2.4f, 3.3f };
        [Tooltip("Depths (z) of the out-of-reach background lanes.")]
        public float[] backgroundLaneDepths = { 7.2f, 8.1f };
        [Tooltip("Seconds between an NPC's behaviour decisions (stop, turn, change pace).")]
        public Vector2 decisionInterval = new Vector2(3f, 9f);
        [Range(0f, 1f)] public float stopChance = 0.22f;
        [Range(0f, 1f)] public float turnChance = 0.16f;
        [Range(0f, 1f)] public float paceChangeChance = 0.3f;
        [Range(0f, 1f)] public float laneChangeChance = 0.18f;
        public Vector2 idleDuration = new Vector2(1.5f, 4.5f);
        [Tooltip("Uniform scale variation per NPC.")]
        public Vector2 scaleRange = new Vector2(0.9f, 1.08f);

        [Header("Player & Interaction")]
        public float playerSpeed = 2.4f;
        [Tooltip("How close the player must be (metres, XZ) to talk to an NPC.")]
        public float interactionRadius = 1.7f;
        [Tooltip("NPCs will not accept a new conversation for this long after one ends.")]
        public float conversationCooldown = 2.5f;

        [Header("Dialogue")]
        public DialogueProviderType provider = DialogueProviderType.Mock;
        [Tooltip("OpenNPC backend endpoint used by OpenNPCDialogueProvider. Never put API keys here.")]
        public string openNpcEndpoint = "http://localhost:8787/v1/dialogue";
        public float requestTimeoutSeconds = 20f;
        [Tooltip("Turns of history sent with each request.")]
        [Range(2, 40)] public int historyTurns = 12;
        [Tooltip("Mock provider 'thinking' delay, so the pipeline is visible in the debug panel.")]
        public Vector2 mockLatency = new Vector2(0.35f, 0.8f);
        [Tooltip("Seconds a speech bubble stays after the conversation ends.")]
        public float dialogueDuration = 5f;
        public float typewriterCharsPerSecond = 55f;
        [Tooltip("Personas bundle (synced from framework/examples/personas by the editor pipeline).")]
        public TextAsset personaBundle;
        public int personaGeneratorSeed = 2026;
        public string locationName = "Market Street";

        [Header("Debug")]
        public bool debugPanelOnStart;
        public bool showPipeline = true;

        private static OpenNPCConfig _cached;

        /// <summary>Loads the project config, or an in-memory default if none exists.</summary>
        public static OpenNPCConfig Load()
        {
            if (_cached != null)
                return _cached;
            _cached = Resources.Load<OpenNPCConfig>(ResourcePath);
            if (_cached == null)
            {
                Debug.LogWarning("[OpenNPC] No config at Resources/" + ResourcePath + "; using defaults.");
                _cached = CreateInstance<OpenNPCConfig>();
            }
            _cached.ApplyUrlOverrides(Application.absoluteURL);
            return _cached;
        }

        /// <summary>?provider=mock|opennpc&amp;endpoint=...&amp;debug=1</summary>
        public void ApplyUrlOverrides(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            int q = url.IndexOf('?');
            if (q < 0)
                return;
            foreach (string pair in url.Substring(q + 1).Split('&'))
            {
                string[] kv = pair.Split(new[] { '=' }, 2);
                if (kv.Length != 2)
                    continue;
                string key = Uri.UnescapeDataString(kv[0]).ToLowerInvariant();
                string value = Uri.UnescapeDataString(kv[1]);
                switch (key)
                {
                    case "provider":
                        if (Enum.TryParse(value, true, out DialogueProviderType p))
                            provider = p;
                        break;
                    case "endpoint":
                        if (value.StartsWith("http://") || value.StartsWith("https://"))
                            openNpcEndpoint = value;
                        break;
                    case "debug":
                        debugPanelOnStart = value == "1" || value == "true";
                        break;
                }
            }
        }
    }
}
