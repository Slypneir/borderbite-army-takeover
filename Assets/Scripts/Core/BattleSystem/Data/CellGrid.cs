using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Data
{
    /// <summary>
    /// The square map of cells, row by row from the lower left corner: index = y * <see cref="Size"/> + x. Every cell is of one
    /// side; a protected cell (an aura of a building, layer 3) keeps its side against brushes and enclosures. A cell of water
    /// (layer 5) is neutral for ever: protected, out of the counts and the shares. The counts of the sides are kept on every
    /// change, <see cref="Version"/> grows with it: the drawing compares it instead of the cells.
    /// </summary>
    public sealed class CellGrid
    {
        private readonly Side[] _cells;
        private readonly bool[] _protected;
        private readonly bool[] _water;
        private readonly int[] _counts = new int[3];

        public CellGrid(int size, bool[] water = null)
        {
            Size = size;
            _cells = new Side[size * size];
            _protected = new bool[size * size];
            _water = water;
            LandTotal = _cells.Length;
            if (water != null)
            {
                foreach (var wet in water)
                {
                    if (wet)
                        LandTotal--;
                }
            }

            _counts[(int)Side.Neutral] = LandTotal;
        }

        public int Size { get; }

        public int Total => _cells.Length;

        /// <summary>The cells of land: the shares are of them.</summary>
        public int LandTotal { get; }

        /// <summary>Is there any water on the map: off, the rules skip every test of it.</summary>
        public bool HasWater => _water != null;

        public int Version { get; private set; }

        public Side this[int index] => _cells[index];

        public Side At(int x, int y) => _cells[y * Size + x];

        public bool IsProtected(int index) => _protected[index] || (_water != null && _water[index]);

        public bool IsWater(int index) => _water != null && _water[index];

        /// <summary>Water, or off the map: the sea.</summary>
        public bool IsWaterAt(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size || (_water != null && _water[y * Size + x]);

        public int Count(Side side) => _counts[(int)side];

        /// <summary>The share of the land of a side, in permille, rounded down.</summary>
        public int Permille(Side side) => (int)((long)_counts[(int)side] * 1000 / System.Math.Max(1, LandTotal));

        public bool IsEdge(int index)
        {
            var x = index % Size;
            var y = index / Size;
            return x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
        }

        /// <summary>Gives the cell to a side unless it is protected; true when the cell changed.</summary>
        public bool Paint(int index, Side side)
        {
            if (IsProtected(index) || _cells[index] == side)
                return false;

            Set(index, side);
            return true;
        }

        /// <summary>Gives the cell to a side even when it is protected, never water: the start of a battle and the commands of the developer.</summary>
        public void Set(int index, Side side)
        {
            var old = _cells[index];
            if (old == side || IsWater(index))
                return;

            _counts[(int)old]--;
            _counts[(int)side]++;
            _cells[index] = side;
            Version++;
        }

        public void SetProtected(int index, bool value)
        {
            _protected[index] = value;
            Version++;
        }
    }
}
