# Инструменты проекта

## Мост к редактору

`bash Tools/WebGLCheck/bridge.sh '<json>' [секунды]` пишет пакет в `EditorBridge/instructions.json` и печатает `result.json`. Консоль — `EditorBridge/console.log`.

| Задача | Пакет |
| --- | --- |
| Компиляция | `{"commands":[{"op":"refresh","compile":true}]}`, затем опрос `consoleCounts` до `isCompiling: false` |
| Разрешить пакеты после правки `manifest.json` | `call` на `UnityEditor.PackageManager.Client, UnityEditor.CoreModule`, метод `Resolve` |
| Собрать всё с чистой копии | `call` на `Core.EditorTools.ProjectSetup, Assembly-CSharp-Editor`, метод `BuildEverything`; первый запуск импортирует TMP Essentials, запустить второй раз |
| EditMode-тесты | `call` на `Core.EditorTools.AgentTestRunner, Assembly-CSharp-Editor`, метод `Run`, `args:[null]` → `EditorBridge/test_results.json` |
| Скрипты под все площадки | `call` на `Core.EditorTools.PlatformBuild, Assembly-CSharp-Editor`, метод `CheckScripts` |
| Сборка | `call` на `Core.EditorTools.PlatformBuild, Assembly-CSharp-Editor`, метод `BuildYouTube`, `BuildCrazyGames` или `BuildYandex` → `Build/<площадка>`, `Logs/build_summary.json` |
| Команды разработчика | `call` на `Meta.DeveloperTools.Services.DevBridge, Assembly-CSharp`: `State`, `SetLanguage`, `BattleCheckStart(кадры)`, `BattleCheckResult`, `SimulatePause`, `SimulateAudio`, `SimulateFocus`, `Interstitial(место)` |

Найденные ограничения:

- В `call` тип указывать со сборкой: `Тип, Assembly-CSharp-Editor`.
- Операции `openScene` в мосте нет; путь `screenshot` считается от папки `EditorBridge`.
- `setPlayMode` — последней командой пакета. Во время сборки мост занят, ответ придёт по её окончании.
- Без символа площадки сборка плеера не компилируется намеренно: собирать только через `Tools/Build`.

## Проверка сборки в браузере

`Tools/WebGLCheck`:

- `serve.mjs <папка> <порт> [gzip|plain|unity]` — локальный сервер; `gzip` ведёт себя как серверы Playables.
- `cdp_check.mjs <адрес> <маркер> [таймаут] [браузер] [--viewport=WxH --screenshot= --report= --stub= --block= --settle=]` — headless-Chrome, консоль, запросы, внешние адреса, время до маркера, скролл страницы.
- `build_report.mjs <папка> [запрещённые,слова] [отчёт.json]` — вес файлов на диске, в gzip и Brotli, поиск слов и адресов в распакованном содержимом.
- `stub_youtube.js` — поддельный `ytgame`: порядок вызовов SDK, пауза, звук, сохранение вне YouTube.

Маркер готовности — строка `[BootFlow] ready`. Программный WebGL (SwiftShader) сильно медленнее видеокарты: время загрузки здесь — только для сравнения сборок.
