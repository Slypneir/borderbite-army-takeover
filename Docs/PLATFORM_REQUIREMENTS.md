# Требования площадок и что сделано в проекте

Сверено с официальной документацией 2–3 ноября 2026. Требования обновляются: перед отправкой на модерацию сверить ещё раз. Источники — в конце.

## Отличия от постановки

| Что просили | Что в проекте | Почему |
| --- | --- | --- |
| Brotli + Decompression Fallback на всех сборках | CrazyGames и Яндекс — как просили. **Playables — без сжатия Unity** (`PlatformBuildProfile.YouTube.Compression`) | FAQ сертификации Google: сжатие Unity не рекомендуется, а если необходимо — обязательно zip и своя распаковка в загрузчике; Brotli не поддерживается. Страница Unity-обёртки: Compression Format = Disabled, «gzip и brotli Unity не поддерживаются». Серверы YouTube сами сжимают файлы, начальный вес считается после этого сжатия |
| — | `Run In Background` включён | Playables запрещает Page Visibility API: пауза только от `onPause`/`onResume`. Иначе Unity останавливается сама при потере фокуса |
| — | Отслеживание фокуса есть только в сборках CrazyGames и Яндекса | Яндекс п. 1.3 требует глушить звук при потере фокуса; Playables то же самое запрещает делать в обход SDK |

## YouTube Playables

| Требование | Уровень | Как выполнено |
| --- | --- | --- |
| Начальная загрузка до `gameReady` < 30 МиБ, цель < 15 МиБ, считается после HTTP-сжатия | MUST / SHOULD | 9,65 МиБ у пустой сборки (`Docs/Reports/playables_empty_build.md`) |
| Каждый файл < 30 МиБ, цель < 512 КиБ | MUST / SHOULD | `.wasm` 19,78 МиБ на диске. Цель 512 КиБ для `.wasm` Unity недостижима |
| Вся игра < 250 МиБ, не больше 8 000 файлов | MUST | Да |
| Имена файлов — латиница, цифры, `_ - .`; только относительные пути | MUST | Шаблон `Playables` и имена сборки |
| Нет внешних вызовов, в том числе аналитики | MUST | Сервисы Unity выключены; в замере только запросы SDK |
| SDK загружается раньше кода игры | MUST | `<script src="https://www.youtube.com/game_api/v1">` первым в `<head>` шаблона |
| `firstFrameReady`, затем `gameReady`, когда можно играть | MUST | `BootFlow`: `SignalFirstFrame` → `SignalReady` |
| Пауза только от SDK; остановить цикл, музыку, ввод, сеть, отрисовку | MUST | `platform_youtube.jslib` останавливает главный цикл, `PlaybackGate` — время и звук |
| Выключенный звук YouTube важнее настроек игры | MUST | `isAudioEnabled`, `onAudioEnabledChange` → `PlaybackState.PlatformMuted` |
| `saveData` только после успешного `loadData`; < 3 МиБ, цель < 500 КиБ; других способов сохранять нет | MUST | `PlatformCapabilities.LoadBeforeSave`, `SaveLimit` |
| Куча JS не больше 512 МБ | MUST | Maximum Memory 512 МБ |
| Адаптивность под любое соотношение сторон, без блокировки ориентации | MUST | `ScreenLayoutService`, якоря и `SafeAreaFitter`; блокировки нет |
| Esc не перехватывается через `preventDefault` | MUST | `WebGLInput.captureAllKeyboardInput = false` в `BootFlow` |
| Загрузка < 5 с | SHOULD | Проверить на устройстве: замер в headless-Chrome с программным WebGL не показателен |
| Межуровневая реклама только между разными экранами; rewarded только по кнопке | MUST | `AdPolicy_YouTube`; показ через `IAdService` |

## CrazyGames

| Требование | Как выполнено |
| --- | --- |
| Начальная загрузка ≤ 50 МБ, для мобильной главной ≤ 20 МБ; всего ≤ 250 МБ, ≤ 1 500 файлов; относительные пути | Шаблон `CrazyGames`, цель ≤ 20 МБ |
| SDK v3 в `<head>`, `SDK.init()`; `gameplayStart/Stop`; `loadingStart/Stop` | `platform_crazygames.jslib` |
| DPR Unity-игр управляется площадкой | В шаблоне `devicePixelRatio` не трогаем |
| `user-select: none` на `body` | В шаблоне |
| Midgame только на переходах, не на кнопках навигации; частоту ограничивает SDK | `AdPolicy_CrazyGames`: навигация выключена, своя пауза 0 |
| Между двумя уровнями — midgame или «rewarded, чтобы продолжить», не оба | `rewardedExcludesInterstitial` |
| Звук глушится только когда реклама реально пошла; на `adError` игра продолжается, награды нет | `adStart`/`adEnd`, `PlaybackState`, ответ `closed` |
| `muteAudio` важнее настроек игры | `addSettingsChangeListener` → `audioOff`/`audioOn` |
| В Basic Launch реклама отключена | Открытый вопрос №10; выключается `adsEnabled` в ассете |

