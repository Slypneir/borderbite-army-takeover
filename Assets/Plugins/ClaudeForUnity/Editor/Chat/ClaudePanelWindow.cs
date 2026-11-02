using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.ClaudeChat
{
    /// <summary>
    /// In-Editor Claude chat panel. Talks to the local companion over WebSocket and renders the
    /// streaming conversation (assistant text, tool cards, inline screenshots, permission prompts)
    /// in authentic Claude styling. Open via Tools → Claude → Chat Panel.
    /// </summary>
    public sealed class ClaudePanelWindow : EditorWindow
    {
        private const string Url = "ws://127.0.0.1:8787";
        private const string UssPath = "Assets/Plugins/ClaudeForUnity/Editor/Chat/ClaudePanel.uss";
        private const string DarkKey = "ClaudeChat.DarkTheme";

        private static readonly string[] ModelIds = { "claude-opus-4-8", "claude-sonnet-5", "claude-haiku-4-5" };
        private static readonly Dictionary<string, string> ModelNames = new Dictionary<string, string>
        {
            { "claude-opus-4-8", "Opus 4.8" }, { "claude-sonnet-5", "Sonnet 5" }, { "claude-haiku-4-5", "Haiku 4.5" },
        };

        private static readonly string[] Modes = { "default", "acceptEdits", "plan", "bypassPermissions" };
        private static readonly Dictionary<string, string> ModeLabels = new Dictionary<string, string>
        {
            { "default", "Спрашивать" }, { "acceptEdits", "Авто-правки" },
            { "plan", "План" }, { "bypassPermissions", "Без спроса" },
        };

        private readonly ClaudeWsClient _client = new ClaudeWsClient();
        private IVisualElementScheduledItem _pump;

        private VisualElement _root;
        private ScrollView _thread;
        private TextField _input;
        private VisualElement _chips;
        private Button _send;
        private VisualElement _connDot;
        private Label _connLabel;
        private Label _modelLabel;
        private readonly List<Label> _modeLabels = new List<Label>();

        // conversation state
        private VisualElement _activeBody;      // current assistant turn's body container
        private Label _liveLabel;               // streaming text label within the active body
        private VisualElement _working;         // "Claude working…" indicator
        private VisualElement _greeting;        // empty-state block, cleared on first turn
        private readonly Dictionary<string, VisualElement> _toolCards = new Dictionary<string, VisualElement>();
        private readonly List<JObject> _attachments = new List<JObject>();
        private string _mode = "default";
        private string _model = "claude-opus-4-8";
        private bool _dark;

        // Persisted across domain reload (recompile) so the chat survives it.
        [SerializeField] private List<LogItem> _log = new List<LogItem>();
        [SerializeField] private string _companionKey = "";
        [SerializeField] private string _draft = "";              // live composer text, survives domain reload
        [SerializeField] private List<string> _undo = new List<string>(); // composer undo history
        [SerializeField] private List<string> _redo = new List<string>();
        private double _lastEditTime;
        private Label _placeholder;
        private bool _reconnectScheduled;
        private bool _busy; // Claude is mid-turn → send button becomes a stop button

        private const double UndoCoalesceSec = 0.7; // keystrokes within this window fold into one undo step
        private const int UndoCap = 200;

        [MenuItem("Tools/Claude for Unity/Chat Panel", false, 0)]
        public static void Open()
        {
            var w = GetWindow<ClaudePanelWindow>();
            w.titleContent = new GUIContent("Claude");
            w.minSize = new Vector2(360, 480);
            w.Show();
        }

        private void OnEnable() => _dark = EditorPrefs.GetBool(DarkKey, false);

        public void CreateGUI()
        {
            _root = new VisualElement();
            _root.AddToClassList("claude-root");
            if (_dark) _root.AddToClassList("dark");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) rootVisualElement.styleSheets.Add(uss);
            rootVisualElement.Add(_root);

            BuildTitlebar();
            BuildToolbar();
            BuildThread();
            BuildComposer();

            RegisterDragAndDrop();
            if (_log.Count > 0) ReplayLog();
            else ShowGreeting();

            _client.Connect(Url);
            _pump = _root.schedule.Execute(Drain).Every(33);
            SetConnected(false);
            AddSysLine("Подключение к companion…");
        }

        private void OnDisable()
        {
            _pump?.Pause();
            _client.Close("window closed");
        }

        // ------------------------------------------------------------------ UI construction

        private void BuildTitlebar()
        {
            var bar = Row("titlebar");

            var spark = Glyph("✳", 18);
            spark.AddToClassList("spark");
            spark.style.color = Clay();
            spark.style.unityTextAlign = TextAnchor.MiddleCenter;
            bar.Add(spark);

            bar.Add(Text("Claude", "wordmark"));
            bar.Add(Text("Unity", "tag"));
            bar.Add(Spacer());

            var conn = Row("conn");
            _connDot = new VisualElement();
            _connDot.AddToClassList("dot");
            conn.Add(_connDot);
            _connLabel = Text("…", "conn-label");
            conn.Add(_connLabel);
            bar.Add(conn);

            var theme = IconButton(_dark ? "◑" : "◐", ToggleTheme);
            bar.Add(theme);

            _root.Add(bar);
        }

        private void BuildToolbar()
        {
            var bar = Row("toolbar");

            _modelLabel = Text(ModelNames[_model], null);
            var model = new VisualElement();
            model.AddToClassList("pill");
            model.Add(Text("Модель", "k"));
            model.Add(_modelLabel);
            model.Add(Text("▾", "caret"));
            model.RegisterCallback<PointerDownEvent>(_ => ShowModelMenu());
            bar.Add(model);

            var modePill = new VisualElement();
            modePill.AddToClassList("pill");
            var ml = Text(ModeLabels[_mode], null);
            _modeLabels.Add(ml);
            modePill.Add(ml);
            modePill.Add(Text("▾", "caret"));
            modePill.RegisterCallback<PointerDownEvent>(_ => ShowModeMenu());
            bar.Add(modePill);

            bar.Add(Spacer());

            var newBtn = new Button(NewChat) { text = "＋ Новый" };
            newBtn.AddToClassList("newbtn");
            bar.Add(newBtn);

            _root.Add(bar);
        }

        private void BuildThread()
        {
            _thread = new ScrollView(ScrollViewMode.Vertical);
            _thread.AddToClassList("thread");
            _thread.contentContainer.AddToClassList("thread-content");
            _root.Add(_thread);
        }

        private void BuildComposer()
        {
            var composer = new VisualElement();
            composer.AddToClassList("composer");

            _chips = Row("chips");
            _chips.style.display = DisplayStyle.None;
            composer.Add(_chips);

            var box = new VisualElement();
            box.AddToClassList("box");

            var fieldWrap = new VisualElement();
            fieldWrap.AddToClassList("field-wrap");
            _input = new TextField { multiline = true };
            _input.name = "input-field";
            _input.AddToClassList("input");
            _input.SetValueWithoutNotify(_draft ?? "");
            _input.RegisterCallback<KeyDownEvent>(OnInputKey, TrickleDown.TrickleDown);
            fieldWrap.Add(_input);
            _placeholder = Text("Спроси Claude или перетащи объект из сцены…", "ph-over");
            _placeholder.pickingMode = PickingMode.Ignore;
            fieldWrap.Add(_placeholder);
            UpdatePlaceholder();
            _input.RegisterValueChangedCallback(OnInputChanged);
            box.Add(fieldWrap);

            var tools = Row("box-tools");
            tools.Add(IconRound("＋", PickAttachment));

            tools.Add(Spacer());

            _send = new Button(OnSendOrStop) { text = "↑" };
            _send.AddToClassList("send");
            _send.tooltip = "Отправить";
            tools.Add(_send);

            box.Add(tools);
            composer.Add(box);

            composer.Add(Text("Enter — отправить · Shift+Enter — перенос · Ctrl+Z — вернуть текст", "hint"));
            _root.Add(composer);
        }

        // ------------------------------------------------------------------ queue drain / protocol

        private void Drain()
        {
            while (_client.TryDequeue(out var raw))
            {
                JObject msg;
                try { msg = JObject.Parse(raw); }
                catch { continue; }
                HandleMessage(msg);
            }
        }

        private void HandleMessage(JObject m)
        {
            switch ((string)m["t"])
            {
                case "_open":
                    SetConnected(true);
                    _reconnectScheduled = false;
                    // Re-bind to our live session if we have a key (survives recompiles); else new session.
                    _client.Send(new JObject { ["t"] = "hello", ["key"] = _companionKey ?? "" }.ToString(Newtonsoft.Json.Formatting.None));
                    break;
                case "session_key":
                    _companionKey = (string)m["key"];
                    if (m["resumed"]?.Value<bool>() == true) HideWorking();
                    break;
                case "_close":
                    SetConnected(false);
                    ScheduleReconnect();
                    break;
                case "ready":
                    if (m["model"] != null) SetModel((string)m["model"], false);
                    if (m["permissionMode"] != null) SetMode((string)m["permissionMode"], false);
                    AddSysLine("Сессия готова" + (m["sessionId"] != null ? " · " + ((string)m["sessionId"]).Substring(0, 8) : ""));
                    break;
                case "status":
                    AddSysLine((string)m["message"]);
                    break;
                case "error":
                    AddErrLine((string)m["message"]);
                    HideWorking();
                    SetBusy(false);
                    break;
                case "assistant_delta":
                    SetBusy(true);
                    AppendDelta((string)m["text"]);
                    break;
                case "assistant_message":
                    SetAssistantText((string)m["text"]);
                    break;
                case "tool_use":
                    SetBusy(true);
                    AddToolCard((string)m["id"], (string)m["name"], m["input"]);
                    break;
                case "tool_result":
                    UpdateToolResult((string)m["id"], m["isError"]?.Value<bool>() ?? false, (string)m["text"]);
                    break;
                case "image":
                    AddImage((string)m["toolUseId"], (string)m["dataBase64"], (string)m["caption"], (string)m["filePath"]);
                    break;
                case "permission_request":
                    SetBusy(true);
                    AddPermission(m);
                    break;
                case "result":
                    HideWorking();
                    SealTurn();
                    SetBusy(false);
                    break;
            }
            ScrollToBottom();
        }

        // ------------------------------------------------------------------ rendering

        private VisualElement EnsureAssistant()
        {
            if (_activeBody != null) return _activeBody;
            var turn = new VisualElement();
            turn.AddToClassList("turn");
            var row = Row("assistant");
            var mark = new VisualElement();
            mark.AddToClassList("mark");
            var g = Glyph("✳", 14);
            g.style.color = Clay();
            g.style.unityTextAlign = TextAnchor.MiddleCenter;
            mark.Add(g);
            row.Add(mark);
            var body = new VisualElement();
            body.AddToClassList("body");
            row.Add(body);
            turn.Add(row);
            _thread.Add(turn);
            _activeBody = body;
            _liveLabel = null;
            return body;
        }

        private void AppendDelta(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var body = EnsureAssistant();
            if (_liveLabel == null)
            {
                _liveLabel = Msg("");
                body.Add(_liveLabel);
            }
            _liveLabel.text += text;
        }

        private void SetAssistantText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var body = EnsureAssistant();
            if (_liveLabel != null) { _liveLabel.text = text; _liveLabel = null; }
            else body.Add(Msg(text));
            LogAdd("assistant", text);
        }

        private void AddToolCard(string id, string name, JToken input)
        {
            var body = EnsureAssistant();
            _liveLabel = null;

            var display = ShortName(name);
            var arg = ToolArg(name, input);
            var pretty = PrettyInput(input);
            var card = BuildToolCard(display, arg, "run", pretty, null, out var refs);
            refs.logIndex = LogAdd("tool", display, arg, "run", pretty, null);
            body.Add(card);
            if (!string.IsNullOrEmpty(id)) _toolCards[id] = card;
        }

        private void UpdateToolResult(string id, bool isError, string text)
        {
            if (id == null || !_toolCards.TryGetValue(id, out var card)) return;
            if (!(card.userData is ToolCardRefs refs)) return;

            var status = isError ? "err" : "done";
            refs.status.RemoveFromClassList("status-run");
            refs.status.RemoveFromClassList("status-done");
            refs.status.RemoveFromClassList("status-err");
            refs.status.AddToClassList(StatusClass(status));
            refs.statusLabel.text = StatusText(status);

            var preview = ResultPreview(text);
            if (!string.IsNullOrEmpty(preview) && refs.resultLabel != null)
            {
                refs.resultLabel.text = preview;
                refs.resultLabel.style.display = DisplayStyle.Flex;
                if (refs.resultCap != null) refs.resultCap.style.display = DisplayStyle.Flex;
            }
            LogSetResult(refs.logIndex, status, preview);
        }

        private void AddImage(string toolUseId, string base64, string caption = null, string filePath = null)
        {
            if (string.IsNullOrEmpty(base64)) return;
            VisualElement parent = (toolUseId != null && _toolCards.TryGetValue(toolUseId, out var card)) ? card : (_activeBody ?? EnsureAssistant());
            if (parent == null) return;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch { return; }

            // Persist to disk so the image survives a domain reload (replayed from _log by path).
            // Blueprints already arrive with a filePath (saved by the companion); screenshots we cache here.
            var path = filePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                try
                {
                    var dir = Path.Combine(ProjectRoot(), "ClaudePlans", "_cache");
                    Directory.CreateDirectory(dir);
                    path = Path.Combine(dir, $"img-{DateTime.UtcNow.Ticks}.png");
                    File.WriteAllBytes(path, bytes);
                }
                catch { path = null; }
            }

            var tex = LoadTexture(bytes);
            if (tex == null) return;
            RenderImage(parent, tex, caption, path);
            LogAdd("image", path, caption);
        }

        private static Texture2D LoadTexture(byte[] bytes)
        {
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(bytes);
                t.Apply();
                return t;
            }
            catch { return null; }
        }

        /// <summary>Render one image bubble (inline preview + caption + "open full-size" actions when a
        /// saved file path is known). Shared by live <see cref="AddImage"/> and transcript replay.</summary>
        private void RenderImage(VisualElement parent, Texture2D tex, string caption, string path)
        {
            var shot = new VisualElement();
            shot.AddToClassList("shot");
            var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
            img.AddToClassList("img");
            shot.Add(img);
            var cap = Row("cap");
            cap.Add(Text(string.IsNullOrEmpty(caption) ? $"Game view · {tex.width}×{tex.height}" : caption, null));
            shot.Add(cap);

            // A detailed blueprint/screenshot is unreadable when scaled into the panel — offer to open the
            // saved full-res PNG in the OS default viewer (and reveal it in Explorer).
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var p = path;
                var actions = Row("cap");
                actions.Add(new Button(() => { try { EditorUtility.OpenWithDefaultApp(p); } catch { } }) { text = "⤢ Открыть в полном размере" });
                actions.Add(new Button(() => { try { EditorUtility.RevealInFinder(p); } catch { } }) { text = "Показать файл" });
                shot.Add(actions);
            }
            parent.Add(shot);
        }

        private static string ProjectRoot() => Path.GetDirectoryName(Application.dataPath);

        private void AddPermission(JObject m)
        {
            HideWorking();
            var body = EnsureAssistant();
            _liveLabel = null;

            var requestId = (string)m["requestId"];
            var toolName = (string)m["toolName"];
            var input = m["input"] as JObject;

            var perm = new VisualElement();
            perm.AddToClassList("perm");
            var head = Row("perm-head");
            head.Add(Text("Claude хочет: " + ShortName(toolName), null));
            perm.Add(head);

            var pbody = new VisualElement();
            pbody.AddToClassList("perm-body");

            var kv = new VisualElement();
            kv.AddToClassList("perm-kv");
            kv.Add(KvRow("tool", toolName));
            if (input != null)
                foreach (var p in input.Properties())
                    kv.Add(KvRow(p.Name, Compact(p.Value)));
            pbody.Add(kv);

            var actions = Row("perm-actions");
            var allow = new Button(() => Respond(perm, requestId, true)) { text = "Разрешить" };
            allow.AddToClassList("btn"); allow.AddToClassList("btn-primary");
            var deny = new Button(() => Respond(perm, requestId, false)) { text = "Отклонить" };
            deny.AddToClassList("btn"); deny.AddToClassList("btn-ghost");
            actions.Add(allow);
            actions.Add(deny);
            pbody.Add(actions);

            perm.Add(pbody);
            body.Add(perm);
        }

        private void Respond(VisualElement perm, string requestId, bool allow)
        {
            var msg = new JObject
            {
                ["t"] = "permission_response",
                ["requestId"] = requestId,
                ["decision"] = allow ? "allow" : "deny",
            };
            if (!allow) msg["message"] = "Отклонено пользователем";
            _client.Send(msg.ToString(Newtonsoft.Json.Formatting.None));

            perm.Clear();
            perm.RemoveFromClassList("perm");
            perm.AddToClassList("perm-resolved");
            perm.Add(Text(allow ? "✓ Разрешено" : "✕ Отклонено", allow ? "resolved-ok" : "resolved-deny"));
            LogAdd("perm", allow ? "✓ Разрешено" : "✕ Отклонено");
            if (allow) ShowWorking();
        }

        // ------------------------------------------------------------------ send

        private void OnInputKey(KeyDownEvent evt)
        {
            var ctrl = evt.ctrlKey || evt.commandKey;

            // Own the undo/redo shortcuts — UITK's native TextField undo doesn't work in an EditorWindow.
            if (ctrl && evt.keyCode == KeyCode.Z)
            {
                if (evt.shiftKey) Redo(); else Undo();
                evt.StopImmediatePropagation();
                return;
            }
            if (ctrl && evt.keyCode == KeyCode.Y)
            {
                Redo();
                evt.StopImmediatePropagation();
                return;
            }

            var isEnter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter
                          || evt.character == '\n' || evt.character == '\r';
            if (isEnter && !evt.shiftKey)
            {
                evt.StopImmediatePropagation();
                SendCurrent();
            }
        }

        // ---- composer undo/redo (a proper multi-step history the TextField itself lacks) ----

        private void OnInputChanged(ChangeEvent<string> e)
        {
            var now = EditorApplication.timeSinceStartup;
            var added = e.newValue.Length > e.previousValue.Length;
            var last = added && e.newValue.Length > 0 ? e.newValue[e.newValue.Length - 1] : '\0';
            var wordBoundary = added && (last == ' ' || last == '\n' || last == '\t');
            var bigChange = Mathf.Abs(e.newValue.Length - e.previousValue.Length) > 1; // paste / multi-char delete
            var paused = now - _lastEditTime > UndoCoalesceSec;

            if (_undo.Count == 0 || paused || bigChange || wordBoundary)
            {
                _undo.Add(e.previousValue);
                if (_undo.Count > UndoCap) _undo.RemoveAt(0);
            }
            _lastEditTime = now;
            _redo.Clear();       // a fresh edit invalidates the redo branch
            _draft = e.newValue;
            UpdatePlaceholder();
        }

        private void Undo()
        {
            if (_undo.Count == 0) return;
            _redo.Add(_input.value);
            var v = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            ApplyText(v);
        }

        private void Redo()
        {
            if (_redo.Count == 0) return;
            _undo.Add(_input.value);
            var v = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            ApplyText(v);
        }

        private void ApplyText(string v)
        {
            _input.SetValueWithoutNotify(v ?? "");
            _draft = v ?? "";
            UpdatePlaceholder();
        }

        private void UpdatePlaceholder()
        {
            if (_placeholder != null)
                _placeholder.style.display = string.IsNullOrEmpty(_input.value) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSendOrStop()
        {
            if (_busy) Interrupt();
            else SendCurrent();
        }

        /// <summary>Stop Claude mid-turn (interrupt the running SDK query).</summary>
        private void Interrupt()
        {
            if (_client.IsOpen)
                _client.Send(new JObject { ["t"] = "interrupt" }.ToString(Newtonsoft.Json.Formatting.None));
            HideWorking();
            SetBusy(false);
            AddSysLine("Остановлено.");
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_send == null) return;
            _send.text = busy ? "■" : "↑";
            _send.tooltip = busy ? "Остановить" : "Отправить";
            _send.EnableInClassList("send-stop", busy);
        }

        private void SendCurrent()
        {
            var text = (_input.value ?? "").Trim();
            if (string.IsNullOrEmpty(text) && _attachments.Count == 0) return;
            if (!_client.IsOpen) { AddErrLine("Нет соединения с companion."); return; }

            var wasBusy = _busy; // sending mid-turn queues the message; tell the user so it doesn't feel lost
            HideGreeting();

            // echo the user's turn
            var turn = new VisualElement();
            turn.AddToClassList("turn");
            var card = new VisualElement();
            card.AddToClassList("user");
            if (!string.IsNullOrEmpty(text)) card.Add(Msg(text));
            if (_attachments.Count > 0)
            {
                var ar = Row("chips");
                ar.style.marginTop = 8;
                foreach (var a in _attachments) ar.Add(Chip((string)a["label"], null));
                card.Add(ar);
            }
            turn.Add(card);
            _thread.Add(turn);
            LogAdd("user", text);

            var msg = new JObject { ["t"] = "user_message", ["text"] = text };
            if (_attachments.Count > 0)
            {
                var arr = new JArray();
                foreach (var a in _attachments) arr.Add(a.DeepClone());
                msg["attachments"] = arr;
            }
            _client.Send(msg.ToString(Newtonsoft.Json.Formatting.None));

            if (!string.IsNullOrEmpty(text))
            {
                _undo.Add(text); // so Ctrl+Z can bring a just-sent message back to re-edit
                if (_undo.Count > UndoCap) _undo.RemoveAt(0);
                _redo.Clear();
            }
            ApplyText("");
            _attachments.Clear();
            RefreshChips();
            SealTurn();
            if (wasBusy) AddSysLine("Сообщение отправлено — Клод учтёт его, как закончит текущий шаг.");
            ShowWorking();
            SetBusy(true);
            ScrollToBottom();
        }

        // ------------------------------------------------------------------ attachments

        private void RegisterDragAndDrop()
        {
            _root.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            _root.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                foreach (var o in DragAndDrop.objectReferences) AddAttachment(o);
            });
        }

        private void PickAttachment()
        {
            var o = Selection.activeObject;
            if (o != null) AddAttachment(o);
            else AddSysLine("Выдели объект/скрипт в проекте или сцене, затем нажми ＋.");
        }

        private void AddAttachment(UnityEngine.Object o)
        {
            if (o == null) return;
            string label, kind, value;
            var path = AssetDatabase.GetAssetPath(o);

            if (o is MonoScript ms)
            {
                label = ms.name + ".cs"; kind = "text";
                value = $"// {path}\n{ms.text}";
            }
            else if (o is TextAsset ta)
            {
                label = ta.name; kind = "text"; value = $"// {path}\n{ta.text}";
            }
            else if (o is GameObject go)
            {
                label = go.name; kind = "note";
                value = string.IsNullOrEmpty(path)
                    ? $"Scene GameObject: {GetPath(go.transform)}"
                    : $"Prefab asset: {path}";
            }
            else
            {
                label = o.name; kind = "note";
                value = $"{o.GetType().Name}: {(string.IsNullOrEmpty(path) ? o.name : path)}";
            }

            _attachments.Add(new JObject { ["kind"] = kind, ["value"] = value, ["label"] = label });
            RefreshChips();
        }

        private void RefreshChips()
        {
            _chips.Clear();
            if (_attachments.Count == 0) { _chips.style.display = DisplayStyle.None; return; }
            _chips.style.display = DisplayStyle.Flex;
            for (var i = 0; i < _attachments.Count; i++)
            {
                var idx = i;
                _chips.Add(Chip((string)_attachments[i]["label"], () => { _attachments.RemoveAt(idx); RefreshChips(); }));
            }
        }

        // ------------------------------------------------------------------ helpers: state

        private void SetConnected(bool on)
        {
            if (_connDot != null)
            {
                _connDot.EnableInClassList("dot-off", !on);
            }
            if (_connLabel != null) _connLabel.text = on ? "Max" : "оффлайн";
            if (_send != null) _send.SetEnabled(on);
        }

        private void SealTurn()
        {
            _activeBody = null;
            _liveLabel = null;
        }

        private void ShowWorking()
        {
            HideWorking();
            _working = Row("working");
            _working.Add(Glyph("✳", 12));
            _working.Add(Text("Claude работает…", "msgtext"));
            _thread.Add(_working);
            ScrollToBottom();
        }

        private void HideWorking()
        {
            if (_working != null && _working.parent != null) _working.parent.Remove(_working);
            _working = null;
        }

        private void NewChat()
        {
            _thread.contentContainer.Clear();
            _toolCards.Clear();
            _log.Clear();
            _companionKey = "";
            SealTurn();
            HideWorking();
            _greeting = null;
            ShowGreeting();
            // A fresh session: reconnect (empty key → companion starts a new streaming query).
            _client.Connect(Url);
        }

        private void ToggleTheme()
        {
            _dark = !_dark;
            _root.EnableInClassList("dark", _dark);
            EditorPrefs.SetBool(DarkKey, _dark);
        }

        private void ShowModelMenu()
        {
            var menu = new GenericMenu();
            foreach (var id in ModelIds)
                menu.AddItem(new GUIContent(ModelNames[id]), _model == id, () => SetModel(id, true));
            menu.ShowAsContext();
        }

        private void SetModel(string id, bool send)
        {
            if (string.IsNullOrEmpty(id)) return;
            _model = id;
            if (_modelLabel != null)
                _modelLabel.text = ModelNames.TryGetValue(id, out var n) ? n : PrettyModel(id);
            if (send && _client.IsOpen)
                _client.Send(new JObject { ["t"] = "set_model", ["model"] = id }.ToString(Newtonsoft.Json.Formatting.None));
        }

        private void ShowModeMenu()
        {
            var menu = new GenericMenu();
            foreach (var mode in Modes)
                menu.AddItem(new GUIContent(ModeLabels[mode]), _mode == mode, () => SetMode(mode, true));
            menu.ShowAsContext();
        }

        private void SetMode(string mode, bool send)
        {
            _mode = mode;
            foreach (var l in _modeLabels) l.text = ModeLabels[mode];
            if (send && _client.IsOpen)
                _client.Send(new JObject { ["t"] = "set_permission_mode", ["mode"] = mode }.ToString(Newtonsoft.Json.Formatting.None));
        }

        private void ShowGreeting()
        {
            HideGreeting();
            _greeting = new VisualElement();
            _greeting.AddToClassList("greeting");
            var g = Glyph("✳", 30);
            g.style.color = Clay();
            g.style.unityTextAlign = TextAnchor.MiddleCenter;
            _greeting.Add(g);
            _greeting.Add(Text("Чем помочь в Unity?", "greeting-title"));
            _greeting.Add(Text("Опиши задачу или перетащи объект из сцены. Claude видит сцену, делает изменения и показывает результат.", "greeting-sub"));
            _thread.Add(_greeting);
        }

        private void HideGreeting()
        {
            if (_greeting != null && _greeting.parent != null) _greeting.parent.Remove(_greeting);
            _greeting = null;
        }

        private void ScheduleReconnect()
        {
            if (_reconnectScheduled) return;
            _reconnectScheduled = true;
            _root.schedule.Execute(() =>
            {
                _reconnectScheduled = false;
                if (!_client.IsOpen) _client.Connect(Url);
            }).StartingIn(2500);
        }

        /// <summary>Rebuild the transcript UI from the persisted log after a domain reload.</summary>
        private void ReplayLog()
        {
            VisualElement body = null;
            foreach (var it in _log)
            {
                if (it.kind == "user")
                {
                    body = null;
                    var turn = new VisualElement();
                    turn.AddToClassList("turn");
                    var card = new VisualElement();
                    card.AddToClassList("user");
                    if (!string.IsNullOrEmpty(it.a)) card.Add(Msg(it.a));
                    turn.Add(card);
                    _thread.Add(turn);
                }
                else
                {
                    if (body == null) body = NewAssistantBody();
                    if (it.kind == "assistant") { if (!string.IsNullOrEmpty(it.a)) body.Add(Msg(it.a)); }
                    else if (it.kind == "tool") body.Add(BuildToolCard(it.a, it.b, it.c, it.d, it.e, out _));
                    else if (it.kind == "perm") body.Add(Text(it.a, it.a != null && it.a.StartsWith("✓") ? "resolved-ok" : "resolved-deny"));
                    else if (it.kind == "image")
                    {
                        if (!string.IsNullOrEmpty(it.a) && File.Exists(it.a))
                        {
                            try { var tex = LoadTexture(File.ReadAllBytes(it.a)); if (tex != null) RenderImage(body, tex, it.b, it.a); }
                            catch { }
                        }
                    }
                }
            }
            ScrollToBottom();
        }

        private VisualElement NewAssistantBody()
        {
            var turn = new VisualElement();
            turn.AddToClassList("turn");
            var row = Row("assistant");
            var mark = new VisualElement();
            mark.AddToClassList("mark");
            var g = Glyph("✳", 14);
            g.style.color = Clay();
            g.style.unityTextAlign = TextAnchor.MiddleCenter;
            mark.Add(g);
            row.Add(mark);
            var b = new VisualElement();
            b.AddToClassList("body");
            row.Add(b);
            turn.Add(row);
            _thread.Add(turn);
            return b;
        }

        /// <summary>Build an expandable tool card: header (name · arg · status) that toggles a details
        /// panel with the full call params and a result preview. Used for both live and replayed cards.</summary>
        private VisualElement BuildToolCard(string displayName, string arg, string status, string pretty, string resultPreview, out ToolCardRefs refs)
        {
            var card = new VisualElement();
            card.AddToClassList("tool");

            var head = Row("tool-head");
            head.AddToClassList("tool-head-btn");
            var chevron = Glyph("▸", 11);
            chevron.AddToClassList("tool-chevron");
            head.Add(chevron);
            head.Add(Text(displayName, "tool-name"));
            if (!string.IsNullOrEmpty(arg)) head.Add(Text(arg, "tool-arg"));
            head.Add(Spacer());
            var st = Row("status");
            st.AddToClassList(StatusClass(status));
            var stLabel = Text(StatusText(status), null);
            st.Add(stLabel);
            head.Add(st);
            card.Add(head);

            var details = new VisualElement();
            details.AddToClassList("tool-details");
            details.style.display = DisplayStyle.None;

            var inCap = Text("Параметры", "tool-detail-cap");
            var inText = Selectable(Text(pretty ?? "", "tool-detail-text"));
            var showIn = !string.IsNullOrEmpty(pretty);
            inCap.style.display = showIn ? DisplayStyle.Flex : DisplayStyle.None;
            inText.style.display = showIn ? DisplayStyle.Flex : DisplayStyle.None;
            details.Add(inCap);
            details.Add(inText);

            var resCap = Text("Результат", "tool-detail-cap");
            var resText = Selectable(Text(resultPreview ?? "", "tool-detail-text"));
            var showRes = !string.IsNullOrEmpty(resultPreview);
            resCap.style.display = showRes ? DisplayStyle.Flex : DisplayStyle.None;
            resText.style.display = showRes ? DisplayStyle.Flex : DisplayStyle.None;
            details.Add(resCap);
            details.Add(resText);

            card.Add(details);

            head.RegisterCallback<PointerDownEvent>(_ =>
            {
                var open = details.style.display == DisplayStyle.None;
                details.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = open ? "▾" : "▸";
            });

            refs = new ToolCardRefs { status = st, statusLabel = stLabel, resultCap = resCap, resultLabel = resText };
            card.userData = refs;
            return card;
        }

        private static string StatusClass(string s) => s == "err" ? "status-err" : s == "run" ? "status-run" : "status-done";
        private static string StatusText(string s) => s == "err" ? "ошибка" : s == "run" ? "работает…" : "готово";

        private void AddSysLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _thread.Add(Text(text, "sysline"));
            ScrollToBottom();
        }

        private void AddErrLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _thread.Add(Text(text, "errline"));
            ScrollToBottom();
        }

        private void ScrollToBottom() => _root.schedule.Execute(() => _thread.scrollOffset = new Vector2(0, float.MaxValue));

        // ------------------------------------------------------------------ helpers: build

        private static VisualElement Row(string cls) { var v = new VisualElement(); v.AddToClassList(cls); v.style.flexDirection = FlexDirection.Row; return v; }
        private static VisualElement Spacer() { var v = new VisualElement(); v.AddToClassList("grow"); return v; }
        private static Label Text(string t, string cls) { var l = new Label(t); if (cls != null) l.AddToClassList(cls); return l; }
        private static Label Msg(string t) { var l = new Label(t); l.AddToClassList("msgtext"); l.style.whiteSpace = WhiteSpace.Normal; return Selectable(l); }

        /// <summary>Make a Label's text selectable + copyable (UITK Labels are not by default).</summary>
        private static Label Selectable(Label l)
        {
            l.selection.isSelectable = true;
            l.focusable = true;
            return l;
        }
        private static Label Glyph(string ch, float size) { var l = new Label(ch); l.style.fontSize = size; return l; }

        private Button IconButton(string glyph, Action onClick)
        {
            var b = new Button(onClick) { text = glyph };
            b.AddToClassList("iconbtn");
            return b;
        }

        private Button IconRound(string glyph, Action onClick)
        {
            var b = new Button(onClick) { text = glyph };
            b.AddToClassList("round");
            return b;
        }

        private VisualElement Chip(string label, Action onRemove)
        {
            var c = Row("chip");
            c.Add(Text(label, null));
            if (onRemove != null)
            {
                var x = new Label("✕");
                x.AddToClassList("cx");
                x.RegisterCallback<PointerDownEvent>(_ => onRemove());
                c.Add(x);
            }
            return c;
        }

        private VisualElement KvRow(string k, string v)
        {
            var row = Row("kv");
            row.Add(Text(k, "kk"));
            var vv = Text(v, "vv");
            vv.style.whiteSpace = WhiteSpace.Normal;
            row.Add(vv);
            return row;
        }

        private Color Clay() => _dark ? new Color(0.878f, 0.518f, 0.388f) : new Color(0.851f, 0.467f, 0.341f);

        private static string PrettyModel(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (id.Contains("opus-4-8")) return "Opus 4.8";
            if (id.Contains("sonnet")) return "Sonnet 5";
            if (id.Contains("haiku")) return "Haiku 4.5";
            return id;
        }

        private static string ShortName(string tool)
        {
            if (string.IsNullOrEmpty(tool)) return "";
            var i = tool.LastIndexOf("__", StringComparison.Ordinal);
            var name = i >= 0 ? tool.Substring(i + 2) : tool;
            return name.StartsWith("unity_") ? name.Substring(6) : name;
        }

        /// <summary>Concise, human-readable summary of a tool call's key argument — aware of the
        /// common Claude Code tools (Read/Edit/Grep/Bash/…) as well as the unity_* selectors.</summary>
        private static string ToolArg(string tool, JToken input)
        {
            if (!(input is JObject o)) return "";

            // File-oriented tools: Read, Edit, Write, NotebookEdit
            if (o["file_path"] != null) return Basename((string)o["file_path"]);

            // Search: Grep, Glob (show the pattern, scoped to a path/glob if present)
            if (o["pattern"] != null)
            {
                var pat = Compact(o["pattern"]);
                var scope = (string)(o["path"] ?? o["glob"] ?? o["type"]);
                return string.IsNullOrEmpty(scope) ? pat : $"{pat} · {Basename(scope)}";
            }

            // Shell / web / search / tasks
            if (o["command"] != null) return Compact(o["command"]);
            if (o["url"] != null) return Compact(o["url"]);
            if (o["query"] != null) return Compact(o["query"]);
            if (o["prompt"] != null) return Compact(o["prompt"]);
            if (o["description"] != null) return Compact(o["description"]);

            // unity_* selectors
            foreach (var key in new[] { "target", "source", "name", "field" })
                if (o[key] != null) return $"{key}: {Compact(o[key])}";
            if (o["value"] != null) return Compact(o["value"]);
            if (o["path"] != null) return Basename((string)o["path"]);

            return "";
        }

        /// <summary>Last path segment (file/dir name), truncated — keeps cards readable.</summary>
        private static string Basename(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            var s = p.Replace('\\', '/').TrimEnd('/');
            var i = s.LastIndexOf('/');
            var name = i >= 0 ? s.Substring(i + 1) : s;
            return name.Length > 48 ? name.Substring(0, 45) + "…" : name;
        }

        /// <summary>Full call params as "key: value" lines for the expandable card body.</summary>
        private static string PrettyInput(JToken input)
        {
            if (!(input is JObject o)) return input?.ToString() ?? "";
            var sb = new System.Text.StringBuilder();
            foreach (var p in o.Properties())
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(p.Name).Append(": ").Append(CompactLen(p.Value, 240));
            }
            return sb.ToString();
        }

        private static string CompactLen(JToken t, int max)
        {
            if (t == null) return "";
            var s = (t.Type == JTokenType.String || t.Type == JTokenType.Integer || t.Type == JTokenType.Float || t.Type == JTokenType.Boolean)
                ? t.ToString()
                : t.ToString(Newtonsoft.Json.Formatting.None);
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > max ? s.Substring(0, max - 1) + "…" : s;
        }

        private static string ResultPreview(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var s = text.Trim();
            return s.Length > 900 ? s.Substring(0, 900) + "\n… (обрезано)" : s;
        }

        private static string Compact(JToken t)
        {
            if (t == null) return "";
            if (t.Type == JTokenType.String || t.Type == JTokenType.Integer || t.Type == JTokenType.Float || t.Type == JTokenType.Boolean)
                return t.ToString();
            var s = t.ToString(Newtonsoft.Json.Formatting.None);
            return s.Length > 60 ? s.Substring(0, 57) + "…" : s;
        }

        private static string GetPath(Transform t)
        {
            var p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private sealed class ToolCardRefs
        {
            public VisualElement status;
            public Label statusLabel;
            public Label resultLabel;
            public VisualElement resultCap;
            public int logIndex = -1;
        }

        [Serializable]
        private struct LogItem
        {
            public string kind; // user, assistant, tool, perm
            public string a;    // text / tool name / perm result
            public string b;    // tool arg summary
            public string c;    // tool status: run/done/err
            public string d;    // tool: full params (for expand)
            public string e;    // tool: result preview (for expand)
        }

        private int LogAdd(string kind, string a = null, string b = null, string c = null, string d = null, string e = null)
        {
            _log.Add(new LogItem { kind = kind, a = a, b = b, c = c, d = d, e = e });
            return _log.Count - 1;
        }

        private void LogSetResult(int idx, string status, string resultPreview)
        {
            if (idx < 0 || idx >= _log.Count) return;
            var it = _log[idx];
            it.c = status;
            if (!string.IsNullOrEmpty(resultPreview)) it.e = resultPreview;
            _log[idx] = it;
        }
    }
}
