using Core.DependencyInjection;
using Core.PlatformSystem.Configuration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.EditorTools
{
    /// <summary>
    /// While a player is built, the root of the scene gets the ad policy of the platform: the build carries only that asset
    /// and the scene file on disk stays as it is.
    /// </summary>
    public sealed class AdPolicyInjector : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!BuildPipeline.isBuildingPlayer || PlatformBuild.Current == null)
                return;

            var policy = AssetDatabase.LoadAssetAtPath<AdPolicyConfig>(PlatformBuild.Current.AdPolicy);
            foreach (var root in scene.GetRootGameObjects())
            {
                var installer = root.GetComponentInChildren<RootInstaller>(true);
                if (installer == null)
                    continue;

                var serialized = new SerializedObject(installer);
                serialized.FindProperty("adPolicyConfig").objectReferenceValue = policy;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[AdPolicyInjector] {policy.name}");
                return;
            }
        }
    }
}
