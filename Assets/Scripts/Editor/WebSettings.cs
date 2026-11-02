using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.EditorTools
{
    /// <summary>
    /// The Web settings of the project: Brotli with the fallback decompressor by default (a profile may override), .NET Standard 2.1,
    /// IL2CPP for size, high stripping, no splash screen, 512 MB at most, no services. Compared with the platforms in
    /// Docs/PLATFORM_REQUIREMENTS.md.
    /// </summary>
    public static class WebSettings
    {
        public const int MaximumMemoryMb = 512;
        public const int InitialMemoryMb = 32;

        [MenuItem("Tools/Setup/Apply Web Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = "Borderbite";
            PlayerSettings.productName = "Borderbite: Army Takeover";

            // the pause comes only from the SDK of a platform: Unity must not stop by itself on a lost focus (Playables forbids it)
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.maximumMemorySize = MaximumMemoryMb;
            PlayerSettings.WebGL.initialMemorySize = InitialMemoryMb;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.template = PlatformBuildProfile.YouTube.Template;

            var web = NamedBuildTarget.WebGL;
            PlayerSettings.SetScriptingBackend(web, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(web, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetIl2CppCompilerConfiguration(web, Il2CppCompilerConfiguration.Master);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;

            // Wasm: for disk size with link time optimization (Build Settings, Code Optimization)
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.WebGL), "CodeOptimization", "DiskSizeLTO");

            DisableServices();
            AssetDatabase.SaveAssets();
            Debug.Log("[WebSettings] applied");
        }

        private static void DisableServices()
        {
            PlayerSettings.enableCrashReportAPI = false;
            UnityEditor.Analytics.AnalyticsSettings.enabled = false;
            UnityEditor.Analytics.PerformanceReportingSettings.enabled = false;
            UnityEditor.CrashReporting.CrashReportingSettings.enabled = false;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/UnityConnectSettings.asset"))
            {
                var serialized = new SerializedObject(asset);
                Off(serialized, "m_Enabled");
                Off(serialized, "InsightsSettings.m_EngineDiagnosticsEnabled");
                Off(serialized, "InsightsSettings.m_Enabled");
                Off(serialized, "CrashReportingSettings.m_EnableCloudDiagnosticsReporting");
                Off(serialized, "UnityPurchasingSettings.m_Enabled");
                Off(serialized, "UnityAnalyticsSettings.m_Enabled");
                Off(serialized, "UnityAnalyticsSettings.m_InitializeOnStartup");
                Off(serialized, "UnityAdsSettings.m_Enabled");
                Off(serialized, "PerformanceReportingSettings.m_Enabled");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Off(SerializedObject serialized, string path)
        {
            var property = serialized.FindProperty(path);
            if (property != null)
                property.boolValue = false;
        }
    }
}
