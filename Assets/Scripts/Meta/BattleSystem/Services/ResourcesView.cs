using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Utils;
using Meta.BattleSystem.Views;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Draws the piles of gold and bundles of wood (stage 1 spec 3.1, task 4.19). A pile stands still until mined (T8: no idle
    /// bob; gold keeps its glint); each blow of mining makes it pulse — a short springy bounce of scale — and sends one small
    /// unit flying to the hero. After its last unit the pile is gone.
    /// </summary>
    public sealed class ResourcesView : ILateTickable
    {
        // the vein of the 3D artist (a883021) is 1.1 m across: no more growing to tell it from a flower (spec T8)
        private const float GoldScale = 1f;
        private const float GoldHopsPerSecond = 1.4f;
        private const float PulseSeconds = 0.25f;
        private const float PulseScale = 0.18f;
        private const float UnitScale = 0.35f;
        private const float GlintEvery = 3f;

        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly ViewMaterials _materials;
        private readonly HeroView _hero;
        private readonly Dictionary<int, Transform> _lying = new Dictionary<int, Transform>();
        private readonly List<Flight> _flying = new List<Flight>();
        private readonly Stack<Transform>[] _free = { new Stack<Transform>(), new Stack<Transform>() };
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly Dictionary<int, float> _pulses = new Dictionary<int, float>();
        private EventCursor _events;
        private BattleSimulation _simulation;

        public ResourcesView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleWorld world, ViewMaterials materials,
            HeroView hero)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _world = world;
            _materials = materials;
            _hero = hero;
        }

        public void LateTick()
        {
            var simulation = _battle.Current?.Simulation;
            if (simulation != _simulation)
                Clear(simulation);

            if (simulation == null)
                return;

            _events.Read(simulation, Handle);
            Sync(simulation);
            Fly();
        }

        private void Clear(BattleSimulation simulation)
        {
            _simulation = simulation;
            foreach (var pair in _lying)
                Release(pair.Value);
            _lying.Clear();
            foreach (var flight in _flying)
                Release(flight.Transform);
            _flying.Clear();
        }

        private void Sync(BattleSimulation simulation)
        {
            _present.Clear();
            foreach (var resource in simulation.Resources)
            {
                _present.Add(resource.Id);
                if (!_lying.TryGetValue(resource.Id, out var body))
                {
                    body = Take(resource.Kind);
                    body.localPosition = WorldOf(resource.X, resource.Y);
                    _lying[resource.Id] = body;
                }

                // a blow: a short springy bounce of scale, back to the pile as it was
                var pulse = 1f;
                if (_pulses.TryGetValue(resource.Id, out var age))
                {
                    age += Time.deltaTime;
                    _pulses[resource.Id] = age;
                    var t = Mathf.Clamp01(age / PulseSeconds);
                    pulse = 1f + PulseScale * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
                }

                var scale = 1f;
                if (resource.Kind == ResourceKind.Gold)
                {
                    // gold must not pass for a yellow flower (spec T8): now and then it swells for a glint; it does not hop
                    var phase = Time.time * GoldHopsPerSecond + resource.Id * 0.37f;
                    var glint = Mathf.Repeat(phase, GlintEvery) < 0.25f ? 1f + 0.12f * Mathf.Sin(Mathf.Repeat(phase, GlintEvery) / 0.25f * Mathf.PI) : 1f;
                    scale = GoldScale * glint;
                }

                body.localScale = Vector3.one * (scale * pulse);
                var position = body.localPosition;
                position.y = 0.05f;
                body.localPosition = position;
            }

            _gone.Clear();
            foreach (var id in _lying.Keys)
            {
                if (!_present.Contains(id))
                    _gone.Add(id);
            }

            foreach (var id in _gone)
            {
                Release(_lying[id]);
                _lying.Remove(id);
                _pulses.Remove(id);
            }
        }

        private void Handle(BattleEvent battleEvent)
        {
            if (battleEvent.Kind != EventKind.Pickup || !_lying.TryGetValue(battleEvent.Id, out var body))
                return;

            // one unit: the pile pulses, a small copy of it flies to the hero
            _pulses[battleEvent.Id] = 0f;
            var unit = Take(battleEvent.Detail == (int)ResourceKind.Gold ? ResourceKind.Gold : ResourceKind.Wood);
            unit.localPosition = body.localPosition + Vector3.up * 0.3f;
            _flying.Add(new Flight { Transform = unit, From = unit.localPosition });
        }

        private void Fly()
        {
            var seconds = Mathf.Max(0.01f, _view.PickupSeconds);
            for (var i = _flying.Count - 1; i >= 0; i--)
            {
                var flight = _flying[i];
                flight.Age += Time.deltaTime;
                var t = flight.Age / seconds;
                if (t >= 1f)
                {
                    Release(flight.Transform);
                    _flying.RemoveAt(i);
                    continue;
                }

                var target = _hero.Position + Vector3.up * 0.8f;
                flight.Transform.localPosition = Vector3.Lerp(flight.From, target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.6f);
                flight.Transform.localScale = Vector3.one * (UnitScale * (1f - 0.5f * t));
                _flying[i] = flight;
            }
        }

        private Transform Take(ResourceKind kind)
        {
            var free = _free[(int)kind];
            var body = free.Count > 0 ? free.Pop() : _world.Copy(kind == ResourceKind.Gold ? _world.GoldTemplate : _world.WoodTemplate);
            body.gameObject.SetActive(true);
            body.localScale = Vector3.one;
            body.name = kind.ToString();
            // a model keeps its texture; a placeholder takes the colour of its resource
            _materials.Tint(body, kind == ResourceKind.Gold ? _view.GoldColor : _view.WoodColor);
            return body;
        }

        private void Release(Transform body)
        {
            body.gameObject.SetActive(false);
            _free[body.name == nameof(ResourceKind.Gold) ? (int)ResourceKind.Gold : (int)ResourceKind.Wood].Push(body);
        }

        private Vector3 WorldOf(int x, int y) => new Vector3(x * _rules.MetersPerUnit, 0f, y * _rules.MetersPerUnit);

        private struct Flight
        {
            public Transform Transform;
            public Vector3 From;
            public float Age;
        }
    }
}
