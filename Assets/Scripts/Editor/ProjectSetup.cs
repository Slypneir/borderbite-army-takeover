using System.IO;
using Core.AudioManagement.Configuration;
using Core.AudioManagement.Enums;
using Core.InputManagement.Configuration;
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
        public const string BattleViewConfigPath = "Assets/ScriptableObjects/Battle/BattleViewConfig.asset";
        public const string ScreenConfigPath = "Assets/ScriptableObjects/Screen/ScreenLayoutConfig.asset";
        public const string MoveStickConfigPath = "Assets/ScriptableObjects/Input/MoveStickConfig.asset";
        public const string SoundVolumesPath = "Assets/ScriptableObjects/Audio/SoundVolumes.asset";
        public const string CirclePath = "Assets/Sprites/UI/Placeholder/Circle.png";
        public const string IconFolder = "Assets/Sprites/UI/Icons/";
        public const string OutlineFontMaterial = "Assets/Fonts/PTSans-Regular Outline.mat";

        private const string CellsMaterialPath = "Assets/Materials/Battle/Cells.mat";
        public const string FlatMaterialPath = "Assets/Materials/Placeholder/FlatShaded.mat";
        private const string AudioFolder = "Assets/Audio/";
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
            OutlineFont();
            Icons();
            ModelLibrary.Build();
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
            Create<MoveStickConfig>(MoveStickConfigPath);
            BattleView();
            Sounds();
            Circle();
            AssetDatabase.SaveAssets();
            // fields a config no longer has leave the files; the values the owner set stay
            AssetDatabase.ForceReserializeAssets(new[] { BattleConfigPath, BattleViewConfigPath, ScreenConfigPath, MoveStickConfigPath, SoundVolumesPath });
        }

        private static void BattleView()
        {
            var cells = MaterialAsset(CellsMaterialPath, "Borderbite/Map/Cells");
            var flat = MaterialAsset(FlatMaterialPath, "Borderbite/World/Flat Lit");
            Create<BattleViewConfig>(BattleViewConfigPath);
            var config = AssetDatabase.LoadAssetAtPath<BattleViewConfig>(BattleViewConfigPath);
            if (config.CellsMaterial != null && config.FlatMaterial != null)
                return;

            config.InitForEditor(config.CellsMaterial != null ? config.CellsMaterial : cells, config.FlatMaterial != null ? config.FlatMaterial : flat);
            EditorUtility.SetDirty(config);
        }

        private static Material MaterialAsset(string path, string shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Folder(path);
            material = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>The table of sounds gets a row for every sound that has none; the percents the owner set stay.</summary>
        private static void Sounds()
        {
            Create<SoundVolumes>(SoundVolumesPath);
            var volumes = AssetDatabase.LoadAssetAtPath<SoundVolumes>(SoundVolumesPath);
            var changed = false;
            // a sound lives in Assets/Audio/<Sound>/<sound>.wav next to its sources.txt
            foreach (Sfx sound in System.Enum.GetValues(typeof(Sfx)))
            {
                var path = AudioFolder + sound + "/" + sound.ToString().ToLowerInvariant() + ".wav";
                changed |= volumes.EnsureRow(sound, AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            if (changed)
                EditorUtility.SetDirty(volumes);
        }

        /// <summary>A soft white circle for the placeholder of the stick, until the kit of the interface.</summary>
        private static void Circle()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath) != null)
                return;

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - size / 2f;
                var dy = y + 0.5f - size / 2f;
                var alpha = Mathf.Clamp01(size / 2f - 1f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            Folder(CirclePath);
            File.WriteAllBytes(CirclePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CirclePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void Folder(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            Folder(folder);
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), Path.GetFileName(folder));
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
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return;

            Folder(path);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        }

        private static bool ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                return false;

            AssetDatabase.ImportPackage(TmpEssentials, false);
            return true;
        }

        /// <summary>The icons of the 3D artist for the HUD (gold, wood, army): sprites, point filter, no compression.</summary>
        private static void Icons()
        {
            foreach (var name in new[] { "gold", "wood", "army" })
            {
                var path = IconFolder + name + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.textureType == TextureImporterType.Sprite)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>The font with a dark rim, for labels over the map: they must read on blue soldiers and any land.</summary>
        private static void OutlineFont()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(OutlineFontMaterial) != null)
                return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);
            var material = new Material(font.material) { name = "PTSans-Regular Outline" };
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", 0.25f);
            material.SetColor("_OutlineColor", new Color(0.08f, 0.08f, 0.12f, 1f));
            AssetDatabase.CreateAsset(material, OutlineFontMaterial);
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
