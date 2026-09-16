using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenNPC.EditorTools
{
    /// <summary>
    /// One-command pipeline and builds.
    ///   scripts/unity.sh OpenNPC.EditorTools.DemoPipeline.All            regenerate everything + verify
    ///   scripts/unity.sh OpenNPC.EditorTools.DemoPipeline.BuildWindows   desktop player (Builds/Windows)
    ///   scripts/unity.sh OpenNPC.EditorTools.DemoPipeline.BuildWebGL     WebGL player, copied to web/demo/
    /// </summary>
    public static class DemoPipeline
    {
        [MenuItem("OpenNPC/Run Full Pipeline")]
        public static void All()
        {
            DemoProjectSetup.Run();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            DemoAnimatorBuilder.Run();
            DemoPrefabBuilder.Run();
            DemoSceneBuilder.Run();
            DemoVerify.Run();
        }

        [MenuItem("OpenNPC/Build/Windows")]
        public static void BuildWindows()
        {
            string path = Path.Combine(DemoPaths.BuildsDir, "Windows", "OpenNPC-Demo.exe");
            Build(BuildTarget.StandaloneWindows64, path);
        }

        [MenuItem("OpenNPC/Build/WebGL")]
        public static void BuildWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[OPENNPC] WebGL Build Support module is not installed for this Unity version. " +
                               "Install it from Unity Hub (Installs > Add modules > WebGL Build Support).");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            string output = Path.Combine(DemoPaths.BuildsDir, "WebGL");
            if (!Build(BuildTarget.WebGL, output))
                return;

            // Deploy: copy only the Build/ folder; web/index.html is our own page.
            string target = Path.Combine(DemoPaths.WebDemoDir, "Build");
            if (Directory.Exists(target))
                Directory.Delete(target, true);
            CopyDirectory(Path.Combine(output, "Build"), target);
            WriteBuildManifest(target);
            Debug.Log("[OPENNPC] WebGL build copied to " + target);
        }

        private static bool Build(BuildTarget target, string path)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { DemoPaths.Scene },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[OPENNPC] Build {target}: {report.summary.result}, {report.summary.totalSize / (1024f * 1024f):0.0} MB, " +
                      $"{report.summary.totalErrors} errors -> {path}");
            if (!ok && Application.isBatchMode)
                EditorApplication.Exit(1);
            return ok;
        }

        /// <summary>
        /// web/demo/build.json tells web/script.js the real file names, which change
        /// with the compression setting (.gz, .br, .unityweb …).
        /// </summary>
        private static void WriteBuildManifest(string buildDir)
        {
            string Find(string contains)
            {
                foreach (string f in Directory.GetFiles(buildDir))
                    if (Path.GetFileName(f).Contains(contains))
                        return Path.GetFileName(f);
                Debug.LogError("[OPENNPC] WebGL build has no file matching " + contains);
                return "";
            }
            string json = "{\n" +
                          $"  \"loaderUrl\": \"{Find(".loader.js")}\",\n" +
                          $"  \"dataUrl\": \"{Find(".data")}\",\n" +
                          $"  \"frameworkUrl\": \"{Find(".framework.js")}\",\n" +
                          $"  \"codeUrl\": \"{Find(".wasm")}\",\n" +
                          $"  \"version\": \"{Application.version}\",\n" +
                          $"  \"built\": \"{System.DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\"\n}}\n";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(buildDir), "build.json"), json);
        }

        private static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(from))
                CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
        }
    }
}
