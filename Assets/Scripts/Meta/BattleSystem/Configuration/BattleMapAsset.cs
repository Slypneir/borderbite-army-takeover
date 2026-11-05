using System;
using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using UnityEngine;

namespace Meta.BattleSystem.Configuration
{
    /// <summary>
    /// A map of the owner (layer 5): a file of its own in the project, made and changed in the map editor, a rectangle of any size
    /// the owner sets (5 November). The land is rows of characters from the top (north) down, «#» land and «~» water, so a diff
    /// in git shows the shape; no rows — all land. The bases are given by the corner of four cells under their middle, the places
    /// and piles by their cell, the rocks by their cell, size and turn. The battle gets it as a <see cref="BattleMap"/> through
    /// <see cref="ToMap"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Map", menuName = "Borderbite/Battle/Map")]
    public sealed class BattleMapAsset : ScriptableObject
    {
        public const char Land = '#';
        public const char Water = '~';
        /// <summary>The least and the most cells a side of a map may have (owner, 5 November: the owner sets the size).</summary>
        public const int MinCells = 16;
        public const int MaxCells = 128;

        [Tooltip("Cells along a side of an older square map; the width and the height replace it.")]
        [SerializeField] private int size = 64;

        [Tooltip("Cells across (west to east). 0 — the square of size.")]
        [SerializeField] private int width;

        [Tooltip("Cells up (south to north). 0 — the square of size.")]
        [SerializeField] private int height;

        [Tooltip("The rows of the map from the top down, as many as the height, each as long as the width: «#» land, «~» water. " +
                 "Empty — all land.")]
        [SerializeField] private string[] land = Array.Empty<string>();

        [SerializeField] private BaseSpot playerBase = new BaseSpot { corner = new Vector2Int(8, 8), startRadius = 6 };
        [SerializeField] private BaseSpot enemyBase = new BaseSpot { corner = new Vector2Int(56, 56), startRadius = 6 };
        [SerializeField] private List<SiteSpot> sites = new List<SiteSpot>();
        [SerializeField] private List<PileSpot> piles = new List<PileSpot>();
        [SerializeField] private List<RockSpot> rocks = new List<RockSpot>();

        public int Width => width > 0 ? width : size;
        public int Height => height > 0 ? height : size;
        public BaseSpot PlayerBase => playerBase;
        public BaseSpot EnemyBase => enemyBase;
        public IReadOnlyList<SiteSpot> Sites => sites;
        public IReadOnlyList<PileSpot> Piles => piles;
        public IReadOnlyList<RockSpot> Rocks => rocks;

        /// <summary>Is a cell water; off the map is the sea.</summary>
        public bool IsWater(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return true;

            var row = Height - 1 - y;
            return land != null && row < land.Length && x < land[row].Length && land[row][x] == Water;
        }

        public BattleMap ToMap()
        {
            var w = Width;
            var h = Height;
            bool[] water = null;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (!IsWater(x, y))
                    continue;

                water ??= new bool[w * h];
                water[y * w + x] = true;
            }

            var map = new BattleMap(w, h, water);
            map.Bases[(int)Side.Player] = new MapBase(playerBase.corner.x, playerBase.corner.y, playerBase.startRadius);
            map.Bases[(int)Side.Enemy] = new MapBase(enemyBase.corner.x, enemyBase.corner.y, enemyBase.startRadius);
            foreach (var site in sites)
                map.Sites.Add(new MapSite(site.cell.x, site.cell.y, site.barracks));
            foreach (var pile in piles)
                map.Piles.Add(new MapPile(pile.kind, pile.cell.x, pile.cell.y));
            foreach (var rock in rocks)
                map.Rocks.Add(new MapRock(rock.cell.x, rock.cell.y, (int)rock.size, Mathf.RoundToInt(rock.yaw)));
            return map;
        }

        /// <summary>
        /// A new map of the editor (layer 5 spec 2.1): land of <paramref name="across"/> by <paramref name="up"/> cells with the bases
        /// 8 cells from two corners, start areas of 6, nothing else.
        /// </summary>
        public void Reset(int across, int up)
        {
            size = Mathf.Max(across, up);
            width = across;
            height = up;
            land = Array.Empty<string>();
            playerBase = new BaseSpot { corner = new Vector2Int(8, 8), startRadius = 6 };
            enemyBase = new BaseSpot { corner = new Vector2Int(across - 8, up - 8), startRadius = 6 };
            sites.Clear();
            piles.Clear();
            rocks.Clear();
        }

        public void Reset(int cells) => Reset(cells, cells);

        /// <summary>
        /// A new map of the editor as the owner asked (5 November): all water, the island is painted on it; the bases stand 8 cells
        /// from two corners until they are put elsewhere.
        /// </summary>
        public void ResetToWater(int across, int up)
        {
            Reset(across, up);
            land = new string[up];
            for (var i = 0; i < up; i++)
                land[i] = new string(Water, across);
        }

        public void ResetToWater(int cells) => ResetToWater(cells, cells);

