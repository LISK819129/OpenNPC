using System.Collections.Generic;
using System.Linq;
using OpenNPC.Config;
using OpenNPC.Demo.CameraRig;
using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using OpenNPC.Demo.Population;
using OpenNPC.Demo.UI;
using OpenNPC.Dialogue;
using OpenNPC.Dialogue.Providers;
using OpenNPC.Personas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// Step 5: headless checks of everything that can be checked without playing.
    /// The key one is the demo's thesis: the same question to different personas
    /// must produce different answers. Prints "[OPENNPC] VERIFY: ALL PASS".
    /// </summary>
    public static class DemoVerify
    {
        private static int _failures;

        [MenuItem("OpenNPC/5. Verify")]
        public static void Run()
        {
            _failures = 0;
            Personas();
            Dialogue();
            WireFormat();
            Assets();
            Scene();
            Debug.Log(_failures == 0 ? "[OPENNPC] VERIFY: ALL PASS" : $"[OPENNPC] VERIFY: {_failures} FAILED");
            if (Application.isBatchMode && _failures > 0)
                EditorApplication.Exit(1);
        }

        private static void Check(bool ok, string what)
        {
            Debug.Log((ok ? "[OPENNPC]   ok   " : "[OPENNPC]   FAIL ") + what);
            if (!ok) _failures++;
        }

        private static List<Persona> LoadAuthored()
        {
            var bundle = AssetDatabase.LoadAssetAtPath<TextAsset>(DemoPaths.PersonaBundle);
            return bundle == null ? new List<Persona>() : new PersonaLibrary(bundle.text, 1).Authored.ToList();
        }

        private static void Personas()
        {
            List<Persona> authored = LoadAuthored();
            Check(authored.Count >= 6, $"persona bundle has {authored.Count} authored personas (need >= 6)");
            foreach (string required in new[] { "Ravi", "Maya", "Daniel", "Anjali", "Arjun", "Priya" })
                Check(authored.Any(p => p.name == required), "persona present: " + required);
            Check(authored.Select(p => p.id).Distinct().Count() == authored.Count, "persona ids are unique");

            var library = new PersonaLibrary(AssetDatabase.LoadAssetAtPath<TextAsset>(DemoPaths.PersonaBundle).text, 2026);
            var ids = new HashSet<string>();
            for (int i = 0; i < 40; i++)
                ids.Add(library.Next().id);
            Check(ids.Count == 40, "40 NPCs get 40 distinct personas (authored + generated)");
        }

        private static void Dialogue()
        {
            List<Persona> authored = LoadAuthored();
            var mock = new MockDialogueProvider();
            foreach (string question in new[] { "What do you think about this city?", "Where is the train station?", "What do you do?" })
            {
                var answers = new Dictionary<string, string>();
                foreach (Persona p in authored)
                {
                    DialogueResponse r = mock.Respond(new DialogueRequest(p, new DialogueContext(), null, question));
                    answers[p.name] = r.Text;
                    Debug.Log($"[OPENNPC]        {p.name,-12} \"{question}\" -> \"{r.Text}\" [{r.Emotion}]");
                }
                Check(answers.Values.All(a => !string.IsNullOrWhiteSpace(a)), $"every persona answers \"{question}\"");
                Check(answers.Values.Distinct().Count() == answers.Count, $"all {answers.Count} answers to \"{question}\" are different");
            }

            // Memory: the player's name survives in conversation history.
            Persona ravi = authored.First(p => p.name == "Ravi");
            var history = new ConversationHistory(12);
            string Ask(string msg)
            {
                DialogueResponse r = mock.Respond(new DialogueRequest(ravi, new DialogueContext(), history.Snapshot(), msg));
                history.Add(ConversationRole.Player, msg);
                history.Add(ConversationRole.Npc, r.Text);
                Debug.Log($"[OPENNPC]        memory: \"{msg}\" -> \"{r.Text}\"");
                return r.Text;
            }
            Ask("Hi, my name is Alex");
            Ask("Where is the train station?");
            Check(Ask("What's my name?").Contains("Alex"), "mock provider recalls the player's name from history");
            Check(Ask("Where is the train station?").StartsWith("Told you"), "mock provider notices a repeated question");

            DialogueResponse opening = mock.Respond(new DialogueRequest(ravi, new DialogueContext { dialogueEvent = DialogueRequest.EventConversationStart }, null, null));
            Check(!string.IsNullOrEmpty(opening.Text), "conversation_start event produces an opening line: " + opening.Text);
        }

        private static void WireFormat()
        {
            Persona maya = LoadAuthored().First(p => p.name == "Maya");
            var history = new List<ConversationTurn> { new ConversationTurn(ConversationRole.Player, "hello") };
            string json = OpenNPCDialogueProvider.ToJson(new DialogueRequest(maya, new DialogueContext { location = "Market Street" }, history, "Where is the train station?"));
            Check(json.Contains("\"player_message\":\"Where is the train station?\""), "wire request uses snake_case player_message");
            Check(json.Contains("\"role\":\"player\""), "wire request encodes conversation roles as strings");
            Check(json.Contains("\"personalityTraits\""), "wire request embeds the full persona");
            DialogueResponse parsed = OpenNPCDialogueProvider.FromJson("{\"text\":\"Three streets ahead.\",\"emotion\":\"neutral\"}");
            Check(parsed.Text == "Three streets ahead." && !parsed.IsError, "wire response parses text/emotion");
            Check(OpenNPCDialogueProvider.FromJson("{}").IsError, "empty backend response is reported as an error, not faked");
            Debug.Log("[OPENNPC]        request json: " + (json.Length > 300 ? json.Substring(0, 300) + "…" : json));
        }

        private static void Assets()
        {
            string[] clips = AssetDatabase.LoadAllAssetsAtPath(DemoPaths.CharacterModel).OfType<AnimationClip>()
                                          .Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name).ToArray();
            foreach (string c in new[] { "Idle", "Walk", "Walk_Fast", "Walk_Slow", "Stop", "Turn_Left", "Turn_Right", "Talk" })
                Check(clips.Contains(c), "FBX clip: " + c);

            var npc = AssetDatabase.LoadAssetAtPath<GameObject>(DemoPaths.NpcPrefab);
            Check(npc != null, "NPC.prefab exists");
            if (npc != null)
            {
                foreach (System.Type t in new[] { typeof(NPCController), typeof(NPCPersona), typeof(NPCMovement),
                                                  typeof(NPCInteraction), typeof(NPCDialogue), typeof(NPCAnimation) })
                    Check(npc.GetComponent(t) != null, "NPC.prefab has " + t.Name);
                Check(npc.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 2, "NPC.prefab uses the Blender body + outline meshes");
                Check(npc.GetComponentInChildren<Animator>()?.runtimeAnimatorController != null, "NPC.prefab animator has a controller");
            }
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(DemoPaths.Theme);
            Check(theme != null && theme.display != null && theme.mono != null, "UI theme fonts assigned");
            var config = AssetDatabase.LoadAssetAtPath<OpenNPCConfig>(DemoPaths.ConfigAsset);
            Check(config != null && config.personaBundle != null, "config references the persona bundle");
            Check(config != null && config.provider == DialogueProviderType.Mock, "default provider is Mock (no keys needed)");
        }

        private static void Scene()
        {
            EditorSceneManager.OpenScene(DemoPaths.Scene, OpenSceneMode.Single);
            Check(Object.FindFirstObjectByType<DialogueManager>() != null, "scene has DialogueManager");
            Check(Object.FindFirstObjectByType<PopulationSpawner>() != null, "scene has PopulationSpawner");
            Check(Object.FindFirstObjectByType<PlayerController>() != null, "scene has Player");
            Check(Object.FindFirstObjectByType<SideScrollCamera>() != null, "scene has SideScrollCamera");
            Check(Object.FindFirstObjectByType<DemoUI>() != null, "scene has DemoUI");
            Check(Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null, "scene has EventSystem");
            Check(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == DemoPaths.Scene, "scene is in build settings");
        }
    }
}