## Яндекс Игры

| Требование | Как выполнено |
| --- | --- |
| Все файлы ≤ 100 МБ, `index.html` в корне, без пробелов и кириллицы в именах (1.21, 1.22) | Имена сборки `Yandex.*` |
| `/sdk.js`, `YaGames.init()` (1.1, 1.19.1) | Шаблон `Yandex`, `platform_yandex.jslib` |
| `LoadingAPI.ready()`, `GameplayAPI.start/stop`, `game_api_pause/resume` (1.19.2–1.19.4) | Там же; `SdkPlatform` повторяет `stop`/`start` на паузе |
| Звук стоп при потере фокуса (1.3) | `focusLost`/`focusBack` → `PlaybackState.FocusLost` |
| Поле растянуто по одной оси, длинная сторона не больше двух коротких (1.6.2.1, 1.6.2.2) | `fitStage()` в шаблоне `Yandex` |
| Нет скролла и swipe-to-refresh (1.10.2), лонгтап не выделяет (1.6.1.8) | CSS шаблона: `overflow`, `overscroll-behavior`, `touch-action`, `user-select`, `touch-callout` |
| Автоопределение языка (2.14) | `environment.i18n.lang` → `Loc.Set` |
| Реклама в логических паузах; при показе звук и игра на паузе (4.4, 4.7); rewarded — по кнопке, награда не условие (4.5) | `AdPolicy_Yandex`: только после действия игрока; кнопка «Магазин» разрешена |
| Сохранения: 200 КБ, 100 запросов за 5 минут | `SaveLimit`; `setData(..., true)` |
| Война и конфликты запрещены (3.4.4), реалистичное насилие (3.4.3), интерактивный ИИ (1.23) | Решения в `CLAUDE.md`; ИИ-противники — алгоритм, не генеративный ИИ. Риск слова `border` — открытый вопрос №4 |

## Что проверить вручную

Только на самих площадках: реальный показ рекламы, `gameReady` в тестовом наборе Playables (SDK Test Suite), `debug-mode=16` на Яндексе, QA-режим CrazyGames. Настоящие телефоны, Chromebook 4 ГБ и слабый Android.

## Источники

- Google: [certification](https://developers.google.com/youtube/gaming/playables/certification/requirements), [stability and performance](https://developers.google.com/youtube/gaming/playables/certification/requirements_stability), [integration](https://developers.google.com/youtube/gaming/playables/certification/requirements_integration), [design](https://developers.google.com/youtube/gaming/playables/certification/requirements_design), [monetization](https://developers.google.com/youtube/gaming/playables/certification/requirements_monetization), [certification FAQ](https://developers.google.com/youtube/gaming/playables/support/certification_faq), [SDK reference](https://developers.google.com/youtube/gaming/playables/reference/sdk), [getting started](https://developers.google.com/youtube/gaming/playables/reference/getting_started), [Unity wrapper](https://developers.google.com/youtube/gaming/playables/samples/unity_wrapper).
- CrazyGames: [technical](https://docs.crazygames.com/requirements/technical/), [ads](https://docs.crazygames.com/requirements/ads/), [SDK intro](https://docs.crazygames.com/sdk/intro/), [game](https://docs.crazygames.com/sdk/game/), [user](https://docs.crazygames.com/sdk/user/), [data](https://docs.crazygames.com/sdk/data/), [video ads](https://docs.crazygames.com/sdk/video-ads/).
- Яндекс (список требований от 27 октября 2026): [требования](https://yandex.ru/dev/games/doc/ru/concepts/requirements), [подключение SDK](https://yandex.ru/dev/games/doc/ru/sdk/sdk-about), [реклама](https://yandex.ru/dev/games/doc/ru/sdk/sdk-adv), [события игры](https://yandex.ru/dev/games/doc/ru/sdk/sdk-game-events), [игрок](https://yandex.ru/dev/games/doc/ru/sdk/sdk-player). События `game_api_pause`/`game_api_resume` и `environment.i18n.lang` взяты из брифинга и образца: страницу с их описанием найти не удалось, проверить на модерации.
