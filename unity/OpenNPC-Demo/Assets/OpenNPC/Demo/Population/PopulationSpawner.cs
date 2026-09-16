using System.Collections.Generic;
using OpenNPC.Config;
using OpenNPC.Demo.NPC;
using OpenNPC.Personas;
using UnityEngine;

namespace OpenNPC.Demo.Population
{
    /// <summary>
    /// Spawns the street population from one NPC prefab. Every NPC gets its own
    /// persona and its own physical individuality (scale, head size, pace, lane,
    /// direction, animation phase) — one species, many individuals.
    /// </summary>
    public sealed class PopulationSpawner : MonoBehaviour
    {
        [SerializeField] private NPCController npcPrefab;
        [Tooltip("Authored personas are placed within this distance of the player start so testers meet them first.")]
        [SerializeField] private float authoredSpread = 14f;
        [SerializeField] private int seed = 7;

        private readonly List<NPCController> _spawned = new List<NPCController>();

        public IReadOnlyList<NPCController> Spawned => _spawned;
        public PersonaLibrary Library { get; private set; }

        private void Start()
        {
            if (npcPrefab == null)
            {
                Debug.LogError("[OpenNPC] PopulationSpawner has no NPC prefab.");
                return;
            }
            OpenNPCConfig config = OpenNPCConfig.Load();
            Library = new PersonaLibrary(config.personaBundle != null ? config.personaBundle.text : null,
                                         config.personaGeneratorSeed);
            var rng = new System.Random(seed);

            int total = config.npcCount + config.backgroundNpcCount;
            for (int i = 0; i < total; i++)
            {
                bool background = i >= config.npcCount;
                Persona persona = Library.Next();
                float[] lanes = background ? config.backgroundLaneDepths : config.laneDepths;
                float lane = lanes.Length > 0 ? lanes[i % lanes.Length] : 1.5f;

                float half = config.streetLength * 0.5f;
                float x = !background && !persona.IsGenerated
                    ? Mathf.Lerp(-authoredSpread, authoredSpread, (float)rng.NextDouble())
                    : Mathf.Lerp(-half, half, (float)rng.NextDouble());
                float speed = Mathf.Lerp(config.walkSpeedRange.x, config.walkSpeedRange.y, (float)rng.NextDouble());
                if (background)
                    speed *= 0.85f;
                int direction = rng.NextDouble() < 0.5 ? 1 : -1;

                NPCController npc = Instantiate(npcPrefab, transform);
                npc.Movement.Initialize(x, lane, speed, direction, config.streetLength);
                npc.Initialize(persona, background, seed * 7919 + i);

                float scale = Mathf.Lerp(config.scaleRange.x, config.scaleRange.y, (float)rng.NextDouble());
                float height = Mathf.Lerp(0.94f, 1.07f, (float)rng.NextDouble());
                npc.transform.localScale = new Vector3(scale, scale * height, scale);
                npc.Animation.SetIndividuality(
                    headScale: Mathf.Lerp(0.88f, 1.15f, (float)rng.NextDouble()),
                    playbackRate: Mathf.Lerp(0.92f, 1.08f, (float)rng.NextDouble()),
                    cycleOffset: (float)rng.NextDouble());
                _spawned.Add(npc);
            }
            Debug.Log($"[OpenNPC] Spawned {_spawned.Count} NPCs ({Library.Authored.Count} authored personas).");
        }
    }
}
