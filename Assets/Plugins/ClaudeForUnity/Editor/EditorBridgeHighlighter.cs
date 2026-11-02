using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Core.EditorBridge
{
    /// <summary>
    /// Visual language for automated bridge actions, so a human can follow what the tool is doing:
    /// every action pings + (optionally) selects its target, draws a colour-coded fading highlight
    /// around it in the Scene view, and appends to a small on-screen action feed.
    /// Colours encode intent: create / modify / delete / select / read / pointer.
    /// </summary>
    public enum ActionKind { Create, Modify, Delete, Select, Read, Pointer }

    [InitializeOnLoad]
    public static class EditorBridgeHighlighter
    {
        private const string SelectKey = "EditorBridge.Highlight.AutoSelect";
        private const string MenuPath = "Tools/Claude for Unity/Highlight Actions (Ping + Select)";
        private const double HighlightDur = 1.5; // seconds a scene highlight lives
        private const int FeedMax = 10;

        private struct Entry
        {
            public string label;
            public ActionKind kind;
            public double t0;
            public GameObject go;   // scene target for the bounds box (may be null for assets)
        }

        private static readonly List<Entry> _active = new List<Entry>();
        private static readonly List<Entry> _feed = new List<Entry>();
        private static GUIStyle _labelStyle, _feedStyle;

        static EditorBridgeHighlighter()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static bool AutoSelect
        {
            get => EditorPrefs.GetBool(SelectKey, true);
            set => EditorPrefs.SetBool(SelectKey, value);
        }

        [MenuItem(MenuPath, false, 4)]
        private static void Toggle() => AutoSelect = !AutoSelect;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, AutoSelect);
            return true;
        }

        public static Color ColorOf(ActionKind k)
        {
            switch (k)
            {
                case ActionKind.Create: return new Color(0.30f, 0.90f, 0.45f);
                case ActionKind.Modify: return new Color(0.35f, 0.70f, 1.00f);
                case ActionKind.Delete: return new Color(1.00f, 0.35f, 0.35f);
                case ActionKind.Select: return new Color(1.00f, 0.85f, 0.25f);
                case ActionKind.Read:   return new Color(0.75f, 0.76f, 0.82f);
                case ActionKind.Pointer:return new Color(1.00f, 0.30f, 0.72f);
                default: return Color.white;
            }
        }

        /// <summary>Report an action so it gets pinged, (optionally) selected, boxed and fed.</summary>
        public static void Report(Object target, ActionKind kind, string label)
        {
            try
            {
                var go = target as GameObject ?? (target as Component)?.gameObject;
                var e = new Entry { label = label, kind = kind, t0 = EditorApplication.timeSinceStartup, go = go };
                _active.Add(e);
                _feed.Add(e);
                while (_feed.Count > FeedMax) _feed.RemoveAt(0);

                if (target != null)
                {
                    EditorGUIUtility.PingObject(target);
                    if (AutoSelect && (kind == ActionKind.Create || kind == ActionKind.Select || kind == ActionKind.Modify))
                        Selection.activeObject = target;
                }

                // Play mode: pulse a color-coded ring at the target's on-screen position too.
                if (go != null && EditorApplication.isPlaying)
                    SceneEditorBridge.PulseTarget(go, ColorOf(kind));

                SceneView.RepaintAll();
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.RepaintProjectWindow();
            }
            catch { /* highlighting must never break an action */ }
        }

        private static void Tick()
        {
            if (_active.Count == 0) return;
            var now = EditorApplication.timeSinceStartup;
            for (var i = _active.Count - 1; i >= 0; i--)
                if (now - _active[i].t0 > HighlightDur) _active.RemoveAt(i);
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView view)
        {
            EnsureStyles();
            var now = EditorApplication.timeSinceStartup;

            foreach (var e in _active)
            {
                if (e.go == null) continue;
                var age = (float)((now - e.t0) / HighlightDur);
                var a = Mathf.Clamp01(1f - age);
                var col = ColorOf(e.kind);
                col.a = a;
                DrawTargetBox(e.go, col, e.label, a);
            }

            DrawFeed();
        }

        private static void DrawTargetBox(GameObject go, Color col, string label, float a)
        {
            Handles.color = col;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                var pulse = 1f + 0.08f * (1f - a); // slight outward pop as it fades
                var center = (c[0] + c[2]) * 0.5f;
                for (var i = 0; i < 4; i++) c[i] = center + (c[i] - center) * pulse;
                Handles.DrawAAPolyLine(4f, c[0], c[1], c[2], c[3], c[0]);
                _labelStyle.normal.textColor = new Color(1, 1, 1, a);
                Handles.Label(c[1], label, _labelStyle);
            }
            else
            {
                var r = go.GetComponentInChildren<Renderer>();
                var b = r != null ? r.bounds : new Bounds(go.transform.position, Vector3.one * 0.5f);
                Handles.DrawWireCube(b.center, b.size * (1f + 0.08f * (1f - a)));
                _labelStyle.normal.textColor = new Color(1, 1, 1, a);
                Handles.Label(b.center + Vector3.up * (b.extents.y + 0.1f), label, _labelStyle);
            }
        }

        private static void DrawFeed()
        {
            if (_feed.Count == 0) return;
            Handles.BeginGUI();
            const float w = 280f, line = 18f;
            var h = _feed.Count * line + 12f;
            EditorGUI.DrawRect(new Rect(8, 8, w, h), new Color(0f, 0f, 0f, 0.55f));
            var y = 12f;
            for (var i = _feed.Count - 1; i >= 0; i--) // newest first
            {
                var e = _feed[i];
                EditorGUI.DrawRect(new Rect(14, y + 4, 9, 9), ColorOf(e.kind));
                GUI.Label(new Rect(30, y, w - 36, line), e.label, _feedStyle);
                y += line;
            }
            Handles.EndGUI();
        }

        private static void EnsureStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                _labelStyle.normal.textColor = Color.white;
            }
            if (_feedStyle == null)
            {
                _feedStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };
                _feedStyle.normal.textColor = new Color(0.92f, 0.92f, 0.95f);
            }
        }
    }
}
