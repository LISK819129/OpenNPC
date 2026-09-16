using OpenNPC.Demo.NPC;
using UnityEngine;

namespace OpenNPC.Demo.Player
{
    /// <summary>Finds the closest NPC the player can talk to right now.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("Depth (z) differences count less than street (x) distance: the camera looks along z.")]
        [SerializeField] private float depthWeight = 0.7f;

        public NPCInteraction Target { get; private set; }

        private void Update()
        {
            Target = FindClosest();
        }

        public NPCInteraction FindClosest()
        {
            NPCInteraction best = null;
            float bestDistance = float.MaxValue;
            Vector3 me = transform.position;
            foreach (NPCInteraction candidate in NPCInteraction.All)
            {
                if (!candidate.CanInteract)
                    continue;
                Vector3 d = candidate.Position - me;
                float distance = Mathf.Sqrt(d.x * d.x + d.z * d.z * depthWeight * depthWeight);
                if (distance <= candidate.InteractionRadius && distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
