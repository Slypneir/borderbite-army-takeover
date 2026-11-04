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
    /// smaller. The fill is cut into segments, a point each while the full health is 10 or less (a blow takes one away, read
    /// without numbers, like hearts), five points each above (battle-marks card of the UI, 4 November); 2 units apart. All bars
    /// are one mesh rebuilt each frame, facing the camera: one draw for up to 60 soldiers and two heroes.
    /// A hit (task 4.22, battle-marks card «Попадание по полоске», owner's «A + C»): the lost segment goes white, the bar jerks
    /// sideways (0.15 s), the segment jumps up and swells (0.06–0.16 s), then breaks into voxels of the fill that fly and fade
    /// by 0.86 s, and a «−1» rises (<see cref="Struck"/>, drawn by HitNumbersView). By the player's units it is restrained, by
    /// the enemy's juicier: a bigger jump, more shards, a golden number. The jerk can be turned off (BattleViewConfig.barShake).
    /// </summary>
    public sealed class BarsView : ILateTickable, IDisposable
    {
        private const float HeroHeight = 2.15f;
        private const float SoldierHeight = 1.45f;
        private static readonly Vector2 HeroSize = new Vector2(1.4f, 0.2f);
        private static readonly Vector2 SoldierSize = new Vector2(0.8f, 0.13f);
        private const float Rim = 0.04f;
        /// <summary>The gap between segments, meters: 2 units of the UI over a hero at the default camera.</summary>
        private const float Gap = 0.03f;
        private const int PointsPerSegmentAbove = 5;
        private const int MostPointSegments = 10;
        private static readonly Color Base = new Color(0.1f, 0.11f, 0.14f);
        private static readonly Color HitWhite = new Color(1f, 0.98f, 0.92f);
        // a unit of the canvas over a hero at the default camera, meters (the hero bar of 1.4 m is about 65 units)
        private const float UiUnit = 0.0215f;
        private const float WhiteUntil = 0.06f;
        private const float JumpUntil = 0.16f;
        private const float JerkUntil = 0.15f;
        private const float ShardsUntil = 0.86f;
        private const float Gravity = 4f;

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
        private readonly List<Hit> _hits = new List<Hit>();
        private uint _random = 0x2545F491u;
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

        /// <summary>A unit lost a point: where over the world (the bar), and whether it is the enemy's — for the «−1».</summary>
        public event Action<Vector3, bool> Struck;

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
                _hits.Clear();
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
                var key = -1 - (int)hero.Side;
                if (!hero.Alive)
                {
                    Fell(key);
                    continue;
                }

                var full = simulation.Settings.HeroHealthOf(hero.Side);
                var centre = _heroes.PositionOf(hero.Side) + Vector3.up * HeroHeight;
                var shown = Hurt(key, hero.Health, full, now, centre, HeroSize, hero.Side) || hero.Side == Side.Player;
                if (shown)
                    Bar(centre + right * Jerk(key), HeroSize, hero.Health, full, hero.Side, right, up);
            }

            _alive.Clear();
            foreach (var soldier in simulation.Soldiers)
            {
                _alive.Add(soldier.Id);
                if (!_soldiers.TryGetPosition(soldier.Id, out var position))
                    continue;

                var centre = position + Vector3.up * SoldierHeight;
                // the full health from the rules: a soldier hit before the view first saw it still shows its loss
                if (!Hurt(soldier.Id, soldier.Health, simulation.Settings.SoldierHealthOf(soldier.Side), now, centre, SoldierSize, soldier.Side))
                    continue;

                var track = _tracks[soldier.Id];
                Bar(centre + right * Jerk(soldier.Id), SoldierSize, soldier.Health, track.Full, soldier.Side, right, up);
            }

            // the dead leave their tracks; heroes keep theirs (negative keys)
            _gone.Clear();
            foreach (var key in _tracks.Keys)
            {
                if (key >= 0 && !_alive.Contains(key))
                    _gone.Add(key);
            }

            foreach (var key in _gone)
            {
                Fell(key);
                _tracks.Remove(key);
            }

            DrawHits(Time.deltaTime, right, up);

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

        /// <summary>
        /// True while the unit was hurt within the hold time; the first health seen is its full health when none is given. A loss
        /// starts the effect of a hit at the bar.
        /// </summary>
        private bool Hurt(int key, int health, int full, float now, Vector3 centre, Vector2 size, Side side)
        {
            if (!_tracks.TryGetValue(key, out var track))
            {
                // a known full health starts the track full: a loss before the first look is still a hit
                track = new Track { Full = full > 0 ? full : health, Health = full > 0 ? full : health, HurtAt = -1000f };
                _tracks[key] = track;
            }

            if (full > 0)
                track.Full = full;
            track.Centre = centre;
            track.Size = size;
            track.Side = side;
            if (health < track.Health)
            {
                track.HurtAt = now;
                for (var lost = track.Health - 1; lost >= Mathf.Max(0, health); lost--)
                    Lose(key, centre, size, track.Full, lost, side);
            }

            return now - track.HurtAt < _view.BarHoldSeconds;
        }

        private void Bar(Vector3 centre, Vector2 size, int health, int full, Side side, Vector3 right, Vector3 up)
        {
            var rim = side == Side.Player ? _view.OutlineColor(Side.Player) : _view.OutlineColor(Side.Enemy);
            Quad(centre, size + Vector2.one * (Rim * 2f), 1f, rim, right, up);
            Quad(centre, size, 1f, Base, right, up);

            // segments of the fill, the gaps show the dark base
            full = Mathf.Max(1, full);
            var per = full <= MostPointSegments ? 1 : PointsPerSegmentAbove;
            var segments = (full + per - 1) / per;
            var inner = size - Vector2.one * Rim;
            var width = (inner.x - Gap * (segments - 1)) / segments;
            var color = _view.SoldierColor(side);
            for (var i = 0; i < segments; i++)
            {
                var points = Mathf.Min(per, full - i * per);
                var filled = Mathf.Clamp(health - i * per, 0, points);
                if (filled <= 0)
                    break;

                // the last segment of an uneven health is as narrow as its points
                var segment = new Vector2(width * points / per, inner.y);
                var left = -inner.x / 2f + i * (width + Gap);
                Quad(centre + right * (left + segment.x / 2f), segment, filled / (float)points, color, right, up);
            }
        }

        /// <summary>A unit fell: the points it still had on its bar are lost where it was last seen.</summary>
        private void Fell(int key)
        {
            if (!_tracks.TryGetValue(key, out var track) || track.Health <= 0)
                return;

            for (var lost = track.Health - 1; lost >= 0; lost--)
                Lose(key, track.Centre, track.Size, track.Full, lost, track.Side);
            track.Health = 0;
        }

        /// <summary>The segment of the point <paramref name="lost"/> is lost: its place on the bar, the shards it will break into.</summary>
        private void Lose(int key, Vector3 centre, Vector2 size, int full, int lost, Side side)
        {
            full = Mathf.Max(1, full);
            var per = full <= MostPointSegments ? 1 : PointsPerSegmentAbove;
            var segments = (full + per - 1) / per;
            var inner = size - Vector2.one * Rim;
            var width = (inner.x - Gap * (segments - 1)) / segments;
            var index = lost / per;
            var hit = new Hit
            {
                Key = key,
                Centre = centre,
                Offset = -inner.x / 2f + index * (width + Gap) + (per == 1 ? width / 2f : width * ((lost % per) + 0.5f) / per),
                Size = new Vector2(per == 1 ? width : width / per, inner.y),
                Enemy = side == Side.Enemy,
                Color = _view.SoldierColor(side),
                Rim = _view.OutlineColor(side),
                Shards = new Vector3[side == Side.Enemy ? 6 : 4],
            };
            for (var i = 0; i < hit.Shards.Length; i++)
                hit.Shards[i] = new Vector3(Next() * 2f - 1f, 1f + Next() * 1.5f, 0f);
            _hits.Add(hit);
            Struck?.Invoke(centre + Vector3.up * size.y, hit.Enemy);
        }

        /// <summary>The sideways jerk of a bar for a while after its hit, meters (3 units by own, 4 by the enemy's).</summary>
        private float Jerk(int key)
        {
            if (!_view.BarShake)
                return 0f;

            for (var i = _hits.Count - 1; i >= 0; i--)
            {
                var hit = _hits[i];
                if (hit.Key != key || hit.Age >= JerkUntil)
                    continue;

                var fade = 1f - hit.Age / JerkUntil;
                return Mathf.Sin(hit.Age * 90f) * (hit.Enemy ? 4f : 3f) * UiUnit * fade;
            }

            return 0f;
        }

        /// <summary>The lost segments: white, then a jump and a swell, then shards that fly and fade.</summary>
        private void DrawHits(float deltaTime, Vector3 right, Vector3 up)
        {
            for (var i = _hits.Count - 1; i >= 0; i--)
            {
                var hit = _hits[i];
                hit.Age += deltaTime;
                if (hit.Age >= ShardsUntil)
                {
                    _hits.RemoveAt(i);
                    continue;
                }

                // a living unit carries its effect along; a dead one leaves it where it fell
                if (TryCentre(hit.Key, out var centre))
                    hit.Centre = centre;
                var place = hit.Centre + right * hit.Offset;
                if (hit.Age < WhiteUntil)
                {
                    Quad(place, hit.Size, 1f, HitWhite, right, up);
                }
                else if (hit.Age < JumpUntil)
                {
                    var t = (hit.Age - WhiteUntil) / (JumpUntil - WhiteUntil);
                    var lift = (hit.Enemy ? 18f : 14f) * UiUnit * Mathf.Sin(t * Mathf.PI * 0.5f);
                    var swell = Mathf.Lerp(1f, hit.Enemy ? 1.5f : 1.35f, t);
                    Quad(place + up * lift, hit.Size * swell, 1f, HitWhite, right, up);
                }
                else
                {
                    var t = hit.Age - JumpUntil;
                    var alpha = 1f - t / (ShardsUntil - JumpUntil);
                    var start = place + up * ((hit.Enemy ? 18f : 14f) * UiUnit);
                    var voxel = Vector2.one * (hit.Size.y * 0.9f);
                    foreach (var shard in hit.Shards)
                    {
                        var velocity = new Vector2(shard.x * 0.9f, shard.y * 0.8f);
                        var offset = right * (velocity.x * t) + up * (velocity.y * t - 0.5f * Gravity * t * t);
                        var rim = hit.Rim;
                        rim.a = alpha;
                        var fill = hit.Color;
                        fill.a = alpha;
                        Quad(start + offset, voxel + Vector2.one * Rim, 1f, rim, right, up);
                        Quad(start + offset, voxel, 1f, fill, right, up);
                    }
                }

                _hits[i] = hit;
            }
        }

        private bool TryCentre(int key, out Vector3 centre)
        {
            centre = default;
            if (key < 0)
            {
                var side = (Side)(-1 - key);
                foreach (var hero in _simulation.Heroes)
                {
                    if (hero.Side == side && hero.Alive)
                    {
                        centre = _heroes.PositionOf(side) + Vector3.up * HeroHeight;
                        return true;
                    }
                }

                return false;
            }

            if (!_soldiers.TryGetPosition(key, out var position))
                return false;

            centre = position + Vector3.up * SoldierHeight;
            return true;
        }

        /// <summary>A number in [0, 1) for the look of the shards only; the battle never sees it.</summary>
        private float Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random & 0xFFFFFF) / (float)0x1000000;
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

        private struct Hit
        {
            public int Key;
            public Vector3 Centre;
            public float Offset;
            public Vector2 Size;
            public bool Enemy;
            public Color Color;
            public Color Rim;
            public Vector3[] Shards;
            public float Age;
        }

        private sealed class Track
        {
            public int Full;
            public int Health;
            public float HurtAt;
            public Vector3 Centre;
            public Vector2 Size;
            public Side Side;
        }
    }
}
