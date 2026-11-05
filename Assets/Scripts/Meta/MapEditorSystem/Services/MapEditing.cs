using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using UnityEngine;

namespace Meta.MapEditorSystem.Services
{
    /// <summary>
    /// The map open in the editor (layer 5 spec 2.1–2.3): a working copy of its file, so nothing is written until «Save»; every
    /// finished action is a step of the history, undone and redone whole (a stroke of the brush is one step). The copy differs from
    /// the file while the step it stands on is not the saved one. The brush of land, water and the eraser is a square or a circle of
    /// 1–15 cells; piles, places and bases are put by a click; the mirror repeats every action through the centre of the map.
    /// </summary>
    public sealed class MapEditing
    {
        public const int MostBrush = 15;
        private const int MostSteps = 200;
        // a base is 6 × 6 cells round its corner, a place 3 × 3 round its centre
        private const int BaseHalf = 3;

        public enum Shape
        {
            Square,
            Circle,
        }

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

        /// <summary>The cell symmetric to a cell through the centre of the map.</summary>
        public Vector2Int Mirror(Vector2Int cell) => new Vector2Int(Map.Size - 1 - cell.x, Map.Size - 1 - cell.y);

        /// <summary>
        /// The cells of a brush of <paramref name="size"/> round a cell: a square, or a circle whose cells have their middles within
        /// half the size of the middle of the brush. An even size leans to the lower left.
        /// </summary>
        public static IEnumerable<Vector2Int> Brush(Vector2Int at, int size, Shape shape)
        {
            size = Mathf.Clamp(size, 1, MostBrush);
            var middle = (size - 1) / 2f;
            var radius2 = size * size / 4f + 0.01f;
            var from = at - new Vector2Int((size - 1) / 2, (size - 1) / 2);
            for (var j = 0; j < size; j++)
            for (var i = 0; i < size; i++)
            {
                if (shape == Shape.Circle && (i - middle) * (i - middle) + (j - middle) * (j - middle) > radius2)
                    continue;

                yield return new Vector2Int(from.x + i, from.y + j);
            }
        }

        /// <summary>The square brush of land or water; the step is made by <see cref="Commit"/> at the end of the stroke.</summary>
        public bool Paint(int x, int y, int brush, bool water) => Paint(new Vector2Int(x, y), brush, water, Shape.Square, false);

        /// <summary>The brush of land or water (spec 2.2) round a cell, and round its mirror. True when a cell changed.</summary>
        public bool Paint(Vector2Int at, int brush, bool water, Shape shape, bool mirror)
        {
            if (Map == null)
                return false;

            var changed = false;
            foreach (var cell in Brush(at, brush, shape))
            {
                changed |= Map.SetWater(cell.x, cell.y, water);
                if (mirror)
                {
                    var other = Mirror(cell);
                    changed |= Map.SetWater(other.x, other.y, water);
                }
            }

            return changed;
        }

        public bool PaintLine(int x0, int y0, int x1, int y1, int brush, bool water) =>
            PaintLine(new Vector2Int(x0, y0), new Vector2Int(x1, y1), brush, water, Shape.Square, false);

        /// <summary>The brush along a straight line between two cells, so a quick stroke leaves no gaps.</summary>
        public bool PaintLine(Vector2Int from, Vector2Int to, int brush, bool water, Shape shape, bool mirror)
        {
            var changed = false;
            foreach (var cell in Line(from, to))
                changed |= Paint(cell, brush, water, shape, mirror);
            return changed;
        }

        /// <summary>The eraser: piles, places and rocks under the brush go; the bases stay (a map has both always).</summary>
        public bool Erase(Vector2Int at, int brush, Shape shape, bool mirror)
        {
            if (Map == null)
                return false;

            var area = new HashSet<Vector2Int>(Brush(at, brush, shape));
            if (mirror)
            {
                foreach (var cell in new List<Vector2Int>(area))
                    area.Add(Mirror(cell));
            }

            return Map.RemoveAt(area.Contains) > 0;
        }

        public bool EraseLine(Vector2Int from, Vector2Int to, int brush, Shape shape, bool mirror)
        {
            var changed = false;
            foreach (var cell in Line(from, to))
                changed |= Erase(cell, brush, shape, mirror);
            return changed;
        }

        /// <summary>A pile of gold or wood on a cell of land, free of other things; null when put, else why not.</summary>
        public string PlacePile(ResourceKind kind, Vector2Int cell, bool mirror)
        {
            var error = PileError(cell);
            var other = Mirror(cell);
            if (error == null && mirror && other != cell)
                error = PileError(other);
            if (error != null)
                return error;

            Map.AddPile(kind, cell);
            if (mirror && other != cell)
                Map.AddPile(kind, other);
            return null;
        }

        /// <summary>A place to build of 3 × 3 cells round a cell, all land, clear of other places and of the bases.</summary>
        public string PlaceSite(Vector2Int cell, bool mirror)
        {
            var error = SiteError(cell, null);
            var other = Mirror(cell);
            if (error == null && mirror && other != cell)
                error = SiteError(other, cell);
            if (error != null)
                return error;

            Map.AddSite(cell);
            if (mirror && other != cell)
                Map.AddSite(other);
            return null;
        }

