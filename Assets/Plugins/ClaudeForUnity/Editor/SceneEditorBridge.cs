using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.EditorBridge
{
    /// <summary>
    /// Generic JSON-driven Editor executor. An external instruction file describes a batch of
    /// operations (create/find/modify GameObjects, add components, set any serialized property or
    /// object reference, build prefabs/assets, and inspect the scene). The script runs them in
    /// order, keeps an id-registry so later commands can reference objects created earlier, and
    /// writes a structured result file so the caller can verify state and send a follow-up batch.
    ///
    /// Files live under &lt;ProjectRoot&gt;/EditorBridge/ (outside Assets, so no asset reimport):
    ///   - instructions.json : input batch
    ///   - result.json       : output report (per-command ok/error + requested dumps)
    ///
    /// Instruction file shape:
    /// {
    ///   "target": "scene" | "prefab",         // default "scene"
    ///   "openScene": false,                     // if true + scenePath set, opens that scene first
    ///   "scenePath": "Assets/Scenes/SampleScene.unity",
    ///   "prefabPath": "Assets/Prefabs/UI/Screens/ChatScreen.prefab",  // required when target=prefab
    ///   "save": true,                           // save scene / prefab + assets at the end
    ///   "stopOnError": false,
    ///   "commands": [ { "op": "...", ... }, ... ]
    /// }
    ///
    /// Object selectors (used by "target","parent","value" of reference fields):
    ///   "Some/Path"                              -> id from registry, else hierarchy path
    ///   { "$ref": "id" }
    ///   { "$path": "Canvas/Panel/Title" }
    ///   { "$name": "Title" }                     -> first match anywhere (includes inactive)
    ///   { "$find": "Namespace.TypeName" }        -> first component of that type in scene
    ///   { "$asset": "Assets/...prefab" }         -> asset (GameObject for .prefab, else main asset)
    ///   { "$component": { "ref": "id"|"$path":.., "type": "UnityEngine.UI.Button" } }
    ///   { "$null": true }
    /// </summary>
    public static class SceneEditorBridge
    {
        private const string BridgeFolderName = "EditorBridge";
        private const string DefaultInputFile = "instructions.json";
        private const string DefaultOutputFile = "result.json";

        /// <summary>Absolute path of the default instruction file (&lt;ProjectRoot&gt;/EditorBridge/instructions.json).</summary>
        public static string InputPath => Path.Combine(EnsureBridgeFolder(), DefaultInputFile);

        /// <summary>Absolute path of the default result file.</summary>
        public static string OutputPath => Path.Combine(EnsureBridgeFolder(), DefaultOutputFile);

        [MenuItem("Tools/Claude for Unity/Run Instructions (default file)", false, 0)]
        public static void RunDefault()
        {
            Execute(InputPath, OutputPath);
        }

        [MenuItem("Tools/Claude for Unity/Run Instructions (pick file)...", false, 1)]
        public static void RunPicked()
        {
            var dir = EnsureBridgeFolder();
            var path = EditorUtility.OpenFilePanel("Select instruction JSON", dir, "json");
            if (string.IsNullOrEmpty(path))
                return;

            var output = Path.Combine(Path.GetDirectoryName(path) ?? dir, DefaultOutputFile);
            Execute(path, output);
        }

        [MenuItem("Tools/Claude for Unity/Reveal Bridge Folder", false, 2)]
        public static void RevealFolder()
        {
            var dir = EnsureBridgeFolder();
            EditorUtility.RevealInFinder(dir);
            Debug.Log($"[EditorBridge] Bridge folder: {dir}");
        }

        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Runs an instruction file and writes the result. Public so the file watcher
        /// (<see cref="EditorBridgeAutoRunner"/>) can trigger it without a menu click.
        /// </summary>
        public static void Execute(string inputPath, string outputPath)
        {
            var result = new JObject();
            var entries = new JArray();
            result["results"] = entries;

            if (!File.Exists(inputPath))
            {
                result["ok"] = false;
                result["error"] = $"Instruction file not found: {inputPath}";
                WriteResult(outputPath, result);
                Debug.LogError($"[EditorBridge] {result["error"]}");
                return;
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(inputPath));
            }
            catch (Exception ex)
            {
                result["ok"] = false;
                result["error"] = $"Invalid JSON: {ex.Message}";
                WriteResult(outputPath, result);
                Debug.LogError($"[EditorBridge] {result["error"]}");
                return;
            }

            var ctx = new Context
            {
                Mode = (root.Value<string>("target") ?? "scene").ToLowerInvariant(),
                StopOnError = root.Value<bool?>("stopOnError") ?? false,
                Save = root.Value<bool?>("save") ?? false
            };

            // compactResult:true writes result.json without indentation — smaller to read back
            // when a batch returns big dumps.
            var compactResult = root.Value<bool?>("compactResult") ?? false;

            GameObject prefabRoot = null;
            try
            {
                if (ctx.Mode == "prefab")
                {
                    ctx.PrefabPath = root.Value<string>("prefabPath");
                    if (string.IsNullOrEmpty(ctx.PrefabPath))
                        throw new Exception("target=prefab requires 'prefabPath'.");

                    prefabRoot = PrefabUtility.LoadPrefabContents(ctx.PrefabPath);
                    ctx.PrefabRoot = prefabRoot;
                }
                else
                {
                    if ((root.Value<bool?>("openScene") ?? false))
                    {
                        var scenePath = root.Value<string>("scenePath");
                        if (!string.IsNullOrEmpty(scenePath))
                            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    }

                    ctx.Scene = EditorSceneManager.GetActiveScene();
                }

                var commands = root["commands"] as JArray ?? new JArray();
                var allOk = true;

                for (var i = 0; i < commands.Count; i++)
                {
                    var cmd = commands[i] as JObject;
                    var entry = new JObject { ["index"] = i, ["op"] = cmd?.Value<string>("op") };

                    try
                    {
                        if (cmd == null)
                            throw new Exception("Command is not an object.");

                        Execute(cmd, ctx, entry);
                        entry["ok"] = true;
                    }
                    catch (Exception ex)
                    {
                        entry["ok"] = false;
                        entry["error"] = ex.Message;
                        allOk = false;
                        Debug.LogError($"[EditorBridge] Command #{i} ({entry["op"]}) failed: {ex.Message}");

                        if (ctx.StopOnError)
                        {
                            entries.Add(entry);
                            break;
                        }
                    }

                    entries.Add(entry);
                }

                result["ok"] = allOk;

                // Assertion summary → one field a loop can gate the whole batch on.
                var assertPassed = 0;
                var assertFailed = 0;
                foreach (var e in entries)
                {
                    if ((string)e["op"] != "assert" || e["pass"] == null) continue;
                    if ((bool)e["pass"]) assertPassed++;
                    else assertFailed++;
                }
                if (assertPassed + assertFailed > 0)
                {
                    result["assertionsPassed"] = assertPassed;
                    result["assertionsFailed"] = assertFailed;
                    result["allAssertionsPassed"] = assertFailed == 0;
                }

                if (ctx.Save)
                    SaveAll(ctx, prefabRoot);
            }
            catch (Exception ex)
            {
                result["ok"] = false;
                result["error"] = ex.Message;
                Debug.LogError($"[EditorBridge] Fatal: {ex}");
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            result["isPlaying"] = EditorApplication.isPlaying;
            result["isCompiling"] = EditorApplication.isCompiling;
            if (_lastClickHit != null) { result["lastClickHit"] = _lastClickHit; _lastClickHit = null; }
            result["finishedUtc"] = DateTime.UtcNow.ToString("o");
            // Echo the caller's requestId so an external client (e.g. the Unity MCP server) can
            // correlate this result with the exact batch it wrote, ignoring any stale result.json.
            var reqId = root?.Value<string>("requestId");
            if (!string.IsNullOrEmpty(reqId)) result["requestId"] = reqId;
            WriteResult(outputPath, result, compactResult ? Formatting.None : Formatting.Indented);
            Debug.Log($"[EditorBridge] Done. Result -> {outputPath}");
        }

        private static void SaveAll(Context ctx, GameObject prefabRoot)
        {
            if (ctx.Mode == "prefab" && prefabRoot != null)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ctx.PrefabPath);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(ctx.Scene);
                EditorSceneManager.SaveScene(ctx.Scene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---- Command dispatch ---------------------------------------------------------------

        private static void Execute(JObject cmd, Context ctx, JObject entry)
        {
            var op = cmd.Value<string>("op");
            switch (op)
            {
                case "log":
                    Debug.Log($"[EditorBridge] {cmd.Value<string>("message")}");
                    break;

                case "find":
                {
                    var go = ResolveGameObject(cmd["path"] ?? cmd["name"] ?? cmd["target"], ctx);
                    Register(cmd, ctx, go);
                    entry["found"] = go != null;
                    entry["path"] = go != null ? GetPath(go.transform) : null;
                    if (go != null) EditorBridgeHighlighter.Report(go, ActionKind.Select, "-> " + go.name);
                    break;
                }

                case "findByType":
                {
                    var type = ResolveType(cmd.Value<string>("type"));
                    var comp = FindFirstOfType(type, ctx);
                    Register(cmd, ctx, comp);
                    entry["found"] = comp != null;
                    entry["path"] = comp != null ? GetPath(((Component)comp).transform) : null;
                    if (comp != null) EditorBridgeHighlighter.Report(comp, ActionKind.Select, "-> " + type.Name);
                    break;
                }

                case "createGameObject":
                {
                    var go = new GameObject(cmd.Value<string>("name") ?? "GameObject");
                    var parent = ResolveGameObject(cmd["parent"], ctx) ?? DefaultParent(ctx);
                    if (parent != null)
                        go.transform.SetParent(parent.transform, false);
                    else if (ctx.Mode != "prefab")
                        SceneManager_MoveToActive(go, ctx);

                    if (cmd["components"] is JArray comps)
                    {
                        foreach (var c in comps)
                            AddComponentByName(go, c.Value<string>());
                    }

                    Register(cmd, ctx, go);
                    entry["path"] = GetPath(go.transform);
                    EditorBridgeHighlighter.Report(go, ActionKind.Create, "+ " + go.name);
                    break;
                }

                case "instantiatePrefab":
                {
                    var asset = LoadAsset<GameObject>(cmd.Value<string>("asset"));
                    if (asset == null)
                        throw new Exception($"Prefab asset not found: {cmd.Value<string>("asset")}");

                    var parent = ResolveGameObject(cmd["parent"], ctx) ?? DefaultParent(ctx);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent != null ? parent.transform : null);
                    if (parent == null && ctx.Mode != "prefab")
                        SceneManager_MoveToActive(go, ctx);

                    Register(cmd, ctx, go);
                    entry["path"] = GetPath(go.transform);
                    EditorBridgeHighlighter.Report(go, ActionKind.Create, "+ prefab " + go.name);
                    break;
                }

                case "addComponent":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var comp = AddComponentByName(go, cmd.Value<string>("type"));
                    Register(cmd, ctx, comp);
                    EditorBridgeHighlighter.Report(go, ActionKind.Modify, "+ " + comp.GetType().Name + " on " + go.name);
                    break;
                }

                case "removeComponent":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var type = ResolveType(cmd.Value<string>("type"));
                    var comp = go.GetComponent(type);
                    if (comp != null)
                        UnityEngine.Object.DestroyImmediate(comp, true);
                    break;
                }

                case "setActive":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    go.SetActive(cmd.Value<bool>("value"));
                    break;
                }

                case "rename":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    go.name = cmd.Value<string>("name");
                    break;
                }

                case "setParent":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var parent = ResolveGameObject(cmd["parent"], ctx);
                    go.transform.SetParent(parent != null ? parent.transform : null,
                        cmd.Value<bool?>("worldPositionStays") ?? false);
                    if (cmd["siblingIndex"] != null)
                        go.transform.SetSiblingIndex(cmd.Value<int>("siblingIndex"));
                    break;
                }

                case "delete":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    EditorBridgeHighlighter.Report(go, ActionKind.Delete, "x " + go.name);
                    UnityEngine.Object.DestroyImmediate(go);
                    break;
                }

                case "setProperty":
                case "setReference":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var compName = cmd.Value<string>("component");
                    UnityEngine.Object obj = go;
                    if (!string.IsNullOrEmpty(compName))
                        obj = go.GetComponent(ResolveType(compName)) ?? throw new Exception($"Component '{compName}' not on '{go.name}'.");

                    var field = cmd.Value<string>("field") ?? cmd.Value<string>("path");
                    var so = new SerializedObject(obj);
                    var prop = so.FindProperty(field) ?? throw new Exception($"Serialized field '{field}' not found on {obj.GetType().Name}.");
                    SetSerializedValue(prop, cmd["value"], ctx);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorBridgeHighlighter.Report(go, ActionKind.Modify, "set " + field + " on " + go.name);
                    break;
                }

                case "setRectTransform":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var rt = go.GetComponent<RectTransform>() ?? throw new Exception($"'{go.name}' has no RectTransform.");
                    ApplyRectTransform(rt, cmd);
                    EditorBridgeHighlighter.Report(go, ActionKind.Modify, "rect " + go.name);
                    break;
                }

                case "setTransform":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var tr = go.transform;
                    if (cmd["localPosition"] != null) tr.localPosition = Vec3(cmd["localPosition"]);
                    if (cmd["localEulerAngles"] != null) tr.localEulerAngles = Vec3(cmd["localEulerAngles"]);
                    if (cmd["localScale"] != null) tr.localScale = Vec3(cmd["localScale"]);
                    break;
                }

                case "createPrefab":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var path = cmd.Value<string>("path") ?? throw new Exception("createPrefab requires 'path'.");
                    EnsureAssetFolder(Path.GetDirectoryName(path));
                    var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
                    Register(cmd, ctx, saved);
                    entry["asset"] = path;
                    EditorBridgeHighlighter.Report(saved, ActionKind.Create, "+ prefab " + path);
                    break;
                }

                case "duplicate":
                {
                    var src = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var parent = ResolveGameObject(cmd["parent"], ctx);
                    var parentTr = parent != null ? parent.transform : src.transform.parent;
                    var copy = UnityEngine.Object.Instantiate(src, parentTr);
                    copy.name = cmd.Value<string>("name") ?? src.name;
                    if (cmd["siblingIndex"] != null)
                        copy.transform.SetSiblingIndex(cmd.Value<int>("siblingIndex"));
                    Register(cmd, ctx, copy);
                    entry["path"] = GetPath(copy.transform);
                    EditorBridgeHighlighter.Report(copy, ActionKind.Create, "dup " + copy.name);
                    break;
                }

                case "arrayAdd":
                {
                    var prop = ArrayProperty(cmd, ctx, out var so);
                    var index = prop.arraySize;
                    prop.arraySize = index + 1;
                    SetSerializedValue(prop.GetArrayElementAtIndex(index), cmd["value"], ctx);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    entry["size"] = prop.arraySize;
                    break;
                }

                case "arrayClear":
                {
                    var prop = ArrayProperty(cmd, ctx, out var so);
                    prop.arraySize = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }

                case "createFolder":
                {
                    EnsureAssetFolder(cmd.Value<string>("path"));
                    break;
                }

                case "createScriptableObject":
                {
                    var type = ResolveType(cmd.Value<string>("type"));
                    var path = cmd.Value<string>("path") ?? throw new Exception("createScriptableObject requires 'path'.");
                    EnsureAssetFolder(Path.GetDirectoryName(path));
                    var asset = ScriptableObject.CreateInstance(type);
                    AssetDatabase.CreateAsset(asset, path);
                    Register(cmd, ctx, asset);
                    entry["asset"] = path;
                    EditorBridgeHighlighter.Report(asset, ActionKind.Create, "+ SO " + path);
                    break;
                }

                case "dumpHierarchy":
                {
                    var rootGo = ResolveGameObject(cmd["root"], ctx);
                    var maxDepth = cmd.Value<int?>("maxDepth") ?? 6;
                    var compact = cmd.Value<bool?>("compact") ?? false;
                    var nameContains = cmd.Value<string>("nameContains");
                    var ofTypeName = cmd.Value<string>("ofType");
                    var ofType = string.IsNullOrEmpty(ofTypeName) ? null : ResolveType(ofTypeName);

                    // Compact/filtered mode returns a flat list of one-line entries — far cheaper
                    // in tokens than the nested tree, and lets the caller scope the query.
                    if (compact || nameContains != null || ofType != null)
                    {
                        var maxItems = cmd.Value<int?>("maxItems") ?? 500;
                        var lines = new JArray();
                        void Walk(Transform t, int depth)
                        {
                            if (lines.Count >= maxItems) return;
                            var nameOk = nameContains == null ||
                                         t.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0;
                            var typeOk = ofType == null || t.GetComponent(ofType) != null;
                            if (nameOk && typeOk)
                                lines.Add(CompactLine(t));
                            if (depth < maxDepth)
                                for (var i = 0; i < t.childCount && lines.Count < maxItems; i++)
                                    Walk(t.GetChild(i), depth + 1);
                        }

                        if (rootGo != null) Walk(rootGo.transform, 0);
                        else foreach (var r in GetRoots(ctx)) { if (lines.Count >= maxItems) break; Walk(r.transform, 0); }
                        if (lines.Count >= maxItems)
                            entry["truncatedAt"] = maxItems;
                        entry["data"] = lines;
                        break;
                    }

                    if (rootGo != null)
                    {
                        entry["data"] = DumpNode(rootGo.transform, maxDepth, 0);
                    }
                    else
                    {
                        var arr = new JArray();
                        foreach (var r in GetRoots(ctx))
                            arr.Add(DumpNode(r.transform, maxDepth, 0));
                        entry["data"] = arr;
                    }
                    break;
                }

                case "dumpComponent":
                {
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var compName = cmd.Value<string>("component");
                    var comp = string.IsNullOrEmpty(compName)
                        ? go.GetComponents<Component>().FirstOrDefault()
                        : go.GetComponent(ResolveType(compName));
                    if (comp == null)
                        throw new Exception($"Component '{compName}' not on '{go.name}'.");
                    entry["data"] = DumpSerialized(comp);
                    EditorBridgeHighlighter.Report(comp, ActionKind.Read, "inspect " + comp.GetType().Name);
                    break;
                }

                case "getProperties":
                {
                    // Targeted read: return only the requested serialized fields instead of the
                    // whole component — cheap way to check a handful of values.
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var compName = cmd.Value<string>("component");
                    var comp = string.IsNullOrEmpty(compName)
                        ? go.GetComponents<Component>().FirstOrDefault()
                        : go.GetComponent(ResolveType(compName));
                    if (comp == null)
                        throw new Exception($"Component '{compName}' not on '{go.name}'.");

                    var so = new SerializedObject(comp);
                    var data = new JObject();
                    if (cmd["fields"] is JArray fields)
                    {
                        foreach (var f in fields)
                        {
                            var name = f.Value<string>();
                            var p = so.FindProperty(name);
                            data[name] = p != null ? DescribeProperty(p) : "<not found>";
                        }
                    }
                    entry["type"] = comp.GetType().Name;
                    entry["data"] = data;
                    EditorBridgeHighlighter.Report(comp, ActionKind.Read, "read " + comp.GetType().Name);
                    break;
                }

                case "screenshot":
                {
                    // Visual verification without reading YAML. CaptureScreenshotAsTexture only
                    // works at end-of-frame, but the bridge runs from EditorApplication.update, so
                    // we try it and fall back to the async file capture (Unity writes it at the next
                    // end-of-frame). Downscale/crop happen client-side (screenRect is returned).
                    var file = cmd.Value<string>("path") ?? "shot.png";
                    if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) file += ".png";
                    var abs = Path.Combine(EnsureBridgeFolder(), file);
                    var source = (cmd.Value<string>("source") ?? "game").ToLowerInvariant();

                    // camera/scene: synchronous RenderTexture capture — Edit mode, no focus, no Play.
                    // (Overlay UI won't appear — it isn't camera-rendered. Use for scene/3D/camera UI.)
                    if (source == "camera" || source == "scene")
                    {
                        var cam = source == "scene"
                            ? (SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null)
                            : ResolveCamera(cmd["camera"], ctx);
                        if (cam == null)
                            throw new Exception($"No camera found for source '{source}'.");

                        var maxSize = cmd.Value<int?>("maxSize") ?? 1024;
                        var aw = cam.pixelWidth > 0 ? cam.pixelWidth : 1920;
                        var ah = cam.pixelHeight > 0 ? cam.pixelHeight : 1080;
                        var scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(aw, ah));
                        var w = Mathf.Max(1, Mathf.RoundToInt(aw * scale));
                        var h = Mathf.Max(1, Mathf.RoundToInt(ah * scale));

                        var tex = RenderCamera(cam, w, h);
                        File.WriteAllBytes(abs, tex.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(tex);

                        entry["mode"] = "camera";
                        entry["source"] = source;
                        entry["camera"] = cam.name;
                        entry["width"] = w;
                        entry["height"] = h;
                        entry["path"] = abs;
                        break;
                    }

                    // source == "game" (default): async Game view capture.
                    // ScreenCapture.CaptureScreenshot resolves relative to the project root in the
                    // editor and ignores absolute Windows paths — so pass a project-relative path.
                    var rel = BridgeFolderName + "/" + file;

                    if (cmd["target"] != null)
                    {
                        var go = ResolveGameObject(cmd["target"], ctx);
                        var rt = go != null ? go.GetComponent<RectTransform>() : null;
                        if (rt != null)
                        {
                            var r = ScreenRect(rt);
                            entry["screenRect"] = new JArray(
                                Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y),
                                Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));
                        }
                    }

                    ScreenCapture.CaptureScreenshot(rel, 1); // async: captured at the next end-of-frame

                    // Force that frame to render. Default path steals NO focus: it pumps the player
                    // loop and repaints the views, which is enough in Play mode (+ runInBackground)
                    // even when Unity is unfocused. Only bring Unity to the foreground if explicitly
                    // asked (e.g. Edit mode, where nothing else drives a render).
                    if (cmd.Value<bool?>("focus") ?? false)
                    {
                        FocusUnity(cmd.Value<int?>("restoreAfterMs") ?? 1200);
                        entry["focused"] = true;
                    }
                    else
                    {
                        EditorApplication.QueuePlayerLoopUpdate();
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    }

                    entry["mode"] = "async";
                    entry["path"] = abs;
                    entry["note"] = "written at end of frame (relative to project root); downscale/crop client-side";
                    break;
                }

                case "setPlayMode":
                {
                    // Enter/exit Play mode headlessly. runInBackground is forced so the game keeps
                    // rendering while the user works in another app (needed for screenshots).
                    // The transition is deferred to just after this batch — put this LAST, then send
                    // the next batch (e.g. a screenshot) once Play has settled.
                    var v = (cmd.Value<string>("value") ?? "toggle").ToLowerInvariant();
                    var enter = v == "enter" || v == "play" || v == "true" || v == "on"
                                || (v == "toggle" && !EditorApplication.isPlaying);
                    if (enter)
                    {
                        PlayerSettings.runInBackground = true;
                        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
                    }
                    else if (EditorApplication.isPlaying)
                    {
                        EditorApplication.ExitPlaymode();
                    }
                    entry["requested"] = enter ? "enter" : "exit";
                    entry["wasPlaying"] = EditorApplication.isPlaying;
                    entry["note"] = "transition happens right after this batch; send the next batch once it settles";
                    break;
                }

                case "refresh":
                {
                    // Force an asset refresh (and optional script recompile) WITHOUT focusing the
                    // Editor. This removes the "click Unity to recompile" step after C# edits.
                    AssetDatabase.Refresh();
                    if (cmd.Value<bool?>("compile") ?? false)
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                    entry["note"] = "assets refreshed; if scripts changed a recompile + domain reload follows";
                    break;
                }

                case "focusEditor":
                {
                    // Bring the Unity window to the foreground (e.g. to force an Edit-mode render).
                    // restoreAfterMs > 0 flicks focus back to your previous window afterwards.
                    var restoreMs = cmd.Value<int?>("restoreAfterMs") ?? 0;
                    FocusUnity(restoreMs);
                    entry["note"] = restoreMs > 0 ? $"focused; restoring previous window after {restoreMs}ms" : "focused Unity";
                    break;
                }

                case "click":
                {
                    // Semantic UI click: fire the pointer down/up/click chain so the element's
                    // handlers (Button.onClick, IPointerClickHandler, …) run. No real mouse.
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var ped = NewPointer(cmd["position"], go);
                    ped.pointerPress = go;
                    ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerClickHandler);
                    entry["clicked"] = GetPath(go.transform);
                    break;
                }

                case "drag":
                {
                    // Semantic drag: pointerDown → beginDrag → drag steps → endDrag → pointerUp.
                    // Note: the whole sequence runs in ONE frame, so velocity-based consumers (the
                    // wheel) read it as a fast flick. Give `delta` (screen px) or `from`+`to`.
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var es = EventSystem.current ?? throw new Exception("drag needs Play mode with an active EventSystem.");
                    var rt = go.GetComponent<RectTransform>();
                    var start = cmd["from"] != null ? Vec2(cmd["from"])
                        : (rt != null ? ScreenRect(rt).center : Vector2.zero);
                    var total = cmd["delta"] != null ? Vec2(cmd["delta"])
                        : (cmd["to"] != null ? Vec2(cmd["to"]) - start : Vector2.zero);
                    var steps = Mathf.Max(1, cmd.Value<int?>("steps") ?? 8);

                    var ped = new PointerEventData(es)
                    {
                        button = PointerEventData.InputButton.Left,
                        position = start,
                        pressPosition = start,
                        pointerPress = go,
                        pointerDrag = go,
                    };
                    ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.beginDragHandler);
                    var per = total / steps;
                    for (var i = 0; i < steps; i++)
                    {
                        ped.delta = per;
                        ped.position += per;
                        ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.dragHandler);
                    }
                    ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.endDragHandler);
                    ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerUpHandler);
                    entry["dragged"] = GetPath(go.transform);
                    entry["totalDelta"] = $"{total.x},{total.y}";
                    break;
                }

                case "mouseMove":
                {
                    // Virtual "second mouse": glide a synthetic cursor to a screen position (does NOT
                    // touch the OS cursor). animate:true (default) interpolates over `duration` so the
                    // motion is watchable. Returns immediately; wait ~duration before the next step.
                    var pos = new Vector2(cmd.Value<float>("x"), cmd.Value<float>("y"));
                    var animate = cmd.Value<bool?>("animate") ?? true;
                    var duration = cmd.Value<double?>("duration") ?? 0.4;
                    StartCursorAnim(pos, animate ? duration : 0.0, null);
                    entry["pos"] = $"{pos.x},{pos.y}";
                    entry["animating"] = animate;
                    entry["duration"] = animate ? duration : 0.0;
                    entry["over"] = RaycastTop(pos, out var hovGo) ? GetPath(hovGo.transform) : null;
                    break;
                }

                case "mouseClick":
                {
                    // Glide to the point, THEN click the topmost UGUI hit (browser-like). With
                    // animate:true the click fires on arrival, so its result lands in the NEXT
                    // batch's `lastClickHit` (root) — wait ~duration, then read it / screenshot.
                    var es = EventSystem.current ?? throw new Exception("mouseClick needs Play mode + EventSystem.");
                    var pos = cmd["x"] != null ? new Vector2(cmd.Value<float>("x"), cmd.Value<float>("y")) : _virtualCursor;
                    var animate = cmd.Value<bool?>("animate") ?? true;
                    var duration = cmd.Value<double?>("duration") ?? 0.4;

                    if (animate)
                    {
                        StartCursorAnim(pos, duration, () => { _lastClickHit = DispatchClickAt(pos); });
                        entry["scheduled"] = true;
                        entry["duration"] = duration;
                        entry["pos"] = $"{pos.x},{pos.y}";
                    }
                    else
                    {
                        _virtualCursor = pos;
                        EnsureCursor(pos, true);
                        var hit = DispatchClickAt(pos);
                        _lastClickHit = hit;
                        entry["hit"] = hit;
                    }
                    break;
                }

                case "mouseDrag":
                {
                    var es = EventSystem.current ?? throw new Exception("mouseDrag needs Play mode + EventSystem.");
                    var from = cmd["from"] != null ? Vec2(cmd["from"]) : _virtualCursor;
                    var to = cmd["to"] != null ? Vec2(cmd["to"]) : from;
                    var animate = cmd.Value<bool?>("animate") ?? true;
                    var duration = cmd.Value<double?>("duration") ?? 0.6;

                    var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = from, pressPosition = from };
                    var results = new List<RaycastResult>();
                    es.RaycastAll(ped, results);
                    var target = results.Count > 0 ? results[0].gameObject : null;
                    _virtualCursor = from;
                    EnsureCursor(from, true);
                    if (target == null) { entry["hit"] = null; break; }
                    ped.pointerPressRaycast = results[0];
                    ped.pointerCurrentRaycast = results[0];
                    ped.pointerPress = target;
                    ped.pointerDrag = target;

                    ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.beginDragHandler);
                    _cursorPressed = true; // "grab" tint while dragging

                    if (animate)
                    {
                        var last = from;
                        StartCursorAnim(to, duration,
                            onArrive: () =>
                            {
                                ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.endDragHandler);
                                ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerUpHandler);
                                _cursorPressed = false;
                            },
                            onStep: pos =>
                            {
                                ped.delta = pos - last;
                                last = pos;
                                ped.position = pos;
                                ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.dragHandler);
                            });
                        entry["scheduled"] = true;
                    }
                    else
                    {
                        var steps = Mathf.Max(1, cmd.Value<int?>("steps") ?? 10);
                        var per = (to - from) / steps;
                        for (var i = 0; i < steps; i++)
                        {
                            ped.delta = per; ped.position += per; _virtualCursor = ped.position; EnsureCursor(ped.position, true);
                            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.dragHandler);
                        }
                        ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.endDragHandler);
                        ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerUpHandler);
                        _cursorPressed = false;
                    }
                    entry["target"] = GetPath(target.transform);
                    entry["to"] = $"{to.x},{to.y}";
                    break;
                }

                case "cursorHide":
                {
                    if (_cursorCanvas != null) _cursorCanvas.SetActive(false);
                    break;
                }

                case "invoke":
                {
                    // Call a method on a component by reflection (public or private).
                    var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                    var compName = cmd.Value<string>("component");
                    var comp = string.IsNullOrEmpty(compName)
                        ? go.GetComponents<Component>().FirstOrDefault()
                        : go.GetComponent(ResolveType(compName));
                    if (comp == null)
                        throw new Exception($"Component '{compName}' not on '{go.name}'.");

                    var methodName = cmd.Value<string>("method") ?? throw new Exception("invoke requires 'method'.");
                    var argsArr = cmd["args"] as JArray;
                    var argCount = argsArr?.Count ?? 0;
                    var method = comp.GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == argCount)
                        ?? throw new Exception($"Method '{methodName}' with {argCount} arg(s) not found on {comp.GetType().Name}.");

                    object[] args = null;
                    if (argCount > 0)
                    {
                        var ps = method.GetParameters();
                        args = new object[argCount];
                        for (var i = 0; i < argCount; i++)
                            args[i] = argsArr[i].ToObject(ps[i].ParameterType);
                    }

                    var ret = method.Invoke(comp, args);
                    entry["invoked"] = methodName;
                    if (ret != null) entry["returned"] = ret.ToString();
                    break;
                }

                case "call":
                {
                    // Power tool: invoke ANY method, or get/set ANY field/property (public OR private) by
                    // reflection — STATIC (give 'type') or on an INSTANCE (give 'target' selector, optionally
                    // a component 'type' on it). Args are coerced to the parameter types; the return value is
                    // serialized back. Lets Claude drive game logic directly without simulating input.
                    var member = cmd.Value<string>("member") ?? throw new Exception("call requires 'member'.");
                    var action = (cmd.Value<string>("action") ?? "call").ToLowerInvariant();
                    var typeName = cmd.Value<string>("type");

                    object instance = null;
                    Type type;
                    if (cmd["target"] != null)
                    {
                        var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                        if (!string.IsNullOrEmpty(typeName))
                        {
                            var comp = go.GetComponent(ResolveType(typeName)) ?? throw new Exception($"Component '{typeName}' not on '{go.name}'.");
                            instance = comp; type = comp.GetType();
                        }
                        else { instance = go; type = go.GetType(); }
                    }
                    else
                    {
                        type = ResolveType(typeName ?? throw new Exception("call requires 'type' (static) or 'target' (instance)."));
                    }

                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

                    if (action == "get")
                    {
                        var pi = type.GetProperty(member, flags);
                        object val = pi != null
                            ? pi.GetValue(instance)
                            : (type.GetField(member, flags) ?? throw new Exception($"No field/property '{member}' on {type.Name}.")).GetValue(instance);
                        entry["value"] = SerializeReturn(val);
                    }
                    else if (action == "set")
                    {
                        var pi = type.GetProperty(member, flags);
                        if (pi != null) pi.SetValue(instance, CoerceArg(cmd["value"], pi.PropertyType));
                        else
                        {
                            var fi = type.GetField(member, flags) ?? throw new Exception($"No field/property '{member}' on {type.Name}.");
                            fi.SetValue(instance, CoerceArg(cmd["value"], fi.FieldType));
                        }
                        entry["set"] = member;
                    }
                    else
                    {
                        var argsArr = cmd["args"] as JArray;
                        var argCount = argsArr?.Count ?? 0;
                        var method = type.GetMethods(flags).FirstOrDefault(m => m.Name == member && m.GetParameters().Length == argCount)
                                     ?? throw new Exception($"Method '{member}' with {argCount} arg(s) not found on {type.Name}.");
                        object[] args = null;
                        if (argCount > 0)
                        {
                            var ps = method.GetParameters();
                            args = new object[argCount];
                            for (var i = 0; i < argCount; i++)
                            {
                                var pt = ps[i].ParameterType;
                                if (typeof(UnityEngine.Object).IsAssignableFrom(pt) && argsArr[i].Type != JTokenType.Null)
                                {
                                    var ago = ResolveGameObject(argsArr[i], ctx) ?? throw NotFound(argsArr[i]);
                                    if (pt == typeof(GameObject)) args[i] = ago;
                                    else if (pt == typeof(Transform)) args[i] = ago.transform;
                                    else args[i] = ago.GetComponent(pt) ?? throw new Exception($"Component '{pt.Name}' not on '{ago.name}'.");
                                }
                                else args[i] = CoerceArg(argsArr[i], pt);
                            }
                        }
                        var ret2 = method.Invoke(instance, args);
                        entry["called"] = member;
                        if (method.ReturnType != typeof(void)) entry["returned"] = SerializeReturn(ret2);
                    }
                    break;
                }

                case "eval":
                {
                    // Run a JavaScript snippet via the Jint interpreter with full CLR/Unity access. For
                    // MULTI-STEP test logic (loops, filtering, composition) that would otherwise need many
                    // round-trips. 'UnityEngine' is pre-imported; helpers: find(name)->GameObject,
                    // findType(name)->Type, log(x)->Debug.Log. Returns the value of the last expression.
                    var js = cmd.Value<string>("js") ?? throw new Exception("eval requires 'js'.");
                    var clrAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => { var nm = a.GetName().Name; return nm == "Assembly-CSharp" || nm.StartsWith("UnityEngine"); })
                        .ToArray();
                    var engine = new Jint.Engine(cfg => cfg.AllowClr(clrAssemblies));
                    engine.SetValue("log", new Action<object>(o => Debug.Log("[eval] " + (o ?? "null"))));
                    engine.SetValue("find", new Func<string, GameObject>(GameObject.Find));
                    engine.SetValue("findType", new Func<string, Type>(ResolveType));
                    engine.Execute("var UnityEngine = importNamespace('UnityEngine');");
                    engine.Execute(js);
                    var completion = engine.GetCompletionValue();
                    object clr = (completion == null || completion.IsUndefined() || completion.IsNull()) ? null : completion.ToObject();
                    entry["result"] = SerializeReturn(clr);
                    break;
                }

                case "assert":
                {
                    // Deterministic check → { pass, actual, expected? }. The verification primitive
                    // a loop can branch on. kind: property (default) | exists | active | component |
                    // childCount.
                    var kind = (cmd.Value<string>("kind") ?? "property").ToLowerInvariant();
                    bool pass;
                    string actual;

                    switch (kind)
                    {
                        case "exists":
                        {
                            var go = ResolveGameObject(cmd["target"], ctx);
                            var want = cmd.Value<bool?>("equals") ?? true;
                            pass = (go != null) == want;
                            actual = go != null ? GetPath(go.transform) : "null";
                            break;
                        }
                        case "active":
                        {
                            var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                            var a = (cmd.Value<bool?>("inHierarchy") ?? false) ? go.activeInHierarchy : go.activeSelf;
                            var want = cmd.Value<bool?>("equals") ?? true;
                            pass = a == want;
                            actual = a.ToString();
                            break;
                        }
                        case "component":
                        {
                            var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                            var has = go.GetComponent(ResolveType(cmd.Value<string>("component"))) != null;
                            var want = cmd.Value<bool?>("equals") ?? true;
                            pass = has == want;
                            actual = has.ToString();
                            break;
                        }
                        case "childcount":
                        {
                            var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                            var a = go.transform.childCount;
                            actual = a.ToString();
                            pass = CompareNumber(a, cmd);
                            break;
                        }
                        case "console":
                        {
                            if (!TryConsoleCounts(out var e, out var w, out var l))
                                throw new Exception("UnityEditor.LogEntries API not found.");
                            var metric = (cmd.Value<string>("metric") ?? "errors").ToLowerInvariant();
                            var a = metric == "warnings" ? w : metric == "logs" ? l : e;
                            actual = a.ToString();
                            // default expectation: none (max 0) unless the caller specified bounds
                            pass = cmd["equals"] == null && cmd["min"] == null && cmd["max"] == null
                                ? a == 0
                                : CompareNumber(a, cmd);
                            break;
                        }
                        default: // property
                        {
                            var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
                            var compName = cmd.Value<string>("component");
                            var comp = string.IsNullOrEmpty(compName)
                                ? go.GetComponents<Component>().FirstOrDefault()
                                : go.GetComponent(ResolveType(compName));
                            if (comp == null)
                                throw new Exception($"Component '{compName}' not on '{go.name}'.");
                            var field = cmd.Value<string>("field") ?? throw new Exception("assert property requires 'field'.");
                            var prop = new SerializedObject(comp).FindProperty(field)
                                       ?? throw new Exception($"Serialized field '{field}' not found on {comp.GetType().Name}.");
                            actual = DescribeProperty(prop);
                            pass = CheckProperty(prop, cmd, actual);
                            break;
                        }
                    }

                    entry["kind"] = kind;
                    entry["pass"] = pass;
                    entry["actual"] = actual;
                    if (cmd["equals"] != null) entry["expected"] = cmd["equals"];
                    if (!pass) Debug.LogWarning($"[EditorBridge] assert FAILED ({kind}): expected {cmd["equals"] ?? cmd["min"] ?? cmd["max"]}, got {actual}");
                    break;
                }

                case "menuItem":
                {
                    // Trigger any Editor menu command (Unity's or custom) deterministically — the
                    // non-intrusive way to "click the editor GUI", no real cursor involved.
                    var item = cmd.Value<string>("path") ?? throw new Exception("menuItem requires 'path'.");
                    entry["executed"] = EditorApplication.ExecuteMenuItem(item);
                    break;
                }

                case "osScreenshot":
                {
                    // GLOBAL screenshot: brings Unity forward, captures the whole editor window
                    // ("main") or the entire desktop ("screen"), then RESTORES the prior foreground /
                    // minimized state. Async (written after ~delayMs) — read the PNG at `path`.
                    var file = cmd.Value<string>("path") ?? "editor_screenshot.png";
                    var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                    var abs = Path.Combine(projectRoot, BridgeFolderName, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(abs));
                    var window = (cmd.Value<string>("window") ?? "main").ToLowerInvariant();
                    var delay = cmd.Value<int?>("delayMs") ?? 250;
                    var restore = cmd.Value<bool?>("restore") ?? true;
                    OsInput.CaptureEditor(abs, window, delay, restore);
                    entry["path"] = abs;
                    entry["mode"] = window;
                    entry["note"] = $"captured after ~{delay}ms; editor state restored={restore}";
                    break;
                }

                case "osMouseMove":
                {
                    OsInput.MouseMove(cmd.Value<int?>("x") ?? 0, cmd.Value<int?>("y") ?? 0);
                    entry["moved"] = true;
                    break;
                }

                case "osMouseClick":
                {
                    // Real OS click at absolute screen (x,y). Omit x/y to click at the current cursor.
                    var mx = cmd["x"] != null ? (int?)cmd.Value<int>("x") : null;
                    var my = cmd["y"] != null ? (int?)cmd.Value<int>("y") : null;
                    var button = cmd.Value<string>("button") ?? "left";
                    var dbl = cmd.Value<bool?>("double") ?? false;
                    OsInput.MouseClick(mx, my, button, dbl);
                    entry["clicked"] = true;
                    break;
                }

                case "osMouseDrag":
                {
                    var from = Vec2(cmd["from"] ?? throw new Exception("osMouseDrag requires 'from'."));
                    var to = Vec2(cmd["to"] ?? throw new Exception("osMouseDrag requires 'to'."));
                    var button = cmd.Value<string>("button") ?? "left";
                    var steps = cmd.Value<int?>("steps") ?? 20;
                    OsInput.MouseDrag((int)from.x, (int)from.y, (int)to.x, (int)to.y, button, steps);
                    entry["dragged"] = true;
                    break;
                }

                case "osKeyPress":
                {
                    // Real OS keystroke to the focused window (tap "key" count times; holdMs holds each).
                    var key = cmd.Value<string>("key") ?? throw new Exception("osKeyPress requires 'key'.");
                    var count = cmd.Value<int?>("count") ?? 1;
                    var holdMs = cmd.Value<int?>("holdMs") ?? 0;
                    OsInput.KeyPress(key, count, holdMs);
                    entry["pressed"] = key;
                    entry["count"] = count;
                    break;
                }

                case "osKeyDown":
                {
                    var key = cmd.Value<string>("key") ?? throw new Exception("osKeyDown requires 'key'.");
                    OsInput.KeyDown(key);
                    entry["down"] = key;
                    break;
                }

                case "osKeyUp":
                {
                    var key = cmd.Value<string>("key") ?? throw new Exception("osKeyUp requires 'key'.");
                    OsInput.KeyUp(key);
                    entry["up"] = key;
                    break;
                }

                case "osHotkey":
                {
                    var arr = cmd["keys"] as JArray ?? throw new Exception("osHotkey requires 'keys' array, e.g. [\"ctrl\",\"s\"].");
                    var keys = new string[arr.Count];
                    for (var ki = 0; ki < arr.Count; ki++) keys[ki] = arr[ki].ToString();
                    OsInput.Hotkey(keys);
                    entry["hotkey"] = string.Join("+", keys);
                    break;
                }

                case "osType":
                {
                    var text = cmd.Value<string>("text") ?? throw new Exception("osType requires 'text'.");
                    OsInput.TypeText(text);
                    entry["typed"] = text.Length;
                    break;
                }

                case "exportPackage":
                {
                    // Build the portable Claude tooling into a .unitypackage for dropping into other projects.
                    var outPath = cmd.Value<string>("path"); // optional; null → ClaudeForUnity-Packages/<timestamp>
                    var abs = ClaudeForUnityExporter.Export(outPath);
                    if (string.IsNullOrEmpty(abs)) throw new Exception("exportPackage failed (no tooling assets found).");
                    entry["path"] = abs;
                    break;
                }

                case "clearConsole":
                {
                    // Reset the console so a following consoleCounts only sees new messages.
                    entry["cleared"] = TryClearConsole();
                    break;
                }

                case "consoleCounts":
                {
                    if (TryConsoleCounts(out var e, out var w, out var l))
                    {
                        entry["errors"] = e;
                        entry["warnings"] = w;
                        entry["logs"] = l;
                    }
                    else
                    {
                        entry["unavailable"] = true;
                        entry["note"] = "UnityEditor.LogEntries API not found on this Unity version";
                    }
                    break;
                }

                case "saveScene":
                    EditorSceneManager.MarkSceneDirty(ctx.Scene);
                    EditorSceneManager.SaveScene(ctx.Scene);
                    break;

                case "saveAssets":
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    break;

                default:
                    throw new Exception($"Unknown op '{op}'.");
            }
        }

        // ---- Value setting ------------------------------------------------------------------

        private static SerializedProperty ArrayProperty(JObject cmd, Context ctx, out SerializedObject so)
        {
            var go = ResolveGameObject(cmd["target"], ctx) ?? throw NotFound(cmd["target"]);
            var compName = cmd.Value<string>("component");
            UnityEngine.Object obj = go;
            if (!string.IsNullOrEmpty(compName))
                obj = go.GetComponent(ResolveType(compName)) ?? throw new Exception($"Component '{compName}' not on '{go.name}'.");

            var field = cmd.Value<string>("field") ?? cmd.Value<string>("path");
            so = new SerializedObject(obj);
            var prop = so.FindProperty(field) ?? throw new Exception($"Serialized field '{field}' not found on {obj.GetType().Name}.");
            if (!prop.isArray)
                throw new Exception($"Field '{field}' is not an array/list.");
            return prop;
        }

        private static void SetSerializedValue(SerializedProperty prop, JToken value, Context ctx)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.longValue = value.Value<long>();
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.Value<bool>();
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.Value<float>();
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.Value<string>();
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = ParseColor(value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = ResolveObject(value, ctx);
                    break;
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.String)
                    {
                        var name = value.Value<string>();
                        var idx = Array.FindIndex(prop.enumNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                        if (idx < 0) throw new Exception($"Enum value '{name}' not found. Options: {string.Join(", ", prop.enumNames)}");
                        prop.enumValueIndex = idx;
                    }
                    else
                    {
                        prop.intValue = value.Value<int>();
                    }
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = Vec2(value);
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = Vec3(value);
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = Vec4(value);
                    break;
                case SerializedPropertyType.Quaternion:
                    prop.quaternionValue = Quaternion.Euler(Vec3(value));
                    break;
                case SerializedPropertyType.ArraySize:
                    prop.arraySize = value.Value<int>();
                    break;
                default:
                    throw new Exception($"Unsupported property type '{prop.propertyType}' for '{prop.name}'.");
            }
        }

        private static void ApplyRectTransform(RectTransform rt, JObject cmd)
        {
            if (cmd["anchorMin"] != null) rt.anchorMin = Vec2(cmd["anchorMin"]);
            if (cmd["anchorMax"] != null) rt.anchorMax = Vec2(cmd["anchorMax"]);
            if (cmd["pivot"] != null) rt.pivot = Vec2(cmd["pivot"]);
            if (cmd["sizeDelta"] != null) rt.sizeDelta = Vec2(cmd["sizeDelta"]);
            if (cmd["anchoredPosition"] != null) rt.anchoredPosition = Vec2(cmd["anchoredPosition"]);
            if (cmd["offsetMin"] != null) rt.offsetMin = Vec2(cmd["offsetMin"]);
            if (cmd["offsetMax"] != null) rt.offsetMax = Vec2(cmd["offsetMax"]);
            if (cmd["localScale"] != null) rt.localScale = Vec3(cmd["localScale"]);
            if (cmd["eulerAngles"] != null) rt.localEulerAngles = Vec3(cmd["eulerAngles"]);
        }

        // ---- Selectors ----------------------------------------------------------------------

        private static UnityEngine.Object ResolveObject(JToken token, Context ctx)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            if (token.Type == JTokenType.String)
            {
                var s = token.Value<string>();
                if (ctx.Registry.TryGetValue(s, out var reg))
                    return reg;
                var go = FindByPath(s, ctx) ?? FindByName(s, ctx);
                return go;
            }

            if (token is JObject o)
            {
                if (o["$null"] != null) return null;
                if (o["$ref"] != null)
                    return ctx.Registry.TryGetValue(o.Value<string>("$ref"), out var r) ? r : null;
                if (o["$path"] != null)
                    return FindByPath(o.Value<string>("$path"), ctx);
                if (o["$name"] != null)
                    return FindByName(o.Value<string>("$name"), ctx);
                if (o["$find"] != null)
                    return FindFirstOfType(ResolveType(o.Value<string>("$find")), ctx);
                if (o["$asset"] != null)
                    return LoadAssetGeneric(o.Value<string>("$asset"));
                if (o["$component"] is JObject c)
                {
                    var owner = ResolveGameObject(c["ref"] ?? c["$path"] ?? c["path"] ?? c["target"], ctx);
                    if (owner == null) return null;
                    return owner.GetComponent(ResolveType(c.Value<string>("type")));
                }
                if (o["$child"] is JObject ch)
                {
                    var owner = ResolveGameObject(ch["ref"] ?? ch["$path"] ?? ch["path"] ?? ch["target"], ctx);
                    if (owner == null) return null;
                    var descendant = SearchByName(owner.transform, ch.Value<string>("name"));
                    if (descendant == null) return null;
                    var type = ch.Value<string>("type");
                    return string.IsNullOrEmpty(type)
                        ? (UnityEngine.Object)descendant.gameObject
                        : descendant.GetComponent(ResolveType(type));
                }
            }

            return null;
        }

        private static GameObject ResolveGameObject(JToken token, Context ctx)
        {
            var obj = ResolveObject(token, ctx);
            switch (obj)
            {
                case null: return null;
                case GameObject go: return go;
                case Component comp: return comp.gameObject;
                default: return null;
            }
        }

        private static GameObject FindByPath(string path, Context ctx)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var parts = path.Split('/');
            foreach (var root in GetRoots(ctx))
            {
                if (root.name != parts[0]) continue;
                var current = root.transform;
                var ok = true;
                for (var i = 1; i < parts.Length && ok; i++)
                {
                    var next = FindChild(current, parts[i]);
                    if (next == null) { ok = false; break; }
                    current = next;
                }
                if (ok) return current.gameObject;
            }

            // Fallback: the first segment may not be a scene root (e.g. a deep object name).
            // Locate it anywhere, then descend by child names.
            var start = FindByName(parts[0], ctx);
            if (start != null)
            {
                var current = start.transform;
                var ok = true;
                for (var i = 1; i < parts.Length && ok; i++)
                {
                    var next = FindChild(current, parts[i]);
                    if (next == null) { ok = false; break; }
                    current = next;
                }
                if (ok) return current.gameObject;
            }

            return null;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name)
                    return parent.GetChild(i);
            return null;
        }

        private static GameObject FindByName(string name, Context ctx)
        {
            foreach (var root in GetRoots(ctx))
            {
                var found = SearchByName(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform SearchByName(Transform t, string name)
        {
            if (t.name == name) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var r = SearchByName(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static UnityEngine.Object FindFirstOfType(Type type, Context ctx)
        {
            foreach (var root in GetRoots(ctx))
            {
                var comp = root.GetComponentInChildren(type, true);
                if (comp != null) return comp;
            }
            return null;
        }

        private static GameObject[] GetRoots(Context ctx)
        {
            if (ctx.Mode == "prefab" && ctx.PrefabRoot != null)
                return new[] { ctx.PrefabRoot };
            return ctx.Scene.GetRootGameObjects();
        }

        private static GameObject DefaultParent(Context ctx)
        {
            return ctx.Mode == "prefab" ? ctx.PrefabRoot : null;
        }

        private static void SceneManager_MoveToActive(GameObject go, Context ctx)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, ctx.Scene);
        }

        // ---- Components / types / assets ----------------------------------------------------

        private static Component AddComponentByName(GameObject go, string typeName)
        {
            var type = ResolveType(typeName);
            return go.AddComponent(type);
        }

        /// <summary>Coerce a JSON token into a target CLR type for a reflection call arg / field set:
        /// Unity vector/color/quaternion from a [..] array, enums by name or int, else Json.NET ToObject.</summary>
        private static object CoerceArg(JToken token, Type t)
        {
            if (token == null || token.Type == JTokenType.Null)
                return t.IsValueType ? Activator.CreateInstance(t) : null;

            if (t == typeof(Vector2)) { var v = ToFloats(token, 2); return new Vector2(v[0], v[1]); }
            if (t == typeof(Vector3)) { var v = ToFloats(token, 3); return new Vector3(v[0], v[1], v[2]); }
            if (t == typeof(Vector4)) { var v = ToFloats(token, 4); return new Vector4(v[0], v[1], v[2], v[3]); }
            if (t == typeof(Quaternion)) { var v = ToFloats(token, 4); return new Quaternion(v[0], v[1], v[2], v[3]); }
            if (t == typeof(Color)) { var v = ToFloats(token, 4); return new Color(v[0], v[1], v[2], v[3]); }

            if (t.IsEnum)
                return token.Type == JTokenType.Integer ? Enum.ToObject(t, token.ToObject<int>()) : Enum.Parse(t, token.ToString(), true);

            try { return token.ToObject(t); }
            catch { return Convert.ChangeType(token.ToString(), t); }
        }

        private static float[] ToFloats(JToken token, int n)
        {
            var r = new float[Math.Max(n, 4)];
            if (n == 4) r[3] = 1f; // sensible default alpha/w
            if (token is JArray arr)
            {
                for (var i = 0; i < n && i < arr.Count; i++) r[i] = arr[i].ToObject<float>();
            }
            else if (token is JObject o)
            {
                string[] keys = { "x", "y", "z", "w" };
                string[] ckeys = { "r", "g", "b", "a" };
                for (var i = 0; i < n; i++)
                {
                    if (o[keys[i]] != null) r[i] = o[keys[i]].ToObject<float>();
                    else if (o[ckeys[i]] != null) r[i] = o[ckeys[i]].ToObject<float>();
                }
            }
            return r;
        }

        /// <summary>Serialize a reflection return value / field value into a compact JSON token.</summary>
        private static JToken SerializeReturn(object val)
        {
            if (val == null) return JValue.CreateNull();
            if (val is string s) return s;
            if (val is bool b) return b;
            if (val.GetType().IsPrimitive || val is decimal) return JToken.FromObject(val);
            if (val is Enum e) return e.ToString();
            if (val is Vector2 v2) return new JArray(v2.x, v2.y);
            if (val is Vector3 v3) return new JArray(v3.x, v3.y, v3.z);
            if (val is Vector4 v4) return new JArray(v4.x, v4.y, v4.z, v4.w);
            if (val is Quaternion q) return new JArray(q.x, q.y, q.z, q.w);
            if (val is Color c) return new JArray(c.r, c.g, c.b, c.a);
            if (val is UnityEngine.Object uo) return uo == null ? (JToken)JValue.CreateNull() : $"{uo.name} ({uo.GetType().Name})";
            if (val is System.Collections.IEnumerable en)
            {
                var arr = new JArray();
                var cnt = 0;
                foreach (var item in en)
                {
                    if (cnt++ >= 20) { arr.Add("… (truncated)"); break; }
                    arr.Add(SerializeReturn(item));
                }
                return arr;
            }
            return val.ToString();
        }

        private static Type ResolveType(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new Exception("Type name is empty.");

            if (Shortcuts.TryGetValue(name, out var full))
                name = full;

            var direct = Type.GetType(name);
            if (direct != null) return direct;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null) return t;
            }

            // Fallback: match by simple name across assemblies.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }
                var match = types.FirstOrDefault(t => t.Name == name && typeof(UnityEngine.Object).IsAssignableFrom(t));
                if (match != null) return match;
            }

            throw new Exception($"Could not resolve type '{name}'.");
        }

        private static readonly Dictionary<string, string> Shortcuts = new Dictionary<string, string>
        {
            { "RectTransform", "UnityEngine.RectTransform" },
            { "Transform", "UnityEngine.Transform" },
            { "CanvasGroup", "UnityEngine.CanvasGroup" },
            { "Canvas", "UnityEngine.Canvas" },
            { "CanvasScaler", "UnityEngine.UI.CanvasScaler" },
            { "GraphicRaycaster", "UnityEngine.UI.GraphicRaycaster" },
            { "Image", "UnityEngine.UI.Image" },
            { "RawImage", "UnityEngine.UI.RawImage" },
            { "Button", "UnityEngine.UI.Button" },
            { "Toggle", "UnityEngine.UI.Toggle" },
            { "Slider", "UnityEngine.UI.Slider" },
            { "ScrollRect", "UnityEngine.UI.ScrollRect" },
            { "Mask", "UnityEngine.UI.Mask" },
            { "RectMask2D", "UnityEngine.UI.RectMask2D" },
            { "HorizontalLayoutGroup", "UnityEngine.UI.HorizontalLayoutGroup" },
            { "VerticalLayoutGroup", "UnityEngine.UI.VerticalLayoutGroup" },
            { "GridLayoutGroup", "UnityEngine.UI.GridLayoutGroup" },
            { "ContentSizeFitter", "UnityEngine.UI.ContentSizeFitter" },
            { "LayoutElement", "UnityEngine.UI.LayoutElement" },
            { "Text", "UnityEngine.UI.Text" },
            { "TMP_Text", "TMPro.TextMeshProUGUI" },
            { "TextMeshProUGUI", "TMPro.TextMeshProUGUI" },
        };

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static UnityEngine.Object LoadAssetGeneric(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.EndsWith(".prefab")) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = (folder ?? string.Empty).Replace("\\", "/");
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            var name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }

        // ---- Dumps --------------------------------------------------------------------------

        private static JObject DumpNode(Transform t, int maxDepth, int depth)
        {
            var node = new JObject
            {
                ["name"] = t.name,
                ["active"] = t.gameObject.activeSelf,
                ["components"] = new JArray(t.GetComponents<Component>()
                    .Select(c => c == null ? "<missing>" : c.GetType().Name))
            };

            if (t is RectTransform rt)
            {
                node["rect"] = new JObject
                {
                    ["anchorMin"] = $"{rt.anchorMin.x},{rt.anchorMin.y}",
                    ["anchorMax"] = $"{rt.anchorMax.x},{rt.anchorMax.y}",
                    ["sizeDelta"] = $"{rt.sizeDelta.x},{rt.sizeDelta.y}",
                    ["anchoredPosition"] = $"{rt.anchoredPosition.x},{rt.anchoredPosition.y}"
                };
            }

            if (depth < maxDepth && t.childCount > 0)
            {
                var children = new JArray();
                for (var i = 0; i < t.childCount; i++)
                    children.Add(DumpNode(t.GetChild(i), maxDepth, depth + 1));
                node["children"] = children;
            }
            else if (t.childCount > 0)
            {
                node["childCount"] = t.childCount;
            }

            return node;
        }

        private static JObject DumpSerialized(UnityEngine.Object obj)
        {
            var result = new JObject { ["type"] = obj.GetType().FullName };
            var fields = new JObject();
            var so = new SerializedObject(obj);
            var it = so.GetIterator();
            var enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                fields[it.propertyPath] = DescribeProperty(it);
            }
            result["fields"] = fields;
            return result;
        }

        private static string DescribeProperty(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    return p.objectReferenceValue != null
                        ? $"{p.objectReferenceValue.GetType().Name}:{p.objectReferenceValue.name}"
                        : "null";
                case SerializedPropertyType.Integer: return p.longValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.floatValue.ToString("R");
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length ? p.enumNames[p.enumValueIndex] : p.intValue.ToString();
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                default: return $"<{p.propertyType}>";
            }
        }

        private static string CompactLine(Transform t)
        {
            var comps = string.Join(",", t.GetComponents<Component>()
                .Select(c => c == null ? "<missing>" : c.GetType().Name));
            var inactive = t.gameObject.activeInHierarchy ? "" : " (inactive)";
            return $"{GetPath(t)} [{comps}]{inactive}";
        }

        // ---- Screenshot ---------------------------------------------------------------------

        /// <summary>Screen-space pixel rect of a RectTransform (bottom-left origin, matching Screen.*).</summary>
        private static Rect ScreenRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // for an overlay canvas these are already screen pixels
            var canvas = rt.GetComponentInParent<Canvas>();

            Vector2 ToScreen(Vector3 world)
            {
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
                    return RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, world);
                return new Vector2(world.x, world.y);
            }

            var bl = ToScreen(corners[0]);
            var tr = ToScreen(corners[2]);
            var xMin = Mathf.Min(bl.x, tr.x);
            var yMin = Mathf.Min(bl.y, tr.y);
            return new Rect(xMin, yMin, Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        }

        // ---- Semantic input / virtual mouse -------------------------------------------------

        private static Vector2 _virtualCursor;
        private static GameObject _cursorCanvas;

        private static bool RaycastTop(Vector2 pos, out GameObject hit)
        {
            hit = null;
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = pos };
            var results = new List<RaycastResult>();
            es.RaycastAll(ped, results);
            if (results.Count == 0) return false;
            hit = results[0].gameObject;
            return true;
        }

        private static Sprite _arrowSprite;

        // Big gradient arrow + a click-ripple ring on a top-most overlay canvas — a synthetic
        // "second cursor" that clearly stands apart from the OS pointer. Positions the tip at the
        // click point every frame. Never intercepts input.
        private static void EnsureCursor(Vector2 pos, bool show)
        {
            if (_cursorCanvas == null) BuildCursor();
            _cursorArrow.anchoredPosition = pos;
            _cursorRipple.anchoredPosition = pos;
            if (_cursorCanvas.activeSelf != show) _cursorCanvas.SetActive(show);
        }

        private static void BuildCursor()
        {
            _cursorCanvas = new GameObject("BridgeCursorCanvas", typeof(Canvas));
            var cv = _cursorCanvas.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = short.MaxValue;

            // ripple ring (behind the arrow), centered on the tip
            var rip = new GameObject("Ripple", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            rip.transform.SetParent(_cursorCanvas.transform, false);
            _rippleImg = rip.GetComponent<UnityEngine.UI.Image>();
            _rippleImg.sprite = RingSprite();
            _rippleImg.raycastTarget = false;
            _rippleImg.color = new Color(1f, 0.3f, 0.72f, 0f);
            _cursorRipple = (RectTransform)rip.transform;
            _cursorRipple.anchorMin = _cursorRipple.anchorMax = Vector2.zero;
            _cursorRipple.pivot = new Vector2(0.5f, 0.5f);
            _cursorRipple.sizeDelta = new Vector2(150f, 150f);

            // arrow (front); pivot at the tip so anchoredPosition == click point
            var ar = new GameObject("Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            ar.transform.SetParent(_cursorCanvas.transform, false);
            _arrowImg = ar.GetComponent<UnityEngine.UI.Image>();
            _arrowImg.sprite = ArrowSprite();
            _arrowImg.color = Color.white;
            _arrowImg.raycastTarget = false;
            _cursorArrow = (RectTransform)ar.transform;
            _cursorArrow.anchorMin = _cursorArrow.anchorMax = Vector2.zero;
            _cursorArrow.pivot = new Vector2(22f / 160f, 1f - 22f / 200f); // tip (margin,margin) of the sprite
            _cursorArrow.sizeDelta = new Vector2(160f, 200f);              // ~3x the old cursor
        }

        // Click ripple + press dip; call right after a click/press lands.
        private static void TriggerPulse()
        {
            _pulseActive = true;
            _pulseStart = EditorApplication.timeSinceStartup;
            if (_cursorCanvas != null && !_cursorCanvas.activeSelf) _cursorCanvas.SetActive(true);
        }

        /// <summary>Play-mode action feedback: pulse a colored ring at a target's screen position.</summary>
        public static void PulseTarget(GameObject go, Color color)
        {
            if (!EditorApplication.isPlaying || go == null) return;

            Vector2 pos;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                pos = ScreenRect(rt).center;
            }
            else
            {
                var cam = Camera.main ?? Camera.allCameras.FirstOrDefault(c => c.isActiveAndEnabled);
                if (cam == null) return;
                var sp = cam.WorldToScreenPoint(go.transform.position);
                if (sp.z < 0f) return; // behind the camera
                pos = new Vector2(sp.x, sp.y);
            }
            PulseAt(pos, color);
        }

        public static void PulseAt(Vector2 screenPos, Color color)
        {
            if (_actionCanvas == null)
            {
                _actionCanvas = new GameObject("BridgeActionCanvas", typeof(Canvas));
                var cv = _actionCanvas.GetComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                cv.sortingOrder = short.MaxValue - 1;

                var ring = new GameObject("ActionPulse", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                ring.transform.SetParent(_actionCanvas.transform, false);
                _actionRingImg = ring.GetComponent<UnityEngine.UI.Image>();
                _actionRingImg.sprite = RingSprite();
                _actionRingImg.raycastTarget = false;
                _actionRing = (RectTransform)ring.transform;
                _actionRing.anchorMin = _actionRing.anchorMax = Vector2.zero;
                _actionRing.pivot = new Vector2(0.5f, 0.5f);
                _actionRing.sizeDelta = new Vector2(130f, 130f);
            }

            _actionRing.anchoredPosition = screenPos;
            _actionPulseColor = color;
            _actionPulseStart = EditorApplication.timeSinceStartup;
            _actionPulseActive = true;
            _actionCanvas.SetActive(true);
        }

        // Applies the transient visual states each frame:
        //   click → magenta ripple expands + fades, arrow does a quick press dip;
        //   drag  → steady cyan "grab" halo + a slightly smaller arrow;
        //   idle  → just the crisp gradient arrow.
        private static void UpdateCursorVisual()
        {
            if (_cursorArrow == null) return;
            var arrowScale = _cursorPressed ? 0.9f : 1f;

            if (_pulseActive)
            {
                var pt = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - _pulseStart) / PulseDur));
                var rs = Mathf.Lerp(0.35f, 1.9f, pt);
                _cursorRipple.localScale = new Vector3(rs, rs, 1f);
                _rippleImg.color = new Color(1f, 0.3f, 0.72f, Mathf.Lerp(0.85f, 0f, pt)); // magenta ripple
                var dip = pt < 0.4f ? Mathf.Sin(pt / 0.4f * Mathf.PI) : 0f;               // quick down-and-up
                arrowScale *= 1f - 0.18f * dip;
                if (pt >= 1f) _pulseActive = false;
            }
            else if (_cursorPressed)
            {
                _cursorRipple.localScale = Vector3.one * 0.72f;
                _rippleImg.color = new Color(0.35f, 0.85f, 1f, 0.55f); // steady cyan grab halo
            }
            else
            {
                var c = _rippleImg.color; c.a = 0f; _rippleImg.color = c;
            }

            _cursorArrow.localScale = new Vector3(arrowScale, arrowScale, 1f);
            if (_arrowImg != null) _arrowImg.color = Color.white; // keep the gradient crisp
        }

        // Premium up-left arrow: magenta→violet gradient fill, white edge highlight, dark hug
        // outline and a soft magenta glow halo. Rasterized once. Tip at (margin,margin).
        private static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;

            const int W = 160, H = 200, margin = 22, glowRange = 12;
            int aw = W - 2 * margin, ah = H - 2 * margin;
            var norm = new[]
            {
                new Vector2(0.00f, 0.00f), new Vector2(0.00f, 0.74f), new Vector2(0.20f, 0.57f),
                new Vector2(0.33f, 0.85f), new Vector2(0.46f, 0.79f), new Vector2(0.33f, 0.52f),
                new Vector2(0.56f, 0.52f),
            };
            var p = new Vector2[norm.Length];
            for (var i = 0; i < norm.Length; i++)
                p[i] = new Vector2(margin + norm[i].x * aw, margin + norm[i].y * ah); // y DOWN from tip

            var mask = new bool[W * H];
            for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                mask[y * W + x] = PointInPoly(new Vector2(x, y), p);

            // distance from the arrow (0 = filled) for outline + glow bands
            var dist = new int[W * H];
            for (var i = 0; i < dist.Length; i++) dist[i] = mask[i] ? 0 : int.MaxValue;
            for (var k = 1; k <= glowRange; k++)
            {
                var add = new List<int>();
                for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    var idx = y * W + x;
                    if (dist[idx] != int.MaxValue) continue;
                    if ((x > 0 && dist[idx - 1] == k - 1) || (x < W - 1 && dist[idx + 1] == k - 1) ||
                        (y > 0 && dist[idx - W] == k - 1) || (y < H - 1 && dist[idx + W] == k - 1))
                        add.Add(idx);
                }
                foreach (var idx in add) dist[idx] = k;
            }

            var c0 = new Color(1.00f, 0.20f, 0.62f);   // magenta (tip)
            var c1 = new Color(0.42f, 0.20f, 0.95f);   // violet  (tail)
            var outline = new Color(0.06f, 0.02f, 0.12f, 1f);
            var glow = new Color(1.0f, 0.28f, 0.78f);

            var img = new Color[W * H];
            for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var idx = y * W + x;
                var d = dist[idx];
                if (d == 0)
                {
                    var t = Mathf.Clamp01((y - margin) / (ah * 0.85f));
                    var c = Color.Lerp(c0, c1, t);
                    var edge = (x > 0 && !mask[idx - 1]) || (y > 0 && !mask[idx - W]); // up-left rim
                    if (edge) c = Color.Lerp(c, Color.white, 0.55f);
                    img[idx] = c;
                }
                else if (d <= 2) img[idx] = outline;
                else if (d <= glowRange) img[idx] = new Color(glow.r, glow.g, glow.b, 0.5f * (1f - (d - 2f) / (glowRange - 2f)));
                else img[idx] = new Color(0, 0, 0, 0);
            }

            // flip Y into texture space (tip -> top)
            var texPx = new Color[W * H];
            for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                texPx[(H - 1 - y) * W + x] = img[y * W + x];

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels(texPx);
            tex.Apply();
            _arrowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(margin / (float)W, 1f - margin / (float)H), 100f);
            _arrowSprite.name = "BridgeCursorArrow";
            return _arrowSprite;
        }

        // Soft white ring for the click ripple (tinted at runtime, scaled up + faded out).
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int S = 128;
            var center = new Vector2(S / 2f, S / 2f);
            const float radius = 48f, width = 9f;
            var px = new Color[S * S];
            for (var y = 0; y < S; y++)
            for (var x = 0; x < S; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), center);
                var a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / width);
                px[y * S + x] = new Color(1f, 1f, 1f, a * a);
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels(px);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            _ringSprite.name = "BridgeCursorRing";
            return _ringSprite;
        }

        private static bool PointInPoly(Vector2 pt, Vector2[] poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > pt.y) != (poly[j].y > pt.y)) &&
                    (pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }

        // ---- Animated virtual cursor (browser-like: glide to target, then act) --------------

        private static bool _animActive;
        private static Vector2 _animFrom, _animTo;
        private static double _animStart, _animDuration;
        private static Action _animOnArrive;
        private static Action<Vector2> _animOnStep;   // per-frame callback (drag dispatch)
        private static string _lastClickHit;

        private static Sprite _ringSprite;
        private static RectTransform _cursorArrow, _cursorRipple;
        private static UnityEngine.UI.Image _arrowImg, _rippleImg;
        private static bool _pulseActive;             // click ripple + press dip
        private static double _pulseStart;
        private const double PulseDur = 0.42;
        private static bool _cursorPressed;           // held/dragging tint

        // Action pulse: a color-coded ring at a target's screen position for non-pointer actions.
        private static GameObject _actionCanvas;
        private static RectTransform _actionRing;
        private static UnityEngine.UI.Image _actionRingImg;
        private static bool _actionPulseActive;
        private static double _actionPulseStart;
        private static Color _actionPulseColor = Color.white;
        private const double ActionPulseDur = 0.9;

        [InitializeOnLoadMethod]
        private static void InitCursorAnimator()
        {
            EditorApplication.update -= AnimTick;
            EditorApplication.update += AnimTick;
        }

        private static void StartCursorAnim(Vector2 target, double duration, Action onArrive, Action<Vector2> onStep = null)
        {
            _animFrom = _virtualCursor;
            _animTo = target;
            _animStart = EditorApplication.timeSinceStartup;
            _animDuration = Math.Max(0.0, duration);
            _animOnArrive = onArrive;
            _animOnStep = onStep;
            _animActive = true;
            EnsureCursor(_virtualCursor, true);
        }

        private static void AnimTick()
        {
            if (!_animActive && !_pulseActive && !_actionPulseActive) return;

            if (_animActive || _pulseActive)
            {
                if (_animActive)
                {
                    var t = _animDuration <= 0.0
                        ? 1f
                        : Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - _animStart) / _animDuration));
                    _virtualCursor = Vector2.Lerp(_animFrom, _animTo, Mathf.SmoothStep(0f, 1f, t)); // ease in/out
                    _animOnStep?.Invoke(_virtualCursor); // drag dispatch, if any

                    if (t >= 1f)
                    {
                        _animActive = false;
                        var cb = _animOnArrive;
                        _animOnArrive = null;
                        _animOnStep = null;
                        cb?.Invoke();
                    }
                }

                EnsureCursor(_virtualCursor, true);
                UpdateCursorVisual();
            }

            if (_actionPulseActive)
            {
                var pt = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - _actionPulseStart) / ActionPulseDur));
                var rs = Mathf.Lerp(0.3f, 2.3f, pt);
                _actionRing.localScale = new Vector3(rs, rs, 1f);
                var c = _actionPulseColor; c.a = Mathf.Lerp(0.85f, 0f, pt);
                _actionRingImg.color = c;
                if (pt >= 1f)
                {
                    _actionPulseActive = false;
                    if (_actionCanvas != null) _actionCanvas.SetActive(false);
                }
            }

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); // render even if unfocused
        }

        private static string DispatchClickAt(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return null;
            var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left, position = pos, pressPosition = pos };
            var results = new List<RaycastResult>();
            es.RaycastAll(ped, results);
            if (results.Count == 0) return null;
            var target = results[0].gameObject;
            ped.pointerPressRaycast = results[0];
            ped.pointerCurrentRaycast = results[0];
            ped.pointerPress = target;
            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerClickHandler);
            TriggerPulse(); // click ripple + press dip
            return GetPath(target.transform);
        }

        private static PointerEventData NewPointer(JToken posToken, GameObject go)
        {
            var es = EventSystem.current;
            if (es == null)
                throw new Exception("Semantic input needs Play mode with an active EventSystem.");

            var pos = Vector2.zero;
            if (posToken != null)
                pos = Vec2(posToken);
            else
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) pos = ScreenRect(rt).center;
            }

            return new PointerEventData(es)
            {
                button = PointerEventData.InputButton.Left,
                position = pos,
                pressPosition = pos,
            };
        }

        private static Camera ResolveCamera(JToken token, Context ctx)
        {
            if (token != null)
            {
                var go = ResolveGameObject(token, ctx);
                var c = go != null ? go.GetComponent<Camera>() : null;
                if (c != null) return c;
            }
            if (Camera.main != null) return Camera.main;
            return Camera.allCameras.FirstOrDefault(c => c.isActiveAndEnabled) ?? Camera.allCameras.FirstOrDefault();
        }

        // Synchronous render of a camera into a texture — works in Edit mode, no focus, no Play.
        private static Texture2D RenderCamera(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // ---- Editor focus (Windows) ---------------------------------------------------------

        private static void FocusUnity(int restoreAfterMs)
        {
#if UNITY_EDITOR_WIN
            var prev = GetForegroundWindow();
            var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd == IntPtr.Zero) return;

            ForceForeground(hwnd);

            if (restoreAfterMs > 0 && prev != IntPtr.Zero && prev != hwnd)
            {
                var restoreAt = EditorApplication.timeSinceStartup + restoreAfterMs / 1000.0;
                void Restore()
                {
                    if (EditorApplication.timeSinceStartup < restoreAt) return;
                    EditorApplication.update -= Restore;
                    ForceForeground(prev);
                }
                EditorApplication.update += Restore;
            }
#else
            EditorWindow.focusedWindow?.Focus();
#endif
        }

