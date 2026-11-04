using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using UnityEditor;
using UnityEngine;

namespace Core.EditorTools
{
    /// <summary>
    /// The map files of the project (layer 5): the folder, the test map made from the square island of the battle config and set
    /// as the map of the test level, and a sample with water — a lake in the middle, two bays and cut corners, mirrored through the
    /// centre — so the water of a map is seen in a battle before the editor.
    /// </summary>
    public static class MapFiles
    {
        public const string Folder = "Assets/ScriptableObjects/Maps/";
        public const string TestPath = Folder + "Test.asset";
        public const string LakesPath = Folder + "Lakes.asset";

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

            if (AssetDatabase.LoadAssetAtPath<BattleMapAsset>(LakesPath) == null)
            {
                var lakes = ScriptableObject.CreateInstance<BattleMapAsset>();
                lakes.Set(Lakes(config.SquareMap()));
                AssetDatabase.CreateAsset(lakes, LakesPath);
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

        /// <summary>The square island with water: a round lake in the middle, a bay into each side's flank, two cut corners.</summary>
        private static BattleMap Lakes(BattleMap square)
        {
            var size = square.Size;
            var last = size - 1;
            var water = new bool[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // a lake of radius 6 round the middle of the map
                var dx = 2 * x + 1 - size;
                var dy = 2 * y + 1 - size;
                var lake = dx * dx + dy * dy <= 4 * 6 * 6;
                // a bay from the west under the place (12, 36), and its mirror from the east
                var bay = (x <= 9 && y >= 22 && y <= 27) || (x >= last - 9 && y >= last - 27 && y <= last - 22);
                // the north-west and the south-east corners cut off
                var corner = x + (last - y) < 10 || (last - x) + y < 10;
                water[y * size + x] = lake || bay || corner;
            }

            var map = new BattleMap(size, water);
            map.Bases[(int)Side.Player] = square.Bases[(int)Side.Player];
            map.Bases[(int)Side.Enemy] = square.Bases[(int)Side.Enemy];
            map.Sites.AddRange(square.Sites);
            map.Piles.AddRange(square.Piles);
            map.Rocks.Add(new MapRock(32, 33, 2, 30));
            map.Rocks.Add(new MapRock(20, 44, 1, 0));
            map.Rocks.Add(new MapRock(last - 20, last - 44, 1, 180));
            map.Rocks.Add(new MapRock(4, 24, 0, 45));
            map.Rocks.Add(new MapRock(last - 4, last - 24, 0, 225));
            return map;
        }
    }
}
