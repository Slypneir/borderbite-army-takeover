using System.IO;
using Core.PlatformSystem.Configuration;
using Core.ScreenSystem.Configuration;
using Meta.BattleSystem.Configuration;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Core.EditorTools
{
    /// <summary>
    /// Everything a clean checkout needs, in order: Tools/Build Everything. An existing config asset is left alone: the owner tunes it.
    /// </summary>
    public static class ProjectSetup
    {
        public const string PlatformConfigs = "Assets/ScriptableObjects/Platform/";
        public const string EditorPolicy = PlatformConfigs + "AdPolicy_Editor.asset";
        public const string BattleConfigPath = "Assets/ScriptableObjects/Battle/BattleConfig.asset";
        public const string ScreenConfigPath = "Assets/ScriptableObjects/Screen/ScreenLayoutConfig.asset";
        public const string FontAsset = "Assets/Fonts/PTSans-Regular SDF.asset";

        private const string FontSource = "Assets/Fonts/PTSans-Regular.ttf";
        private const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string TmpResources = "Assets/TextMesh Pro/Resources/";
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "«»—–…№ЁАБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдежзийклмнопрстуфхцчшщъыьэюяё";

        [MenuItem("Tools/Build Everything")]
        public static void BuildEverything()
        {
            WebSettings.Apply();
            RenderPipelineSetup.Apply();
            GenerateConfigs();
            PlatformBuild.SelectJslib(null);
            if (ImportTmpEssentials())
            {
                Debug.Log("[ProjectSetup] TMP Essentials imported: run Build Everything once more");
                return;
            }

            BuildFont();
            MainSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] everything built");
        }

        [MenuItem("Tools/Setup/Generate Configs")]
        public static void GenerateConfigs()
        {
            // the rules of each platform: Docs/PLATFORM_REQUIREMENTS.md
            Policy("AdPolicy_YouTube", true, true, false, 180f, false);
            Policy("AdPolicy_CrazyGames", true, true, false, 0f, true);
            Policy("AdPolicy_Yandex", false, true, true, 0f, false);
            Policy("AdPolicy_Editor", true, true, false, 0f, false);
            Create<BattleConfig>(BattleConfigPath);
            Create<ScreenLayoutConfig>(ScreenConfigPath);
            AssetDatabase.SaveAssets();
        }

        private static void Policy(string name, bool transition, bool nextOrRetry, bool navigation, float minSeconds, bool rewardedExcludes)
        {
            var path = PlatformConfigs + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<AdPolicyConfig>(path) != null)
                return;

            var policy = ScriptableObject.CreateInstance<AdPolicyConfig>();
            policy.InitForEditor(transition, nextOrRetry, navigation, minSeconds, rewardedExcludes);
            AssetDatabase.CreateAsset(policy, path);
        }

        private static void Create<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        private static bool ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                return false;

            AssetDatabase.ImportPackage(TmpEssentials, false);
            return true;
        }

        /// <summary>A static atlas of Latin and Cyrillic: the source font stays out of the build, the default fonts of TMP are removed.</summary>
        private static void BuildFont()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);
            if (fontAsset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
                fontAsset = TMP_FontAsset.CreateFontAsset(font, 40, 5, GlyphRenderMode.SDFAA, 1024, 512, AtlasPopulationMode.Dynamic);
                fontAsset.TryAddCharacters(Characters, out var missing);
                if (!string.IsNullOrEmpty(missing))
                    Debug.LogWarning($"[ProjectSetup] not in the atlas: {missing}");

                fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(fontAsset, FontAsset);
                fontAsset.atlasTexture.name = "PTSans-Regular Atlas";
                fontAsset.material.name = "PTSans-Regular Material";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
            }

            var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpResources + "TMP Settings.asset"));
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
            settings.FindProperty("m_fallbackFontAssets").ClearArray();
            settings.FindProperty("m_defaultSpriteAsset").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.DeleteAsset(TmpResources + "Fonts & Materials");
            AssetDatabase.DeleteAsset(TmpResources + "Sprite Assets");
            AssetDatabase.DeleteAsset("Assets/TextMesh Pro/Fonts");
            AssetDatabase.DeleteAsset("Assets/TextMesh Pro/Sprites");
        }
    }
}
