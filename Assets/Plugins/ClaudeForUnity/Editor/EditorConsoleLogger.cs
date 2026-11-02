using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Core.EditorBridge
{
    /// <summary>
    /// Mirrors the Unity Console to a file (&lt;ProjectRoot&gt;/EditorBridge/console.log) so an
    /// external tool can read all logs — including messages from background threads (e.g. the
    /// WebSocket) and from Play mode. The file is truncated when entering Play mode, so it always
    /// holds the latest run; edit-mode logs append after it.
    ///
    /// Toggle via Tools → Editor Bridge → Capture Console To File.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorConsoleLogger
    {
        private const string EnabledKey = "EditorBridge.ConsoleCapture.Enabled";
        private const string CaptureMenu = "Tools/Claude for Unity/Capture Console To File";
        private const long MaxBytes = 8 * 1024 * 1024; // safety cap; trims when exceeded

        private static readonly ConcurrentQueue<string> Queue = new ConcurrentQueue<string>();
        private static readonly object FileLock = new object();
        private static string _logPath;

        static EditorConsoleLogger()
        {
            _logPath = ResolveLogPath();

            // Re-subscribe cleanly across domain reloads.
            Application.logMessageReceivedThreaded -= OnLog;
            EditorApplication.update -= Flush;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            if (Enabled)
            {
                Application.logMessageReceivedThreaded += OnLog;
                EditorApplication.update += Flush;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }
        }

        private static bool Enabled => EditorPrefs.GetBool(EnabledKey, true);

        [MenuItem(CaptureMenu, false, 4)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(EnabledKey, !Enabled);

            // Apply immediately without waiting for a domain reload.
            Application.logMessageReceivedThreaded -= OnLog;
            EditorApplication.update -= Flush;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            if (Enabled)
            {
                Application.logMessageReceivedThreaded += OnLog;
                EditorApplication.update += Flush;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                Debug.Log($"[EditorBridge] Console capture ON -> {_logPath}");
            }
            else
            {
                Debug.Log("[EditorBridge] Console capture OFF");
            }
        }

        [MenuItem(CaptureMenu, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(CaptureMenu, Enabled);
            return true;
        }

        [MenuItem("Tools/Claude for Unity/Clear Console Log", false, 5)]
        private static void Clear()
        {
            lock (FileLock)
            {
                try { File.WriteAllText(_logPath, $"# cleared {Now()}\n"); }
                catch (Exception ex) { Debug.LogError($"[EditorBridge] Clear log failed: {ex.Message}"); }
            }
        }

        [MenuItem("Tools/Claude for Unity/Open Console Log", false, 6)]
        private static void Open()
        {
            if (File.Exists(_logPath))
                EditorUtility.RevealInFinder(_logPath);
            else
                Debug.Log($"[EditorBridge] No log yet at {_logPath}");
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            var line = $"[{Now()}] [{type}] {condition}";
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                !string.IsNullOrEmpty(stackTrace))
            {
                line += "\n" + stackTrace.TrimEnd();
            }

            Queue.Enqueue(line);
        }

        private static void Flush()
        {
            if (Queue.IsEmpty)
                return;

            var sb = new StringBuilder();
            while (Queue.TryDequeue(out var line))
                sb.Append(line).Append('\n');

            if (sb.Length == 0)
                return;

            lock (FileLock)
            {
                try
                {
                    TrimIfTooLarge();
                    File.AppendAllText(_logPath, sb.ToString());
                }
                catch
                {
                    // Never let logging throw.
                }
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            // Start a fresh capture for the upcoming Play session.
            lock (FileLock)
            {
                try { File.WriteAllText(_logPath, $"# ==== ENTER PLAY {Now()} ====\n"); }
                catch { /* ignore */ }
            }
        }

        private static void TrimIfTooLarge()
        {
            try
            {
                var info = new FileInfo(_logPath);
                if (!info.Exists || info.Length < MaxBytes)
                    return;

                // Keep the last half to bound the file size.
                var text = File.ReadAllText(_logPath);
                File.WriteAllText(_logPath, "# trimmed\n" + text.Substring(text.Length / 2));
            }
            catch
            {
                // ignore
            }
        }

        private static string ResolveLogPath()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var dir = Path.Combine(root, "EditorBridge");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "console.log");
        }

        private static string Now() => DateTime.Now.ToString("HH:mm:ss.fff");
    }
}
