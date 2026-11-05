using System.Collections.Generic;
using Core.BattleSystem.Enums;
using UnityEngine;

namespace Meta.BattleSystem.Utils
{
    /// <summary>
    /// The land of the sides as borders of «Civilization V» (task 4.21, the 3D artist's surface README «Земля сторон»): for each
    /// side a signed distance in cells at the middle of every cell, + inside, − outside — the distance to the nearest cell of the
    /// other state less 0.5 — on a grid of (width + 2) by (height + 2) with a ring of neutral cells around the map, so the line also
    /// runs along the shore; then a gauss blur (σ 1.1 cells) rounds the corners. Kept in a texture of that size, bilinear: red the player,
    /// green the enemy, ±<see cref="Range"/> cells over a byte. Integers of the battle never see it.
    /// </summary>
    public sealed class TerritoryField
    {
        /// <summary>The distance kept, cells: the fill fades over 2 m (4 cells), further on it is the same.</summary>
        public const float Range = 4f;
        private const float Sigma = 1.1f;

        private readonly int _width;
        private readonly int _height;
        private readonly int _side;
        private readonly int _rows;
        private readonly float[] _distance;
        private readonly float[] _blurred;
        private readonly bool[] _inside;
        private readonly Color32[] _pixels;
        private readonly (int X, int Y, float D)[] _offsets;
        private readonly float[] _kernel;

        public TerritoryField(int width, int height)
        {
            _width = width;
            _height = height;
            _side = width + 2;
            _rows = height + 2;
            var cells = _side * _rows;
            _distance = new float[cells];
            _blurred = new float[cells];
            _inside = new bool[cells];
            _pixels = new Color32[cells];
            Texture = new Texture2D(_side, _rows, TextureFormat.RGBA32, false, true)
            {
                name = "Territory",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            // the cells round one, nearest first, as far as the distance is kept
            var reach = Mathf.CeilToInt(Range + 1f);
            var offsets = new List<(int, int, float)>();
            for (var y = -reach; y <= reach; y++)
            for (var x = -reach; x <= reach; x++)
            {
                var d = Mathf.Sqrt(x * x + y * y);
                if ((x != 0 || y != 0) && d <= reach)
                    offsets.Add((x, y, d));
            }

            offsets.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            _offsets = offsets.ToArray();

            var radius = Mathf.CeilToInt(Sigma * 3f);
            _kernel = new float[radius * 2 + 1];
            var sum = 0f;
            for (var i = -radius; i <= radius; i++)
            {
                _kernel[i + radius] = Mathf.Exp(-(i * i) / (2f * Sigma * Sigma));
                sum += _kernel[i + radius];
            }

            for (var i = 0; i < _kernel.Length; i++)
                _kernel[i] /= sum;
        }

        public Texture2D Texture { get; }

        /// <summary>Builds the fields of both sides from the owners shown now, a cell per index of the map, and uploads them.</summary>
        public void Build(Side[] owners)
        {
            Field(owners, Side.Player);
            for (var i = 0; i < _blurred.Length; i++)
                _pixels[i].r = Encode(_blurred[i]);
            Field(owners, Side.Enemy);
            for (var i = 0; i < _blurred.Length; i++)
            {
                _pixels[i].g = Encode(_blurred[i]);
                _pixels[i].a = 255;
            }

            Texture.SetPixels32(_pixels);
            Texture.Apply(false);
        }

        private void Field(Side[] owners, Side side)
        {
            for (var y = 0; y < _rows; y++)
            for (var x = 0; x < _side; x++)
            {
                var mx = x - 1;
                var my = y - 1;
                _inside[y * _side + x] = mx >= 0 && my >= 0 && mx < _width && my < _height && owners[my * _width + mx] == side;
            }

            for (var y = 0; y < _rows; y++)
            for (var x = 0; x < _side; x++)
            {
                var index = y * _side + x;
                var inside = _inside[index];
                var found = Range + 0.5f;
                foreach (var (dx, dy, d) in _offsets)
                {
                    var ox = x + dx;
                    var oy = y + dy;
                    // past the ring everything is outside, as the ring itself
                    var other = ox >= 0 && oy >= 0 && ox < _side && oy < _rows && _inside[oy * _side + ox];
                    if (other == inside)
                        continue;

                    found = d;
                    break;
                }

                var distance = Mathf.Min(found - 0.5f, Range);
                _distance[index] = inside ? distance : -distance;
            }

            Blur();
        }

        /// <summary>A separable gauss blur, edges clamped: the corners of the cells round off.</summary>
        private void Blur()
        {
            var radius = _kernel.Length / 2;
            for (var y = 0; y < _rows; y++)
            for (var x = 0; x < _side; x++)
            {
                var sum = 0f;
                for (var k = -radius; k <= radius; k++)
                    sum += _distance[y * _side + Mathf.Clamp(x + k, 0, _side - 1)] * _kernel[k + radius];
                _blurred[y * _side + x] = sum;
            }

            for (var x = 0; x < _side; x++)
            for (var y = 0; y < _rows; y++)
            {
                var sum = 0f;
                for (var k = -radius; k <= radius; k++)
                    sum += _blurred[Mathf.Clamp(y + k, 0, _rows - 1) * _side + x] * _kernel[k + radius];
                _distance[y * _side + x] = sum;
            }

            System.Array.Copy(_distance, _blurred, _distance.Length);
        }

        private static byte Encode(float cells) => (byte)Mathf.RoundToInt(Mathf.Clamp01(cells / Range * 0.5f + 0.5f) * 255f);
    }
}
