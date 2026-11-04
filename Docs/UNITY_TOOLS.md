# Инструменты проекта

## Мост к редактору

`bash Tools/WebGLCheck/bridge.sh '<json>' [секунды]` пишет пакет в `EditorBridge/instructions.json` и печатает `result.json`. Консоль — `EditorBridge/console.log`.

| Задача | Пакет |
| --- | --- |
| Компиляция | `{"commands":[{"op":"refresh","compile":true}]}`, затем опрос `consoleCounts` до `isCompiling: false` |
| Разрешить пакеты после правки `manifest.json` | `call` на `UnityEditor.PackageManager.Client, UnityEditor.CoreModule`, метод `Resolve` |
| Модели художника | `call` на `Core.EditorTools.ModelLibrary, Assembly-CSharp-Editor`: `Build` (префабы из `.glb`, входит в `BuildEverything`), `Describe(id)` (узлы, позиции, размеры) |
| Собрать всё с чистой копии | `call` на `Core.EditorTools.ProjectSetup, Assembly-CSharp-Editor`, метод `BuildEverything`; первый запуск импортирует TMP Essentials, запустить второй раз |
| EditMode-тесты | `call` на `Core.EditorTools.AgentTestRunner, Assembly-CSharp-Editor`, метод `Run`, `args:[null]` → `EditorBridge/test_results.json` |
| Скрипты под все площадки | `call` на `Core.EditorTools.PlatformBuild, Assembly-CSharp-Editor`, метод `CheckScripts` |
| Сборка | `call` на `Core.EditorTools.PlatformBuild, Assembly-CSharp-Editor`, метод `BuildYouTube`, `BuildCrazyGames` или `BuildYandex` → `Build/<площадка>`, `Logs/build_summary.json` |
| Команды разработчика | `call` на `Meta.DeveloperTools.Services.DevBridge, Assembly-CSharp`: `State`, `SetLanguage`, `BattleCheckStart(кадры)`, `BattleCheckResult`, `SimulatePause`, `SimulateAudio`, `SimulateFocus`, `Interstitial(место)` |
| Бой | `DevBridge`: `Battle` (тик, итог, доли в ‰, герой в клетках), `Restart`, `Walk(x,y)`, `WalkTicks(x,y,тики)`, `WalkPath("x,y,тики;…")`, `PaintRect(сторона,x0,y0,x1,y1)`, `FillShare(сторона,проценты)`, `ForceResult(Victory\|Defeat)`, `ShowDebug(bool)` (как F1: сетка, тики, кадры), `Overview(bool)` (вся карта сверху), `FreezeAfterCapture(секунды)` (пауза площадки после следующего окружения — волна и «+N%» остаются на скриншоте), `FreezeIn(секунды)` (пауза площадки через столько секунд — поймать бой). Слой 2: `Give(сторона,золото,дерево)`, `Soldiers(сторона,число,x,y)` (бойцы вокруг клетки; без героя держатся этой точки), `Build(сторона,место 0–5)`, `Sites` (места: центр, хозяин, казарма, прогресс). Стороны: `Player`, `Enemy`, `Neutral` |
| Ввод и окно Game | `call` на `Core.EditorTools.InputScript, Assembly-CSharp-Editor`: `SetGameView(ш,в)`, `TouchDown/TouchMove/TouchUp(x,y)` (палец на виртуальном тачскрине), `Mouse(x,y,нажата)`, `Keys("W,A")` (пусто — отпустить). Координаты — пиксели окна Game, от нижнего левого угла |
| Помощники в оболочке | `. Tools/WebGLCheck/selfcheck.sh`, затем `play enter`, `db Battle`, `is SetGameView "[1080,1920]"`, `shot <имя>` (в `Docs/Verification/stage-1/`), `counts` |

Найденные ограничения:

- В `call` тип указывать со сборкой: `Тип, Assembly-CSharp-Editor`.
- Операции `openScene` в мосте нет; путь `screenshot` считается от папки `EditorBridge`.
- `setPlayMode` — последней командой пакета. Во время сборки мост занят, ответ придёт по её окончании.
- Операции `wait` в мосте нет, она пишет ошибку в консоль; ждать — `sleep` в оболочке между пакетами.
- Операции `exists` в мосте нет (только `assert` с `kind:"exists"`); `find` не видит объекты в `DontDestroyOnLoad` — для них `eval` с `GameObject.Find`.
- В Play Mode один пакет отвечает за 1–3,5 с: путь героя задавать `WalkTicks`/`WalkPath`, а короткие моменты (волна окружения) ловить `FreezeAfterCapture`, а не `sleep`.
- `click`/`mouseDrag` моста идут в EventSystem мимо Input System: стик ими не проверить, только `InputScript`.
- Встроенный меш `Capsule.fbx` — 2 × 4 × 2 единицы (а `Cube.fbx` — 1 × 1 × 1): масштаб капсулы делить пополам (заглушки без моделей).
- glTFast отражает ось x модели: «левое» и «правое» у частей сохраняются, но угол берега после импорта закрывает −x, +z.
- Без символа площадки сборка плеера не компилируется намеренно: собирать только через `Tools/Build`.

## Проверка сборки в браузере

`Tools/WebGLCheck`:

- `serve.mjs <папка> <порт> [gzip|plain|unity]` — локальный сервер; `gzip` ведёт себя как серверы Playables.
- `cdp_check.mjs <адрес> <маркер> [таймаут] [браузер] [--viewport=WxH --screenshot= --report= --stub= --block= --settle=]` — headless-Chrome, консоль, запросы, внешние адреса, время до маркера, скролл страницы.
- `build_report.mjs <папка> [запрещённые,слова] [отчёт.json]` — вес файлов на диске, в gzip и Brotli, поиск слов и адресов в распакованном содержимом.
- `stub_youtube.js` — поддельный `ytgame`: порядок вызовов SDK, пауза, звук, сохранение вне YouTube.

Маркер готовности — строка `[BootFlow] ready`. Первый запуск `cdp_check.mjs` после новой сборки иногда закрывает запрос `.wasm` с `ERR_ABORTED` и считает его как 0 байт, хотя игра загружается: вес по сети брать из повторного запуска. Программный WebGL (SwiftShader) сильно медленнее видеокарты: время загрузки здесь — только для сравнения сборок.
