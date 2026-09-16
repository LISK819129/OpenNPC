using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using OpenNPC.Demo.CameraRig;
using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using OpenNPC.Demo.Population;
using OpenNPC.Demo.UI;
using OpenNPC.Dialogue;
using UnityEngine;

namespace OpenNPC.Demo.Dev
{
    /// <summary>
    /// Plays the success-criteria script inside a real build and records evidence:
    /// walk to an NPC, press E, ask "What do you think about this city?", walk to a
    /// second NPC, ask the same thing, open debug + persona panels, screenshot each
    /// step, and write every reply to a report.
    ///
    /// Enabled by the command-line flag <c>-autopilot</c> (desktop builds). Output:
    /// Application.persistentDataPath/autopilot/.
    /// </summary>
    public sealed class DemoAutopilot : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private ConversationController conversation;
        [SerializeField] private PopulationSpawner population;
        [SerializeField] private DemoUI ui;
        [SerializeField] private SideScrollCamera cameraRig;
        [SerializeField] private string[] targetNames = { "Ravi", "Maya", "Daniel" };
        [SerializeField] private string question = "What do you think about this city?";

        private readonly StringBuilder _report = new StringBuilder();
        private string _dir;
        private int _shot;
        private int _failures;

        public static bool Requested => Environment.GetCommandLineArgs().Contains("-autopilot");

        private void Start()
        {
            if (!Requested)
            {
                enabled = false;
                return;
            }
            _dir = Path.Combine(Application.persistentDataPath, "autopilot");
            Directory.CreateDirectory(_dir);
            foreach (string f in Directory.GetFiles(_dir))
                File.Delete(f);
            StartCoroutine(Run());
        }

        private void Log(string line)
        {
            Debug.Log("[AUTOPILOT] " + line);
            _report.AppendLine(line);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1.5f);
            // Harness only: a brisk player so fast walkers can be caught within the time limit.
            OpenNPC.Config.OpenNPCConfig.Load().playerSpeed *= 1.8f;
            Log($"population: {population.Spawned.Count} NPCs, provider: {DialogueManager.Instance.Provider.Name}");
            yield return Shoot("street");

            foreach (string targetName in targetNames)
            {
                NPCController npc = population.Spawned.FirstOrDefault(n => n.Persona.DisplayName == targetName);
                if (npc == null)
                {
                    Log($"FAIL: no NPC named {targetName}");
                    _failures++;
                    continue;
                }
                yield return WalkTo(npc);
                bool started = false;
                float deadline = Time.time + 6f;
                while (!started && Time.time < deadline)
                {
                    if (npc.Interaction.CanInteract && Vector3.Distance(Flat(player.transform.position), Flat(npc.transform.position)) <= npc.Interaction.InteractionRadius)
                    {
                        conversation.Begin(npc);
                        started = conversation.InConversation;
                    }
                    yield return null;
                }
                if (!started)
                {
                    Log($"FAIL: could not start a conversation with {targetName}");
                    _failures++;
                    continue;
                }
                yield return WaitForReply(npc, "opening");
                ui.Input.Submit(question);
                string reply = null;
                yield return WaitForReply(npc, question, r => reply = r);
                if (reply == null) _failures++;
                yield return new WaitForSeconds(2.2f);
                yield return Shoot($"talk_{targetName.ToLowerInvariant()}");

                if (targetName == targetNames[targetNames.Length - 1])
                {
                    ui.Debug.Toggle();
                    ui.Inspector.Show(npc.Persona.Persona);
                    yield return new WaitForSeconds(1.0f);
                    yield return Shoot("debug_and_persona");
                    ui.Debug.Toggle();
                    ui.Inspector.Hide();
                }
                conversation.End();
                yield return new WaitForSeconds(1.0f);
            }

            Log(_failures == 0 ? "RESULT: PASS" : $"RESULT: FAIL ({_failures})");
            File.WriteAllText(Path.Combine(_dir, "report.txt"), _report.ToString());
            yield return new WaitForSeconds(0.5f);
            Application.Quit(_failures == 0 ? 0 : 1);
        }

        private IEnumerator WaitForReply(NPCController npc, string label, Action<string> onReply = null)
        {
            DialogueResponse before = npc.Dialogue.LastResponse;
            float deadline = Time.time + 10f;
            while (npc.Dialogue.LastResponse == before && Time.time < deadline)
                yield return null;
            if (npc.Dialogue.LastResponse == before)
            {
                Log($"FAIL: {npc.Persona.DisplayName} did not reply to '{label}'");
                yield break;
            }
            string text = npc.Dialogue.LastResponse.Text;
            Log($"{npc.Persona.DisplayName,-8} <- \"{label}\"\n         -> \"{text}\"  [{npc.Dialogue.LastResponse.Emotion}]");
            onReply?.Invoke(text);
        }

        private IEnumerator WalkTo(NPCController npc)
        {
            float deadline = Time.time + 25f;
            while (Time.time < deadline)
            {
                Vector3 to = Flat(npc.transform.position) - Flat(player.transform.position);
                // Aim slightly ahead of where the NPC is walking.
                to += new Vector3(npc.Movement.Direction * npc.Movement.CurrentSpeed * 0.6f, 0f, 0f);
                Vector3 goal = to - to.normalized * 0.9f;
                if (goal.magnitude < 0.25f)
                    break;
                player.ScriptedInput = new Vector2(goal.x, goal.z).normalized;
                yield return null;
            }
            player.ScriptedInput = Vector2.zero;
            yield return new WaitForSeconds(0.2f);
            player.ScriptedInput = null;
        }

        private IEnumerator Shoot(string label)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_dir, $"{_shot++:00}_{label}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log("screenshot " + path);
            yield return null;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
