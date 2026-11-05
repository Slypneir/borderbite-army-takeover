using Core.AudioManagement.Configuration;
using Core.DependencyInjection;
using Core.InputManagement.Configuration;
using Core.InputManagement.Views;
using Core.PlatformSystem.Configuration;
using Core.ScreenSystem.Configuration;
using Core.ScreenSystem.Views;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Views;
using Meta.FlowSystem.Views;
using Meta.ProgressSystem.Configuration;
using Meta.LocalizationSystem.Views;
using Meta.MapEditorSystem.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core.EditorTools
{
    /// <summary>
    /// The one scene of the game, always from this code: Tools/Setup/Build Main Scene. The camera, the objects of the battle, the
    /// root installer and a Canvas. The views on the canvas (stick, territory bar, results, «+N%», the line of the developer)
    /// are placeholders of layer 1 without the kit and the theme: the builders of borderbite-ui will build them later and
    /// wire the same components.
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        // placeholder tokens of layer 1, from borderbite-ui STYLE §1–§2 where they exist
        private const int CanvasReference = 720;
        private const int EdgeMargin = 24;
        private const int BarHeight = 56;
        private const int BarTextSize = 30;
        private const int MarkWidth = 4;
        private const int TitleSize = 96;
        private const int CountdownSize = 64;
        private const int BodySize = 32;
        private const int CaptionSize = 24;
        private const int HeadingSize = 48;
        private const int CrownsSize = 72;
        private const int ButtonHeight = 128;
        private const int ButtonWidth = 360;
        private const int ButtonTextSize = 44;
        private const int PopupSize = 52;
        private const int DebugSize = 24;
        private static readonly Color TextColor = new Color(0.96f, 0.96f, 0.92f);
        private const int BarPadding = 6;
        private const int CounterHeight = 56;
        private const int CounterWidth = 180;
        // the icons of the HUD counters: not smaller than 48 units (UI review of f1d3ccb)
        private const int CounterIcon = 48;
        private const int CounterPadding = 8;
        private const int CounterTextSize = 34;
        private const int RingSize = 56;
        private const int LabelTextSize = 26;
        // the icons in the labels over places and barracks, and over the piles (battle-marks card, «Значки над картой»)
        private const int LabelIcon = 64;
        private const int MapIcon = 48;
        // the pin of a place to build (task 4.26): the medallion and the whole with its tail
        private const float PinMedallion = 64f;
        private const float PinTall = 76f;
        private const int HealthWidth = 90;
        private const int HealthHeight = 12;
        private const float TitleHeight = 0.72f;
        private static readonly Color BarBackColor = new Color(0.32f, 0.33f, 0.31f);
        private static readonly Color BarBaseColor = new Color(0.1f, 0.11f, 0.14f);
        private static readonly Color DimColor = new Color(0.05f, 0.06f, 0.09f, 0.86f);
        // neutral: yellow is gold, a signal of the game (borderbite-ui STYLE §3)
        private static readonly Color ButtonColor = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color ButtonTextColor = new Color(0.1f, 0.11f, 0.14f);

        [MenuItem("Tools/Setup/Build Main Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var view = AssetDatabase.LoadAssetAtPath<BattleViewConfig>(ProjectSetup.BattleViewConfigPath);

            var camera = BuildCamera(view);
            var world = BuildWorld();
            var canvas = BuildCanvas(view, out var results, out var upgrades, out var pause, out var menu, out var editor);
            BuildRoot(camera, world, results, upgrades, pause, canvas);
            var root = Object.FindFirstObjectByType<RootInstaller>(FindObjectsInactive.Include);
            Wire(root, ("mainMenuView", menu), ("mapEditorView", editor));
            BuildEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[MainSceneBuilder] built");
        }

        private static Camera BuildCamera(BattleViewConfig view)
        {
            var go = new GameObject("MainCamera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(new Vector3(8f, 50f, -20f), Quaternion.Euler(60f, 0f, 0f));
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // the water reaches the horizon: the empty background is the same colour
            camera.backgroundColor = view.WaterColor;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            return camera;
        }

        private static BattleWorld BuildWorld()
        {
            var go = new GameObject("BattleWorld");
            var world = go.AddComponent<BattleWorld>();
            var surface = Mesh(go.transform, "Surface", "Quad.fbx");
            var island = Mesh(go.transform, "Island", "Cube.fbx");
            var water = Mesh(go.transform, "Water", "Quad.fbx");
            var hero = new GameObject("Hero").transform;
            hero.SetParent(go.transform, false);
            Model(hero, "hero", true,
                // the built-in capsule mesh is 2 × 4 × 2 units: a hero 0.6 m wide and 1 m tall
                ("paint", "Capsule.fbx", new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.25f, 0.3f)),
                ("paint", "Cube.fbx", new Vector3(0f, 0.7f, 0.3f), Vector3.one * 0.24f));
            var templates = new GameObject("Templates").transform;
            templates.SetParent(go.transform, false);

            var serialized = new SerializedObject(world);
            serialized.FindProperty("soldierTemplate").objectReferenceValue = Model(templates, "soldier", false,
                ("paint", "Capsule.fbx", new Vector3(0f, 0.35f, 0f), new Vector3(0.225f, 0.175f, 0.225f)),
                ("paint", "Cube.fbx", new Vector3(0f, 0.5f, 0.22f), Vector3.one * 0.18f));
            serialized.FindProperty("goldTemplate").objectReferenceValue = Model(templates, "gold_pile", false,
                ("paint", "Cube.fbx", new Vector3(-0.1f, 0.1f, -0.05f), Vector3.one * 0.22f),
                ("paint", "Cube.fbx", new Vector3(0.12f, 0.1f, 0.06f), Vector3.one * 0.22f),
                ("paint", "Cube.fbx", new Vector3(0.02f, 0.3f, 0f), Vector3.one * 0.2f));
            serialized.FindProperty("woodTemplate").objectReferenceValue = Model(templates, "wood_bundle", false,
                ("paint", "Cube.fbx", new Vector3(0f, 0.08f, -0.09f), new Vector3(0.6f, 0.15f, 0.15f)),
                ("paint", "Cube.fbx", new Vector3(0f, 0.08f, 0.09f), new Vector3(0.6f, 0.15f, 0.15f)),
                ("paint", "Cube.fbx", new Vector3(0f, 0.23f, 0f), new Vector3(0.6f, 0.15f, 0.15f)));
            serialized.FindProperty("siteTemplate").objectReferenceValue = Model(templates, "build_spot", false,
                ("body", "Cube.fbx", new Vector3(0f, 0.015f, 0f), new Vector3(1.26f, 0.03f, 1.26f)),
                ("paint", "Cube.fbx", new Vector3(0f, 0.01f, 0f), new Vector3(1.5f, 0.02f, 1.5f)));
            serialized.FindProperty("barracksTemplate").objectReferenceValue = Model(templates, "barracks", false,
                ("body", "Cube.fbx", new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 1.3f)),
                ("paint", "Cube.fbx", new Vector3(0f, 1f, 0f), new Vector3(1.45f, 0.22f, 1.45f)));
            serialized.FindProperty("baseTemplate").objectReferenceValue = Model(templates, "base", false,
                ("body", "Cube.fbx", new Vector3(0f, 0.6f, 0f), new Vector3(3f, 1.2f, 3f)),
                ("paint", "Cube.fbx", new Vector3(0f, 1.6f, 0f), new Vector3(1.3f, 2f, 1.3f)));
            serialized.FindProperty("shoreTemplate").objectReferenceValue = Model(templates, "shore_straight", false);
            var variants = serialized.FindProperty("shoreVariants");
            variants.arraySize = 0;
            foreach (var id in new[] { "shore_straight", "shore_straight_b", "shore_straight_c" })
            {
                var piece = id == "shore_straight" ? (Transform)serialized.FindProperty("shoreTemplate").objectReferenceValue : Model(templates, id, false);
                if (piece == null)
                    continue;

                variants.arraySize++;
                variants.GetArrayElementAtIndex(variants.arraySize - 1).objectReferenceValue = piece;
            }

            serialized.FindProperty("shoreCornerTemplate").objectReferenceValue = Model(templates, "shore_corner", false);
            var rocks = serialized.FindProperty("rockTemplates");
            rocks.arraySize = 0;
            foreach (var id in new[] { "rock_small", "rock_large", "rock_tall" })
            {
                var rock = Model(templates, id, false);
                if (rock == null)
                    continue;

                rocks.arraySize++;
                rocks.GetArrayElementAtIndex(rocks.arraySize - 1).objectReferenceValue = rock;
            }

            serialized.FindProperty("debrisTemplate").objectReferenceValue = Template(templates, "Debris",
                ("Cube", "Cube.fbx", Vector3.zero, Vector3.one));
            serialized.FindProperty("shotTemplate").objectReferenceValue = Template(templates, "Shot",
                ("Trace", "Cube.fbx", Vector3.zero, Vector3.one));
            serialized.FindProperty("soldierDeathTemplate").objectReferenceValue = Model(templates, "soldier_debris", false);
            serialized.FindProperty("heroDeathTemplate").objectReferenceValue = Model(templates, "hero_debris", false);
            serialized.FindProperty("dustTemplate").objectReferenceValue = Model(templates, "fx_dust_burst", false);
            serialized.FindProperty("surface").objectReferenceValue = surface;
            serialized.FindProperty("island").objectReferenceValue = island;
            serialized.FindProperty("water").objectReferenceValue = water;
            serialized.FindProperty("hero").objectReferenceValue = hero;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return world;
        }

        /// <summary>
        /// The model of the 3D artist under <paramref name="parent"/> (Editor/ModelLibrary), or, while it is missing, a placeholder
        /// of the given parts with the flat material; null when there is neither. A template is inactive, a part of the scene stays.
        /// </summary>
        private static Transform Model(Transform parent, string id, bool active,
            params (string Name, string Mesh, Vector3 Position, Vector3 Scale)[] placeholder)
        {
            var prefab = ModelLibrary.Prefab(id);
            Transform model;
            if (prefab != null)
            {
                model = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
            }
            else if (placeholder.Length > 0)
            {
                model = Template(parent, id, placeholder);
                var flat = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.FlatMaterialPath);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterial = flat;
            }
            else
            {
                return null;
            }

            model.gameObject.SetActive(active);
            return model;
        }

        /// <summary>An inactive placeholder of boxes and capsules; the views copy it and colour it.</summary>
        private static Transform Template(Transform parent, string name, params (string Name, string Mesh, Vector3 Position, Vector3 Scale)[] parts)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            foreach (var part in parts)
            {
                var renderer = Mesh(root, part.Name, part.Mesh);
                renderer.transform.localPosition = part.Position;
                renderer.transform.localScale = part.Scale;
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static MeshRenderer Mesh(Transform parent, string name, string builtin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(builtin);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static void BuildRoot(Camera camera, BattleWorld world, ResultsView results, UpgradesView upgrades, PauseView pause, GameObject canvas)
        {
            var go = new GameObject("RootInstaller");
            go.SetActive(false);
            var installer = go.AddComponent<RootInstaller>();
            var serialized = new SerializedObject(installer);
            serialized.FindProperty("adPolicyConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AdPolicyConfig>(ProjectSetup.EditorPolicy);
            serialized.FindProperty("battleConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BattleConfig>(ProjectSetup.BattleConfigPath);
            serialized.FindProperty("battleViewConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BattleViewConfig>(ProjectSetup.BattleViewConfigPath);
            serialized.FindProperty("screenLayoutConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ProjectSetup.ScreenConfigPath);
            serialized.FindProperty("moveStickConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveStickConfig>(ProjectSetup.MoveStickConfigPath);
            serialized.FindProperty("soundVolumes").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SoundVolumes>(ProjectSetup.SoundVolumesPath);
            serialized.FindProperty("mainCamera").objectReferenceValue = camera;
            serialized.FindProperty("battleWorld").objectReferenceValue = world;
            serialized.FindProperty("resultsView").objectReferenceValue = results;
            serialized.FindProperty("upgradesView").objectReferenceValue = upgrades;
            serialized.FindProperty("pauseView").objectReferenceValue = pause;
            serialized.FindProperty("progressConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ProgressConfig>(ProjectSetup.ProgressConfigPath);
            var inject = serialized.FindProperty("autoInjectGameObjects");
            inject.arraySize = 1;
            inject.GetArrayElementAtIndex(0).objectReferenceValue = canvas;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
        }

        private static GameObject BuildCanvas(BattleViewConfig view, out ResultsView results, out UpgradesView upgrades, out PauseView pause,
            out MainMenuView menu, out MapEditorView editor)
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasReference, CanvasReference);
            // a square reference and Expand: the short side of the window is always the same number of units, in both orientations
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)go.transform;

            var icons = BuildResourceIcons(root);
            var labels = BuildWorldLabels(root);
            var marks = BuildMarks(root);
            var popups = BuildPopups(root);
            var numbers = BuildHitNumbers(root);
            BuildStick(root);
            var area = Rect(root, "SafeArea", Vector2.zero, Vector2.one);
            area.gameObject.AddComponent<SafeAreaFitter>();
            var hud = BuildHud(area, view, out var pauseButton);
            var territory = hud;
            var counters = hud;
            Wire(labels, ("hudBottom", counters));
            Wire(icons, ("hudBottom", counters), ("labels", labels));
            Wire(marks, ("hudBottom", counters));
            var orders = BuildOrders(area);
            BuildResourceFlight(area, hud);
            Wire(labels, ("orderBar", orders.Row));
            Wire(marks, ("orderBar", orders.Row));
            Wire(icons, ("orderBar", orders.Row));
            var hint = BuildHint(root, counters);
            BuildDebug(area);
            results = BuildResults(area);
            upgrades = BuildUpgrades(area);
            pause = BuildPause(area);
            // the main menu and the map editor cover the whole window, over everything else (owner, 5 November)
            menu = BuildMainMenu(area);
            editor = BuildMapEditor(area);

            // the layers of the battle hide under a window (UI review of layers 3–4)
            var layers = go.AddComponent<BattleLayersView>();
            var serialized = new SerializedObject(layers);
            var over = serialized.FindProperty("overWorld");
            var overLayers = new Component[] { icons, labels, marks, popups, numbers, hint, orders };
            over.arraySize = overLayers.Length;
            for (var i = 0; i < overLayers.Length; i++)
                over.GetArrayElementAtIndex(i).objectReferenceValue = Group(overLayers[i].gameObject);
            var hudProperty = serialized.FindProperty("hud");
            hudProperty.arraySize = 2;
            hudProperty.GetArrayElementAtIndex(0).objectReferenceValue = Group(territory.gameObject);
            hudProperty.GetArrayElementAtIndex(1).objectReferenceValue = Group(counters.gameObject);
            serialized.FindProperty("pauseButton").objectReferenceValue = Group(pauseButton.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static CanvasGroup Group(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null)
                group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            return group;
        }

        private static void BuildStick(RectTransform root)
        {
            var go = new GameObject("MoveStick", typeof(RectTransform), typeof(CanvasGroup), typeof(MoveStickView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.CirclePath);
            var stickBase = Rect((RectTransform)go.transform, "Base", Vector2.zero, Vector2.zero);
            stickBase.pivot = new Vector2(0.5f, 0.5f);
            Picture(stickBase, circle, Color.white);
            var knob = Rect(stickBase, "Knob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            Picture(knob, circle, Color.white);

            Wire(go.GetComponent<MoveStickView>(), ("stickBase", stickBase), ("knob", knob), ("group", group));
        }

        private const string HudFolder = "Assets/Sprites/UI/Hud/";

        private static Sprite Hud(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(HudFolder + name + ".png");

        /// <summary>
        /// The top HUD as one iron plate (battle-hud card «Вид в стиле игры», art of the 3D artist 3a3c4f0): the frame sliced at a
        /// scale of 0.75, rivets on the long sides; the territory bar — a track with the fills under a mask, the blue from the left,
        /// the red from the right, pins with flags at 70 % of each, the percents cream and bold; the counters — icons 48 and
        /// numbers; the pause — a neutral button 64 with its icon, a target of 104. <see cref="HudLayoutView"/> lays it out in two
        /// rows on a tall screen and one on a wide one.
        /// </summary>
        private static RectTransform BuildHud(RectTransform area, BattleViewConfig view, out RectTransform pause)
        {
            var plate = Rect(area, "Hud", new Vector2(0f, 1f), new Vector2(1f, 1f));
            var frame = Picture(plate, Hud("hud_frame_256"), Color.white);
            frame.type = Image.Type.Sliced;
            frame.fillCenter = true;
            // a rim of 48 px at 0.75: 36 units
            frame.pixelsPerUnitMultiplier = 48f / 36f;
            frame.raycastTarget = true;

            var tops = new RectTransform[3];
            var bottoms = new RectTransform[3];
            for (var i = 0; i < 3; i++)
            {
                tops[i] = Rivet(plate, "RivetTop" + i);
                bottoms[i] = Rivet(plate, "RivetBottom" + i);
            }

            var bar = Rect(plate, "TerritoryBar", Vector2.zero, Vector2.one);
            var track = Picture(bar, Hud("bar_track_128x64"), Color.white);
            track.type = Image.Type.Sliced;
            var inner = Rect(bar, "Inner", Vector2.zero, Vector2.one);
            inner.offsetMin = new Vector2(8f, 8f);
            inner.offsetMax = new Vector2(-8f, -8f);
            var mask = Picture(inner, Hud("bar_mask_128x64"), Color.white);
            mask.type = Image.Type.Sliced;
            // the rim of the mask scaled to the inside of the track (48 high)
            mask.pixelsPerUnitMultiplier = 64f / 48f;
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var playerFill = Rect(inner, "PlayerFill", Vector2.zero, new Vector2(0.1f, 1f));
            var playerImage = Picture(playerFill, Hud("bar_fill_own_128x48"), Color.white);
            playerImage.type = Image.Type.Tiled;
            var enemyFill = Rect(inner, "EnemyFill", new Vector2(0.9f, 0f), Vector2.one);
            var enemyImage = Picture(enemyFill, Hud("bar_fill_enemy_128x48"), Color.white);
            enemyImage.type = Image.Type.Tiled;
            var playerMark = Pin(bar, "PlayerMark", "bar_pin_own_64x128");
            var enemyMark = Pin(bar, "EnemyMark", "bar_pin_enemy_64x128");
            var playerLabel = Text(inner, "PlayerPercent", BodySize, TextAlignmentOptions.Left);
            ((RectTransform)playerLabel.transform).offsetMin = new Vector2(12f, 0f);
            var enemyLabel = Text(inner, "EnemyPercent", BodySize, TextAlignmentOptions.Right);
            ((RectTransform)enemyLabel.transform).offsetMax = new Vector2(-12f, 0f);
            foreach (var label in new[] { playerLabel, enemyLabel })
            {
                label.fontStyle = FontStyles.Bold;
                label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);
            }

            Wire(bar.gameObject.AddComponent<TerritoryBarView>(), ("playerFill", playerFill), ("enemyFill", enemyFill),
                ("playerMark", playerMark), ("enemyMark", enemyMark), ("playerLabel", playerLabel), ("enemyLabel", enemyLabel));

            var row = Rect(plate, "Counters", Vector2.zero, Vector2.zero);
            var gold = Counter(row, "Gold", 0, "icon_gold_128", view.GoldColor, out var goldGain);
            var wood = Counter(row, "Wood", 1, "icon_wood_128", view.WoodColor, out var woodGain);
            var army = Counter(row, "Army", 2, "army", view.PlayerSoldierColor, out _);
            // the soldier of the army icon is small in its picture: a larger picture gives a figure as big as gold and wood
            var armyIcon = (RectTransform)army.transform.parent.Find("Icon");
            armyIcon.sizeDelta = Vector2.one * 64f;
            armyIcon.anchoredPosition = new Vector2(CounterPadding - 8f, 0f);
            Wire(row.gameObject.AddComponent<ResourceHudView>(), ("goldLabel", gold), ("woodLabel", wood), ("armyLabel", army),
                ("goldGain", goldGain), ("woodGain", woodGain), ("goldIcon", gold.transform.parent.Find("Icon")),
                ("woodIcon", wood.transform.parent.Find("Icon")), ("armyIcon", armyIcon),
                ("goldFlash", Silhouette(gold.transform.parent.Find("Icon"))), ("woodFlash", Silhouette(wood.transform.parent.Find("Icon"))),
                ("armyFlash", Silhouette(armyIcon)));

            pause = BuildPauseButton(plate);
            Wire(plate.gameObject.AddComponent<HudLayoutView>(), ("plate", plate), ("bar", bar), ("counters", row), ("pause", pause));
            var layout = new SerializedObject(plate.GetComponent<HudLayoutView>());
            foreach (var (field, rivets) in new[] { ("topRivets", tops), ("bottomRivets", bottoms) })
            {
                var list = layout.FindProperty(field);
                list.arraySize = rivets.Length;
                for (var i = 0; i < rivets.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = rivets[i];
            }

            layout.ApplyModifiedPropertiesWithoutUndo();
            return plate;
        }

        private static RectTransform Rivet(RectTransform plate, string name)
        {
            var rivet = Rect(plate, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rivet.sizeDelta = Vector2.one * 24f;
            Picture(rivet, Hud("hud_rivet_32"), Color.white);
            return rivet;
        }

        /// <summary>A pin with a flag over the bar: the middle of the picture is the mark of the goal and the middle of the bar.</summary>
        private static RectTransform Pin(RectTransform bar, string name, string sprite)
        {
            var pin = Rect(bar, name, new Vector2(0.7f, 0.5f), new Vector2(0.7f, 0.5f));
            pin.sizeDelta = new Vector2(64f, 128f);
            Picture(pin, Hud(sprite), Color.white);
            return pin;
        }

        /// <summary>The pause (spec T10): a neutral iron button 64 with the icon (0.615 of it), pressed a little lower; a target of 104.</summary>
        private static RectTransform BuildPauseButton(RectTransform plate)
        {
            const float target = 104f;
            var rect = Rect(plate, "PauseButton", Vector2.zero, Vector2.zero);
            rect.sizeDelta = Vector2.one * target;
            var hit = Picture(rect, null, new Color(0f, 0f, 0f, 0f));
            hit.raycastTarget = true;
            var face = Rect(rect, "Face", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            face.sizeDelta = Vector2.one * 64f;
            var image = Picture(face, Hud("btn_neutral_normal_256"), Color.white);
            var icon = Rect(face, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            icon.sizeDelta = Vector2.one * (64f * 0.615f);
            Picture(icon, Hud("icon_pause_256"), Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = Hud("btn_neutral_pressed_256"), highlightedSprite = null, selectedSprite = null };
            rect.gameObject.AddComponent<CanvasGroup>();
            var view = rect.gameObject.AddComponent<PauseButtonView>();
            Wire(view, ("button", button));
            return rect;
        }

        private static TMP_Text Counter(RectTransform row, string name, int index, string icon, Color color, out TMP_Text gain)
        {
            var cell = Rect(row, name, new Vector2(index / 3f, 0f), new Vector2((index + 1) / 3f, 1f));
            var iconRect = Rect(cell, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(CounterPadding, 0f);
            iconRect.sizeDelta = Vector2.one * CounterIcon;
            // the icon of the 3D artist, or a coloured circle while it is missing
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.IconFolder + icon + ".png");
            Picture(iconRect, sprite != null ? sprite : AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.CirclePath), sprite != null ? Color.white : color);
            var label = Text(cell, "Value", CounterTextSize, TextAlignmentOptions.Left);
            label.fontStyle = FontStyles.Bold;
            label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);
            ((RectTransform)label.transform).offsetMin = new Vector2(CounterPadding * 2 + CounterIcon, 0f);
            gain = Text(cell, "Gain", CounterTextSize, TextAlignmentOptions.Right);
            ((RectTransform)gain.transform).offsetMax = new Vector2(-8f, 0f);
            gain.alpha = 0f;
            return label;
        }

        /// <summary>
        /// The row of the orders (army-orders card, task 4.18): five buttons 104 with a frame of their group (own — blue, attack —
        /// dark red) and the active frame over it, the icon 64 on top, a light strip under, the key in the corner, a point for a
        /// hint 72 over each; a transparent catcher 16 around the row keeps the stick off; the name of an order over the row.
        /// </summary>
        private static OrdersView BuildOrders(RectTransform area)
        {
            const float button = 104f;
            const float gap = 32f;
            var go = new GameObject("Orders", typeof(RectTransform), typeof(OrdersView));
            go.transform.SetParent(area, false);
            Stretch((RectTransform)go.transform);
            var row = Rect((RectTransform)go.transform, "Row", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(5 * button + 4 * gap, button);
            var catcher = Rect(row, "StickGuard", Vector2.zero, Vector2.one);
            catcher.offsetMin = new Vector2(-16f, -16f);
            catcher.offsetMax = new Vector2(16f, 16f);
            Picture(catcher, null, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;

            string[] orders = { "rally", "capture", "units", "barracks", "base" };
            var views = new OrderButtonView[orders.Length];
            for (var i = 0; i < orders.Length; i++)
            {
                var group = i < 2 ? "own" : "attack";
                var slot = Rect(row, "Order_" + orders[i], Vector2.zero, Vector2.zero);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = Vector2.one * button;
                slot.anchoredPosition = new Vector2(button / 2f + i * (button + gap), button / 2f);
                var body = Rect(slot, "Body", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                body.sizeDelta = Vector2.one * button;
                var frame = Rect(body, "Frame", Vector2.zero, Vector2.one);
                var frameImage = Picture(frame, Icon($"btn_order_{group}_normal_256"), Color.white);
                frameImage.raycastTarget = true;
                var active = Rect(body, "Active", Vector2.zero, Vector2.one);
                Picture(active, Icon($"btn_order_{group}_active_256"), new Color(1f, 1f, 1f, 0f));
                // the icon is 1.15 of the button, over the frame, it may go onto the rim (owner, 4 November)
                var icon = Rect(body, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                icon.sizeDelta = Vector2.one * (button * 1.15f);
                var iconSprite = Icon($"icon_order_{orders[i]}_256");
                var iconImage = Picture(icon, iconSprite, Color.white);
                iconImage.preserveAspect = true;
                if (iconSprite == null)
                {
                    // a letter while the icon is missing (card: placeholders with a letter)
                    var letter = Text(icon, "Letter", LabelTextSize, TextAlignmentOptions.Center);
                    letter.text = orders[i].Substring(0, 1).ToUpperInvariant();
                }

                var strip = Rect(slot, "Strip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                strip.sizeDelta = new Vector2(48f, 6f);
                strip.anchoredPosition = new Vector2(0f, -14f);
                Picture(strip, null, new Color(TextColor.r, TextColor.g, TextColor.b, 0f));
                var key = Text(body, "Key", 20, TextAlignmentOptions.TopRight);
                var keyRect = (RectTransform)key.transform;
                keyRect.offsetMin = new Vector2(0f, 0f);
                keyRect.offsetMax = new Vector2(-10f, -8f);
                key.fontStyle = FontStyles.Bold;
                key.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);
                var hint = Rect(slot, "HintAnchor", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                hint.anchoredPosition = new Vector2(0f, 72f);

                var view = slot.gameObject.AddComponent<OrderButtonView>();
                Wire(view, ("body", body), ("frame", frameImage), ("activeFrame", active.GetComponent<Image>()), ("icon", iconImage),
                    ("strip", strip.GetComponent<Image>()), ("key", key),
                    ("frameNormal", Icon($"btn_order_{group}_normal_256")), ("frameActive", Icon($"btn_order_{group}_active_256")),
                    ("frameNormalSmall", Icon($"btn_order_{group}_normal_128")), ("frameActiveSmall", Icon($"btn_order_{group}_active_128")),
                    ("iconLarge", iconSprite), ("iconSmall", Icon($"icon_order_{orders[i]}_128")));
                views[i] = view;
            }

            var toast = Text(row, "Name", BodySize, TextAlignmentOptions.Bottom);
            var toastRect = (RectTransform)toast.transform;
            toastRect.anchorMin = new Vector2(0f, 1f);
            toastRect.anchorMax = new Vector2(1f, 1f);
            toastRect.pivot = new Vector2(0.5f, 0f);
            toastRect.sizeDelta = new Vector2(0f, BodySize * 1.4f);
            toastRect.anchoredPosition = new Vector2(0f, 24f);
            toast.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);
            toast.alpha = 0f;

            var ordersView = go.GetComponent<OrdersView>();
            var serialized = new SerializedObject(ordersView);
            serialized.FindProperty("greyMaterial").objectReferenceValue = GreyMaterial();
            serialized.FindProperty("row").objectReferenceValue = row;
            serialized.FindProperty("toast").objectReferenceValue = toast;
            var list = serialized.FindProperty("buttons");
            list.arraySize = views.Length;
            for (var i = 0; i < views.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return ordersView;
        }

        /// <summary>A white silhouette of an icon over it, unseen until a flash (task 4.25).</summary>
        private static Image Silhouette(Transform icon)
        {
            var rect = Rect((RectTransform)icon, "Flash", Vector2.zero, Vector2.one);
            var image = Picture(rect, icon.GetComponent<Image>().sprite, new Color(1f, 1f, 1f, 0f));
            image.preserveAspect = icon.GetComponent<Image>().preserveAspect;
            image.material = WhiteMaterial();
            return image;
        }

        /// <summary>A UI material that draws a sprite as a white silhouette (Grayscale lightened to white), made once.</summary>
        private static Material WhiteMaterial()
        {
            const string path = "Assets/Materials/UI/White.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Borderbite/UI/Grayscale");
            if (shader == null)
                return null;

            if (!AssetDatabase.IsValidFolder("Assets/Materials/UI"))
                AssetDatabase.CreateFolder("Assets/Materials", "UI");
            material = new Material(shader);
            material.SetFloat("_Lighten", 1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>The material of a grey UI image (shader Borderbite/UI/Grayscale), made once.</summary>
        private static Material GreyMaterial()
        {
            const string path = "Assets/Materials/UI/Grayscale.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Borderbite/UI/Grayscale");
            if (shader == null)
                return null;

            if (!AssetDatabase.IsValidFolder("Assets/Materials/UI"))
                AssetDatabase.CreateFolder("Assets/Materials", "UI");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// The flight of resources (task 4.25, resource-flight card): a layer over the HUD in the safe area with an icon, a spark (a
        /// square) and a ring (a disc) to copy; the counters it lands in are those of the HUD.
        /// </summary>
        private static void BuildResourceFlight(RectTransform area, RectTransform hud)
        {
            var go = new GameObject("ResourceFlight", typeof(RectTransform), typeof(ResourceFlightView));
            go.transform.SetParent(area, false);
            Stretch((RectTransform)go.transform);
            var icon = Rect((RectTransform)go.transform, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            icon.sizeDelta = Vector2.one * 48f;
            var iconImage = Picture(icon, Icon("icon_gold_128"), Color.white);
            iconImage.preserveAspect = true;
            var spark = Rect((RectTransform)go.transform, "Spark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            spark.sizeDelta = Vector2.one * 8f;
            var sparkImage = Picture(spark, null, Color.white);
            var ring = Rect((RectTransform)go.transform, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ring.sizeDelta = Vector2.one * 26f;
            var ringImage = Picture(ring, AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.RingPath), Color.white);
            Wire(go.GetComponent<ResourceFlightView>(), ("template", iconImage), ("sparkTemplate", sparkImage), ("ringTemplate", ringImage),
                ("goldIcon", Icon("icon_gold_128")), ("woodIcon", Icon("icon_wood_128")), ("hud", hud.GetComponentInChildren<ResourceHudView>()));
        }

        /// <summary>An icon over each pile of gold and bundle of wood (battle-marks card, «Значки над картой»).</summary>
        private static ResourceIconsView BuildResourceIcons(RectTransform root)
        {
            var go = new GameObject("ResourceIcons", typeof(RectTransform), typeof(ResourceIconsView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var template = Rect((RectTransform)go.transform, "Template", Vector2.zero, Vector2.zero);
            template.pivot = new Vector2(0.5f, 0f);
            template.sizeDelta = Vector2.one * MapIcon;
            var image = Picture(template, Icon("icon_gold_128"), Color.white);
            var view = go.GetComponent<ResourceIconsView>();
            Wire(view, ("template", image), ("goldIcon", Icon("icon_gold_128")), ("woodIcon", Icon("icon_wood_128")),
                ("goldIconSmall", Icon("icon_gold_64")), ("woodIconSmall", Icon("icon_wood_64")));
            return view;
        }

        /// <summary>
        /// A label with a ring of progress, copied over each place of the player: a row of icons and numbers, «[barracks][wood] 10»
        /// or «[gold] 5 · [soldier] N/30» (battle-marks card), the icons 64.
        /// </summary>
        private static WorldLabelsView BuildWorldLabels(RectTransform root)
        {
            var go = new GameObject("WorldLabels", typeof(RectTransform), typeof(WorldLabelsView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var template = Rect((RectTransform)go.transform, "Template", Vector2.zero, Vector2.zero);
            template.pivot = new Vector2(0.5f, 0f);
            template.sizeDelta = new Vector2(280f, PinTall);
            template.gameObject.AddComponent<CanvasGroup>();

            // the pin of an empty place (task 4.26): a medallion 64 with a tail to 76, its tip at the root; a placeholder disc with
            // the icon of the barracks until the art of the 3D artist; the ring of building round the medallion
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.CirclePath);
            var pin = Rect(template, "Pin", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            pin.pivot = new Vector2(0.5f, 0f);
            pin.sizeDelta = new Vector2(PinMedallion, PinTall);
            var tail = Rect(pin, "Tail", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            tail.sizeDelta = Vector2.one * 18f;
            tail.anchoredPosition = new Vector2(0f, 12f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Picture(tail, null, new Color(0.18f, 0.19f, 0.23f, 1f));
            var plate = Rect(pin, "Plate", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            plate.pivot = new Vector2(0.5f, 1f);
            plate.sizeDelta = Vector2.one * PinMedallion;
            Picture(plate, circle, Color.white);
            var siteIcon = Rect(plate, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            siteIcon.sizeDelta = Vector2.one * 48f;
            Picture(siteIcon, Icon("icon_barracks_128"), Color.white).preserveAspect = true;
            var ring = Rect(pin, "Ring", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ring.sizeDelta = Vector2.one * (PinMedallion + 16f);
            ring.anchoredPosition = new Vector2(0f, -PinMedallion / 2f);
            var image = Picture(ring, circle, new Color(1f, 1f, 1f, 0.85f));
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            ring.SetAsFirstSibling();
            // the art of the 3D artist (mark_site_*, the whole pin with the barracks): the medallion takes the whole pin, the
            // placeholder tail and icon go
            var markOwn = Hud("mark_site_own_128x152");
            var markLocked = Hud("mark_site_locked_128x152");
            if (markOwn != null && markLocked != null)
            {
                plate.anchorMin = Vector2.zero;
                plate.anchorMax = Vector2.one;
                plate.offsetMin = Vector2.zero;
                plate.offsetMax = Vector2.zero;
                plate.GetComponent<Image>().sprite = markOwn;
                Object.DestroyImmediate(siteIcon.gameObject);
                Object.DestroyImmediate(tail.gameObject);
                // the medallion of the art is W / 1.15 across, its middle 56 of 152 px from the top (c1fd3cb): the ring of
                // building round it, radius 0.52 of the width
                ring.anchoredPosition = new Vector2(0f, -PinTall * 56f / 152f);
                ring.sizeDelta = Vector2.one * (PinMedallion * 1.04f);
            }
            var row = Rect(template, "Row", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(0f, LabelIcon);
            row.gameObject.AddComponent<CanvasGroup>();
            // a dark rounded backing (surface/1 #1c1d24 at 0.85) so the price reads on the light pad and the woodpile (UI review of 4.26)
            var backing = Picture(row, Hud("bar_mask_64x32"), new Color(0.11f, 0.114f, 0.141f, 0.85f));
            backing.type = Image.Type.Sliced;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 14, 0, 0);
            // 8 between an icon and its number, 24 between the groups: 8, the dot of 8, 8 (UI designer, 4 November)
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var name in new[] { "Icon1", "Icon2", "Text1", "Dot", "Icon3", "Text2" })
            {
                if (name == "Dot")
                {
                    var dot = Rect(row, name, Vector2.zero, Vector2.zero);
                    Picture(dot, AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.CirclePath), TextColor);
                    var size = dot.gameObject.AddComponent<LayoutElement>();
                    size.preferredWidth = 8f;
                    size.preferredHeight = 8f;
                    continue;
                }

                if (name.StartsWith("Icon"))
                {
                    var icon = Rect(row, name, Vector2.zero, Vector2.zero);
                    Picture(icon, Icon("icon_gold_128"), Color.white).preserveAspect = true;
                    var element = icon.gameObject.AddComponent<LayoutElement>();
                    element.preferredWidth = LabelIcon;
                    element.preferredHeight = LabelIcon;
                    continue;
                }

                var label = Text(row, name, LabelTextSize, TextAlignmentOptions.Left);
                label.fontStyle = FontStyles.Bold;
                label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);
            }

            var view = go.GetComponent<WorldLabelsView>();
            Wire(view, ("template", template), ("goldIcon", Icon("icon_gold_128")), ("woodIcon", Icon("icon_wood_128")),
                ("barracksIcon", Icon("icon_barracks_128")), ("armyIcon", Icon("army")),
                ("siteOwn", Hud("mark_site_own_128x152")), ("siteOther", Hud("mark_site_locked_128x152")), ("goldIconSmall", Icon("icon_gold_64")),
                ("woodIconSmall", Icon("icon_wood_64")), ("barracksIconSmall", Icon("icon_barracks_64")));
            return view;
        }

        /// <summary>
        /// Marks of layer 3: a health bar of a building (a rim, a dark base, a light fill anchored left), a sign at the edge (a
        /// red disc with a dark rim, a point turned to the event, a mark of a building or of land inside) and the countdown of the
        /// hero's return. Placeholders until the kit of the interface designer.
        /// </summary>
        private static BattleMarksView BuildMarks(RectTransform root)
        {
            var go = new GameObject("BattleMarks", typeof(RectTransform), typeof(BattleMarksView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var marks = (RectTransform)go.transform;
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.CirclePath);

            var bar = Rect(marks, "BarTemplate", Vector2.zero, Vector2.zero);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(120f, HealthHeight + 4f);
            Picture(bar, null, TextColor);
            var back = Rect(bar, "Back", Vector2.zero, Vector2.one);
            back.offsetMin = new Vector2(2f, 2f);
            back.offsetMax = new Vector2(-2f, -2f);
            Picture(back, null, BarBaseColor);
            var fill = Rect(bar, "Fill", Vector2.zero, Vector2.one);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            Picture(fill, null, TextColor);

            var sign = Rect(marks, "SignTemplate", Vector2.zero, Vector2.zero);
            sign.pivot = new Vector2(0.5f, 0.5f);
            sign.sizeDelta = Vector2.one * 96f;
            sign.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            // the point: a dark square turned 45° and pushed out to the edge of the disc, under it
            var point = Rect(sign, "Point", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            point.sizeDelta = Vector2.zero;
            var tip = Rect(point, "Tip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            tip.sizeDelta = Vector2.one * 40f;
            tip.anchoredPosition = new Vector2(0f, 40f);
            tip.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Picture(tip, null, BarBaseColor);
            var rim = Rect(sign, "Rim", Vector2.zero, Vector2.one);
            Picture(rim, circle, BarBaseColor);
            var disc = Rect(sign, "Disc", Vector2.zero, Vector2.one);
            disc.offsetMin = new Vector2(4f, 4f);
            disc.offsetMax = new Vector2(-4f, -4f);
            Picture(disc, circle, new Color(0.9f, 0.2f, 0.17f));
            // a building: a house of a square and a roof; land: a diamond
            var building = Rect(sign, "Building", new Vector2(0.3f, 0.28f), new Vector2(0.7f, 0.62f));
            Picture(building, null, TextColor);
            var roof = Rect(building, "Roof", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            roof.sizeDelta = Vector2.one * 22f;
            roof.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Picture(roof, null, TextColor);
            var land = Rect(sign, "Land", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            land.sizeDelta = Vector2.one * 40f;
            land.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Picture(land, null, TextColor);
            // a wave of the enemy coming (task 4.27): the crossed swords of the order «Бойцы»
            var wave = Rect(sign, "Wave", new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            Picture(wave, Icon("icon_order_units_128"), Color.white).preserveAspect = true;

            var countdown = Text(marks, "Countdown", CountdownSize, TextAlignmentOptions.Bottom);
            var countdownRect = (RectTransform)countdown.transform;
            countdownRect.anchorMin = Vector2.zero;
            countdownRect.anchorMax = Vector2.zero;
            countdownRect.pivot = new Vector2(0.5f, 0f);
            countdownRect.sizeDelta = new Vector2(200f, CountdownSize * 1.2f);
            countdown.fontStyle = FontStyles.Bold;
            countdown.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);

            var view = go.GetComponent<BattleMarksView>();
            Wire(view, ("barTemplate", bar), ("signTemplate", sign), ("countdown", countdown));
            return view;
        }

        private static void BuildDebug(RectTransform area)
        {
            var go = new GameObject("BattleDebug", typeof(RectTransform), typeof(BattleDebugView));
            go.transform.SetParent(area, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(EdgeMargin, -EdgeMargin * 3 - BarHeight - CounterHeight - DebugSize * 1.5f);
            rect.offsetMax = new Vector2(-EdgeMargin, -EdgeMargin * 3 - BarHeight - CounterHeight);
            var label = Text(rect, "Line", DebugSize, TextAlignmentOptions.TopLeft);
            Wire(go.GetComponent<BattleDebugView>(), ("label", label));
        }

        private static ResultsView BuildResults(RectTransform area)
        {
            var go = new GameObject("Results", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(ResultsView));
            go.transform.SetParent(area, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            go.GetComponent<Image>().color = DimColor;

            var title = Text(rect, "Title", TitleSize, TextAlignmentOptions.Center);
            var titleRect = (RectTransform)title.transform;
            // high above the hero, who stands in the middle of the screen
            titleRect.anchorMin = new Vector2(0f, TitleHeight);
            titleRect.anchorMax = new Vector2(1f, TitleHeight);
            titleRect.sizeDelta = new Vector2(0f, TitleSize * 1.5f);
            titleRect.anchoredPosition = Vector2.zero;

            var button = Rect(rect, "Retry", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            button.pivot = new Vector2(0.5f, 0f);
            button.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            button.anchoredPosition = new Vector2(0f, EdgeMargin * 2);
            var image = Picture(button, null, ButtonColor);
            image.raycastTarget = true;
            var retry = button.gameObject.AddComponent<Button>();
            retry.targetGraphic = image;
            var caption = Text(button, "Label", ButtonTextSize, TextAlignmentOptions.Center);
            caption.color = ButtonTextColor;

            // under the outcome: the reason, the blue share, the crowns large with the crown
            var reason = Line(rect, "Reason", BodySize, TitleHeight, -TitleSize * 0.95f);
            var share = Line(rect, "Share", BodySize, TitleHeight, -TitleSize * 0.95f - BodySize * 1.6f);
            var crowns = Line(rect, "Crowns", CrownsSize, TitleHeight, -TitleSize * 0.95f - BodySize * 3.2f - CrownsSize * 0.6f);
            crowns.alignment = TextAlignmentOptions.Left;
            var crownsRect = (RectTransform)crowns.transform;
            crownsRect.anchorMin = new Vector2(0.5f, TitleHeight);
            crownsRect.anchorMax = new Vector2(0.5f, TitleHeight);
            crownsRect.pivot = new Vector2(0f, 0.5f);
            crownsRect.sizeDelta = new Vector2(240f, CrownsSize * 1.4f);
            crownsRect.anchoredPosition = new Vector2(-20f, crownsRect.anchoredPosition.y);
            var crownIcon = Rect(rect, "CrownIcon", new Vector2(0.5f, TitleHeight), new Vector2(0.5f, TitleHeight));
            crownIcon.pivot = new Vector2(1f, 0.5f);
            crownIcon.sizeDelta = Vector2.one * CrownsSize;
            crownIcon.anchoredPosition = new Vector2(-32f, crownsRect.anchoredPosition.y);
            Picture(crownIcon, Icon("crown"), Color.white);

            var view = go.GetComponent<ResultsView>();
            Wire(view, ("group", go.GetComponent<CanvasGroup>()), ("title", title), ("reason", reason), ("share", share),
                ("crowns", crowns), ("caption", caption), ("retry", retry), ("retryRect", button));
            return view;
        }

        /// <summary>A line of text across the screen at a height (a share of it) and an offset, units.</summary>
        private static TextMeshProUGUI Line(RectTransform parent, string name, int size, float height, float offset)
        {
            var text = Text(parent, name, size, TextAlignmentOptions.Center);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, height);
            rect.anchorMax = new Vector2(1f, height);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, size * 1.4f);
            rect.anchoredPosition = new Vector2(0f, offset);
            return text;
        }

        private static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.IconFolder + name + ".png");

        /// <summary>A light button with a dark caption: the main action of a screen.</summary>
        private static (Button Button, RectTransform Rect, TextMeshProUGUI Caption) MainButton(RectTransform parent, string name, Vector2 anchor, float width, float height)
        {
            var rect = Rect(parent, name, anchor, anchor);
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);
            var image = Picture(rect, null, ButtonColor);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var caption = Text(rect, "Label", ButtonTextSize, TextAlignmentOptions.Center);
            caption.color = ButtonTextColor;
            return (button, rect, caption);
        }

        /// <summary>
        /// The upgrades (screens/upgrades.md): the crowns on top, a holder of the four cards in the middle (the view lays them out)
        /// and the main button below; a card template — the whole card one button: icon, name, effect, step, ten ticks, price.
        /// </summary>
        private static UpgradesView BuildUpgrades(RectTransform area)
        {
            var go = new GameObject("Upgrades", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(UpgradesView));
            go.transform.SetParent(area, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            go.GetComponent<Image>().color = DimColor;

            var balanceIcon = Rect(rect, "BalanceIcon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            balanceIcon.pivot = new Vector2(1f, 1f);
            balanceIcon.sizeDelta = Vector2.one * 64f;
            // on top in the middle: the HUD of the battle is hidden between levels (UI review)
            var hud = EdgeMargin * 2f;
            balanceIcon.anchoredPosition = new Vector2(-8f, -hud);
            Picture(balanceIcon, Icon("crown"), Color.white);
            var balance = Text(rect, "Balance", HeadingSize, TextAlignmentOptions.Left);
            var balanceRect = (RectTransform)balance.transform;
            balanceRect.anchorMin = balanceRect.anchorMax = new Vector2(0.5f, 1f);
            balanceRect.pivot = new Vector2(0f, 1f);
            balanceRect.sizeDelta = new Vector2(240f, 64f);
            balanceRect.anchoredPosition = new Vector2(8f, -hud);

            var cards = Rect(rect, "Cards", Vector2.zero, Vector2.one);
            cards.offsetMin = new Vector2(EdgeMargin, ButtonHeight + EdgeMargin * 3);
            cards.offsetMax = new Vector2(-EdgeMargin, -(hud + 64f + EdgeMargin));

            var card = Rect(cards, "CardTemplate", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            card.sizeDelta = new Vector2(280f, 300f);
            var back = Picture(card, null, BarBaseColor);
            back.raycastTarget = true;
            card.gameObject.AddComponent<Button>().targetGraphic = back;
            // the back stays opaque; what is on it dims when the crowns are short (UI review)
            var content = Rect(card, "Content", Vector2.zero, Vector2.one);
            content.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var icon = Rect(content, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            icon.pivot = new Vector2(0.5f, 1f);
            icon.sizeDelta = Vector2.one * 96f;
            icon.anchoredPosition = new Vector2(0f, -16f);
            Picture(icon, null, Color.white);
            CardLine(content, "Name", BodySize, -116f);
            CardLine(content, "Effect", CaptionSize, -160f);
            CardLine(content, "Step", CaptionSize, -196f);
            var ticks = Rect(content, "Ticks", new Vector2(0f, 1f), new Vector2(1f, 1f));
            ticks.pivot = new Vector2(0.5f, 1f);
            ticks.offsetMin = new Vector2(24f, -238f);
            ticks.offsetMax = new Vector2(-24f, -226f);
            for (var i = 0; i < 10; i++)
            {
                var tick = Rect(ticks, "Tick" + i, new Vector2(i / 10f, 0f), new Vector2((i + 1) / 10f, 1f));
                tick.offsetMin = new Vector2(2f, 0f);
                tick.offsetMax = new Vector2(-2f, 0f);
                Picture(tick, null, Color.white);
            }

            var price = CardLine(content, "Price", BodySize, -250f);
            price.alignment = TextAlignmentOptions.Center;
            var priceIcon = Rect(content, "PriceIcon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            priceIcon.pivot = new Vector2(1f, 1f);
            priceIcon.sizeDelta = Vector2.one * 36f;
            priceIcon.anchoredPosition = new Vector2(-40f, -254f);
            Picture(priceIcon, Icon("crown"), Color.white);

            var main = MainButton(rect, "Play", new Vector2(0.5f, 0f), ButtonWidth, ButtonHeight);
            main.Rect.anchoredPosition = new Vector2(0f, EdgeMargin * 2);

            var view = go.GetComponent<UpgradesView>();
            Wire(view, ("group", go.GetComponent<CanvasGroup>()), ("cards", cards), ("cardTemplate", card), ("balance", balance),
                ("play", main.Button), ("playRect", main.Rect), ("playCaption", main.Caption));
            var serialized = new SerializedObject(view);
            var icons = serialized.FindProperty("icons");
            icons.arraySize = 4;
            var names = new[] { "up_hero", "up_brush", "up_soldiers", "up_gold" };
            for (var i = 0; i < 4; i++)
                icons.GetArrayElementAtIndex(i).objectReferenceValue = Icon(names[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static TextMeshProUGUI CardLine(RectTransform card, string name, int size, float y)
        {
            var text = Text(card, name, size, TextAlignmentOptions.Center);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-16f, size * 1.3f);
            rect.anchoredPosition = new Vector2(0f, y);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = size;
            return text;
        }

        /// <summary>The pause (screens/pause.md): a window in the middle over the dim, «Pause», «Again» above, «Go on» below.</summary>
        private static PauseView BuildPause(RectTransform area)
        {
            var go = new GameObject("Pause", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(PauseView));
            go.transform.SetParent(area, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            go.GetComponent<Image>().color = DimColor;
            var window = Rect(rect, "Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            window.sizeDelta = new Vector2(480f, 648f);
            Picture(window, null, BarBaseColor);
            var title = Text(window, "Title", HeadingSize, TextAlignmentOptions.Center);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, HeadingSize * 1.6f);
            titleRect.anchoredPosition = new Vector2(0f, -32f);
            var restart = MainButton(window, "Restart", new Vector2(0.5f, 0f), 400f, 104f);
            restart.Rect.anchoredPosition = new Vector2(0f, 32f + 128f + 56f);
            restart.Button.targetGraphic.color = new Color(0.32f, 0.33f, 0.36f);
            restart.Caption.color = TextColor;
            var rim = restart.Rect.gameObject.AddComponent<Outline>();
            rim.effectColor = TextColor;
            rim.effectDistance = new Vector2(4f, -4f);
            rim.enabled = false;
            var resume = MainButton(window, "Resume", new Vector2(0.5f, 0f), 400f, 128f);
            resume.Rect.anchoredPosition = new Vector2(0f, 32f);
            var leave = MainButton(window, "Leave", new Vector2(0.5f, 0f), 400f, 104f);
            leave.Rect.anchoredPosition = new Vector2(0f, 32f + 128f + 56f + 104f + 24f);
            leave.Button.targetGraphic.color = new Color(0.32f, 0.33f, 0.36f);
            leave.Caption.color = TextColor;
            var view = go.GetComponent<PauseView>();
            Wire(view, ("group", go.GetComponent<CanvasGroup>()), ("title", title), ("resume", resume.Button), ("resumeCaption", resume.Caption),
                ("restart", restart.Button), ("restartCaption", restart.Caption), ("restartRim", rim), ("leave", leave.Button),
                ("leaveCaption", leave.Caption));
            return view;
        }

        /// <summary>
        /// The main menu (screens/main-menu.md): an opaque cover of the whole window, the name of the game above the middle,
        /// «Play» — the main button — and «Editor» under it, dark, for the owner only.
        /// </summary>
        private static MainMenuView BuildMainMenu(RectTransform area)
        {
            var go = new GameObject("MainMenu", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(MainMenuView));
            go.transform.SetParent(area, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            go.GetComponent<Image>().color = BarBaseColor;
            var title = Line(rect, "Title", TitleSize, 0.68f, 0f);
            var play = MainButton(rect, "Play", new Vector2(0.5f, 0.5f), ButtonWidth + 40f, ButtonHeight);
            play.Rect.pivot = new Vector2(0.5f, 0.5f);
            play.Rect.anchoredPosition = new Vector2(0f, -40f);
            var editor = MainButton(rect, "Editor", new Vector2(0.5f, 0.5f), ButtonWidth + 40f, 96f);
            editor.Rect.pivot = new Vector2(0.5f, 0.5f);
            editor.Rect.anchoredPosition = new Vector2(0f, -40f - ButtonHeight / 2f - 32f - 48f);
            editor.Button.targetGraphic.color = new Color(0.32f, 0.33f, 0.36f);
            editor.Caption.color = TextColor;
            var view = go.GetComponent<MainMenuView>();
            Wire(view, ("group", go.GetComponent<CanvasGroup>()), ("title", title), ("play", play.Button), ("playCaption", play.Caption),
                ("editor", editor.Button), ("editorCaption", editor.Caption));
            return view;
        }

        // the map editor: a tool of the owner, dense as a tool (screens/map-editor.md)
        private const float EditorSide = 340f;
        private const float EditorRow = 32f;
        private const int EditorText = 20;
        private const float EditorGap = 6f;
        private static readonly Color EditorPanel = new Color(0.14f, 0.15f, 0.18f, 1f);
        private static readonly Color EditorBack = new Color(0.06f, 0.07f, 0.09f, 1f);

        /// <summary>
        /// The map editor (screens/map-editor.md): a panel of tools on the left in one column, the map in the rest of the window
        /// with its name above and a line of numbers below, and a dialog over everything for «Save?» and «Delete?».
        /// </summary>
        private static MapEditorView BuildMapEditor(RectTransform area)
        {
            var go = new GameObject("MapEditor", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(MapEditorView));
            go.transform.SetParent(area, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            go.GetComponent<Image>().color = EditorBack;

            // the map: the rest of the window
            var viewport = Rect(rect, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(EditorSide + 12f, 48f);
            viewport.offsetMax = new Vector2(-12f, -52f);
            var back = Picture(viewport, null, new Color(0.03f, 0.04f, 0.05f, 1f));
            back.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var input = viewport.gameObject.AddComponent<MapViewportInput>();
            var mapRect = Rect(viewport, "Map", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            mapRect.sizeDelta = Vector2.one * 600f;
            var raw = mapRect.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var (frame, frameLabel) = BuildMapFrame(mapRect);

            var title = Text(rect, "Title", 30, TextAlignmentOptions.Center);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(EditorSide + 12f, -48f);
            titleRect.offsetMax = new Vector2(-12f, -6f);
            var status = Text(rect, "Status", EditorText, TextAlignmentOptions.Center);
            // a long line of numbers shrinks to fit rather than run off the window
            status.enableAutoSizing = true;
            status.fontSizeMin = 12;
            status.fontSizeMax = EditorText;
            var statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.offsetMin = new Vector2(EditorSide + 12f, 8f);
            statusRect.offsetMax = new Vector2(-12f, 42f);
            var message = Text(viewport, "Message", 24, TextAlignmentOptions.Center);
            var messageRect = (RectTransform)message.transform;
            messageRect.anchorMin = new Vector2(0f, 0f);
            messageRect.anchorMax = new Vector2(1f, 0f);
            messageRect.pivot = new Vector2(0.5f, 0f);
            messageRect.offsetMin = new Vector2(0f, 12f);
            messageRect.offsetMax = new Vector2(0f, 48f);
            message.color = new Color(1f, 0.9f, 0.7f, 1f);
            message.text = string.Empty;

            // the panel of tools: one column
            var panel = Rect(rect, "Panel", Vector2.zero, new Vector2(0f, 1f));
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(EditorSide, 0f);
            Picture(panel, null, EditorPanel);
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(14, 14, 12, 12);
            column.spacing = 5f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            Caption(panel, "Header", "Редактор карт", 28, 34f);
            Caption(panel, "MapsLabel", "Карты", EditorText, 22f);
            var list = BuildMapList(panel, out var template);
            var fileRow = EditorRowOf(panel, "FileRow");
            var create = Tool(fileRow, "New", "Новая");
            var copy = Tool(fileRow, "Copy", "Копия");
            var sizeRow = EditorRowOf(panel, "MapSizeRow");
            var widthField = NameField(sizeRow);
            widthField.name = "Width";
            ((TMP_Text)widthField.placeholder).text = "ширина";
            widthField.contentType = TMP_InputField.ContentType.IntegerNumber;
            var by = Caption(sizeRow, "By", "на", EditorText, EditorRow);
            by.alignment = TextAlignmentOptions.Center;
            by.GetComponent<LayoutElement>().preferredWidth = 26f;
            var heightField = NameField(sizeRow);
            heightField.name = "Height";
            ((TMP_Text)heightField.placeholder).text = "высота";
            heightField.contentType = TMP_InputField.ContentType.IntegerNumber;
            var resize = Tool(sizeRow, "Resize", "Задать");
            resize.GetComponent<LayoutElement>().flexibleWidth = 0f;
            resize.GetComponent<LayoutElement>().preferredWidth = 80f;
            var nameRow = EditorRowOf(panel, "NameRow");
            var nameField = NameField(nameRow);
            var rename = Tool(nameRow, "Rename", "Имя");
            rename.GetComponent<LayoutElement>().flexibleWidth = 0f;
            rename.GetComponent<LayoutElement>().preferredWidth = 80f;
            Caption(panel, "LandLabel", "Земля", EditorText, 22f);
            var landRow = EditorRowOf(panel, "LandRow");
            var land = Tool(landRow, "Land", "Суша");
            var water = Tool(landRow, "Water", "Вода");
            var eraser = Tool(landRow, "Eraser", "Ластик");
            var shapeRow = EditorRowOf(panel, "ShapeRow");
            var square = Tool(shapeRow, "Square", "Квадрат");
            var circle = Tool(shapeRow, "Circle", "Круг");
            var brushLabel = Caption(panel, "BrushLabel", "Кисть: 5", EditorText, 22f);
            var slider = BrushSlider(panel);
            Caption(panel, "ObjectsLabel", "Объекты", EditorText, 22f);
            var objectRow = EditorRowOf(panel, "ObjectRow");
            var gold = Tool(objectRow, "Gold", "Золото");
            var wood = Tool(objectRow, "Wood", "Дерево");
            var site = Tool(objectRow, "Site", "Место");
            var baseRow = EditorRowOf(panel, "BaseRow");
            var playerBase = Tool(baseRow, "PlayerBase", "Штаб игрока");
            var enemyBase = Tool(baseRow, "EnemyBase", "Штаб врага");
            var viewRow = EditorRowOf(panel, "ViewRow");
            var mirror = Tool(viewRow, "Mirror", "Зеркало");
            var grid = Tool(viewRow, "Grid", "Сетка");
            var historyRow = EditorRowOf(panel, "HistoryRow");
            var undo = Tool(historyRow, "Undo", "Отменить");
            var redo = Tool(historyRow, "Redo", "Повторить");
            var save = Tool(EditorRowOf(panel, "SaveRow"), "Save", "Сохранить");
            var exitRow = EditorRowOf(panel, "ExitRow");
            var play = Tool(exitRow, "Play", "Играть");
            var exit = Tool(exitRow, "Exit", "В меню");

            // the dialog over everything
            var dialogGo = new GameObject("Dialog", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            dialogGo.transform.SetParent(rect, false);
            var dialogRect = (RectTransform)dialogGo.transform;
            Stretch(dialogRect);
            dialogGo.GetComponent<Image>().color = DimColor;
            var window = Rect(dialogRect, "Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            window.sizeDelta = new Vector2(560f, 220f);
            Picture(window, null, EditorPanel);
            var dialogText = Text(window, "Text", 24, TextAlignmentOptions.Center);
            dialogText.textWrappingMode = TextWrappingModes.Normal;
            var dialogTextRect = (RectTransform)dialogText.transform;
            dialogTextRect.offsetMin = new Vector2(24f, 84f);
            dialogTextRect.offsetMax = new Vector2(-24f, -20f);
            var buttons = Rect(window, "Buttons", Vector2.zero, new Vector2(1f, 0f));
            buttons.pivot = new Vector2(0.5f, 0f);
            buttons.offsetMin = new Vector2(24f, 20f);
            buttons.offsetMax = new Vector2(-24f, 20f + 48f);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 12f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            var yes = Tool(buttons, "Yes", "Да");
            var no = Tool(buttons, "No", "Нет");
            var cancel = Tool(buttons, "Cancel", "Отмена");

            var view = go.GetComponent<MapEditorView>();
            Wire(view, ("group", go.GetComponent<CanvasGroup>()), ("viewport", viewport), ("map", raw), ("input", input),
                ("widthField", widthField), ("heightField", heightField), ("resize", resize), ("frame", frame), ("frameLabel", frameLabel),
                ("listContent", list), ("itemTemplate", template), ("nameField", nameField), ("title", title), ("status", status),
                ("message", message), ("create", create), ("copy", copy), ("rename", rename), ("land", land), ("water", water),
                ("eraser", eraser), ("square", square), ("circle", circle), ("brushSize", slider), ("brushLabel", brushLabel),
                ("gold", gold), ("wood", wood), ("site", site), ("playerBase", playerBase), ("enemyBase", enemyBase),
                ("mirror", mirror), ("grid", grid), ("save", save), ("undo", undo), ("redo", redo), ("play", play), ("exit", exit),
                ("dialog", dialogGo.GetComponent<CanvasGroup>()), ("dialogText", dialogText), ("dialogYes", yes),
                ("dialogYesCaption", yes.GetComponentInChildren<TMP_Text>()), ("dialogNo", no),
                ("dialogNoCaption", no.GetComponentInChildren<TMP_Text>()), ("dialogCancel", cancel));
            return view;
        }

        /// <summary>
        /// The frame of the map in the editor (owner, 5 November): the outline of the new bounds while pulled, with their size above,
        /// and eight handles — the corners and the middles of the edges — pulled by the mouse.
        /// </summary>
        private static (RectTransform Frame, TMP_Text Label) BuildMapFrame(RectTransform mapRect)
        {
            var light = new Color(0.96f, 0.95f, 0.88f, 1f);
            var frame = Rect(mapRect, "Frame", Vector2.zero, Vector2.one);
            foreach (var (name, min, max, size) in new[]
                     {
                         ("West", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f)),
                         ("East", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(3f, 0f)),
                         ("South", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f)),
                         ("North", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 3f)),
                     })
            {
                var bar = Rect(frame, name, min, max);
                bar.sizeDelta = size;
                Picture(bar, null, light);
            }

            var label = Text(frame, "Size", 22, TextAlignmentOptions.Bottom);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, 30f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);
            label.text = string.Empty;

            for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                    continue;

                var anchor = new Vector2((x + 1) / 2f, (y + 1) / 2f);
                var handle = Rect(mapRect, $"Handle{x + 1}{y + 1}", anchor, anchor);
                handle.sizeDelta = Vector2.one * (x != 0 && y != 0 ? 20f : 16f);
                var image = Picture(handle, null, light);
                image.raycastTarget = true;
                var outline = handle.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.06f, 0.07f, 0.09f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
                handle.gameObject.AddComponent<MapResizeHandle>().SetEdgeForEditor(new Vector2Int(x, y));
            }

            return (frame, label);
        }

        /// <summary>The list of maps: a scrolled column of buttons made from a template, a map per button.</summary>
        private static RectTransform BuildMapList(RectTransform panel, out Button template)
        {
            var scroll = Rect(panel, "Maps", Vector2.zero, Vector2.one);
            scroll.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;
            Picture(scroll, null, EditorBack).raycastTarget = true;
            var viewport = Rect(scroll, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(4f, 4f);
            viewport.offsetMax = new Vector2(-4f, -4f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 4f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var view = scroll.gameObject.AddComponent<ScrollRect>();
            view.viewport = viewport;
            view.content = content;
            view.horizontal = false;
            view.movementType = ScrollRect.MovementType.Clamped;
            view.scrollSensitivity = 24f;
            template = Tool(content, "Item", "Карта");
            var label = template.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            ((RectTransform)label.transform).offsetMin = new Vector2(10f, 0f);
            ((RectTransform)label.transform).offsetMax = new Vector2(-40f, 0f);
            // the cross that deletes this map, at the right end of its row
            var delete = Tool((RectTransform)template.transform, "Delete", "X");
            Destroy(delete.GetComponent<LayoutElement>());
            var deleteRect = (RectTransform)delete.transform;
            deleteRect.anchorMin = new Vector2(1f, 0f);
            deleteRect.anchorMax = new Vector2(1f, 1f);
            deleteRect.pivot = new Vector2(1f, 0.5f);
            deleteRect.offsetMin = new Vector2(-34f, 3f);
            deleteRect.offsetMax = new Vector2(-3f, -3f);
            delete.targetGraphic.color = new Color(0.32f, 0.33f, 0.36f);
            delete.GetComponentInChildren<TMP_Text>().color = TextColor;
            return content;
        }

        private static void Destroy(Object target) => Object.DestroyImmediate(target);

        /// <summary>The slider of the size of the brush, 1–15 cells: a dark groove, a light fill and a light knob.</summary>
        private static Slider BrushSlider(RectTransform panel)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = "BrushSize";
            go.transform.SetParent(panel, false);
            go.AddComponent<LayoutElement>().preferredHeight = 24f;
            foreach (var image in go.GetComponentsInChildren<Image>(true))
            {
                image.sprite = null;
                image.color = image.name == "Background" ? EditorBack : ButtonColor;
            }

            var slider = go.GetComponent<Slider>();
            slider.handleRect.sizeDelta = new Vector2(18f, 0f);
            return slider;
        }

        private static RectTransform EditorRowOf(RectTransform parent, string name)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.one);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = EditorRow;
            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = EditorGap;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            return rect;
        }

        /// <summary>A light button of the editor with a dark caption, as wide as its row lets it.</summary>
        private static Button Tool(RectTransform parent, string name, string caption)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.one);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = EditorRow;
            element.flexibleWidth = 1f;
            var image = Picture(rect, null, ButtonColor);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Text(rect, "Label", EditorText, TextAlignmentOptions.Center);
            label.color = ButtonTextColor;
            label.text = caption;
            return button;
        }

        private static TextMeshProUGUI Caption(RectTransform parent, string name, string text, int size, float height)
        {
            var label = Text(parent, name, size, TextAlignmentOptions.MidlineLeft);
            label.text = text;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        /// <summary>The field of the name of the open map: «Имя» renames its file to what is typed.</summary>
        private static TMP_InputField NameField(RectTransform row)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "Name";
            go.transform.SetParent(row, false);
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = EditorRow;
            element.flexibleWidth = 3f;
            var field = go.GetComponent<TMP_InputField>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectSetup.FontAsset);
            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSize = EditorText;
                text.alignment = TextAlignmentOptions.MidlineLeft;
            }

            field.textComponent.color = ButtonTextColor;
            ((TMP_Text)field.placeholder).text = "имя карты";
            field.lineType = TMP_InputField.LineType.SingleLine;
            return field;
        }

        /// <summary>The pause button of the HUD, at the right of the row of counters: two bars on a dark square.</summary>
        /// <summary>
        /// The hint of level 1 (screens/first-hint.md): a hand low in the middle (a finger sliding) with one word, the WASD keys
        /// for a computer, and an arrow (a shaft and a head) the view puts by the hero or at the edge. Light with a dark rim.
        /// </summary>
        private static HintView BuildHint(RectTransform root, RectTransform hudBottom)
        {
            var go = new GameObject("Hint", typeof(RectTransform), typeof(HintView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var hint = (RectTransform)go.transform;
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectSetup.CirclePath);

            var hand = Rect(hint, "Hand", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            hand.sizeDelta = new Vector2(320f, 160f);
            hand.anchoredPosition = new Vector2(0f, 200f + 80f);
            var track = Rect(hand, "Track", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            track.sizeDelta = new Vector2(240f, 8f);
            track.anchoredPosition = new Vector2(0f, 20f);
            Picture(track, null, new Color(0.96f, 0.95f, 0.88f, 0.5f));
            var finger = Rect(hand, "Finger", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            finger.sizeDelta = Vector2.one * 64f;
            finger.anchoredPosition = new Vector2(0f, 20f);
            var fingerRim = Picture(finger, circle, BarBaseColor);
            var fingerFill = Rect(finger, "Fill", Vector2.zero, Vector2.one);
            fingerFill.offsetMin = new Vector2(4f, 4f);
            fingerFill.offsetMax = new Vector2(-4f, -4f);
            Picture(fingerFill, circle, TextColor);
            var word = Text(hand, "Word", BodySize, TextAlignmentOptions.Center);
            var wordRect = (RectTransform)word.transform;
            wordRect.anchorMin = new Vector2(0f, 0f);
            wordRect.anchorMax = new Vector2(1f, 0f);
            wordRect.pivot = new Vector2(0.5f, 0f);
            wordRect.sizeDelta = new Vector2(0f, BodySize * 1.4f);
            word.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);

            var keys = Rect(hint, "Keys", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            keys.sizeDelta = new Vector2(220f, 150f);
            keys.anchoredPosition = new Vector2(0f, 200f + 75f);
            foreach (var (letter, x, y) in new[] { ("W", 0f, 38f), ("A", -72f, -34f), ("S", 0f, -34f), ("D", 72f, -34f) })
            {
                var key = Rect(keys, letter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                key.sizeDelta = Vector2.one * 64f;
                key.anchoredPosition = new Vector2(x, y);
                Picture(key, null, BarBaseColor);
                var face = Rect(key, "Face", Vector2.zero, Vector2.one);
                face.offsetMin = new Vector2(4f, 4f);
                face.offsetMax = new Vector2(-4f, -4f);
                Picture(face, null, TextColor);
                var label = Text(key, "Letter", BodySize, TextAlignmentOptions.Center);
                label.text = letter;
                label.color = ButtonTextColor;
            }

            var arrow = Rect(hint, "Arrow", Vector2.zero, Vector2.zero);
            arrow.sizeDelta = new Vector2(64f, 96f);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            var shaft = Rect(arrow, "Shaft", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            shaft.pivot = new Vector2(0.5f, 0f);
            shaft.sizeDelta = new Vector2(22f, 56f);
            Picture(shaft, null, BarBaseColor);
            var shaftFill = Rect(shaft, "Fill", Vector2.zero, Vector2.one);
            shaftFill.offsetMin = new Vector2(4f, 4f);
            shaftFill.offsetMax = new Vector2(-4f, 0f);
            Picture(shaftFill, null, TextColor);
            var head = Rect(arrow, "Head", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            head.sizeDelta = Vector2.one * 46f;
            head.anchoredPosition = new Vector2(0f, -36f);
            head.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Picture(head, null, BarBaseColor);
            var headFill = Rect(head, "Fill", Vector2.zero, Vector2.one);
            headFill.offsetMin = new Vector2(5f, 5f);
            headFill.offsetMax = new Vector2(-5f, -5f);
            Picture(headFill, null, TextColor);

            Wire(go.GetComponent<HintView>(), ("hand", hand), ("finger", finger), ("handWord", word), ("keys", keys), ("arrow", arrow),
                ("hudBottom", hudBottom));
            return go.GetComponent<HintView>();
        }

        /// <summary>The «−1» over a hit bar (task 4.22): a bold label with a dark rim, copied per hit.</summary>
        private static HitNumbersView BuildHitNumbers(RectTransform root)
        {
            var go = new GameObject("HitNumbers", typeof(RectTransform), typeof(HitNumbersView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var label = Text((RectTransform)go.transform, "Template", 24, TextAlignmentOptions.Center);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(80f, 32f);
            label.fontStyle = FontStyles.Bold;
            label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);
            var view = go.GetComponent<HitNumbersView>();
            Wire(view, ("template", label));
            return view;
        }

        private static CapturePopupView BuildPopups(RectTransform root)
        {
            var go = new GameObject("CapturePopups", typeof(RectTransform), typeof(CapturePopupView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var template = Text((RectTransform)go.transform, "Template", PopupSize, TextAlignmentOptions.Center);
            template.fontStyle = FontStyles.Bold;
            template.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectSetup.OutlineFontMaterial);
            var rect = (RectTransform)template.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, PopupSize * 1.5f);
            Wire(go.GetComponent<CapturePopupView>(), ("template", template));
            return go.GetComponent<CapturePopupView>();
        }

        private static RectTransform Rect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image Picture(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(RectTransform parent, string name, int size, TextAlignmentOptions alignment)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.one);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectSetup.FontAsset);
            label.fontSize = size;
            label.color = TextColor;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void Localize(TMP_Text label, string key)
        {
            var localized = label.gameObject.AddComponent<LocalizedLabel>();
            var serialized = new SerializedObject(localized);
            serialized.FindProperty("key").stringValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Wire(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in fields)
                serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
