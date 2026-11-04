using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// The enclosure rule: an area of cells not of a side, connected by the four sides of a cell, that does not touch the edge of
    /// the map or water (layer 5 spec 2.2: water is the edge) is surrounded by the side and becomes its own, protected cells
    /// excepted. One pass over the map per side, in the
    /// order of the indices: the same map gives the same areas in the same order.
    /// </summary>
    public sealed class EnclosureFinder
    {
        private readonly int[] _mark;
        private readonly int[] _queue;
        private int _generation;
        private bool _touchesWater;

        public EnclosureFinder(int cells)
        {
            _mark = new int[cells];
            _queue = new int[cells];
        }

        /// <summary>
        /// Gives <paramref name="side"/> every area it surrounds; an area of more than <paramref name="maxCells"/> cells that are not
        /// protected stays as it is, whole (stage 1 spec 2.5).
        /// </summary>
        public void Enclose(CellGrid grid, Side side, int maxCells, List<int[]> taken, List<int> fromEnemy)
        {
            _generation++;
            var size = grid.Size;
            for (var start = 0; start < grid.Total; start++)
            {
                if (grid[start] == side || _mark[start] == _generation || grid.IsWater(start))
                    continue;

                var count = Flood(grid, side, start, size, out var touchesEdge, out var open);
                if (touchesEdge || open > maxCells)
                    continue;

                var cells = new List<int>(count);
                var lost = 0;
                var other = side == Side.Player ? Side.Enemy : Side.Player;
                for (var i = 0; i < count; i++)
                {
                    var was = grid[_queue[i]];
                    if (!grid.Paint(_queue[i], side))
                        continue;

                    cells.Add(_queue[i]);
                    if (was == other)
                        lost++;
                }

                if (cells.Count > 0)
                {
                    taken.Add(cells.ToArray());
                    fromEnemy.Add(lost);
                }
            }
        }

        private int Flood(CellGrid grid, Side side, int start, int size, out bool touchesEdge, out int open)
        {
            _touchesWater = false;
            touchesEdge = false;
            open = 0;
            var head = 0;
            var tail = 0;
            _queue[tail++] = start;
            _mark[start] = _generation;
            while (head < tail)
            {
                var index = _queue[head++];
                if (!grid.IsProtected(index))
                    open++;

                var x = index % size;
                var y = index / size;
                if (x == 0 || y == 0 || x == size - 1 || y == size - 1)
                    touchesEdge = true;

                if (x > 0) Visit(grid, side, index - 1, ref tail);
                if (x < size - 1) Visit(grid, side, index + 1, ref tail);
                if (y > 0) Visit(grid, side, index - size, ref tail);
                if (y < size - 1) Visit(grid, side, index + size, ref tail);
            }

            touchesEdge |= _touchesWater;
            return tail;
        }

        private void Visit(CellGrid grid, Side side, int index, ref int tail)
        {
            if (_mark[index] == _generation || grid[index] == side)
                return;

            if (grid.IsWater(index))
            {
                _touchesWater = true;
                return;
            }

            _mark[index] = _generation;
            _queue[tail++] = index;
        }
    }
}
