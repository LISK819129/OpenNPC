using System.Collections.Generic;
using UnityEditor;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// Import rules for the Blender stickman. Re-exporting from Blender needs no
    /// manual fixing in Unity: scale, rig type, clip names and loop flags are
    /// re-applied on every import. Materials are not imported — the prefab
    /// builder assigns the project's flat materials.
    /// </summary>
    public sealed class DemoAssetPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> LoopingClips = new HashSet<string>
        {
            "Idle", "Walk_Slow", "Walk", "Walk_Fast", "Talk",
        };

        private bool IsCharacter => assetPath.StartsWith(DemoPaths.Characters) && assetPath.EndsWith(".fbx");

        private void OnPreprocessModel()
        {
            if (!IsCharacter)
                return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = true;   // the prefab builder measures the mesh to find "forward"
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsCharacter)
                return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string shortName = clip.takeName;
                int bar = shortName.LastIndexOf('|');
                if (bar >= 0)
                    shortName = shortName.Substring(bar + 1);
                clip.name = shortName;
                clip.loopTime = LoopingClips.Contains(shortName);
                clip.loopPose = false;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
