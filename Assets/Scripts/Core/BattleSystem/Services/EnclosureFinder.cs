using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// The enclosure rule: an area of cells not of a side, connected by the four sides of a cell, that does not touch the edge of
    /// the map is surrounded by the side and becomes its own, protected cells excepted. One pass over the map per side, in the
    /// order of the indices: the same map gives the same areas in the same order.
    /// </summary>
    public sealed class EnclosureFinder
    {
        private readonly int[] _mark;
        private readonly int[] _queue;
        private int _generation;

        public EnclosureFinder(int cells)
        {
            _mark = new int[cells];
            _queue = new int[cells];
        }

        /// <summary>Gives <paramref name="side"/> every area it surrounds; an area over <paramref name="maxCells"/> stays as it is.</summary>
        public void Enclose(CellGrid grid, Side side, int maxCells, List<int[]> taken)
        {
            _generation++;
            var size = grid.Size;
            for (var start = 0; start < grid.Total; start++)
            {
                if (grid[start] == side || _mark[start] == _generation)
                    continue;

                var count = Flood(grid, side, start, size, out var touchesEdge);
                if (touchesEdge || count > maxCells)
                    continue;

                var cells = new List<int>(count);
                for (var i = 0; i < count; i++)
                {
                    if (grid.Paint(_queue[i], side))
                        cells.Add(_queue[i]);
                }

                if (cells.Count > 0)
                    taken.Add(cells.ToArray());
            }
        }

        private int Flood(CellGrid grid, Side side, int start, int size, out bool touchesEdge)
        {
            touchesEdge = false;
            var head = 0;
            var tail = 0;
            _queue[tail++] = start;
            _mark[start] = _generation;
            while (head < tail)
            {
                var index = _queue[head++];
                var x = index % size;
                var y = index / size;
                if (x == 0 || y == 0 || x == size - 1 || y == size - 1)
                    touchesEdge = true;

                if (x > 0) Visit(grid, side, index - 1, ref tail);
                if (x < size - 1) Visit(grid, side, index + 1, ref tail);
                if (y > 0) Visit(grid, side, index - size, ref tail);
                if (y < size - 1) Visit(grid, side, index + size, ref tail);
            }

            return tail;
        }

        private void Visit(CellGrid grid, Side side, int index, ref int tail)
        {
            if (_mark[index] == _generation || grid[index] == side)
                return;

            _mark[index] = _generation;
            _queue[tail++] = index;
        }
    }
}
