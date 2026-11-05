using System.Collections.Generic;
using Meta.BattleSystem.Configuration;

namespace Meta.MapEditorSystem.Interfaces
{
    /// <summary>
    /// The map files of the project (layer 5 spec 2.1): each a file of its own in Assets/ScriptableObjects/Maps, seen in git.
    /// Only the Unity editor can write them: elsewhere <see cref="Available"/> is false and the map editor is not offered.
    /// </summary>
    public interface IMapLibrary
    {
        bool Available { get; }

        /// <summary>The names of the maps, in order.</summary>
        IReadOnlyList<string> Names();

        BattleMapAsset Load(string name);

        /// <summary>Writes the map under its name, a new file or over the old one.</summary>
        void Save(string name, BattleMapAsset map);

        /// <summary>A new map of <paramref name="cells"/>, all water, under a free name; returns the name.</summary>
        string Create(int cells);

        /// <summary>A copy under a free name; returns the name.</summary>
        string Copy(string name);

        /// <summary>Renames a map; null when done, else why not.</summary>
        string Rename(string name, string newName);

        /// <summary>Deletes a map; null when done, else why not (the map of the test level stays).</summary>
        string Delete(string name);

        /// <summary>The map the test level plays: the battle config's.</summary>
        string TestMap { get; }
    }
}