#if UNITY_EDITOR_WIN
        private const int SW_RESTORE = 9;
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        // Bypass Windows' foreground-lock by briefly attaching to the current foreground thread's input.
        private static void ForceForeground(IntPtr hwnd)
        {
            var fg = GetForegroundWindow();
            var fgThread = GetWindowThreadProcessId(fg, out _);
            var thisThread = GetCurrentThreadId();
            var attached = fgThread != thisThread && AttachThreadInput(fgThread, thisThread, true);
            // Only un-minimize — never SW_RESTORE a maximized window (it would shrink a full-screen editor).
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            if (attached) AttachThreadInput(fgThread, thisThread, false);
        }
#endif

        // ---- Console (internal LogEntries API via reflection) -------------------------------

        private static Type _logEntriesType;
        private static Type LogEntriesType =>
            _logEntriesType ??= Type.GetType("UnityEditor.LogEntries,UnityEditor");

        private static bool TryConsoleCounts(out int errors, out int warnings, out int logs)
        {
            errors = warnings = logs = 0;
            var m = LogEntriesType?.GetMethod("GetCountsByType",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) return false;
            var args = new object[] { 0, 0, 0 };
            m.Invoke(null, args); // ref int error, ref int warning, ref int log
            errors = (int)args[0];
            warnings = (int)args[1];
            logs = (int)args[2];
            return true;
        }

        private static bool TryClearConsole()
        {
            var m = LogEntriesType?.GetMethod("Clear",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) return false;
            m.Invoke(null, null);
            return true;
        }

        // ---- Assertions ---------------------------------------------------------------------

        private static bool CheckProperty(SerializedProperty p, JObject cmd, string actualStr)
        {
            if (p.propertyType == SerializedPropertyType.Boolean)
                return cmd["equals"] == null || p.boolValue == cmd.Value<bool>("equals");

            double? num = p.propertyType == SerializedPropertyType.Integer ? p.longValue
                : p.propertyType == SerializedPropertyType.Float ? (double?)p.floatValue
                : null;
            if (num.HasValue)
                return CompareNumber(num.Value, cmd);

            // string / enum / object / vector / color → compare the described string
            return cmd["equals"] == null
                   || string.Equals(actualStr, cmd.Value<string>("equals"), StringComparison.OrdinalIgnoreCase);
        }

        private static bool CompareNumber(double actual, JObject cmd)
        {
            if (cmd["min"] != null && actual < cmd.Value<double>("min")) return false;
            if (cmd["max"] != null && actual > cmd.Value<double>("max")) return false;
            if (cmd["equals"] != null)
            {
                var tol = cmd.Value<double?>("approx") ?? 0.0001;
                return Math.Abs(actual - cmd.Value<double>("equals")) <= tol;
            }
            return true; // no equals; any min/max already passed
        }

        // ---- Parsing helpers ----------------------------------------------------------------

        private static Vector2 Vec2(JToken t)
        {
            if (t is JArray a) return new Vector2(a[0].Value<float>(), a[1].Value<float>());
            return new Vector2(t.Value<float>("x"), t.Value<float>("y"));
        }

        private static Vector3 Vec3(JToken t)
        {
            if (t is JArray a) return new Vector3(a[0].Value<float>(), a[1].Value<float>(), a.Count > 2 ? a[2].Value<float>() : 0f);
            return new Vector3(t.Value<float>("x"), t.Value<float>("y"), t.Value<float?>("z") ?? 0f);
        }

        private static Vector4 Vec4(JToken t)
        {
            if (t is JArray a) return new Vector4(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>(), a[3].Value<float>());
            return new Vector4(t.Value<float>("x"), t.Value<float>("y"), t.Value<float>("z"), t.Value<float>("w"));
        }

        private static Color ParseColor(JToken t)
        {
            if (t.Type == JTokenType.String)
            {
                if (ColorUtility.TryParseHtmlString(t.Value<string>(), out var c)) return c;
                return Color.white;
            }
            if (t is JArray a)
                return new Color(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>(), a.Count > 3 ? a[3].Value<float>() : 1f);
            return new Color(t.Value<float>("r"), t.Value<float>("g"), t.Value<float>("b"), t.Value<float?>("a") ?? 1f);
        }

        // ---- Misc ---------------------------------------------------------------------------

        private static void Register(JObject cmd, Context ctx, UnityEngine.Object obj)
        {
            var id = cmd.Value<string>("id");
            if (!string.IsNullOrEmpty(id) && obj != null)
                ctx.Registry[id] = obj;
        }

        private static Exception NotFound(JToken token)
        {
            return new Exception($"Target not found: {token}");
        }

        private static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string EnsureBridgeFolder()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var dir = Path.Combine(root, BridgeFolderName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private static void WriteResult(string path, JObject result, Formatting formatting = Formatting.Indented)
        {
            try
            {
                File.WriteAllText(path, result.ToString(formatting));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorBridge] Could not write result: {ex.Message}");
            }
        }

        private sealed class Context
        {
            public string Mode;
            public bool StopOnError;
            public bool Save;
            public string PrefabPath;
            public GameObject PrefabRoot;
            public UnityEngine.SceneManagement.Scene Scene;
            public readonly Dictionary<string, UnityEngine.Object> Registry = new Dictionary<string, UnityEngine.Object>();
        }
    }
}
