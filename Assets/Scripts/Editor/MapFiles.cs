using Meta.BattleSystem.Configuration;
using UnityEditor;
using UnityEngine;

namespace Core.EditorTools
{
    /// <summary>
    /// The map files of the project (layer 5): the folder, and the test map made from the square island of the battle config and
    /// set as the map of the test level when it is missing. Other maps are the owner's, made in the map editor; nothing here
    /// makes them again once deleted.
    /// </summary>
    public static class MapFiles
    {
        public const string Folder = "Assets/ScriptableObjects/Maps/";
        public const string TestPath = Folder + "Test.asset";

        [MenuItem("Tools/Setup/Map Files")]
        public static void Ensure()
        {
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Maps");

            var config = AssetDatabase.LoadAssetAtPath<BattleConfig>(ProjectSetup.BattleConfigPath);
            if (config == null)
                return;

            var test = AssetDatabase.LoadAssetAtPath<BattleMapAsset>(TestPath);
            if (test == null)
            {
                test = ScriptableObject.CreateInstance<BattleMapAsset>();
                test.Set(config.SquareMap());
                AssetDatabase.CreateAsset(test, TestPath);
            }

            var serialized = new SerializedObject(config);
            var field = serialized.FindProperty("map");
            if (field.objectReferenceValue == null)
            {
                field.objectReferenceValue = test;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
        }
    }
}
