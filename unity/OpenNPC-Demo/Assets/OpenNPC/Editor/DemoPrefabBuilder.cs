using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// Step 3: NPC.prefab and Player.prefab, both wrapping the SAME Blender FBX.
    ///   NPC    = ink body, white rim   (a person with a persona)
    ///   Player = white body, ink rim   (a person without one)
    /// Components are separate MonoBehaviours; the model is a child so the
    /// logic root stays unscaled-by-import and easy to reason about.
    /// </summary>
    public static class DemoPrefabBuilder
    {
        [MenuItem("OpenNPC/3. Build Prefabs")]
        public static void Run()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(DemoPaths.CharacterModel);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DemoPaths.AnimatorController);
            if (fbx == null || controller == null)
            {
                Debug.LogError("[OPENNPC] Build the animator and import the FBX before prefabs.");
                return;
            }

            float yawFix = MeasureForwardCorrection(fbx);
            Debug.Log($"[OPENNPC] Model forward correction: {yawFix:0} deg");

            // --- NPC ---
            var npc = new GameObject("NPC");
            BuildModel(npc.transform, fbx, controller, yawFix, DemoProjectSetup.Mat("M_Ink"), DemoProjectSetup.Mat("M_Outline_White"));
            npc.AddComponent<NPCPersona>();
            npc.AddComponent<NPCMovement>();
            var npcAnim = npc.AddComponent<NPCAnimation>();
            SetAnimator(npcAnim, npc);
            npc.AddComponent<NPCInteraction>();
            npc.AddComponent<NPCDialogue>();
            npc.AddComponent<NPCController>();
            PrefabUtility.SaveAsPrefabAsset(npc, DemoPaths.NpcPrefab);
            Object.DestroyImmediate(npc);

            // --- Player ---
            var player = new GameObject("Player");
            BuildModel(player.transform, fbx, controller, yawFix, DemoProjectSetup.Mat("M_White"), DemoProjectSetup.Mat("M_Outline_Ink"));
            var playerAnim = player.AddComponent<NPCAnimation>();
            SetAnimator(playerAnim, player);
            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerInteractor>();
            var conv = player.AddComponent<ConversationController>();
            var so = new SerializedObject(conv);
            so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            so.FindProperty("interactor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, DemoPaths.PlayerPrefab);
            Object.DestroyImmediate(player);

            AssetDatabase.SaveAssets();
            Debug.Log("[OPENNPC] Prefabs built: " + DemoPaths.NpcPrefab + ", " + DemoPaths.PlayerPrefab);
        }

        private static void BuildModel(Transform parent, GameObject fbx, AnimatorController controller, float yawFix,
                                       Material body, Material outline)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Model";
            model.transform.SetParent(parent, false);
            model.transform.localRotation = Quaternion.Euler(0f, yawFix, 0f);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();   // Unity's fake-null defeats ??
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = r.name.Contains("Outline") ? outline : body;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.skinnedMotionVectors = false;
            }

            // Ground contact: a flat pale ellipse. Cheaper and calmer than real shadows.
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "Shadow";
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.transform.SetParent(parent, false);
            shadow.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            shadow.transform.localScale = new Vector3(0.7f, 0.001f, 0.34f);
            var mr = shadow.GetComponent<MeshRenderer>();
            mr.sharedMaterial = DemoProjectSetup.Mat("M_Shade");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static void SetAnimator(NPCAnimation anim, GameObject root)
        {
            var so = new SerializedObject(anim);
            so.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Which way does the imported character face? The feet point forward, so
        /// the average of the lowest vertices sits in front of the ankles. Returns
        /// the yaw that turns the model to face +Z, snapped to 90 degrees.
        /// </summary>
        public static float MeasureForwardCorrection(GameObject fbx)
        {
            var instance = (GameObject)Object.Instantiate(fbx);
            try
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                var baked = new Mesh();
                foreach (SkinnedMeshRenderer r in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (r.name.Contains("Outline")) continue;
                    r.BakeMesh(baked, true);
                    foreach (Vector3 v in baked.vertices)
                    {
                        Vector3 w = r.transform.TransformPoint(v);
                        if (w.y < 0.1f) { sum += w; count++; }
                    }
                }
                Object.DestroyImmediate(baked);
                if (count == 0) return 0f;
                Vector3 mean = sum / count;
                float yaw = Mathf.Atan2(mean.x, mean.z) * Mathf.Rad2Deg;   // current facing
                return Mathf.Round(-yaw / 90f) * 90f;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