        /// <summary>
        /// Moves the base of a side to a corner of cells (its middle), whole on the map and clear of the other base; with the mirror
        /// the other base goes to the symmetric corner. The base may stand on water for now: «Check» of task 5.4 tells it.
        /// </summary>
        public string PlaceBase(Side side, Vector2Int corner, bool mirror)
        {
            var size = Map.Size;
            corner = new Vector2Int(Mathf.Clamp(corner.x, BaseHalf, size - BaseHalf), Mathf.Clamp(corner.y, BaseHalf, size - BaseHalf));
            var otherSide = side == Side.Player ? Side.Enemy : Side.Player;
            var otherCorner = mirror ? new Vector2Int(size - corner.x, size - corner.y) : CornerOf(otherSide);
            if (Mathf.Abs(corner.x - otherCorner.x) < BaseHalf * 2 && Mathf.Abs(corner.y - otherCorner.y) < BaseHalf * 2)
                return "штабы налезают друг на друга";

            Map.SetBase(side, corner);
            if (mirror)
                Map.SetBase(otherSide, otherCorner);
            return null;
        }

        public Vector2Int CornerOf(Side side) => side == Side.Enemy ? Map.EnemyBase.corner : Map.PlayerBase.corner;

        /// <summary>
        /// A start pile closer than <paramref name="keepOffCells"/> to the edge of a place to build or to the walls of a base: the
        /// battle keeps new resources off there (task 4.29), the editor warns of a start pile there (layer 5 spec 2.5).
        /// </summary>
        public bool PileCrowds(Vector2Int cell, float keepOffCells)
        {
            bool Near(float cx, float cy, float half)
            {
                var dx = Mathf.Max(0f, Mathf.Abs(cell.x + 0.5f - cx) - half);
                var dy = Mathf.Max(0f, Mathf.Abs(cell.y + 0.5f - cy) - half);
                return dx * dx + dy * dy < keepOffCells * keepOffCells;
            }

            foreach (var site in Map.Sites)
            {
                if (Near(site.cell.x + 0.5f, site.cell.y + 0.5f, 1.5f))
                    return true;
            }

            return Near(Map.PlayerBase.corner.x, Map.PlayerBase.corner.y, BaseHalf) || Near(Map.EnemyBase.corner.x, Map.EnemyBase.corner.y, BaseHalf);
        }

        /// <summary>The start piles closer than the keep-off to a place or a base: warnings of the editor.</summary>
        public int CrowdingPiles(float keepOffCells)
        {
            var count = 0;
            foreach (var pile in Map.Piles)
            {
                if (PileCrowds(pile.cell, keepOffCells))
                    count++;
            }

            return count;
        }

        private string PileError(Vector2Int cell)
        {
            if (Map.IsWater(cell.x, cell.y))
                return "ресурс ставится только на сушу";
            if (OnBase(cell) || OnSite(cell))
                return "здесь уже стоит штаб или место";
            foreach (var pile in Map.Piles)
            {
                if (pile.cell == cell)
                    return "здесь уже лежит ресурс";
            }

            return null;
        }

        private string SiteError(Vector2Int cell, Vector2Int? pending)
        {
            for (var y = cell.y - 1; y <= cell.y + 1; y++)
            for (var x = cell.x - 1; x <= cell.x + 1; x++)
            {
                if (Map.IsWater(x, y))
                    return "место под постройку — 3 на 3 клетки суши";
                if (OnBase(new Vector2Int(x, y)))
                    return "место налезает на штаб";
            }

            foreach (var site in Map.Sites)
            {
                if (Mathf.Abs(site.cell.x - cell.x) < 3 && Mathf.Abs(site.cell.y - cell.y) < 3)
                    return "место налезает на другое место";
            }

            if (pending.HasValue && Mathf.Abs(pending.Value.x - cell.x) < 3 && Mathf.Abs(pending.Value.y - cell.y) < 3)
                return "место и его зеркало налезают друг на друга";
            return null;
        }

        private bool OnBase(Vector2Int cell) => InBase(Map.PlayerBase.corner, cell) || InBase(Map.EnemyBase.corner, cell);

        private static bool InBase(Vector2Int corner, Vector2Int cell) =>
            cell.x >= corner.x - BaseHalf && cell.x < corner.x + BaseHalf && cell.y >= corner.y - BaseHalf && cell.y < corner.y + BaseHalf;

        private bool OnSite(Vector2Int cell)
        {
            foreach (var site in Map.Sites)
            {
                if (Mathf.Abs(site.cell.x - cell.x) <= 1 && Mathf.Abs(site.cell.y - cell.y) <= 1)
                    return true;
            }

            return false;
        }

        private static IEnumerable<Vector2Int> Line(Vector2Int from, Vector2Int to)
        {
            var steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
            for (var i = 0; i <= steps; i++)
            {
                var t = steps == 0 ? 0f : (float)i / steps;
                yield return new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)), Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t)));
            }
        }
    }
}
