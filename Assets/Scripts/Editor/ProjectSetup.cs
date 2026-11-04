using System.Collections.Generic;
using System.IO;
using Core.AudioManagement.Configuration;
using Core.AudioManagement.Enums;
using Core.InputManagement.Configuration;
using Core.PlatformSystem.Configuration;
using Core.ScreenSystem.Configuration;
using Meta.BattleSystem.Configuration;
using Meta.ProgressSystem.Configuration;
using Newtonsoft.Json.Linq;
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
        public const string RingPath = "Assets/Sprites/UI/Placeholder/Ring.png";
        public const string IconFolder = "Assets/Sprites/UI/Icons/";
        public const string OutlineFontMaterial = "Assets/Fonts/PTSans-Regular Outline.mat";

        private const string CellsMaterialPath = "Assets/Materials/Battle/Cells.mat";
        public const string ProgressConfigPath = "Assets/ScriptableObjects/Progress/ProgressConfig.asset";
        public const string SurfaceConfigPath = "Assets/ScriptableObjects/Battle/SurfaceConfig.asset";
        private const string SurfaceFolder = "Assets/Textures/Surface/";
        private const string WaterFolder = "Assets/Textures/Water/";
        private const string WaterMaterialPath = "Assets/Materials/Battle/Water.mat";
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
            Create<ProgressConfig>(ProgressConfigPath);
            Create<ScreenLayoutConfig>(ScreenConfigPath);
            Create<MoveStickConfig>(MoveStickConfigPath);
            BattleView();
            Surface();
            Water();
            Sounds();
            Ring();
            Circle();
            AssetDatabase.SaveAssets();
            // fields a config no longer has leave the files; the values the owner set stay
            AssetDatabase.ForceReserializeAssets(new[] { BattleConfigPath, BattleViewConfigPath, SurfaceConfigPath, ProgressConfigPath, ScreenConfigPath, MoveStickConfigPath, SoundVolumesPath });
        }

        private static void BattleView()
        {
            var cells = MaterialAsset(CellsMaterialPath, "Borderbite/Map/Cells");
            var flat = MaterialAsset(FlatMaterialPath, "Borderbite/World/Flat Lit");
            Create<BattleViewConfig>(BattleViewConfigPath);
            var config = AssetDatabase.LoadAssetAtPath<BattleViewConfig>(BattleViewConfigPath);
            var outlined = Shader.Find("Borderbite/World/Flat Lit Outlined");
            if (config.OutlinedShader != outlined)
            {
                config.SetOutlinedShaderForEditor(outlined);
                EditorUtility.SetDirty(config);
            }

            var bars = Shader.Find("Borderbite/World/Bars");
            if (config.BarsShader != bars)
            {
                config.SetBarsShaderForEditor(bars);
                EditorUtility.SetDirty(config);
            }

            if (config.CellsMaterial != null && config.FlatMaterial != null)
                return;

            config.InitForEditor(config.CellsMaterial != null ? config.CellsMaterial : cells, config.FlatMaterial != null ? config.FlatMaterial : flat);
            EditorUtility.SetDirty(config);
        }

        /// <summary>
        /// The grass of the 3D artist (Assets/Textures/Surface): textures readable with the point filter, and the stamps and kinds
        /// of the decor from grass_decor.json into SurfaceConfig. The numbers of the paint are the config's own and stay.
        /// </summary>
        private static void Surface()
        {
            var grass = SurfaceTexture("grass_base.png", TextureWrapMode.Repeat);
            var decor = SurfaceTexture("grass_decor.png", TextureWrapMode.Clamp);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(SurfaceFolder + "grass_decor.json");
            if (grass == null || decor == null || json == null)
                return;

            var data = JObject.Parse(json.text);
            var kinds = new List<SurfaceConfig.Kind>();
            var kindIndex = new Dictionary<string, int>();
            foreach (var pair in (JObject)data["kinds"])
            {
                var kind = (JObject)pair.Value;
                kindIndex[pair.Key] = kinds.Count;
                kinds.Add(new SurfaceConfig.Kind
                {
                    Id = pair.Key,
                    Density = (float)kind["density"],
                    Spacing = (float)kind["spacing"],
                    GroupMin = (int)kind["group"][0],
                    GroupMax = (int)kind["group"][1],
                    Radius = (float)kind["radius"],
                });
            }

            var stamps = new List<SurfaceConfig.Stamp>();
            foreach (var pair in (JObject)data["stamps"])
            {
                var stamp = (JObject)pair.Value;
                var w = (int)stamp["w"];
                var h = (int)stamp["h"];
                // the json counts from the top left of the PNG, Unity keeps rows from the bottom
                stamps.Add(new SurfaceConfig.Stamp
                {
                    Id = pair.Key,
                    Rect = new RectInt((int)stamp["x"], decor.height - (int)stamp["y"] - h, w, h),
                    Kind = kindIndex.TryGetValue((string)stamp["kind"], out var index) ? index : -1,
                });
            }

            Create<SurfaceConfig>(SurfaceConfigPath);
            var config = AssetDatabase.LoadAssetAtPath<SurfaceConfig>(SurfaceConfigPath);
            config.InitForEditor(grass, decor, (int?)data["pxPerMetre"] ?? 8, stamps.ToArray(), kinds.ToArray());
            EditorUtility.SetDirty(config);

            var view = AssetDatabase.LoadAssetAtPath<BattleViewConfig>(BattleViewConfigPath);
            if (view.Surface != config)
            {
                view.SetSurfaceForEditor(config);
                EditorUtility.SetDirty(view);
            }
        }

        /// <summary>
        /// The sea of the 3D artist: water_foam.png as linear data (point, repeat), and the material Water.mat with every number
        /// of water.json. The json is the source: to tune the sea, change it (or the material, then the json too).
        /// </summary>
        private static void Water()
        {
            var foamPath = WaterFolder + "water_foam.png";
            if (AssetImporter.GetAtPath(foamPath) is TextureImporter importer && (importer.sRGBTexture || importer.filterMode != FilterMode.Point
                || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Repeat || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = false;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(WaterFolder + "water.json");
            var foam = AssetDatabase.LoadAssetAtPath<Texture2D>(foamPath);
            if (json == null || foam == null)
                return;

            var material = MaterialAsset(WaterMaterialPath, "Borderbite/World/Water");
            material.shader = Shader.Find("Borderbite/World/Water");
            var data = JObject.Parse(json.text);
            material.SetTexture("_FoamTex", foam);
            foreach (var (property, key) in new[]
                     {
                         ("_Shallow", "shallow"), ("_Deep", "deep"), ("_Abyss", "abyss"), ("_Crest", "crest"), ("_Trough", "trough"),
                         ("_Foam", "foam"), ("_FoamRim", "foamRim"),
                     })
            {
                if (ColorUtility.TryParseHtmlString((string)data[key], out var color))
                    material.SetColor(property, color);
            }

            material.SetFloat("_PixelsPerMeter", (float?)data["pxPerM"] ?? 8f);
            foreach (var (property, key) in new[]
                     {
                         ("_AbyssFrom", "abyssFrom"), ("_AbyssTo", "abyssTo"), ("_AbyssWarp", "abyssWarp"), ("_PatchTint", "patchTint"),
                         ("_ShoreLip", "shoreLip"), ("_Slope", "slope"), ("_RockSlope", "rockSlope"), ("_FullDepth", "fullDepth"),
                         ("_AlphaEdge", "alphaEdge"),
                     })
                material.SetFloat(property, (float)data[key]);

            var waves = data["waves"];
            var trains = (JArray)waves["trains"];
            var weights = Vector4.zero;
            var phases = Vector4.zero;
            for (var t = 0; t < 3 && t < trains.Count; t++)
            {
                var train = trains[t];
                material.SetVector("_Train" + t, new Vector4((float)train["dir"][0], (float)train["dir"][1], (float)train["length"], (float)train["period"]));
                weights[t] = (float)train["weight"];
                phases[t] = (float)train["phase"];
            }

            material.SetVector("_TrainWeight", weights);
            material.SetVector("_TrainPhase", phases);
            material.SetVector("_Amp", new Vector4((float)waves["amp"]["min"], (float)waves["amp"]["max"], (float)waves["amp"]["patch"], 0f));
            material.SetVector("_Calm", new Vector4((float)waves["calm"]["from"], (float)waves["calm"]["to"], 0f, 0f));
            material.SetVector("_Tint", new Vector4((float)waves["crestTint"], (float)waves["troughTint"], 0f, 0f));
            var caps = data["caps"];
            material.SetVector("_Caps", new Vector4((float)caps["tile"], (float)caps["period"], (float)caps["life"], (float)caps["strength"]));
            material.SetFloat("_CapsHeight", (float?)caps["minHeight"] ?? 0.1f);
            var shore = data["shoreFoam"];
            material.SetVector("_ShoreFoam", new Vector4((float)shore["min"], (float)shore["max"], (float)shore["period"], (float)shore["lace"]));
            material.SetFloat("_ShoreFoamAlpha", (float)shore["alpha"]);
            material.SetFloat("_Waves", 1f);
            var rocks = data["rockRadius"];
            material.SetVector("_RockRadii", new Vector4((float)rocks["rock_small"], (float)rocks["rock_large"], (float)rocks["rock_tall"], 0f));
            EditorUtility.SetDirty(material);

            var view = AssetDatabase.LoadAssetAtPath<BattleViewConfig>(BattleViewConfigPath);
            if (view.WaterMaterial != material)
            {
                view.SetWaterForEditor(material);
                EditorUtility.SetDirty(view);
            }
        }

        private static Texture2D SurfaceTexture(string file, TextureWrapMode wrap)
        {
            var path = SurfaceFolder + file;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return null;

            if (importer.textureType != TextureImporterType.Default || importer.filterMode != FilterMode.Point || !importer.isReadable
                || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != wrap || importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.filterMode = FilterMode.Point;
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = wrap;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

        /// <summary>A white ring 128 px with a line of 10 px: drawn at 64 units it is 5 thick (the landing of a flight, task 4.25).</summary>
        private static void Ring()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(RingPath) != null)
                return;

            const int size = 128;
            const float line = 10f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - size / 2f;
                var dy = y + 0.5f - size / 2f;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = Mathf.Clamp01(size / 2f - 1f - r) * Mathf.Clamp01(r - (size / 2f - 1f - line));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            Folder(RingPath);
            File.WriteAllBytes(RingPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RingPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RingPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
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

        /// <summary>
        /// The icons of the 3D artist for the HUD (army, crown, upgrades): sprites, bilinear filter, no compression. Bilinear, not
        /// point: 64 px icons land on the screen at a scale that is not whole (72–110 px), and point sampling gives uneven pixels.
        /// The icons of gold, wood and the barracks (128 px) are over the map, in the labels and in the HUD: with mipmaps.
        /// </summary>
        /// <summary>
        /// The art of the top HUD (3D artist 3a3c4f0, README of export/borderbite/hud): sprites, bilinear, no compression, no
        /// mipmaps, clamped; the fills repeat (Tiled, full rect); the frame, the track and the mask are 9-sliced.
        /// </summary>
        private static void HudSprites()
        {
            const string folder = "Assets/Sprites/UI/Hud/";
            var borders = new Dictionary<string, int>
            {
                { "hud_frame_256", 48 }, { "hud_frame_128", 24 },
                { "bar_track_128x64", 24 }, { "bar_track_64x32", 12 },
                { "bar_mask_128x64", 24 }, { "bar_mask_64x32", 12 },
            };
            foreach (var path in System.IO.Directory.GetFiles(folder, "*.png"))
            {
                var asset = path.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(asset) is TextureImporter importer))
                    continue;

                var name = System.IO.Path.GetFileNameWithoutExtension(asset);
                var fill = name.StartsWith("bar_fill_");
                var border = borders.TryGetValue(name, out var b) ? new Vector4(b, b, b, b) : Vector4.zero;
                var wrap = fill ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border && importer.wrapMode == wrap
                    && !importer.mipmapEnabled && importer.filterMode == FilterMode.Bilinear)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = wrap;
                importer.spriteBorder = border;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static void Icons()
        {
            HudSprites();
            foreach (var name in new[] { "gold", "wood", "army", "crown", "up_hero", "up_brush", "up_soldiers", "up_gold" })
            {
                var path = IconFolder + name + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.textureType == TextureImporterType.Sprite
                    && importer.filterMode == FilterMode.Bilinear)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            // the icons over the map and in the labels shrink more than twice in a small window (800 × 450): mipmaps and a
            // trilinear filter keep them from shimmering (the UI designer, 4 November)
            var mipped = new List<string> { "icon_gold_128", "icon_wood_128", "icon_barracks_128", "icon_gold_64", "icon_wood_64", "icon_barracks_64" };
            // the buttons of the orders (task 4.18): frames 256 and 128, icons 128 and 64
            foreach (var order in new[] { "rally", "capture", "units", "barracks", "base" })
                mipped.AddRange(new[] { $"icon_order_{order}_256", $"icon_order_{order}_128", $"icon_order_{order}_64" });
            foreach (var group in new[] { "own", "attack" })
            foreach (var state in new[] { "normal", "active" })
                mipped.AddRange(new[] { $"btn_order_{group}_{state}_256", $"btn_order_{group}_{state}_128" });
            foreach (var name in mipped)
            {
                var path = IconFolder + name + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.textureType == TextureImporterType.Sprite
                    && importer.mipmapEnabled && importer.filterMode == FilterMode.Trilinear)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
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
