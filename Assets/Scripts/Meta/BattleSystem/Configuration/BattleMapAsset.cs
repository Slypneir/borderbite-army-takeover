using System;
using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using UnityEngine;

namespace Meta.BattleSystem.Configuration
{
    /// <summary>
    /// A map of the owner (layer 5): a file of its own in the project, made and changed in the map editor. The land is rows of
    /// characters from the top (north) down, «#» land and «~» water, so a diff in git shows the shape; no rows — all land. The
    /// bases are given by the corner of four cells under their middle, the places and piles by their cell, the rocks by their
    /// cell, size and turn. The battle gets it as a <see cref="BattleMap"/> through <see cref="ToMap"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Map", menuName = "Borderbite/Battle/Map")]
    public sealed class BattleMapAsset : ScriptableObject
    {
        public const char Land = '#';
        public const char Water = '~';
        public static readonly int[] Sizes = { 48, 64, 80 };

        [Tooltip("Cells along a side of the square: 48, 64 or 80 (Т-Р2).")]
        [SerializeField] private int size = 64;

        [Tooltip("The rows of the map from the top down: «#» land, «~» water. Empty — all land.")]
        [SerializeField] private string[] land = Array.Empty<string>();

        [SerializeField] private BaseSpot playerBase = new BaseSpot { corner = new Vector2Int(8, 8), startRadius = 6 };
        [SerializeField] private BaseSpot enemyBase = new BaseSpot { corner = new Vector2Int(56, 56), startRadius = 6 };
        [SerializeField] private List<SiteSpot> sites = new List<SiteSpot>();
        [SerializeField] private List<PileSpot> piles = new List<PileSpot>();
        [SerializeField] private List<RockSpot> rocks = new List<RockSpot>();

        public int Size => size;
        public BaseSpot PlayerBase => playerBase;
        public BaseSpot EnemyBase => enemyBase;
        public IReadOnlyList<SiteSpot> Sites => sites;
        public IReadOnlyList<PileSpot> Piles => piles;
        public IReadOnlyList<RockSpot> Rocks => rocks;

        /// <summary>Is a cell water; off the map is the sea.</summary>
        public bool IsWater(int x, int y)
        {
            if (x < 0 || y < 0 || x >= size || y >= size)
                return true;

            var row = size - 1 - y;
            return land != null && row < land.Length && x < land[row].Length && land[row][x] == Water;
        }

        public BattleMap ToMap()
        {
            bool[] water = null;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (!IsWater(x, y))
                    continue;

                water ??= new bool[size * size];
                water[y * size + x] = true;
            }

            var map = new BattleMap(size, water);
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
        /// A new map of the editor (layer 5 spec 2.1): a square of land of <paramref name="cells"/> with the bases 8 cells from two
        /// corners, start areas of 6, nothing else.
        /// </summary>
        public void Reset(int cells)
        {
            size = cells;
            land = Array.Empty<string>();
            playerBase = new BaseSpot { corner = new Vector2Int(8, 8), startRadius = 6 };
            enemyBase = new BaseSpot { corner = new Vector2Int(cells - 8, cells - 8), startRadius = 6 };
            sites.Clear();
            piles.Clear();
            rocks.Clear();
        }

        /// <summary>Makes a cell water or land; true when it changed. The rows appear with the first water.</summary>
        public bool SetWater(int x, int y, bool water)
        {
            if (x < 0 || y < 0 || x >= size || y >= size || IsWater(x, y) == water)
                return false;

            if (land == null || land.Length != size)
            {
                land = new string[size];
                for (var i = 0; i < size; i++)
                    land[i] = new string(Land, size);
            }

            var row = size - 1 - y;
            var line = land[row].PadRight(size, Land).ToCharArray();
            line[x] = water ? Water : Land;
            land[row] = new string(line);
            return true;
        }

        /// <summary>The cells of land.</summary>
        public int LandCells()
        {
            var count = 0;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (!IsWater(x, y))
                    count++;
            }

            return count;
        }

        /// <summary>Writes a whole map over this one: the editor and the move of the test map (task 5.5).</summary>
        public void Set(BattleMap map)
        {
            size = map.Size;
            var rows = new List<string>();
            if (map.Water != null)
            {
                var line = new char[size];
                for (var y = size - 1; y >= 0; y--)
                {
                    for (var x = 0; x < size; x++)
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
            [Tooltip("The corner of four cells under the middle of the 6 × 6 base.")]
            public Vector2Int corner;
            [Tooltip("Radius of the start area round that corner, cells. Starting value: 6.")]
            public int startRadius;
        }

        [Serializable]
        public struct SiteSpot
        {
            [Tooltip("The centre cell of the 3 × 3 place.")]
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
