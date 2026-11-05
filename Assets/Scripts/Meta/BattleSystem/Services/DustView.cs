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
    /// The dust of own land (task 4.33; battle-marks card «Своя земля: щит, блок, пыль»; spec 2.4a: a unit runs faster there):
    /// every hero and soldier of either side walking on the land of its side leaves at its feet, once in 0.12 s, a puff of 3 soft
    /// voxels 5–8 units across (UI review of 4.33), the light tone of its land — they go back and a little up, grow twice and are gone in 0.45 s.
    /// No dust on the other side's land or on neutral land; at most 60 puffs at a time, more are not born. Cubes in the world,
    /// a placeholder until the art of the 3D artist.
    /// </summary>
    public sealed class DustView : ILateTickable, IDisposable
    {
        private const float Every = 0.12f;
        private const float Life = 0.45f;
        private const int MostPuffs = 60;
        private const int Voxels = 3;
        // a unit of the canvas at the camera of the battle, meters (BarsView)
        private const float UiUnit = 0.0215f;
        private static readonly Color[] PlayerTones = { new Color32(0xe3, 0xec, 0xff, 0xff), new Color32(0xb9, 0xcd, 0xf5, 0xff) };
        private static readonly Color[] EnemyTones = { new Color32(0xff, 0xe1, 0xdc, 0xff), new Color32(0xf2, 0xb8, 0xae, 0xff) };
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly IBattleDriver _battle;
        private readonly BattleConfig _rules;
        private readonly BattleViewConfig _view;
        private readonly BattleWorld _world;
        private readonly HeroView _heroes;
        private readonly SoldiersView _soldiers;
        private readonly List<Voxel> _voxels = new List<Voxel>();
        private readonly Dictionary<int, Walker> _walkers = new Dictionary<int, Walker>();
        private readonly List<int> _gone = new List<int>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Material _material;
        private uint _random = 0x9e3779b9u;
        private int _frame;

        public DustView(IBattleDriver battle, BattleConfig rules, BattleViewConfig view, BattleWorld world, HeroView heroes, SoldiersView soldiers)
        {
            _battle = battle;
            _rules = rules;
            _view = view;
            _world = world;
            _heroes = heroes;
            _soldiers = soldiers;
        }

        public void LateTick()
        {
            var simulation = _battle.Current?.Simulation;
            if (simulation == null)
                return;

            var now = Time.time;
            _frame++;
            for (var i = 0; i < simulation.Heroes.Count; i++)
            {
                var hero = simulation.Heroes[i];
                if (hero.Alive)
                    Walk(simulation, -1 - i, hero.Side, hero.X, hero.Y, _heroes.PositionOf(hero.Side), now);
            }

            foreach (var soldier in simulation.Soldiers)
            {
                if (_soldiers.TryGetPosition(soldier.Id, out var position))
                    Walk(simulation, soldier.Id, soldier.Side, soldier.X, soldier.Y, position, now);
            }

            // the units not seen this frame are gone
            _gone.Clear();
            foreach (var pair in _walkers)
            {
                if (pair.Value.Frame != _frame)
                    _gone.Add(pair.Key);
            }

            foreach (var key in _gone)
                _walkers.Remove(key);

            Animate(now);
        }

        public void Dispose()
        {
            if (_material != null)
                UnityEngine.Object.Destroy(_material);
        }

        private void Walk(BattleSimulation simulation, int key, Side side, int x, int y, Vector3 drawn, float now)
        {
            if (!_walkers.TryGetValue(key, out var walker))
            {
                walker = new Walker { X = x, Y = y, Next = now };
                _walkers.Add(key, walker);
            }

            walker.Frame = _frame;
            var moving = x != walker.X || y != walker.Y;
            var direction = new Vector3(x - walker.X, 0f, y - walker.Y);
            if (moving)
            {
                walker.Direction = direction.normalized;
                walker.X = x;
                walker.Y = y;
                walker.MovedAt = now;
            }

            // walking: moved within the last ticks of the battle
            var walking = now - walker.MovedAt < 0.1f;
            if (!walking || now < walker.Next)
                return;

            var cx = x / BattleSettings.UnitsPerCell;
            var cy = y / BattleSettings.UnitsPerCell;
            if (!simulation.Grid.Inside(cx, cy) || simulation.Grid.At(cx, cy) != side)
                return;

            walker.Next = now + Every;
            Puff(drawn, walker.Direction, side == Side.Player ? PlayerTones : EnemyTones, now);
        }

        private void Puff(Vector3 at, Vector3 direction, Color[] tones, float now)
        {
            var alive = 0;
            foreach (var voxel in _voxels)
            {
                if (voxel.Transform.gameObject.activeSelf)
                    alive++;
            }

            if (alive / Voxels >= MostPuffs)
                return;

            for (var i = 0; i < Voxels; i++)
            {
                var voxel = Take();
                var size = Mathf.Lerp(5f, 8f, Next()) * UiUnit;
                voxel.Size = size;
                voxel.At = now;
                var side = new Vector3(-direction.z, 0f, direction.x) * (Next() - 0.5f) * 0.6f;
                voxel.Velocity = -direction * (0.5f + Next() * 0.4f) + side + Vector3.up * (0.3f + Next() * 0.3f);
                voxel.Transform.position = at + Vector3.up * 0.06f + side * 0.2f;
                voxel.Transform.rotation = Quaternion.Euler(Next() * 360f, Next() * 360f, Next() * 360f);
                _block.SetColor(BaseColorId, tones[i % tones.Length]);
                voxel.Renderer.SetPropertyBlock(_block);
                voxel.Transform.gameObject.SetActive(true);
            }
        }

        private void Animate(float now)
        {
            var dt = Time.deltaTime;
            foreach (var voxel in _voxels)
            {
                if (!voxel.Transform.gameObject.activeSelf)
                    continue;

                var t = (now - voxel.At) / Life;
                if (t >= 1f)
                {
                    voxel.Transform.gameObject.SetActive(false);
                    continue;
                }

                // it grows twice and melts away at the end: the material has no alpha, the size fades it
                voxel.Transform.position += voxel.Velocity * dt;
                var grow = voxel.Size * (1f + t);
                voxel.Transform.localScale = Vector3.one * (t < 0.6f ? grow : grow * (1f - (t - 0.6f) / 0.4f));
            }
        }

        private Voxel Take()
        {
            foreach (var voxel in _voxels)
            {
                if (!voxel.Transform.gameObject.activeSelf)
                    return voxel;
            }

            if (_material == null)
                _material = new Material(_view.FlatMaterial) { name = "Dust" };

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(cube.GetComponent<Collider>());
            cube.name = "Dust";
            cube.transform.SetParent(_world.transform, false);
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var fresh = new Voxel { Transform = cube.transform, Renderer = renderer };
            _voxels.Add(fresh);
            return fresh;
        }

        private float Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return (_random & 0xffffff) / 16777216f;
        }

        private sealed class Walker
        {
            public int X;
            public int Y;
            public Vector3 Direction = Vector3.forward;
            public float MovedAt = -10f;
            public float Next;
            public int Frame;
        }

        private sealed class Voxel
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public float Size;
            public float At;
        }
    }
}
