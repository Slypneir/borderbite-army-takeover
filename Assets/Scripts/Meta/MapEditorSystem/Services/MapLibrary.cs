using System;
using System.Collections.Generic;
using Meta.BattleSystem.Configuration;
using Meta.MapEditorSystem.Interfaces;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Meta.MapEditorSystem.Services
{
    /// <summary>
    /// The map files in Assets/ScriptableObjects/Maps through the AssetDatabase of the Unity editor, in Play Mode too; in a build
    /// there are no files to write, and the library is not available.
    /// </summary>
    public sealed class MapLibrary : IMapLibrary
    {
        public const string Folder = "Assets/ScriptableObjects/Maps";

        private readonly BattleConfig _config;

        public MapLibrary(BattleConfig config) => _config = config;

#if UNITY_EDITOR
        public bool Available => true;

        public string TestMap => _config.TestMap != null ? _config.TestMap.name : null;

        public IReadOnlyList<string> Names()
        {
            var names = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:BattleMapAsset", new[] { Folder }))
                names.Add(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        public BattleMapAsset Load(string name) => AssetDatabase.LoadAssetAtPath<BattleMapAsset>(PathOf(name));

        public void Save(string name, BattleMapAsset map)
        {
            var asset = Load(name);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BattleMapAsset>();
                EditorUtility.CopySerialized(map, asset);
                asset.name = name;
                AssetDatabase.CreateAsset(asset, PathOf(name));
            }
            else
            {
                EditorUtility.CopySerialized(map, asset);
                asset.name = name;
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public string Create(int cells)
        {
            var name = Free("Карта");
            var asset = ScriptableObject.CreateInstance<BattleMapAsset>();
            asset.Reset(cells);
            AssetDatabase.CreateAsset(asset, PathOf(name));
            AssetDatabase.SaveAssetIfDirty(asset);
            return name;
        }

        public string Copy(string name)
        {
            var copy = Free(name + " копия");
            AssetDatabase.CopyAsset(PathOf(name), PathOf(copy));
            return copy;
        }

        public string Rename(string name, string newName)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName))
                return "пустое имя";
            if (newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                return "в имени есть запрещённые знаки";
            if (newName == name)
                return null;
            if (Load(newName) != null)
                return $"карта «{newName}» уже есть";

            var error = AssetDatabase.RenameAsset(PathOf(name), newName);
            return string.IsNullOrEmpty(error) ? null : error;
        }

        public string Delete(string name)
        {
            if (name == TestMap)
                return "это карта тестового уровня — её не удалить";

            return AssetDatabase.DeleteAsset(PathOf(name)) ? null : "не удалось удалить файл";
        }

        private static string PathOf(string name) => $"{Folder}/{name}.asset";

        private string Free(string stem)
        {
            for (var i = 1; ; i++)
            {
                var name = i == 1 && stem.EndsWith("копия") ? stem : $"{stem} {i}";
                if (Load(name) == null)
                    return name;
            }
        }
#else
        public bool Available => false;
        public string TestMap => null;
        public IReadOnlyList<string> Names() => Array.Empty<string>();
        public BattleMapAsset Load(string name) => null;
        public void Save(string name, BattleMapAsset map) { }
        public string Create(int cells) => null;
        public string Copy(string name) => null;
        public string Rename(string name, string newName) => "no map files in a build";
        public string Delete(string name) => "no map files in a build";
#endif
    }
}
