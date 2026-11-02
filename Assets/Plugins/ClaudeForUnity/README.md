# Claude for Unity

Claude-driven Unity Editor automation: a file-based EditorBridge, an in-Editor chat panel that talks to
Claude, real OS mouse/keyboard, global screenshots, direct reflection calls, and a JavaScript (Jint) eval
layer. Everything the Unity side needs lives under this one folder: `Assets/Plugins/ClaudeForUnity/`.

The **Node side** (companion + MCP server) lives outside `Assets/` and is copied separately — see below.

## Folder layout
```
Assets/Plugins/ClaudeForUnity/
  README.md                     ← this file
  Jint/Jint.dll                 ← JS interpreter for unity_eval (Jint 2.11.58, netstandard2.0)
  Editor/
    SceneEditorBridge.cs        ← the bridge (runs JSON command batches → result.json)
    EditorBridgeAutoRunner.cs   ← watches EditorBridge/instructions.json
    EditorBridgeHighlighter.cs  ← action highlighting in the editor
    EditorConsoleLogger.cs      ← mirrors the Console to EditorBridge/console.log
    OsInput.cs                  ← global screenshots + real OS mouse/keyboard (Windows)
    ChatWsProbe.cs              ← WebSocket handshake probe (dev helper)
    ClaudeForUnityExporter.cs   ← this package exporter (menu: Tools/Claude for Unity/Export Package)
    Chat/
      ClaudePanelWindow.cs      ← the chat panel (EditorWindow)
      ClaudeWsClient.cs         ← WebSocket client → companion
      ClaudePanel.uss           ← panel styles
```

All editor menus live under **Tools/Claude for Unity/** (Chat Panel, Export Package, bridge/console helpers).

## Prerequisites in the target project
- **Newtonsoft Json** — add the package `com.unity.nuget.newtonsoft-json` (Package Manager → Add by name).
  The bridge/panel use `Newtonsoft.Json.Linq`.
- **Windows** for the OS mouse/keyboard/global-screenshot ops (they are `#if UNITY_EDITOR_WIN`).

## Node side (copy separately, once per machine/repo)
1. Copy `Tools/UnityMCP/` and `Tools/ChatCompanion/` (incl. `Tools/ChatCompanion/standards/`) into the repo.
2. In each: `npm install` then `npm run build`.
3. One-time subscription login for the Agent SDK's bundled CLI:
   `Tools/ChatCompanion/node_modules/@anthropic-ai/claude-agent-sdk-win32-x64/claude.exe auth login`
   (or set `ANTHROPIC_API_KEY` for BYOK).
4. Run the companion: `node Tools/ChatCompanion/dist/index.js` (auto-discovers the project root and
   spawns the Unity MCP server).
5. In Unity: **Tools/Claude for Unity/Chat Panel** — connects to `ws://127.0.0.1:8787`.

## Re-exporting the latest version
**Tools/Claude for Unity/Export Package** (or MCP `unity_export_package`). Output goes to
`<projectRoot>/ClaudeForUnity-Packages/`.
