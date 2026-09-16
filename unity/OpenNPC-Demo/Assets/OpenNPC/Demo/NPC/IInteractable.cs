using UnityEngine;

namespace OpenNPC.Demo.NPC
{
    /// <summary>Anything the player can walk up to and press E on.</summary>
    public interface IInteractable
    {
        string DisplayName { get; }
        /// <summary>World position the "[E] Talk to …" prompt hangs from.</summary>
        Vector3 PromptAnchor { get; }
        Vector3 Position { get; }
        bool CanInteract { get; }
        float InteractionRadius { get; }
    }
}
