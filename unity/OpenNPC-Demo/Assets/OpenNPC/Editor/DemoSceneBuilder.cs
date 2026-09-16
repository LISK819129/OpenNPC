using System.IO;
using OpenNPC.Demo.CameraRig;
using OpenNPC.Demo.Dev;
using OpenNPC.Demo.NPC;
using OpenNPC.Demo.Player;
using OpenNPC.Demo.Population;
using OpenNPC.Demo.UI;
using OpenNPC.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// Step 4: the OpenNPC_Street scene, regenerated from code. A side-on street
    /// drawn in ink lines like an illustration: walkable pavement at the front,
    /// a road, a far pavement for background NPCs, and a row of shop fronts.
    /// Hand edits to the scene are overwritten; change this file instead.
    /// </summary>
    public static class DemoSceneBuilder
    {
        // Street layout (metres). Front (camera side) to back.
        private const float PavementFront = -0.5f;
        private const float Kerb = 4.4f;
        private const float RoadCentre = 5.5f;
        private const float FarKerb = 6.6f;
        private const float BuildingLine = 9.4f;
        private const float HalfLength = 34f;
        private const float NearRoadCentre = -2.9f;
        private const float NearKerb = -5.2f;
        private const float Line = 0.05f;

        private static readonly (string sign, float width, float height)[] Buildings =
        {
            ("BOOKS", 6f, 5.5f), ("", 5f, 8.5f), ("PHARMACY", 7f, 4.8f), ("", 4.5f, 7f), ("CAFÉ", 6f, 5f),
            ("NOODLES", 5.5f, 6.2f), ("", 6.5f, 9.5f), ("HOTEL", 7f, 8f), ("", 5f, 5.5f), ("STATION", 11f, 6.8f),
        };

        [MenuItem("OpenNPC/4. Build Scene")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DemoPaths.Scene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject environment = BuildEnvironment();
            environment.isStatic = true;

            var systems = new GameObject("Systems");
            systems.AddComponent<DialogueManager>();

            var npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemoPaths.NpcPrefab);
            var population = new GameObject("Population").AddComponent<PopulationSpawner>();
            var popSo = new SerializedObject(population);
            popSo.FindProperty("npcPrefab").objectReferenceValue = npcPrefab.GetComponent<NPCController>();
            popSo.ApplyModifiedPropertiesWithoutUndo();

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemoPaths.PlayerPrefab);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 0f, 1.9f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = DemoProjectSetup.Paper;
            cam.fieldOfView = 31f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            cam.allowMSAA = true;
            cam.allowHDR = false;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;
            camData.renderShadows = false;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<SideScrollCamera>();
            rig.Target = player.transform;
            rig.SnapToTarget();

            // UI
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var ui = new GameObject("UI").AddComponent<DemoUI>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UITheme>(DemoPaths.Theme);
            uiSo.FindProperty("worldCamera").objectReferenceValue = cam;
            uiSo.FindProperty("cameraRig").objectReferenceValue = rig;
            uiSo.FindProperty("conversation").objectReferenceValue = player.GetComponent<ConversationController>();
            uiSo.FindProperty("interactor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            var autopilot = new GameObject("DevAutopilot").AddComponent<DemoAutopilot>();
            var apSo = new SerializedObject(autopilot);
            apSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            apSo.FindProperty("conversation").objectReferenceValue = player.GetComponent<ConversationController>();
            apSo.FindProperty("population").objectReferenceValue = population;
            apSo.FindProperty("ui").objectReferenceValue = ui;
            apSo.FindProperty("cameraRig").objectReferenceValue = rig;
            apSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, DemoPaths.Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DemoPaths.Scene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[OPENNPC] Scene built: " + DemoPaths.Scene);
        }

        // ------------------------------------------------------------------ //
        private static GameObject BuildEnvironment()
        {
            var root = new GameObject("Environment");
            var ink = new StreetMeshBuilder();
            var rng = new System.Random(3);

            // Ground lines: pavement edge, kerbs, road centre dashes, building base.
            ink.GroundLine(-HalfLength, PavementFront, HalfLength, PavementFront, Line * 1.6f);
            ink.GroundLine(-HalfLength, Kerb, HalfLength, Kerb, Line * 1.4f);
            ink.GroundLine(-HalfLength, FarKerb, HalfLength, FarKerb, Line * 1.2f);
            for (float x = -HalfLength; x < HalfLength; x += 3f)
                ink.GroundLine(x, RoadCentre, x + 1.4f, RoadCentre, Line);
            ink.GroundLine(-HalfLength, BuildingLine, HalfLength, BuildingLine, Line);
            // Near road between the camera and the pavement, so the ground fills the frame.
            ink.GroundLine(-HalfLength, NearKerb, HalfLength, NearKerb, Line * 1.8f);
            for (float x = -HalfLength + 1.5f; x < HalfLength; x += 3f)
                ink.GroundLine(x, NearRoadCentre, x + 1.4f, NearRoadCentre, Line * 1.2f);
            for (float x = 9f; x < 12.5f; x += 0.7f)                 // zebra crossing toward the station
                ink.GroundLine(x, PavementFront - 0.25f, x, NearKerb + 0.25f, 0.32f);
            // Paving joints on the walkable pavement: short strokes, like a sketch.
            for (float x = -HalfLength; x < HalfLength; x += 2.2f)
                ink.GroundLine(x, PavementFront + 0.05f, x + 0.35f, PavementFront + 0.9f, Line * 0.5f);

            // Shop fronts.
            float cursor = -HalfLength + 1f;
            foreach ((string sign, float width, float height) in Buildings)
            {
                float x0 = cursor, x1 = cursor + width, z = BuildingLine;
                ink.Rect(x0, 0f, x1, height, z, Line * 1.6f);
                bool station = sign == "STATION";
                // door
                float doorW = station ? 2.4f : 1.1f;
                float doorX = x0 + width * (station ? 0.5f : 0.3f) - doorW * 0.5f;
                ink.Rect(doorX, 0f, doorX + doorW, station ? 2.8f : 2.2f, z - 0.01f, Line);
                // windows
                int cols = Mathf.Max(1, Mathf.FloorToInt((width - 1f) / 1.5f));
                for (float y = station ? 3.9f : 2.9f; y + 1.1f < height - 0.4f; y += 1.7f)
                    for (int c = 0; c < cols; c++)
                    {
                        float wx = x0 + 0.75f + c * ((width - 1.5f) / cols) + 0.15f;
                        if (!station && y < 3f && wx < doorX + doorW + 0.2f && wx + 0.9f > doorX - 0.2f) continue;
                        ink.Rect(wx, y, wx + 0.9f, y + 1.1f, z - 0.01f, Line * 0.8f);
                        if (rng.NextDouble() < 0.35) ink.Line(wx, y + 0.55f, wx + 0.9f, y + 0.55f, z - 0.01f, Line * 0.5f);
                    }
                // shop window at street level
                if (!station && !string.IsNullOrEmpty(sign))
                    ink.Rect(x0 + width * 0.52f, 0.7f, x1 - 0.5f, 2.3f, z - 0.01f, Line);
                // awning / roof detail
                if (!string.IsNullOrEmpty(sign) && !station)
                {
                    ink.Line(x0 + 0.3f, 2.75f, x1 - 0.3f, 2.75f, z - 0.02f, Line * 1.2f);
                    for (float ax = x0 + 0.3f; ax < x1 - 0.3f; ax += 0.6f)
                        ink.Line(ax, 2.75f, ax + 0.3f, 2.45f, z - 0.02f, Line * 0.7f);
                }
                else if (rng.NextDouble() < 0.6)
                {
                    ink.Line(x0 + width * 0.7f, height, x0 + width * 0.7f, height + 0.9f, z, Line);
                    ink.Rect(x0 + width * 0.62f, height + 0.9f, x0 + width * 0.78f, height + 1.2f, z, Line * 0.8f);
                }
                if (station)
                {
                    ink.Circle(x0 + width * 0.5f, height - 1.1f, 0.55f, z - 0.01f, Line * 1.2f);
                    ink.Line(x0 + width * 0.5f, height - 1.1f, x0 + width * 0.5f, height - 0.75f, z - 0.02f, Line);
                    ink.Line(x0 + width * 0.5f, height - 1.1f, x0 + width * 0.5f + 0.25f, height - 1.1f, z - 0.02f, Line);
                }
                if (!string.IsNullOrEmpty(sign))
                    Sign(root, sign, new Vector3(x0 + width * 0.5f, station ? height - 2.4f : 3.35f, z - 0.05f), station ? 1.1f : 0.8f);
                cursor = x1 + 0.6f;
            }

            // Street furniture on the kerb line (between walkable pavement and road).
            for (float x = -HalfLength + 4f; x < HalfLength; x += 9f)
            {
                ink.Line(x, 0f, x, 3.7f, Kerb, Line * 1.3f);
                ink.Line(x, 3.7f, x + 0.7f, 3.9f, Kerb, Line * 1.1f);
                ink.Rect(x + 0.55f, 3.62f, x + 0.95f, 3.82f, Kerb, Line * 0.9f);
            }
            // Signpost pointing at the station (the demo's favourite question).
            float sx = 6.5f;
            ink.Line(sx, 0f, sx, 2.5f, Kerb - 0.1f, Line * 1.2f);
            ink.Rect(sx - 0.1f, 1.95f, sx + 1.9f, 2.45f, Kerb - 0.1f, Line);
            Sign(root, "STATION →", new Vector3(sx + 0.9f, 2.2f, Kerb - 0.15f), 0.36f);
            // Bench.
            float bx = -7f;
            ink.Line(bx, 0.45f, bx + 1.8f, 0.45f, Kerb - 0.2f, Line * 1.4f);
            ink.Line(bx, 0.85f, bx + 1.8f, 0.85f, Kerb - 0.2f, Line * 1.1f);
            ink.Line(bx + 0.15f, 0f, bx + 0.15f, 0.85f, Kerb - 0.2f, Line);
            ink.Line(bx + 1.65f, 0f, bx + 1.65f, 0.85f, Kerb - 0.2f, Line);
            // Trees on the far pavement.
            for (float x = -HalfLength + 7f; x < HalfLength; x += 11f)
            {
                ink.Line(x, 0f, x, 2.3f, BuildingLine - 0.6f, Line * 1.4f);
                ink.Circle(x, 3.0f, 0.8f, BuildingLine - 0.6f, Line * 1.3f, 28);
                ink.Line(x, 2.3f, x - 0.35f, 2.8f, BuildingLine - 0.6f, Line * 0.8f);
            }

            var street = new GameObject("StreetLines", typeof(MeshFilter), typeof(MeshRenderer));
            street.transform.SetParent(root.transform, false);
            string meshPath = DemoPaths.Root + "/Art/Environment/StreetLines.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
            AssetDatabase.DeleteAsset(meshPath);
            Mesh mesh = ink.ToMesh("StreetLines");
            AssetDatabase.CreateAsset(mesh, meshPath);
            street.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = street.GetComponent<MeshRenderer>();
            mr.sharedMaterial = DemoProjectSetup.Mat("M_Ink_TwoSided");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Debug.Log($"[OPENNPC] Street: {ink.QuadCount} ink quads in one mesh.");
            return root;
        }

        private static void Sign(GameObject root, string text, Vector3 position, float height)
        {
            var go = new GameObject("Sign_" + text, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(root.transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.position = position;
            const float pixelsPerMetre = 200f;
            rt.sizeDelta = new Vector2(12f * pixelsPerMetre, height * pixelsPerMetre * 1.2f);
            rt.localScale = Vector3.one / pixelsPerMetre;
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(DemoPaths.Theme);
            Text label = UIFactory.Label("Text", rt, theme.display, Mathf.RoundToInt(height * pixelsPerMetre), DemoProjectSetup.Ink, TextAnchor.MiddleCenter);
            UIFactory.Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            // Not every display font has an arrow glyph; WebGL has no OS font fallback.
            label.text = theme.display.HasCharacter('→') ? text : text.Replace("→", ">");
        }
    }
}
