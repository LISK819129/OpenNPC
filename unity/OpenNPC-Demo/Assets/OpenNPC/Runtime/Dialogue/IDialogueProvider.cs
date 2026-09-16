using System;

namespace OpenNPC.Dialogue
{
    /// <summary>
    /// The integration boundary. An NPC never knows whether its words come from
    /// templates, a local model, an OpenAI-compatible server or the OpenNPC
    /// backend — it only ever talks to this interface through DialogueManager.
    ///
    /// Callback-based on purpose: Unity WebGL has no threads, and a callback works
    /// the same for a synchronous mock and for a UnityWebRequest in flight.
    /// </summary>
    public interface IDialogueProvider
    {
        /// <summary>Shown in debug tools.</summary>
        string Name { get; }

        /// <summary>
        /// Produce one NPC line. <paramref name="onComplete"/> must be invoked exactly
        /// once, on the main thread, even on failure (use DialogueResponse.Error).
        /// </summary>
        void Generate(DialogueRequest request, Action<DialogueResponse> onComplete);
    }
}
