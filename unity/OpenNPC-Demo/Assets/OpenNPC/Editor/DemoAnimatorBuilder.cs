using System.IO;
using System.Linq;
using OpenNPC.Demo.NPC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// Step 2: the stickman AnimatorController, generated from the clips inside the
    /// Blender FBX. Walk speeds come from blender/exports/stickman_clips.json, so the
    /// blend thresholds always match the stride lengths the clips were authored with.
    /// </summary>
    public static class DemoAnimatorBuilder
    {
        [System.Serializable]
        private sealed class ClipInfo { public string name; public int frames; public bool loop; public float nativeSpeed; }
        [System.Serializable]
        private sealed class ClipManifest { public int fps; public float height; public ClipInfo[] clips; }

        [MenuItem("OpenNPC/2. Build Animator")]
        public static void Run()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(DemoPaths.CharacterModel)
                                                 .OfType<AnimationClip>()
                                                 .Where(c => !c.name.StartsWith("__preview__"))
                                                 .ToArray();
            AnimationClip Clip(string n)
            {
                AnimationClip c = clips.FirstOrDefault(x => x.name == n);
                if (c == null) Debug.LogError($"[OPENNPC] Clip '{n}' missing from {DemoPaths.CharacterModel}");
                return c;
            }

            float Speed(string n, float fallback)
            {
                if (!File.Exists(DemoPaths.ExternalClipManifest)) return fallback;
                var manifest = JsonUtility.FromJson<ClipManifest>(File.ReadAllText(DemoPaths.ExternalClipManifest));
                ClipInfo info = manifest.clips?.FirstOrDefault(c => c.name == n);
                return info != null && info.nativeSpeed > 0f ? info.nativeSpeed : fallback;
            }

            AssetDatabase.DeleteAsset(DemoPaths.AnimatorController);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(DemoPaths.AnimatorController);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("TurnLeft", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TurnRight", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Stop", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle"), 0f);
            tree.AddChild(Clip("Walk_Slow"), Speed("Walk_Slow", 0.88f));
            tree.AddChild(Clip("Walk"), Speed("Walk", 1.6f));
            tree.AddChild(Clip("Walk_Fast"), Speed("Walk_Fast", 2.55f));
            sm.defaultState = locomotion;

            AnimatorState talk = sm.AddState("Talk");
            talk.motion = Clip("Talk");
            Transition(locomotion, talk, 0.2f).AddCondition(AnimatorConditionMode.If, 0f, "Talking");
            Transition(talk, locomotion, 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");

            foreach ((string clipName, string trigger) in new[] { ("Turn_Left", "TurnLeft"), ("Turn_Right", "TurnRight"), ("Stop", "Stop") })
            {
                AnimatorState s = sm.AddState(clipName);
                s.motion = Clip(clipName);
                Transition(locomotion, s, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, trigger);
                AnimatorStateTransition back = Transition(s, locomotion, 0.15f);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OPENNPC] Animator built with {clips.Length} clips: {string.Join(", ", clips.Select(c => c.name))}");

            // Keep the component's parameter names honest.
            foreach (string p in new[] { "Speed", "Talking", "TurnLeft", "TurnRight", "Stop" })
                if (controller.parameters.All(x => Animator.StringToHash(x.name) != Animator.StringToHash(p)))
                    Debug.LogError("[OPENNPC] Animator parameter missing: " + p);
            _ = NPCAnimation.SpeedParam;
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.hasFixedDuration = true;
            return t;
        }
    }
}
