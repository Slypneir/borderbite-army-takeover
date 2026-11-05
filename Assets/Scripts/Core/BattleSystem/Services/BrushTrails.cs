using System.Collections.Generic;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// The brush follows the trail (owner, 5 November; spec 2.4, task 4.31): the middle of the brush of a hero or a soldier is not
    /// the unit but where it was <see cref="Structs.BattleSettings.BrushDelayTicks"/> ago, so the paint runs behind it along its
    /// path with every turn. A ring of the last positions in ticks per unit; a unit new, back or unseen for a tick starts its trail
    /// where it stands, and while the ring fills the oldest position leads. A unit that stands still is caught up by its trail.
    /// Integers only: the same paint on every device.
    /// </summary>
    public sealed class BrushTrails
    {
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<int> _gone = new List<int>();
        private readonly int _length;

        public BrushTrails(int delayTicks) => _length = System.Math.Max(0, delayTicks) + 1;

        /// <summary>The unit with a key stands at (x, y) on this tick.</summary>
        public void Record(int key, int x, int y, int tick)
        {
            if (!_tracks.TryGetValue(key, out var track))
            {
                track = new Track { X = new int[_length], Y = new int[_length], Seen = int.MinValue };
                _tracks.Add(key, track);
            }

            // a unit not seen on the last tick (just hired, back to life) starts its trail anew
            if (track.Seen != tick - 1)
                track.Count = 0;

            track.Head = (track.Head + 1) % _length;
            track.X[track.Head] = x;
            track.Y[track.Head] = y;
            track.Count = System.Math.Min(track.Count + 1, _length);
            track.Seen = tick;
        }

        /// <summary>The middle of the brush of the unit: where it was the delay ago, or the oldest of its trail.</summary>
        public void Point(int key, out int x, out int y)
        {
            var track = _tracks[key];
            var index = (track.Head - (track.Count - 1) + _length) % _length;
            x = track.X[index];
            y = track.Y[index];
        }

        /// <summary>The trails of the units not seen on this tick (dead, gone) are dropped.</summary>
        public void Forget(int tick)
        {
            _gone.Clear();
            foreach (var pair in _tracks)
            {
                if (pair.Value.Seen < tick)
                    _gone.Add(pair.Key);
            }

            foreach (var key in _gone)
                _tracks.Remove(key);
        }

        private sealed class Track
        {
            public int[] X;
            public int[] Y;
            public int Head;
            public int Count;
            public int Seen;
        }
    }
}
