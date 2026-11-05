using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// Land cut off from its side goes back to grass (owner, 4 November): a piece of a side's colour that no longer touches,
    /// cell to cell, any of its anchors — the protected aura of its base or of a standing barracks — becomes neutral. Within the
    /// rules of enclosures: a piece larger than their limit stays as it is, whole. Cells by four neighbours, in the order of the
    /// grid: the same on every device.
    /// </summary>
    public sealed class OrphanFinder
    {
        private readonly int[] _mark;
        private readonly int[] _queue;
        private int _generation;

        public OrphanFinder(int cells)
        {
            _mark = new int[cells];
            _queue = new int[cells];
        }

        /// <summary>Takes the cut off pieces of <paramref name="side"/> back to grass; each piece is added to <paramref name="dropped"/>.</summary>
        public void Drop(CellGrid grid, Side side, int maxCells, List<int[]> dropped)
        {
            _generation++;

            // the land joined to an anchor of the side
            var tail = 0;
            for (var i = 0; i < grid.Total; i++)
            {
                if (grid[i] == side && grid.IsProtected(i))
                {
                    _mark[i] = _generation;
                    _queue[tail++] = i;
                }
            }

            // no anchor at all (its base has fallen): the battle is decided, nothing to drop
            if (tail == 0)
                return;

            Flood(grid, side, 0, ref tail);

            // every other piece of the side is cut off
            for (var start = 0; start < grid.Total; start++)
            {
                if (grid[start] != side || _mark[start] == _generation)
                    continue;

                var from = tail;
                _mark[start] = _generation;
                _queue[tail++] = start;
                Flood(grid, side, from, ref tail);
                var count = tail - from;
                if (count > maxCells)
                    continue;

                var cells = new int[count];
                for (var i = 0; i < count; i++)
                {
                    cells[i] = _queue[from + i];
                    grid.Paint(cells[i], Side.Neutral);
                }

                dropped.Add(cells);
            }
        }

        private void Flood(CellGrid grid, Side side, int head, ref int tail)
        {
            var width = grid.Width;
            var height = grid.Height;
            while (head < tail)
            {
                var index = _queue[head++];
                var x = index % width;
                var y = index / width;
                if (x > 0) Visit(grid, side, index - 1, ref tail);
                if (x < width - 1) Visit(grid, side, index + 1, ref tail);
                if (y > 0) Visit(grid, side, index - width, ref tail);
                if (y < height - 1) Visit(grid, side, index + width, ref tail);
            }
        }

        private void Visit(CellGrid grid, Side side, int index, ref int tail)
        {
            if (_mark[index] == _generation || grid[index] != side)
                return;

            _mark[index] = _generation;
            _queue[tail++] = index;
        }
    }
}
