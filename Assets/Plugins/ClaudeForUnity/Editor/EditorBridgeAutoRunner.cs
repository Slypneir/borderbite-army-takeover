using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Core.EditorBridge
{
    /// <summary>
    /// Watches the bridge instruction file and runs <see cref="SceneEditorBridge.Execute"/>
    /// automatically whenever it changes. This lets an external tool drive the open Editor by
    /// just rewriting EditorBridge/instructions.json — no menu click, no second Unity instance,
    /// and it operates on the live (possibly unsaved) scene.
    ///
    /// Toggle via Tools → Editor Bridge → Auto-Run (Watch File). Keep the Editor open; if it
    /// seems slow to react while unfocused, click the Editor once (Unity throttles background ticks).
    /// </summary>
    [InitializeOnLoad]
    public static class EditorBridgeAutoRunner
    {
        private const string EnabledKey = "EditorBridge.AutoRun.Enabled";
        private const string MenuPath = "Tools/Claude for Unity/Auto-Run (Watch File)";
        private const double PollInterval = 1.0;   // seconds between file checks
        private const double SettleDelay = 0.4;    // wait for the write to finish before running

        private static long _lastRunTicks;
        private static long _pendingTicks = -1;
        private static double _pendingSince;
        private static double _nextPoll;
        private static volatile bool _fsDirty;
        private static FileSystemWatcher _watcher;

        static EditorBridgeAutoRunner()
        {
            // Treat the file as already processed at load, so we only react to *new* writes.
            var path = SafeInputPath();
            _lastRunTicks = (path != null && File.Exists(path)) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
            EditorApplication.update += Update;

            // A FileSystemWatcher reacts to a write immediately (even while Unity is unfocused),
            // so we don't sit through the poll interval before noticing. The threadpool callback
            // only flips a bool; the actual run still happens on the main thread in Update().
            TrySetupWatcher(path);
            AssemblyReloadEvents.beforeAssemblyReload += DisposeWatcher;
        }

        private static void TrySetupWatcher(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                var dir = Path.GetDirectoryName(path);
                var file = Path.GetFileName(path);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                _watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                };
                void OnChange(object s, FileSystemEventArgs e) => _fsDirty = true;
                _watcher.Changed += OnChange;
                _watcher.Created += OnChange;
                _watcher.Renamed += (s, e) => _fsDirty = true;
            }
            catch
            {
                // The watcher is a bonus; polling in Update() still works without it.
            }
        }

        private static void DisposeWatcher()
        {
            try { _watcher?.Dispose(); } catch { }
            _watcher = null;
        }

        private static bool Enabled => EditorPrefs.GetBool(EnabledKey, true);

        [MenuItem(MenuPath, false, 3)]
        private static void Toggle() => EditorPrefs.SetBool(EnabledKey, !Enabled);

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Update()
        {
            if (!Enabled)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            var now = EditorApplication.timeSinceStartup;
            // The watcher saw a write → check right now instead of waiting for the poll interval.
            if (_fsDirty)
            {
                _fsDirty = false;
                _nextPoll = 0;
            }
            if (now < _nextPoll)
                return;
            _nextPoll = now + PollInterval;

            var path = SafeInputPath();
            if (path == null || !File.Exists(path))
                return;

            var ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (ticks == _lastRunTicks)
                return;

            // Debounce: only run once the same timestamp has been stable for a moment, so we never
            // read a half-written file.
            if (ticks != _pendingTicks)
            {
                _pendingTicks = ticks;
                _pendingSince = now;
                return;
            }

            if (now - _pendingSince < SettleDelay)
                return;

            _lastRunTicks = ticks;
            _pendingTicks = -1;

            try
            {
                Debug.Log("[EditorBridge] Auto-run: instruction file changed, executing.");
                SceneEditorBridge.Execute(path, SceneEditorBridge.OutputPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorBridge] Auto-run failed: {ex}");
            }
        }

        private static string SafeInputPath()
        {
            try { return SceneEditorBridge.InputPath; }
            catch { return null; }
        }
    }
}
