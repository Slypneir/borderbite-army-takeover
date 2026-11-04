using System;
using System.Collections.Generic;
using Meta.BattleSystem.Configuration;
using UnityEngine;

namespace Meta.BattleSystem.Utils
{
    /// <summary>
    /// Strews the decor of the 3D artist over a copy of the grass, from the seed of the level: the same seed gives the same
    /// meadow. Per kind, in order: centres of groups by dart throwing, no closer than the spacing (30 tries each), the first
    /// piece of a group in its centre and the rest around it; the edges wrap, as the grass is seamless. After
    /// <c>scatter(seed)</c> of the generator (<c>models/borderbite/_grass.js</c>); an exact match is not needed.
    /// </summary>
    public static class GrassScatter
    {
        private const int Tries = 30;

        /// <summary>The grass with decor, rows from the bottom as Unity keeps them; null when the textures are missing.</summary>
        public static Color32[] Island(SurfaceConfig config, uint seed, out int size)
        {
            size = 0;
            if (config == null || config.GrassBase == null)
                return null;

            size = config.GrassBase.width;
            var pixels = config.GrassBase.GetPixels32();
            if (config.GrassDecor == null)
                return pixels;

            var decor = config.GrassDecor.GetPixels32();
            var decorWidth = config.GrassDecor.width;
            var random = new Mulberry32(seed);
            var area = (float)size / config.PixelsPerMeter * size / config.PixelsPerMeter;
            var centres = new List<Vector2>();
            var ofKind = new List<int>();
            for (var k = 0; k < config.Kinds.Length; k++)
            {
                var kind = config.Kinds[k];
                ofKind.Clear();
                for (var s = 0; s < config.Stamps.Length; s++)
                {
                    if (config.Stamps[s].Kind == k)
                        ofKind.Add(s);
                }

                if (ofKind.Count == 0)
                    continue;

                centres.Clear();
                var groups = Mathf.RoundToInt(kind.Density * area / ((kind.GroupMin + kind.GroupMax) / 2f));
                for (var g = 0; g < groups; g++)
                {
                    for (var t = 0; t < Tries; t++)
                    {
                        var centre = new Vector2(random.Next() * size, random.Next() * size);
                        if (!Far(centres, centre, kind.Spacing, size))
                            continue;

                        centres.Add(centre);
                        var count = kind.GroupMin + (int)(random.Next() * (kind.GroupMax - kind.GroupMin + 1));
                        for (var n = 0; n < count; n++)
                        {
                            var stamp = config.Stamps[ofKind[(int)(random.Next() * ofKind.Count)]];
                            var angle = random.Next() * Mathf.PI * 2f;
                            var distance = n > 0 ? kind.Radius * (0.4f + 0.6f * random.Next()) : 0f;
                            var x = Mathf.FloorToInt(centre.x + Mathf.Cos(angle) * distance - stamp.Rect.width / 2f);
                            var y = Mathf.FloorToInt(centre.y + Mathf.Sin(angle) * distance - stamp.Rect.height / 2f);
                            Put(pixels, size, decor, decorWidth, stamp.Rect, x, y);
                        }

                        break;
                    }
                }
            }

            return pixels;
        }

        private static bool Far(List<Vector2> centres, Vector2 point, float spacing, int size)
        {
            foreach (var other in centres)
            {
                var dx = Mathf.Abs(other.x - point.x);
                var dy = Mathf.Abs(other.y - point.y);
                dx = Mathf.Min(dx, size - dx);
                dy = Mathf.Min(dy, size - dy);
                if (dx * dx + dy * dy < spacing * spacing)
                    return false;
            }

            return true;
        }

        private static void Put(Color32[] pixels, int size, Color32[] decor, int decorWidth, RectInt rect, int x, int y)
        {
            for (var j = 0; j < rect.height; j++)
            for (var i = 0; i < rect.width; i++)
            {
                var source = decor[(rect.y + j) * decorWidth + rect.x + i];
                if (source.a == 0)
                    continue;

                var tx = ((x + i) % size + size) % size;
                var ty = ((y + j) % size + size) % size;
                pixels[ty * size + tx] = source;
            }
        }

        /// <summary>The random numbers of the generator's scatter, 0..1.</summary>
        private struct Mulberry32
        {
            private uint _state;

            public Mulberry32(uint seed) => _state = seed;

            public float Next()
            {
                unchecked
                {
                    _state += 0x6D2B79F5u;
                    var t = _state;
                    t = (t ^ (t >> 15)) * (t | 1u);
                    t ^= t + (t ^ (t >> 7)) * (t | 61u);
                    return (float)((t ^ (t >> 14)) / 4294967296.0);
                }
            }
        }
    }
}
