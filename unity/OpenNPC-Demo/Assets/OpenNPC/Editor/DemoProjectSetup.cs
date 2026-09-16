using System.IO;
using System.Linq;
using System.Text;
using OpenNPC.Config;
using OpenNPC.Demo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// Step 1: bring external assets in (Blender FBX, fonts, persona JSON) and
    /// configure the project for a lightweight flat look on desktop and WebGL.
    /// Idempotent.
    /// </summary>
    public static class DemoProjectSetup
    {
        public static readonly Color Ink = new Color32(0x11, 0x11, 0x11, 0xFF);
        public static readonly Color Paper = new Color32(0xF3, 0xF1, 0xEC, 0xFF);
        public static readonly Color Shade = new Color32(0xE3, 0xE0, 0xD8, 0xFF);

        [MenuItem("OpenNPC/1. Sync External Assets + Setup Project")]
        public static void Run()
        {
            foreach (string dir in new[] { DemoPaths.Settings, DemoPaths.Materials, DemoPaths.Fonts, DemoPaths.Characters,
                                           DemoPaths.Prefabs, DemoPaths.Resources, Path.GetDirectoryName(DemoPaths.Scene) })
                Directory.CreateDirectory(dir);

            SyncExternalAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            UniversalRenderPipelineAsset pipeline = CreatePipeline();
            ApplyProjectSettings(pipeline);
            CreateMaterials();
            CreateTheme();
            CreateConfig();
            AssetDatabase.SaveAssets();
            Debug.Log("[OPENNPC] Project setup complete.");
        }

        // ------------------------------------------------------------------ //
        public static void SyncExternalAssets()
        {
            Copy(DemoPaths.ExternalFbx, DemoPaths.CharacterModel);
            Copy(Path.Combine(DemoPaths.ExternalFonts, "BebasNeue", "BebasNeue-Regular.ttf"), DemoPaths.Fonts + "/BebasNeue-Regular.ttf");
            Copy(Path.Combine(DemoPaths.ExternalFonts, "BebasNeue", "OFL.txt"), DemoPaths.Fonts + "/BebasNeue-OFL.txt");
            Copy(Path.Combine(DemoPaths.ExternalFonts, "DejaVuSansMono", "DejaVuSansMono.ttf"), DemoPaths.Fonts + "/DejaVuSansMono.ttf");
            Copy(Path.Combine(DemoPaths.ExternalFonts, "DejaVuSansMono", "DejaVuSansMono-Bold.ttf"), DemoPaths.Fonts + "/DejaVuSansMono-Bold.ttf");
            Copy(Path.Combine(DemoPaths.ExternalFonts, "DejaVuSansMono", "LICENSE_DEJAVU"), DemoPaths.Fonts + "/DejaVu-LICENSE.txt");

            // personas/*.json -> one bundle, ordered by id so authored NPC order is stable.
            string[] files = Directory.Exists(DemoPaths.ExternalPersonas)
                ? Directory.GetFiles(DemoPaths.ExternalPersonas, "*.json") : new string[0];
            var personas = files.Select(File.ReadAllText)
                                .Select(json => (json, id: ExtractId(json)))
                                .OrderBy(p => p.id)
                                .Select(p => p.json.Trim());
            var sb = new StringBuilder("{\n\"personas\": [\n");
            sb.Append(string.Join(",\n", personas));
            sb.Append("\n]\n}\n");
            File.WriteAllText(DemoPaths.PersonaBundle, sb.ToString());
            Debug.Log($"[OPENNPC] Synced FBX, fonts and {files.Length} personas from {DemoPaths.RepoRoot}");
        }

        private static string ExtractId(string json)
        {
            int i = json.IndexOf("\"id\"");
            if (i < 0) return "zzz";
            int q1 = json.IndexOf('"', json.IndexOf(':', i) + 1);
            int q2 = json.IndexOf('"', q1 + 1);
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static void Copy(string from, string to)
        {
            if (!File.Exists(from))
            {
                Debug.LogError("[OPENNPC] Missing external asset: " + from);
                return;
            }
            if (File.Exists(to) && new FileInfo(to).Length == new FileInfo(from).Length &&
                File.ReadAllBytes(to).SequenceEqual(File.ReadAllBytes(from)))
                return;
            File.Copy(from, to, true);
        }

        // ------------------------------------------------------------------ //
        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(DemoPaths.RendererAsset);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, DemoPaths.RendererAsset);
            }
            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            renderer.postProcessData = null;
            EditorUtility.SetDirty(renderer);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(DemoPaths.PipelineAsset);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, DemoPaths.PipelineAsset);
            }
            asset.supportsHDR = false;
            asset.msaaSampleCount = 4;       // the whole look is edges; MSAA is the one luxury
            asset.renderScale = 1f;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            asset.shadowDistance = 0f;
            var so = new SerializedObject(asset);
            Set(so, "m_MainLightRenderingMode", 0);
            Set(so, "m_MainLightShadowsSupported", false);
            Set(so, "m_AdditionalLightsRenderingMode", 0);
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_SoftShadowsSupported", false);
            Set(so, "m_UseSRPBatcher", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void Set(SerializedObject so, string path, object value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null) return;
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }

        private static void ApplyProjectSettings(UniversalRenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                QualitySettings.vSyncCount = 1;
            }
            QualitySettings.SetQualityLevel(current, false);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "OpenNPC";
            PlayerSettings.productName = "OpenNPC Demo";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;

            // WebGL: small, and loadable from any static host (GitHub Pages cannot set
            // Content-Encoding headers, so the loader decompresses gzip itself).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;

            // Input System for gameplay + legacy input for uGUI InputField.
            var projectSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty handler = projectSettings.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue != 2)
            {
                handler.intValue = 2;
                projectSettings.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[OPENNPC] Active input handling set to Both (takes effect next editor start).");
            }

            Shader flat = Shader.Find("OpenNPC/Flat");
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty list = graphics.FindProperty("m_AlwaysIncludedShaders");
            if (flat != null && list != null)
            {
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    present |= list.GetArrayElementAtIndex(i).objectReferenceValue == flat;
                if (!present)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = flat;
                }
                graphics.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ //
        public static Material Mat(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{DemoPaths.Materials}/{name}.mat");

        private static void CreateMaterials()
        {
            Shader flat = Shader.Find("OpenNPC/Flat");
            if (flat == null)
            {
                Debug.LogError("[OPENNPC] Shader OpenNPC/Flat failed to compile.");
                return;
            }
            Flat("M_Ink", flat, Ink, CullMode.Back, 0f);
            Flat("M_White", flat, Color.white, CullMode.Back, 0f);
            Flat("M_Paper", flat, Paper, CullMode.Back, 0f);
            Flat("M_Shade", flat, Shade, CullMode.Off, 0f);
            Flat("M_Ink_TwoSided", flat, Ink, CullMode.Off, 0f);
            Flat("M_Outline_White", flat, Color.white, CullMode.Front, 0.45f);
            Flat("M_Outline_Ink", flat, Ink, CullMode.Front, 0.45f);
        }

        private static void Flat(string name, Shader shader, Color colour, CullMode cull, float push)
        {
            string path = $"{DemoPaths.Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Cull", (float)cull);
            m.SetFloat("_OutlinePush", push);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        private static void CreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(DemoPaths.Theme);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, DemoPaths.Theme);
            }
            theme.display = AssetDatabase.LoadAssetAtPath<Font>(DemoPaths.Fonts + "/BebasNeue-Regular.ttf");
            theme.mono = AssetDatabase.LoadAssetAtPath<Font>(DemoPaths.Fonts + "/DejaVuSansMono.ttf");
            theme.monoBold = AssetDatabase.LoadAssetAtPath<Font>(DemoPaths.Fonts + "/DejaVuSansMono-Bold.ttf");
            theme.debugBackground = new Color32(0x11, 0x11, 0x11, 0xFF);   // solid ink: a tool, not an overlay
            if (theme.display == null || theme.mono == null)
                Debug.LogError("[OPENNPC] Fonts did not import.");
            EditorUtility.SetDirty(theme);
        }

        private static void CreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<OpenNPCConfig>(DemoPaths.ConfigAsset);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<OpenNPCConfig>();
                AssetDatabase.CreateAsset(config, DemoPaths.ConfigAsset);
            }
            config.personaBundle = AssetDatabase.LoadAssetAtPath<TextAsset>(DemoPaths.PersonaBundle);
            EditorUtility.SetDirty(config);
        }
    }
}
