using System.Collections.Generic;
using UnityEngine;

namespace OpenNPC.Demo.NPC
{
    /// <summary>
    /// Makes an NPC talkable. Keeps a static registry so the player can find
    /// nearby NPCs without physics queries.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCInteraction : MonoBehaviour, IInteractable
    {
        private static readonly List<NPCInteraction> Registry = new List<NPCInteraction>();
        public static IReadOnlyList<NPCInteraction> All => Registry;

        [SerializeField] private float promptHeight = 2.25f;

        private NPCController _controller;
        private float _radius = 1.7f;
        private float _cooldownUntil;

        public NPCController Controller => _controller;
        public string DisplayName => _controller != null ? _controller.Persona.DisplayName : name;
        public Vector3 Position => transform.position;
        public Vector3 PromptAnchor => transform.position + Vector3.up * promptHeight * transform.lossyScale.y;
        public float InteractionRadius => _radius;

        public bool CanInteract =>
            isActiveAndEnabled && _controller != null && !_controller.IsBackground &&
            _controller.State != NPCState.Talking && Time.time >= _cooldownUntil;

        private void Awake() => _controller = GetComponent<NPCController>();
        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        public void Configure(float radius) => _radius = radius;
        public void StartCooldown(float seconds) => _cooldownUntil = Time.time + seconds;
    }
}
