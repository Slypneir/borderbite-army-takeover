using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Core.EditorTools
{
    /// <summary>
    /// The Web build of one platform. The symbol of the platform compiles its adapter only, one jslib is linked, the page template
    /// and the ad policy are its own; the settings are put back afterwards. Batch mode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Core.EditorTools.PlatformBuild.BuildFromCommandLine -platform youtube
    /// </summary>
    public static class PlatformBuild
    {
        private const string SummaryFile = "Logs/build_summary.json";
        private const string UnityBotList = "slurp|yandex";

        /// <summary>The profile being built; the scene processor reads it.</summary>
        public static PlatformBuildProfile Current { get; private set; }

        [MenuItem("Tools/Build/Web for YouTube Playables")]
        public static void BuildYouTube() => Build(PlatformBuildProfile.YouTube.Id);

        [MenuItem("Tools/Build/Web for CrazyGames")]
        public static void BuildCrazyGames() => Build(PlatformBuildProfile.CrazyGames.Id);

        [MenuItem("Tools/Build/Web for Yandex Games")]
        public static void BuildYandex() => Build(PlatformBuildProfile.Yandex.Id);

        public static void BuildFromCommandLine()
        {
            var summary = Build(Arg("-platform", PlatformBuildProfile.YouTube.Id));
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.StartsWith("Succeeded") ? 0 : 1);
        }

        /// <summary>Compiles the player scripts of every platform without building: the code under its symbols is checked.</summary>
        [MenuItem("Tools/Build/Check Web Scripts")]
        public static string CheckScripts()
        {
            var report = new StringBuilder();
            foreach (var profile in PlatformBuildProfile.All)
            {
                var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
                {
                    group = BuildTargetGroup.WebGL,
                    target = BuildTarget.WebGL,
                    extraScriptingDefines = new[] { profile.Symbol },
                };
                var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/WebScriptCheck_" + profile.Id);
                var ok = result.assemblies != null && result.assemblies.Count > 0;
                report.Append(profile.Id).Append(ok ? ": compiled " + result.assemblies.Count + " assemblies\n" : ": FAILED\n");
            }

            Debug.Log($"[PlatformBuild] {report}");
            return report.ToString();
        }

        /// <summary>Builds and returns a one-line summary (also written to Logs/build_summary.json).</summary>
        public static string Build(string platform)
        {
            var profile = PlatformBuildProfile.Find(platform);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            WebSettings.Apply();
            var template = PlayerSettings.WebGL.template;
            var format = PlayerSettings.WebGL.compressionFormat;
            var caching = PlayerSettings.WebGL.dataCaching;
            try
            {
                Current = profile;
                PlayerSettings.WebGL.template = profile.Template;
                PlayerSettings.WebGL.compressionFormat = profile.Compression;
                PlayerSettings.WebGL.dataCaching = profile.DataCaching;
                SelectJslib(profile.Jslib);
                return BuildPlayer(profile, profile.OutputDir);
            }
            finally
            {
                Current = null;
                PlayerSettings.WebGL.template = template;
                PlayerSettings.WebGL.compressionFormat = format;
                PlayerSettings.WebGL.dataCaching = caching;
                SelectJslib(null);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Links one jslib of the folder, or none: outside a build all stay off, so the settings in git do not change.</summary>
        public static void SelectJslib(string selected)
        {
            foreach (var other in PlatformBuildProfile.All)
            {
                var importer = (PluginImporter)AssetImporter.GetAtPath(other.Jslib);
                var on = other.Jslib == selected;
                if (importer.GetCompatibleWithAnyPlatform() || importer.GetCompatibleWithPlatform(BuildTarget.WebGL) != on)
                {
                    importer.SetCompatibleWithAnyPlatform(false);
                    importer.SetCompatibleWithPlatform(BuildTarget.WebGL, on);
                    importer.SaveAndReimport();
                }
            }
        }

        private static string BuildPlayer(PlatformBuildProfile profile, string outputDir)
        {
            var root = Directory.GetParent(Application.dataPath)!.FullName;
            var outDir = Path.Combine(root, outputDir);
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, true);

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { profile.Symbol },
            };
            var started = DateTime.UtcNow;
            var report = BuildPipeline.BuildPlayer(options);
            var seconds = (int)(DateTime.UtcNow - started).TotalSeconds;
            var errors = report.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content).Take(20).ToList();
            var result = report.summary.result.ToString();
            if (report.summary.result == BuildResult.Succeeded)
            {
                var found = FindForbidden(outDir, profile);
                if (found.Count > 0)
                {
                    result = "Failed: other platforms in the build";
                    errors.AddRange(found);
                }
            }

            WriteSummary(root, profile, result, seconds, outDir, errors);
            var line = $"{result} in {seconds}s, {profile.Id}, on disk {DirectorySize(outDir) / (1024 * 1024)} MiB, errors {errors.Count}";
            Debug.Log($"[PlatformBuild] {line}");
            return line;
        }

        /// <summary>
        /// The files of a build must hold no word of another platform. A quick check of what is not compressed; Tools/WebGLCheck/build_report.mjs
        /// also looks inside the packed data.
        /// </summary>
        private static List<string> FindForbidden(string outDir, PlatformBuildProfile profile)
        {
            var found = new List<string>();
            foreach (var file in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories))
            {
                if (!IsPlainFile(file))
                    continue;

                // the only allowed hit: the list of search bots in the loader of Unity, not code of a platform
                var text = Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(file)).Replace(UnityBotList, "");
                foreach (var word in profile.Forbidden)
                {
                    if (text.Contains(word))
                        found.Add($"{Path.GetFileName(file)} holds «{word}»");
                }
            }

            return found;
        }

        private static bool IsPlainFile(string file) => !file.EndsWith(".br") && !file.EndsWith(".gz") && !file.EndsWith(".unityweb");

        private static long DirectorySize(string dir) =>
            Directory.Exists(dir) ? new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;

        private static void WriteSummary(string root, PlatformBuildProfile profile, string result, int seconds, string outDir, List<string> errors)
        {
            var json = "{\n  \"platform\": \"" + profile.Id + "\",\n  \"result\": " + Newtonsoft.Json.JsonConvert.ToString(result) +
                       ",\n  \"seconds\": " + seconds + ",\n  \"bytesOnDisk\": " + DirectorySize(outDir) +
                       ",\n  \"errors\": [" + string.Join(", ", errors.Select(Newtonsoft.Json.JsonConvert.ToString)) +
                       "],\n  \"output\": " + Newtonsoft.Json.JsonConvert.ToString(outDir) + "\n}\n";
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            File.WriteAllText(Path.Combine(root, SummaryFile), json);
        }

        private static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
    }
}
