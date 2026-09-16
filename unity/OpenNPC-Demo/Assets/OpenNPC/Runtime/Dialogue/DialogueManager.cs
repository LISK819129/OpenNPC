using System;
using OpenNPC.Config;
using OpenNPC.Dialogue.Providers;
using UnityEngine;

namespace OpenNPC.Dialogue
{
    /// <summary>Stages of one request, in order. The debug panel visualises these.</summary>
    public enum PipelineStage
    {
        Idle,
        PlayerMessage,
        Persona,
        Context,
        Provider,
        Response,
        Npc,
    }

    /// <summary>
    /// The only object in a scene that knows which IDialogueProvider is active.
    /// NPCs hand it a DialogueRequest and get a DialogueResponse back; tools
    /// subscribe to its events to observe traffic without touching NPC code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        public IDialogueProvider Provider { get; private set; }
        public PipelineStage Stage { get; private set; }
        public DialogueRequest LastRequest { get; private set; }
        public DialogueResponse LastResponse { get; private set; }
        public float LastLatency { get; private set; }
        public int RequestCount { get; private set; }

        /// <summary>Raised whenever <see cref="Stage"/> changes.</summary>
        public event Action<PipelineStage> StageChanged;
        public event Action<DialogueRequest> RequestStarted;
        public event Action<DialogueRequest, DialogueResponse> ResponseReceived;

        private OpenNPCConfig _config;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[OpenNPC] Duplicate DialogueManager disabled.");
                enabled = false;
                return;
            }
            Instance = this;
            _config = OpenNPCConfig.Load();
            SetProvider(CreateProvider(_config));
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Replace the provider at runtime (e.g. from a settings menu or a test).</summary>
        public void SetProvider(IDialogueProvider provider)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Debug.Log("[OpenNPC] Dialogue provider: " + Provider.Name);
        }

        public IDialogueProvider CreateProvider(OpenNPCConfig config)
        {
            switch (config.provider)
            {
                case DialogueProviderType.OpenNPC:
                    return new OpenNPCDialogueProvider(this, config.openNpcEndpoint, config.requestTimeoutSeconds);
                default:
                    return new MockDialogueProvider(this, config.mockLatency);
            }
        }

        public void Request(DialogueRequest request, Action<DialogueResponse> onComplete)
        {
            LastRequest = request;
            RequestCount++;
            float started = Time.realtimeSinceStartup;
            RequestStarted?.Invoke(request);

            // The first three stages are synchronous work in the engine; they are
            // reported individually so the pipeline view reads as a pipeline.
            SetStage(PipelineStage.PlayerMessage);
            SetStage(PipelineStage.Persona);
            SetStage(PipelineStage.Context);
            SetStage(PipelineStage.Provider);

            bool completed = false;
            Provider.Generate(request, response =>
            {
                if (completed)
                {
                    Debug.LogWarning($"[OpenNPC] {Provider.Name} invoked its callback twice; ignoring.");
                    return;
                }
                completed = true;
                LastLatency = Time.realtimeSinceStartup - started;
                LastResponse = response;
                SetStage(PipelineStage.Response);
                ResponseReceived?.Invoke(request, response);
                onComplete?.Invoke(response);
                SetStage(PipelineStage.Npc);
            });
        }

        private void SetStage(PipelineStage stage)
        {
            Stage = stage;
            StageChanged?.Invoke(stage);
        }
    }
}
