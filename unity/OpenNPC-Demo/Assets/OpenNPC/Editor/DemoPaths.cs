using System.IO;
using UnityEngine;

namespace OpenNPC.EditorTools
{
    /// <summary>Every path the editor pipeline reads or writes, in one place.</summary>
    public static class DemoPaths
    {
        public const string Root = "Assets/OpenNPC";
        public const string Settings = Root + "/Settings";
        public const string Materials = Root + "/Materials";
        public const string Fonts = Root + "/Fonts";
        public const string Characters = Root + "/Art/Characters";
        public const string CharacterModel = Characters + "/OpenNPC_Stickman.fbx";
        public const string AnimatorController = Characters + "/Stickman.controller";
        public const string Prefabs = Root + "/Prefabs";
        public const string NpcPrefab = Prefabs + "/NPC.prefab";
        public const string PlayerPrefab = Prefabs + "/Player.prefab";
        public const string Resources = Root + "/Resources/OpenNPC";
        public const string ConfigAsset = Resources + "/OpenNPCConfig.asset";
        public const string PersonaBundle = Resources + "/personas.json";
        public const string Theme = Settings + "/UITheme.asset";
        public const string PipelineAsset = Settings + "/OpenNPC_URP.asset";
        public const string RendererAsset = Settings + "/OpenNPC_URP_Renderer.asset";
        public const string Scene = Root + "/Scenes/OpenNPC_Street.unity";

        /// <summary>Repository root (three levels above Assets: unity/OpenNPC-Demo/Assets).</summary>
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        public static string ExternalFbx => Path.Combine(RepoRoot, "blender", "exports", "OpenNPC_Stickman.fbx");
        public static string ExternalClipManifest => Path.Combine(RepoRoot, "blender", "exports", "stickman_clips.json");
        public static string ExternalPersonas => Path.Combine(RepoRoot, "framework", "examples", "personas");
        public static string ExternalFonts => Path.Combine(RepoRoot, "assets", "fonts");
        public static string BuildsDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
        public static string WebDemoDir => Path.Combine(RepoRoot, "web", "demo");
    }
}
