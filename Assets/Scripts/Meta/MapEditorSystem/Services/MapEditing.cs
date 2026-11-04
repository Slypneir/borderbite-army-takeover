using System.Collections.Generic;
using Meta.BattleSystem.Configuration;
using UnityEngine;

namespace Meta.MapEditorSystem.Services
{
    /// <summary>
    /// The map open in the editor (layer 5 spec 2.1–2.2): a working copy of its file, so nothing is written until «Save»; every
    /// finished action is a step of the history, undone and redone whole (a stroke of the brush is one step). The copy differs from
    /// the file while the step it stands on is not the saved one.
    /// </summary>
    public sealed class MapEditing
    {
        private const int MostSteps = 200;

        private readonly List<string> _steps = new List<string>();
        private int _at;
        private int _saved;

        /// <summary>The working copy; null while no map is open.</summary>
        public BattleMapAsset Map { get; private set; }

        public string Name { get; private set; }

        /// <summary>There are changes not saved to the file.</summary>
        public bool Dirty => Map != null && _at != _saved;

        public bool CanUndo => _at > 0;

        public bool CanRedo => _at < _steps.Count - 1;

        /// <summary>Opens a copy of the file of a map; the history starts anew.</summary>
        public void Open(string name, BattleMapAsset file)
        {
            Close();
            Map = Object.Instantiate(file);
            Map.name = name;
            Name = name;
            _steps.Clear();
            _steps.Add(JsonUtility.ToJson(Map));
            _at = 0;
            _saved = 0;
        }

        public void Close()
        {
            if (Map != null && Application.isPlaying)
                Object.Destroy(Map);
            else if (Map != null)
                Object.DestroyImmediate(Map);
            Map = null;
            Name = null;
        }

        /// <summary>The map has a new name: its file was renamed.</summary>
        public void Renamed(string name)
        {
            Name = name;
            if (Map != null)
                Map.name = name;
        }

        /// <summary>The file now holds the step the copy stands on.</summary>
        public void Saved() => _saved = _at;

        /// <summary>A finished action: a new step; the undone steps after the current one are gone.</summary>
        public void Commit()
        {
            if (Map == null)
                return;

            var state = JsonUtility.ToJson(Map);
            if (state == _steps[_at])
                return;

            _steps.RemoveRange(_at + 1, _steps.Count - _at - 1);
            _steps.Add(state);
            _at++;
            if (_steps.Count <= MostSteps)
                return;

            _steps.RemoveAt(0);
            _at--;
            _saved--;
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            _at--;
            JsonUtility.FromJsonOverwrite(_steps[_at], Map);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;

            _at++;
            JsonUtility.FromJsonOverwrite(_steps[_at], Map);
            return true;
        }

        /// <summary>
        /// The brush of land or water (spec 2.2): a square of <paramref name="brush"/> cells (1, 3 or 5) round a cell. True when a
        /// cell changed; the step is made by <see cref="Commit"/> at the end of the stroke.
        /// </summary>
        public bool Paint(int x, int y, int brush, bool water)
        {
            if (Map == null)
                return false;

            var half = brush / 2;
            var changed = false;
            for (var dy = -half; dy <= half; dy++)
            for (var dx = -half; dx <= half; dx++)
                changed |= Map.SetWater(x + dx, y + dy, water);
            return changed;
        }

        /// <summary>The brush along a straight line between two cells, so a quick stroke leaves no gaps.</summary>
        public bool PaintLine(int x0, int y0, int x1, int y1, int brush, bool water)
        {
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            var changed = false;
            for (var i = 0; i <= steps; i++)
            {
                var t = steps == 0 ? 0f : (float)i / steps;
                changed |= Paint(Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), brush, water);
            }

            return changed;
        }
    }
}
