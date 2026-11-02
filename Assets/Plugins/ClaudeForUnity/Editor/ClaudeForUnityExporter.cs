using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Core.EditorBridge
{
    /// <summary>
    /// One-button export of the "Claude for Unity" plugin into a .unitypackage, so the latest version can
    /// be dropped into any other Unity project. The whole plugin lives under Assets/Plugins/ClaudeForUnity,
    /// so we simply export that folder (recursively).
    /// Menu: Tools/Claude for Unity/Export Package. Also callable from the bridge (op "exportPackage")
    /// and the MCP tool (unity_export_package).
    /// </summary>
    public static class ClaudeForUnityExporter
    {
        private const string PluginRoot = "Assets/Plugins/ClaudeForUnity";

        [MenuItem("Tools/Claude for Unity/Export Package", false, 100)]
        public static void ExportMenu()
        {
            var outPath = Export(null);
            if (!string.IsNullOrEmpty(outPath))
                EditorUtility.RevealInFinder(outPath);
        }

        /// <summary>
        /// Export the plugin folder to a .unitypackage. Returns the absolute path, or null on failure.
        /// <paramref name="outputPath"/> null → &lt;projectRoot&gt;/ClaudeForUnity-Packages/ClaudeForUnity-&lt;timestamp&gt;.unitypackage;
        /// relative → resolved against the project root; absolute → used as-is.
        /// </summary>
        public static string Export(string outputPath)
        {
            if (!Directory.Exists(PluginRoot))
            {
                Debug.LogError($"[ClaudeForUnity] Plugin folder not found: {PluginRoot}");
                return null;
            }

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (string.IsNullOrEmpty(outputPath))
            {
                var dir = Path.Combine(projectRoot, "ClaudeForUnity-Packages");
                Directory.CreateDirectory(dir);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                outputPath = Path.Combine(dir, $"ClaudeForUnity-{stamp}.unitypackage");
            }
            else if (!Path.IsPathRooted(outputPath))
            {
                outputPath = Path.Combine(projectRoot, outputPath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            AssetDatabase.ExportPackage(PluginRoot, outputPath, ExportPackageOptions.Recurse);
            Debug.Log($"[ClaudeForUnity] Exported {PluginRoot} → {outputPath}");
            return outputPath;
        }
    }
}
