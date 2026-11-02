using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Core.EditorTools
{
    /// <summary>
    /// One light Mobile URP for the whole game: no HDR, no MSAA, no post-processing, no realtime lights or shadows, no depth and
    /// opaque copies, one quality level.
    /// </summary>
    public static class RenderPipelineSetup
    {
        private const string AssetPath = "Assets/Settings/Mobile_RPAsset.asset";

        [MenuItem("Tools/Setup/Lean Render Pipeline")]
        public static string Apply()
        {
            var report = new StringBuilder();
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR", false);
            Set(so, "m_MSAA", 1);
            Set(so, "m_RenderScale", 1f);
            Set(so, "m_RequireDepthTexture", false);
            Set(so, "m_RequireOpaqueTexture", false);
            Set(so, "m_MainLightRenderingMode", 0);
            Set(so, "m_MainLightShadowsSupported", false);
            Set(so, "m_AdditionalLightsRenderingMode", 0);
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_SoftShadowsSupported", false);
            Set(so, "m_ShadowDistance", 0f);
            Set(so, "m_EnableLODCrossFade", false);
            Set(so, "m_SupportsLightLayers", false);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_SupportsTerrainHoles", false);
            Set(so, "m_ReflectionProbeBlending", false);
            Set(so, "m_ReflectionProbeBoxProjection", false);
            Set(so, "m_AdditionalLightsCookieFormat", 3); // no gray cookie format on WebGL 2: its warning would be in the console
            var volumeProfile = so.FindProperty("m_VolumeProfile");
            if (volumeProfile != null)
                volumeProfile.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            var renderers = new SerializedObject(asset).FindProperty("m_RendererDataList");
            for (var i = 0; renderers != null && i < renderers.arraySize; i++)
            {
                var data = renderers.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
                if (data == null)
                    continue;

                foreach (var feature in data.rendererFeatures)
                {
                    if (feature != null)
                        feature.SetActive(false);
                }

                var rso = new SerializedObject(data);
                Set(rso, "m_DepthPrimingMode", 0);
                Set(rso, "m_CopyDepthMode", 0);
                var postProcess = rso.FindProperty("postProcessData");
                if (postProcess != null)
                    postProcess.objectReferenceValue = null;
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                report.Append(data.name).Append(": no post-processing\n");
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            OneQualityLevel(asset);
            AssetDatabase.SaveAssets();
            report.Append(AssetPath).Append(": lean, one quality level");
            Debug.Log($"[RenderPipelineSetup] {report}");
            return report.ToString();
        }

        private static void OneQualityLevel(RenderPipelineAsset asset)
        {
            foreach (var settings in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset"))
            {
                var so = new SerializedObject(settings);
                var levels = so.FindProperty("m_QualitySettings");
                if (levels == null)
                    continue;

                while (levels.arraySize > 1)
                    levels.DeleteArrayElementAtIndex(levels.arraySize - 1);

                var level = levels.GetArrayElementAtIndex(0);
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = asset;
                level.FindPropertyRelative("antiAliasing").intValue = 0;
                level.FindPropertyRelative("vSyncCount").intValue = 0;
                level.FindPropertyRelative("shadows").intValue = 0;
                level.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
                so.FindProperty("m_CurrentQuality").intValue = 0;
                var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
                for (var i = 0; perPlatform != null && i < perPlatform.arraySize; i++)
                    perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Set(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = v;
        }

        private static void Set(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = v;
        }

        private static void Set(SerializedObject so, string name, float v)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = v;
        }
    }
}
