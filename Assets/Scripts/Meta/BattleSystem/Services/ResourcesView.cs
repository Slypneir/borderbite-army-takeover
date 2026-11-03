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
    /// Draws the piles of gold and bundles of wood; a picked one flies to the hero and is gone (stage 1 spec 3.1). A slow bob
    /// makes them read as things to take.
    /// </summary>
    public sealed class ResourcesView : ILateTickable
    {
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
            var bob = Mathf.Sin(Time.time * 3f) * 0.05f;
            foreach (var resource in simulation.Resources)
            {
                _present.Add(resource.Id);
                if (!_lying.TryGetValue(resource.Id, out var body))
                {
                    body = Take(resource.Kind);
                    body.localPosition = WorldOf(resource.X, resource.Y);
                    _lying[resource.Id] = body;
                }

                var position = body.localPosition;
                position.y = 0.05f + bob;
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
            }
        }

        private void Handle(BattleEvent battleEvent)
        {
            if (battleEvent.Kind != EventKind.Pickup || !_lying.TryGetValue(battleEvent.Id, out var body))
                return;

            // the picked pile has left the map but flies to the hero before it is gone
            _lying.Remove(battleEvent.Id);
            _flying.Add(new Flight { Transform = body, From = body.localPosition });
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
                flight.Transform.localScale = Vector3.one * (1f - 0.6f * t);
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
            _materials.Paint(body, kind == ResourceKind.Gold ? _view.GoldColor : _view.WoodColor);
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
