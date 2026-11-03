using System;
using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Views;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Draws the cells: a texel per cell on one quad, the island under it and the water around. A cell that changed side turns
    /// to its colour with a short flash; the cells of an enclosed area wait for a wave that runs from where the ring closed.
    /// </summary>
    public sealed class MapView : ILateTickable, IDisposable
    {
        private static readonly int CellTexId = Shader.PropertyToID("_CellTex");
        private static readonly int CellsId = Shader.PropertyToID("_Cells");
        private static readonly int GridStrengthId = Shader.PropertyToID("_GridStrength");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleDebug _debug;
        private readonly BattleWorld _world;
        private readonly List<int> _active = new List<int>();
        private readonly List<Material> _materials = new List<Material>();

        private BattleSimulation _simulation;
        private Texture2D _texture;
        private Material _cells;
        private Color32[] _pixels;
        private Side[] _shown;
        private Color[] _from;
        private float[] _start;
        private float[] _delay;
        private bool[] _animating;
        private int _version;
        private int _captures;

        public MapView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleDebug debug, BattleWorld world)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _debug = debug;
            _world = world;
            _battle.OnStarted += Take;
        }

        public void LateTick()
        {
            if (_simulation == null || _battle.Current?.Simulation != _simulation)
                return;

            var now = Time.time;
            QueueWave(now);
            if (_simulation.Grid.Version != _version)
                StartChanged(now);

            if (_active.Count > 0)
                Animate(now);

            _cells.SetFloat(GridStrengthId, _debug.ShowDebug ? 0.6f : 0f);
        }

        public void Dispose()
        {
            _battle.OnStarted -= Take;
            foreach (var material in _materials)
                UnityEngine.Object.Destroy(material);
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
        }

        private void Take(BattleRunner runner)
        {
            _simulation = runner.Simulation;
            var size = _simulation.Grid.Size;
            if (_texture == null || _texture.width != size)
                Build(size);

            for (var i = 0; i < _shown.Length; i++)
            {
                _shown[i] = _simulation.Grid[i];
                _pixels[i] = ColorOf(_shown[i]);
                _delay[i] = -1f;
                _animating[i] = false;
            }

            _active.Clear();
            _version = _simulation.Grid.Version;
            _captures = _simulation.Captures.Count;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        private void Build(int size)
        {
            var cells = size * size;
            _pixels = new Color32[cells];
            _shown = new Side[cells];
            _from = new Color[cells];
            _start = new float[cells];
            _delay = new float[cells];
            _animating = new bool[cells];
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
            _texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Cells",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var mapSize = _rules.MapSize;
            _cells = Copy(_view.CellsMaterial);
            _cells.SetTexture(CellTexId, _texture);
            _cells.SetFloat(CellsId, size);
            _world.Surface.sharedMaterial = _cells;
            _world.Surface.transform.localPosition = new Vector3(mapSize / 2f, 0f, mapSize / 2f);
            _world.Surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _world.Surface.transform.localScale = new Vector3(mapSize, mapSize, 1f);

            var depth = _view.IslandDepth;
            _world.Island.sharedMaterial = Flat(_view.IslandColor);
            _world.Island.transform.localPosition = new Vector3(mapSize / 2f, -depth / 2f - 0.001f, mapSize / 2f);
            _world.Island.transform.localScale = new Vector3(mapSize, depth, mapSize);

            // water to the horizon: far wider than any view of the orthographic camera
            _world.Water.sharedMaterial = Flat(_view.WaterColor);
            _world.Water.transform.localPosition = new Vector3(mapSize / 2f, -_view.WaterDepth, mapSize / 2f);
            _world.Water.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _world.Water.transform.localScale = new Vector3(mapSize * 20f, mapSize * 20f, 1f);
        }

        /// <summary>The cells of new enclosures wait for the wave: the farther from the closing of the ring, the later.</summary>
        private void QueueWave(float now)
        {
            var captures = _simulation.Captures;
            for (; _captures < captures.Count; _captures++)
            {
                var capture = captures[_captures];
                var size = _simulation.Grid.Size;
                var originX = capture.OriginX / (float)BattleSettings.UnitsPerCell;
                var originY = capture.OriginY / (float)BattleSettings.UnitsPerCell;
                var farthest = 0.001f;
                foreach (var cell in capture.Cells)
                    farthest = Mathf.Max(farthest, Distance(cell, size, originX, originY));

                foreach (var cell in capture.Cells)
                    _delay[cell] = Distance(cell, size, originX, originY) / farthest * _view.WaveSeconds;
            }
        }

        private void StartChanged(float now)
        {
            var grid = _simulation.Grid;
            for (var i = 0; i < _shown.Length; i++)
            {
                if (grid[i] == _shown[i])
                    continue;

                if (!_animating[i])
                    _active.Add(i);

                _animating[i] = true;

                _from[i] = _pixels[i];
                _shown[i] = grid[i];
                _start[i] = now + Mathf.Max(0f, _delay[i]);
                _delay[i] = -1f;
            }

            _version = grid.Version;
        }

        private void Animate(float now)
        {
            var flash = _view.FlashSeconds;
            for (var a = _active.Count - 1; a >= 0; a--)
            {
                var i = _active[a];
                var t = (now - _start[i]) / flash;
                if (t < 0f)
                    continue;

                var target = ColorOf(_shown[i]);
                if (t >= 1f)
                {
                    _pixels[i] = target;
                    _animating[i] = false;
                    // the last one was already seen in this backward pass: it takes the free place
                    _active[a] = _active[_active.Count - 1];
                    _active.RemoveAt(_active.Count - 1);
                    continue;
                }

                var color = Color.Lerp(_from[i], target, t);
                _pixels[i] = Color.Lerp(color, Color.white, _view.FlashStrength * (1f - t));
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        private Color32 ColorOf(Side side) => side switch
        {
            Side.Player => _view.PlayerColor,
            Side.Enemy => _view.EnemyColor,
            _ => _view.NeutralColor,
        };

        private static float Distance(int cell, int size, float x, float y)
        {
            var dx = cell % size + 0.5f - x;
            var dy = cell / size + 0.5f - y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private Material Flat(Color color)
        {
            var material = Copy(_view.FlatMaterial);
            material.SetColor(BaseColorId, color);
            return material;
        }

        private Material Copy(Material source)
        {
            var material = new Material(source);
            _materials.Add(material);
            return material;
        }
    }
}
