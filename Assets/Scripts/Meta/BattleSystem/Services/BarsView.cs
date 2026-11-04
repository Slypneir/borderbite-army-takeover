using System;
using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Health bars over the heroes and every soldier (task 4.9, spec T12): the fill is the share of health left, in the colour of
    /// the side, on a dark base with a rim (light for the player's units, dark for the enemy's). The player's hero always shows
    /// its bar; the enemy's hero and any soldier only after a blow, until nothing hurt it for a while (5 s). Soldiers' bars are
    /// smaller. All bars are one mesh rebuilt each frame, facing the camera: one draw for up to 60 soldiers and two heroes.
    /// </summary>
    public sealed class BarsView : ILateTickable, IDisposable
    {
        private const float HeroHeight = 2.15f;
        private const float SoldierHeight = 1.45f;
        private static readonly Vector2 HeroSize = new Vector2(1.4f, 0.2f);
        private static readonly Vector2 SoldierSize = new Vector2(0.8f, 0.13f);
        private const float Rim = 0.04f;
        private static readonly Color Base = new Color(0.1f, 0.11f, 0.14f);

        private readonly BattleViewConfig _view;
        private readonly IBattleDriver _battle;
        private readonly HeroView _heroes;
        private readonly SoldiersView _soldiers;
        private readonly Camera _camera;
        private readonly BattleScreen _screen;
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<int> _gone = new List<int>();
        private readonly HashSet<int> _alive = new HashSet<int>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();
        private Mesh _mesh;
        private Material _material;
        private GameObject _holder;
        private BattleSimulation _simulation;

        public BarsView(BattleViewConfig view, IBattleDriver battle, HeroView heroes, SoldiersView soldiers, Camera mainCamera,
            BattleScreen screen)
        {
            _screen = screen;
            _view = view;
            _battle = battle;
            _heroes = heroes;
            _soldiers = soldiers;
            _camera = mainCamera;
        }

        public void LateTick()
        {
            var simulation = _battle.Current?.Simulation;
            if (simulation == null || _view.BarsShader == null)
                return;

            if (_mesh == null)
                Build();

            // under a window the bars go with every other mark over the world
            _holder.SetActive(!_screen.Covered);

            if (simulation != _simulation)
            {
                _simulation = simulation;
                _tracks.Clear();
            }

            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
            var now = Time.time;
            var right = _camera.transform.right;
            var up = _camera.transform.up;

            var heroes = simulation.Heroes;
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (!hero.Alive)
                    continue;

                var key = -1 - (int)hero.Side;
                var full = simulation.Settings.HeroHealthOf(hero.Side);
                var shown = hero.Side == Side.Player || Hurt(key, hero.Health, full, now);
                if (shown)
                    Bar(_heroes.PositionOf(hero.Side) + Vector3.up * HeroHeight, HeroSize, hero.Health / (float)full, hero.Side, right, up);
            }

            _alive.Clear();
            foreach (var soldier in simulation.Soldiers)
            {
                _alive.Add(soldier.Id);
                if (!Hurt(soldier.Id, soldier.Health, 0, now) || !_soldiers.TryGetPosition(soldier.Id, out var position))
                    continue;

                var track = _tracks[soldier.Id];
                Bar(position + Vector3.up * SoldierHeight, SoldierSize, soldier.Health / (float)Mathf.Max(1, track.Full), soldier.Side, right, up);
            }

            // the dead leave their tracks; heroes keep theirs (negative keys)
            _gone.Clear();
            foreach (var key in _tracks.Keys)
            {
                if (key >= 0 && !_alive.Contains(key))
                    _gone.Add(key);
            }

            foreach (var key in _gone)
                _tracks.Remove(key);

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
            _mesh.bounds = new Bounds(_mesh.bounds.center, _mesh.bounds.size + Vector3.one);
        }

        public void Dispose()
        {
            if (_mesh != null)
                Object.Destroy(_mesh);
            if (_material != null)
                Object.Destroy(_material);
            if (_holder != null)
                Object.Destroy(_holder);
        }

        /// <summary>True while the unit was hurt within the hold time; the first health seen is its full health when none is given.</summary>
        private bool Hurt(int key, int health, int full, float now)
        {
            if (!_tracks.TryGetValue(key, out var track))
            {
                track = new Track { Full = full > 0 ? full : health, Health = health, HurtAt = -1000f };
                _tracks[key] = track;
            }

            if (health < track.Health)
                track.HurtAt = now;
            track.Health = health;
            if (full > 0)
                track.Full = full;
            return now - track.HurtAt < _view.BarHoldSeconds;
        }

        private void Bar(Vector3 centre, Vector2 size, float share, Side side, Vector3 right, Vector3 up)
        {
            var rim = side == Side.Player ? _view.OutlineColor(Side.Player) : _view.OutlineColor(Side.Enemy);
            Quad(centre, size + Vector2.one * (Rim * 2f), 1f, rim, right, up);
            Quad(centre, size, 1f, Base, right, up);
            Quad(centre, size - Vector2.one * Rim, Mathf.Clamp01(share), _view.SoldierColor(side), right, up);
        }

        /// <summary>A quad facing the camera, filled from the left by <paramref name="share"/> of its width.</summary>
        private void Quad(Vector3 centre, Vector2 size, float share, Color color, Vector3 right, Vector3 up)
        {
            if (share <= 0f)
                return;

            var left = centre - right * (size.x / 2f);
            var a = left - up * (size.y / 2f);
            var b = left + up * (size.y / 2f);
            var width = right * (size.x * share);
            var start = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(b + width);
            _vertices.Add(a + width);
            Color32 c = color;
            for (var i = 0; i < 4; i++)
                _colors.Add(c);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
        }

        private void Build()
        {
            _mesh = new Mesh { name = "Bars" };
            _mesh.MarkDynamic();
            _material = new Material(_view.BarsShader);
            _holder = new GameObject("Bars");
            _holder.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private sealed class Track
        {
            public int Full;
            public int Health;
            public float HurtAt;
        }
    }
}