        /// <summary>
        /// A new size (owner, 5 November: the bounds he sets are the map): <paramref name="across"/> by <paramref name="up"/> cells,
        /// the old map moved by (<paramref name="shiftX"/>, <paramref name="shiftY"/>) cells — a border pulled west or south moves
        /// it. The new cells are water; what falls off is gone, the bases are kept inside.
        /// </summary>
        public void Resize(int across, int up, int shiftX, int shiftY)
        {
            across = Mathf.Clamp(across, MinCells, MaxCells);
            up = Mathf.Clamp(up, MinCells, MaxCells);
            var rows = new string[up];
            var line = new char[across];
            for (var y = up - 1; y >= 0; y--)
            {
                for (var x = 0; x < across; x++)
                    line[x] = IsWater(x - shiftX, y - shiftY) ? Water : Land;
                rows[up - 1 - y] = new string(line);
            }

            var shift = new Vector2Int(shiftX, shiftY);
            bool Inside(Vector2Int cell, int margin) =>
                cell.x >= margin && cell.y >= margin && cell.x < across - margin && cell.y < up - margin;

            for (var i = sites.Count - 1; i >= 0; i--)
            {
                var site = sites[i];
                site.cell += shift;
                if (Inside(site.cell, 1))
                    sites[i] = site;
                else
                    sites.RemoveAt(i);
            }

            for (var i = piles.Count - 1; i >= 0; i--)
            {
                var pile = piles[i];
                pile.cell += shift;
                if (Inside(pile.cell, 0))
                    piles[i] = pile;
                else
                    piles.RemoveAt(i);
            }

            for (var i = rocks.Count - 1; i >= 0; i--)
            {
                var rock = rocks[i];
                rock.cell += shift;
                if (Inside(rock.cell, 0))
                    rocks[i] = rock;
                else
                    rocks.RemoveAt(i);
            }

            Vector2Int Keep(Vector2Int corner) =>
                new Vector2Int(Mathf.Clamp(corner.x + shiftX, 3, across - 3), Mathf.Clamp(corner.y + shiftY, 3, up - 3));
            playerBase.corner = Keep(playerBase.corner);
            enemyBase.corner = Keep(enemyBase.corner);
            size = Mathf.Max(across, up);
            width = across;
            height = up;
            land = rows;
        }

        /// <summary>Puts the base of a side by the corner of four cells under its middle.</summary>
        public void SetBase(Side side, Vector2Int corner)
        {
            if (side == Side.Enemy)
                enemyBase.corner = corner;
            else
                playerBase.corner = corner;
        }

        public void AddPile(ResourceKind kind, Vector2Int cell) => piles.Add(new PileSpot { kind = kind, cell = cell });

        public void AddSite(Vector2Int cell) => sites.Add(new SiteSpot { cell = cell, barracks = Side.Neutral });

        /// <summary>Takes away the piles, places and rocks whose cell is in the area; returns how many.</summary>
        public int RemoveAt(Predicate<Vector2Int> inArea)
        {
            var count = piles.RemoveAll(pile => inArea(pile.cell));
            count += sites.RemoveAll(site => inArea(site.cell));
            count += rocks.RemoveAll(rock => inArea(rock.cell));
            return count;
        }

        /// <summary>Makes a cell water or land; true when it changed. The rows appear with the first water.</summary>
        public bool SetWater(int x, int y, bool water)
        {
            var w = Width;
            var h = Height;
            if (x < 0 || y < 0 || x >= w || y >= h || IsWater(x, y) == water)
                return false;

            if (land == null || land.Length != h)
            {
                land = new string[h];
                for (var i = 0; i < h; i++)
                    land[i] = new string(Land, w);
            }

            var row = h - 1 - y;
            var chars = land[row].PadRight(w, Land).ToCharArray();
            chars[x] = water ? Water : Land;
            land[row] = new string(chars);
            return true;
        }

        /// <summary>The cells of land.</summary>
        public int LandCells()
        {
            var count = 0;
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (!IsWater(x, y))
                    count++;
            }

            return count;
        }

        /// <summary>Writes a whole map over this one: the editor and the move of the test map (task 5.5).</summary>
        public void Set(BattleMap map)
        {
            width = map.Width;
            height = map.Height;
            size = Mathf.Max(width, height);
            var rows = new List<string>();
            if (map.Water != null)
            {
                var line = new char[width];
                for (var y = height - 1; y >= 0; y--)
                {
                    for (var x = 0; x < width; x++)
                        line[x] = map.IsWater(x, y) ? Water : Land;
                    rows.Add(new string(line));
                }
            }

            land = rows.ToArray();
            var player = map.Bases[(int)Side.Player];
            var enemy = map.Bases[(int)Side.Enemy];
            playerBase = new BaseSpot { corner = new Vector2Int(player.X, player.Y), startRadius = player.StartRadius };
            enemyBase = new BaseSpot { corner = new Vector2Int(enemy.X, enemy.Y), startRadius = enemy.StartRadius };
            sites.Clear();
            foreach (var site in map.Sites)
                sites.Add(new SiteSpot { cell = new Vector2Int(site.X, site.Y), barracks = site.Barracks });
            piles.Clear();
            foreach (var pile in map.Piles)
                piles.Add(new PileSpot { kind = pile.Kind, cell = new Vector2Int(pile.X, pile.Y) });
            rocks.Clear();
            foreach (var rock in map.Rocks)
                rocks.Add(new RockSpot { cell = new Vector2Int(rock.X, rock.Y), size = (RockSize)rock.Size, yaw = rock.Yaw });
        }

        [Serializable]
        public struct BaseSpot
        {
            [Tooltip("The corner of four cells under the middle of the 6 by 6 base.")]
            public Vector2Int corner;
            [Tooltip("Radius of the start area round that corner, cells. Starting value: 6.")]
            public int startRadius;
        }

        [Serializable]
        public struct SiteSpot
        {
            [Tooltip("The centre cell of the 3 by 3 place.")]
            public Vector2Int cell;
            [Tooltip("Neutral — an empty place; a side — its barracks stands from the start.")]
            public Side barracks;
        }

        [Serializable]
        public struct PileSpot
        {
            public ResourceKind kind;
            public Vector2Int cell;
        }

        public enum RockSize
        {
            Small,
            Medium,
            Large,
        }

        [Serializable]
        public struct RockSpot
        {
            public Vector2Int cell;
            public RockSize size;
            [Tooltip("Turn round the vertical, degrees.")]
            public float yaw;
        }
    }
}
