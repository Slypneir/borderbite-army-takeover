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
using Meta.LocalizationSystem.Views;
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
        private const int ButtonHeight = 128;
        private const int ButtonWidth = 360;
        private const int ButtonTextSize = 44;
        private const int PopupSize = 52;
        private const int DebugSize = 24;
        private static readonly Color TextColor = new Color(0.96f, 0.96f, 0.92f);
        private static readonly Color BarBackColor = new Color(0.32f, 0.33f, 0.31f, 0.9f);
        private static readonly Color DimColor = new Color(0.05f, 0.06f, 0.09f, 0.72f);
        private static readonly Color ButtonColor = new Color(0.98f, 0.8f, 0.25f);
        private static readonly Color ButtonTextColor = new Color(0.12f, 0.1f, 0.06f);

        [MenuItem("Tools/Setup/Build Main Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var view = AssetDatabase.LoadAssetAtPath<BattleViewConfig>(ProjectSetup.BattleViewConfigPath);

            var camera = BuildCamera(view);
            var world = BuildWorld();
            var canvas = BuildCanvas(view, out var results);
            BuildRoot(camera, world, results, canvas);
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
            var body = Mesh(hero, "Body", "Capsule.fbx");
            var nose = Mesh(hero, "Nose", "Cube.fbx");

            var serialized = new SerializedObject(world);
            serialized.FindProperty("surface").objectReferenceValue = surface;
            serialized.FindProperty("island").objectReferenceValue = island;
            serialized.FindProperty("water").objectReferenceValue = water;
            serialized.FindProperty("hero").objectReferenceValue = hero;
            serialized.FindProperty("heroBody").objectReferenceValue = body;
            serialized.FindProperty("heroNose").objectReferenceValue = nose;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return world;
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

        private static void BuildRoot(Camera camera, BattleWorld world, ResultsView results, GameObject canvas)
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
            var inject = serialized.FindProperty("autoInjectGameObjects");
            inject.arraySize = 1;
            inject.GetArrayElementAtIndex(0).objectReferenceValue = canvas;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
        }

        private static GameObject BuildCanvas(BattleViewConfig view, out ResultsView results)
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

            BuildPopups(root);
            BuildStick(root);
            var area = Rect(root, "SafeArea", Vector2.zero, Vector2.one);
            area.gameObject.AddComponent<SafeAreaFitter>();
            BuildBar(area, view);
            BuildDebug(area);
            results = BuildResults(area);
            return go;
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

        private static void BuildBar(RectTransform area, BattleViewConfig view)
        {
            var bar = Rect(area, "TerritoryBar", new Vector2(0f, 1f), new Vector2(1f, 1f));
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(EdgeMargin, -EdgeMargin - BarHeight);
            bar.offsetMax = new Vector2(-EdgeMargin, -EdgeMargin);
            Picture(bar, null, BarBackColor);

            var playerFill = Rect(bar, "PlayerFill", Vector2.zero, new Vector2(0.1f, 1f));
            Picture(playerFill, null, view.PlayerColor);
            var enemyFill = Rect(bar, "EnemyFill", new Vector2(0.9f, 0f), Vector2.one);
            Picture(enemyFill, null, view.EnemyColor);
            var playerMark = Mark(bar, "PlayerMark");
            var enemyMark = Mark(bar, "EnemyMark");
            var playerLabel = Text(bar, "PlayerPercent", BarTextSize, TextAlignmentOptions.Left);
            ((RectTransform)playerLabel.transform).offsetMin = new Vector2(EdgeMargin / 2f, 0f);
            var enemyLabel = Text(bar, "EnemyPercent", BarTextSize, TextAlignmentOptions.Right);
            ((RectTransform)enemyLabel.transform).offsetMax = new Vector2(-EdgeMargin / 2f, 0f);

            Wire(bar.gameObject.AddComponent<TerritoryBarView>(), ("playerFill", playerFill), ("enemyFill", enemyFill),
                ("playerMark", playerMark), ("enemyMark", enemyMark), ("playerLabel", playerLabel), ("enemyLabel", enemyLabel));
        }

        private static RectTransform Mark(RectTransform bar, string name)
        {
            var mark = Rect(bar, name, new Vector2(0.7f, 0.5f), new Vector2(0.7f, 0.5f));
            mark.sizeDelta = new Vector2(MarkWidth, BarHeight + 16);
            Picture(mark, null, TextColor);
            return mark;
        }

        private static void BuildDebug(RectTransform area)
        {
            var go = new GameObject("BattleDebug", typeof(RectTransform), typeof(BattleDebugView));
            go.transform.SetParent(area, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(EdgeMargin, -EdgeMargin * 2 - BarHeight - DebugSize * 1.5f);
            rect.offsetMax = new Vector2(-EdgeMargin, -EdgeMargin * 2 - BarHeight);
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
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 0.5f);
            titleRect.sizeDelta = new Vector2(0f, TitleSize * 1.5f);
            titleRect.anchoredPosition = new Vector2(0f, TitleSize);

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
            Localize(caption, "results.retry");

            var view = go.GetComponent<ResultsView>();
            Wire(view, ("group", go.GetComponent<CanvasGroup>()), ("title", title), ("retry", retry), ("retryRect", button));
            return view;
        }

        private static void BuildPopups(RectTransform root)
        {
            var go = new GameObject("CapturePopups", typeof(RectTransform), typeof(CapturePopupView));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var template = Text((RectTransform)go.transform, "Template", PopupSize, TextAlignmentOptions.Center);
            template.fontStyle = FontStyles.Bold;
            template.outlineWidth = 0.2f;
            template.outlineColor = new Color32(20, 20, 30, 255);
            var rect = (RectTransform)template.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, PopupSize * 1.5f);
            Wire(go.GetComponent<CapturePopupView>(), ("template", template));
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
