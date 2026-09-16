using OpenNPC.Personas;
using UnityEngine;

namespace OpenNPC.Demo.NPC
{
    /// <summary>
    /// Holds the persona that makes this NPC a specific person. Assigned at spawn
    /// time by the population system; the prefab itself has no personality.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCPersona : MonoBehaviour
    {
        [SerializeField] private string debugPersonaId;

        public Persona Persona { get; private set; }
        public string NpcId => Persona?.id;
        public string DisplayName => Persona?.name ?? name;

        public void Assign(Persona persona)
        {
            Persona = persona;
            debugPersonaId = persona?.id;
            gameObject.name = persona != null ? $"NPC_{persona.name}_{persona.id}" : "NPC_Unassigned";
        }

        /// <summary>Runtime mood changes (e.g. from a provider response) live on the persona object.</summary>
        public void SetMood(string mood)
        {
            if (Persona != null && !string.IsNullOrEmpty(mood))
                Persona.mood = mood;
        }
    }
}
