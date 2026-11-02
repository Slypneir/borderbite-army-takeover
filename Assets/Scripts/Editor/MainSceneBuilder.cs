using Core.DependencyInjection;
using Core.PlatformSystem.Configuration;
using Core.ScreenSystem.Configuration;
using Core.ScreenSystem.Views;
using Meta.BattleSystem.Configuration;
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
    /// The one scene of the game, always from this code: Tools/Setup/Build Main Scene. The camera, the map placeholder, the root
    /// installer and a Canvas whose texts are tied to the edges through the safe area.
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PlaceholderMaterial = "Assets/Materials/Placeholder/MapPlaceholder.mat";

        // start tokens of the boot screen; the real theme comes with the interface repository
        private const int CanvasReference = 720;
        private const int TitleSize = 56;
        private const int StatusSize = 32;
        private const int EdgeMargin = 24;
        private static readonly Color BackgroundColor = new Color(0.10f, 0.12f, 0.16f);
        private static readonly Color MapColor = new Color(0.22f, 0.45f, 0.36f);
        private static readonly Color TextColor = new Color(0.96f, 0.96f, 0.92f);

        [MenuItem("Tools/Setup/Build Main Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = BuildCamera();
            BuildMapPlaceholder();
            BuildRoot(camera);
            BuildCanvas();
            BuildEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[MainSceneBuilder] built");
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("MainCamera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
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

        private static void BuildMapPlaceholder()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ProjectSetup.ScreenConfigPath);
            var go = new GameObject("MapPlaceholder");
            go.transform.localScale = new Vector3(config.MapWidth, config.MapHeight, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PlaceholderMaterialAsset();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material PlaceholderMaterialAsset()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderMaterial);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = MapColor };
            material.SetColor("_BaseColor", MapColor);
            AssetDatabase.CreateAsset(material, PlaceholderMaterial);
            return material;
        }

        private static void BuildRoot(Camera camera)
        {
            var go = new GameObject("RootInstaller");
            go.SetActive(false);
            var installer = go.AddComponent<RootInstaller>();
            var serialized = new SerializedObject(installer);
            serialized.FindProperty("adPolicyConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AdPolicyConfig>(ProjectSetup.EditorPolicy);
            serialized.FindProperty("battleConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BattleConfig>(ProjectSetup.BattleConfigPath);
            serialized.FindProperty("screenLayoutConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ProjectSetup.ScreenConfigPath);
            serialized.FindProperty("mainCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
        }

        private static void BuildCanvas()
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

            var safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeArea.transform.SetParent(go.transform, false);
            var area = (RectTransform)safeArea.transform;
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            Label(area, "Title", "boot.title", TitleSize, new Vector2(0.5f, 1f), new Vector2(0f, -EdgeMargin), TextAlignmentOptions.Top);
            Label(area, "Status", "boot.status", StatusSize, new Vector2(0.5f, 0f), new Vector2(0f, EdgeMargin), TextAlignmentOptions.Bottom);
        }

        private static void Label(RectTransform parent, string name, string key, int size, Vector2 edge, Vector2 offset, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, edge.y);
            rect.anchorMax = new Vector2(1f, edge.y);
            rect.pivot = edge;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(0f, size * 1.5f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectSetup.FontAsset);
            label.fontSize = size;
            label.color = TextColor;
            label.alignment = alignment;
            label.raycastTarget = false;

            var localized = go.AddComponent<LocalizedLabel>();
            var serialized = new SerializedObject(localized);
            serialized.FindProperty("key").stringValue = key;
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
