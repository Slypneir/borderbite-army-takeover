using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Data
{
    /// <summary>
    /// An enclosed area a side took at once: its cells, the place the ring closed (the hero of the side) and the middle of the
    /// area, both in units, for the wave, the sound and «+N%» of the drawing.
    /// </summary>
    public sealed class Capture
    {
        public Capture(int tick, Side side, int[] cells, int originX, int originY, int centerX, int centerY, int mapTotal)
        {
            Tick = tick;
            Side = side;
            Cells = cells;
            OriginX = originX;
            OriginY = originY;
            CenterX = centerX;
            CenterY = centerY;
            MapTotal = mapTotal;
        }

        public int Tick { get; }
        public Side Side { get; }
        public int[] Cells { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int CenterX { get; }
        public int CenterY { get; }
        public int MapTotal { get; }

        /// <summary>How many percent of the map the area added, rounded, at least 1.</summary>
        public int Percent => System.Math.Max(1, (Cells.Length * 100 + MapTotal / 2) / MapTotal);
    }
}
