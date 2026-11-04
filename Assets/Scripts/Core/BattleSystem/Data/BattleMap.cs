using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;

namespace Core.BattleSystem.Data
{
    /// <summary>
    /// The place of a battle (layer 5 spec 2.2–2.3): a square of cells, land or water, the two bases with their start areas,
    /// the places to build (an empty one, or with a barracks of a side for special levels), the piles lying at the start and the
    /// rocks, which only the drawing reads. Water is like the sea past the edge: nobody walks into it, nobody paints it, it is not
    /// in the shares and an area touching it is not enclosed. Only the place: the numbers of a level stay in the settings (Т-Р5).
    /// </summary>
    public sealed class BattleMap
    {
        public readonly int Size;
        /// <summary>By cell, row by row from the lower left corner; null when the whole square is land.</summary>
        public readonly bool[] Water;
        /// <summary>The bases by side: [Player] and [Enemy]; [Neutral] stays empty.</summary>
        public readonly MapBase[] Bases = new MapBase[3];
        public readonly List<MapSite> Sites = new List<MapSite>();
        /// <summary>The piles at the start, in the order they are laid: the order of their ids.</summary>
        public readonly List<MapPile> Piles = new List<MapPile>();
        public readonly List<MapRock> Rocks = new List<MapRock>();

        public BattleMap(int size, bool[] water = null)
        {
            Size = size;
            Water = water;
        }

        public bool IsWater(int x, int y) => Water != null && Water[y * Size + x];

        /// <summary>Water, or off the map: the sea.</summary>
        public bool IsWaterOrOff(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size || IsWater(x, y);

        /// <summary>
        /// The square island of all land of stage 1 from the settings (spec 2.1): the bases <see cref="BattleSettings.StartOffsetCells"/>
        /// from two corners, the places of <see cref="BattleSettings.SiteCells"/>, the start piles of the player and the same mirrored
        /// for the enemy. A battle with no map of its own is fought here.
        /// </summary>
        public static BattleMap Square(BattleSettings settings)
        {
            var size = settings.MapCells;
            var map = new BattleMap(size);
            var near = settings.StartOffsetCells;
            var far = size - settings.StartOffsetCells;
            map.Bases[(int)Side.Player] = new MapBase(near, near, settings.StartRadiusCells);
            map.Bases[(int)Side.Enemy] = new MapBase(far, far, settings.StartRadiusCells);
            for (var i = 0; i + 1 < settings.SiteCells.Length; i += 2)
                map.Sites.Add(new MapSite(settings.SiteCells[i], settings.SiteCells[i + 1], Side.Neutral));

            var last = size - 1;
            foreach (var mirrored in new[] { false, true })
            {
                Lay(map, settings.StartGoldCells, ResourceKind.Gold, mirrored, last);
                Lay(map, settings.StartWoodCells, ResourceKind.Wood, mirrored, last);
            }

            return map;
        }

        private static void Lay(BattleMap map, int[] cells, ResourceKind kind, bool mirrored, int last)
        {
            for (var i = 0; i + 1 < cells.Length; i += 2)
            {
                var x = mirrored ? last - cells[i] : cells[i];
                var y = mirrored ? last - cells[i + 1] : cells[i + 1];
                map.Piles.Add(new MapPile(kind, x, y));
            }
        }
    }

    /// <summary>A base: the corner of four cells under its middle, and the radius of its start area round that corner, cells.</summary>
    public readonly struct MapBase
    {
        public readonly int X;
        public readonly int Y;
        public readonly int StartRadius;

        public MapBase(int x, int y, int startRadius)
        {
            X = x;
            Y = y;
            StartRadius = startRadius;
        }
    }

    /// <summary>A 3 × 3 place to build by its centre cell; <see cref="Barracks"/> is a side whose barracks stands on it from the start.</summary>
    public readonly struct MapSite
    {
        public readonly int X;
        public readonly int Y;
        public readonly Side Barracks;

        public MapSite(int x, int y, Side barracks)
        {
            X = x;
            Y = y;
            Barracks = barracks;
        }
    }

    /// <summary>A pile of gold or a bundle of wood lying on a cell from the start, of the usual portion.</summary>
    public readonly struct MapPile
    {
        public readonly ResourceKind Kind;
        public readonly int X;
        public readonly int Y;

        public MapPile(ResourceKind kind, int x, int y)
        {
            Kind = kind;
            X = x;
            Y = y;
        }
    }

    /// <summary>A rock of decor (Т-Р3): its cell, size 0–2 (small, medium, large) and turn in degrees. The battle ignores it.</summary>
    public readonly struct MapRock
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Size;
        public readonly int Yaw;

        public MapRock(int x, int y, int size, int yaw)
        {
            X = x;
            Y = y;
            Size = size;
            Yaw = yaw;
        }
    }
}
