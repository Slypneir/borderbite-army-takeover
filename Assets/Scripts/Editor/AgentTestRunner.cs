using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Core.EditorTools
{
    /// <summary>
    /// Runs EditMode tests from code (menu or the Claude for Unity bridge "call" op) and writes a compact
    /// summary to EditorBridge/test_results.json plus NUnit XML to Logs/tests.xml.
    /// </summary>
    public static class AgentTestRunner
    {
        private const string ResultFile = "EditorBridge/test_results.json";

        [MenuItem("Tools/Tests/Run EditMode Tests")]
        public static void RunAll() => Run(null);

        /// <summary>Starts the run; the result file appears when it finishes.</summary>
        public static string Run(string nameFilter)
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.Combine(root, ResultFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new Callbacks(path, Path.Combine(root, "Logs", "tests.xml"));
            api.RegisterCallbacks(callbacks);
            var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "Assembly-CSharp-Editor" } };
            if (!string.IsNullOrEmpty(nameFilter)) filter.groupNames = new[] { nameFilter };
            api.Execute(new ExecutionSettings(filter) { runSynchronously = false });
            return path;
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string _path;
            private readonly string _xmlPath;

            public Callbacks(string path, string xmlPath)
            {
                _path = path;
                _xmlPath = xmlPath;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var failures = new List<string>();
                Collect(result, failures);
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"passed\": ").Append(result.PassCount).Append(",\n");
                sb.Append("  \"failed\": ").Append(result.FailCount).Append(",\n");
                sb.Append("  \"skipped\": ").Append(result.SkipCount).Append(",\n");
                sb.Append("  \"duration\": ").Append(result.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",\n");
                sb.Append("  \"failures\": [");
                for (var i = 0; i < failures.Count; i++)
                {
                    sb.Append(i == 0 ? "\n    " : ",\n    ").Append(Newtonsoft.Json.JsonConvert.ToString(failures[i]));
                }
                sb.Append(failures.Count > 0 ? "\n  ]\n" : "]\n");
                sb.Append("}\n");
                File.WriteAllText(_path, sb.ToString());
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_xmlPath));
                    File.WriteAllText(_xmlPath, result.ToXml().OuterXml);
                }
                catch (IOException) { }
                Debug.Log($"[AgentTestRunner] EditMode finished: passed {result.PassCount}, failed {result.FailCount}");
            }

            private static void Collect(ITestResultAdaptor r, List<string> failures)
            {
                if (!r.HasChildren)
                {
                    if (r.TestStatus == TestStatus.Failed)
                        failures.Add(r.FullName + ": " + r.Message);
                    return;
                }
                foreach (var c in r.Children) Collect(c, failures);
            }
        }
    }
}
