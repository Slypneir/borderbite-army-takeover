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
    /// Draws the soldiers between the last two ticks, a copy of the template per soldier with its `paint` parts in the colour
    /// of its side, turned to where it goes or strikes; legs step, the weapon arm swings a blow. A fallen soldier breaks into
    /// cubes.
    /// </summary>
    public sealed class SoldiersView : ILateTickable
    {
        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly ViewMaterials _materials;
        private readonly EffectsView _effects;
        private readonly Dictionary<int, Body> _bodies = new Dictionary<int, Body>();
        private readonly Stack<Transform> _free = new Stack<Transform>();
        private readonly HashSet<int> _alive = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private readonly Dictionary<Transform, UnitPose> _poses = new Dictionary<Transform, UnitPose>();
        private BattleRunner _runner;
        private EventCursor _events;

        public SoldiersView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleWorld world, ViewMaterials materials,
            EffectsView effects)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _world = world;
            _materials = materials;
            _effects = effects;
            _battle.OnStarted += Take;
        }

        public void LateTick()
        {
            if (_runner == null || _battle.Current != _runner)
                return;

            _events.Read(_runner.Simulation, Handle);
            var alpha = (float)_runner.Alpha;
            foreach (var body in _bodies.Values)
            {
                var position = Vector3.Lerp(body.Previous, body.Current, alpha);
                body.Pose.Update(Vector3.Distance(position, body.Transform.localPosition), Time.deltaTime);
                body.Transform.localPosition = position;
                if (body.Facing.sqrMagnitude > 0f)
                    body.Transform.localRotation = Quaternion.RotateTowards(body.Transform.localRotation, Quaternion.LookRotation(body.Facing), _view.TurnSpeed * Time.deltaTime);
            }
        }

        private void Take(BattleRunner runner)
        {
            if (_runner != null)
                _runner.OnStep -= Step;

            foreach (var body in _bodies.Values)
                Release(body.Transform);
            _bodies.Clear();
            _runner = runner;
            _runner.OnStep += Step;
            Step();
            foreach (var body in _bodies.Values)
                body.Previous = body.Current;
        }

        /// <summary>After a tick: new soldiers get a body, the others move on, the gone ones give theirs back.</summary>
        private void Step()
        {
            _alive.Clear();
            foreach (var soldier in _runner.Simulation.Soldiers)
            {
                _alive.Add(soldier.Id);
                var position = WorldOf(soldier.X, soldier.Y);
                if (!_bodies.TryGetValue(soldier.Id, out var body))
                {
                    var transform = Take(soldier.Side);
                    body = new Body { Transform = transform, Pose = Pose(transform), Previous = position };
                    body.Transform.localPosition = position;
                    _bodies[soldier.Id] = body;
                }
                else
                {
                    body.Previous = body.Current;
                }

                body.Current = position;
                body.Facing = new Vector3(soldier.FacingX, 0f, soldier.FacingY);
                body.Side = soldier.Side;
            }

            _gone.Clear();
            foreach (var id in _bodies.Keys)
            {
                if (!_alive.Contains(id))
                    _gone.Add(id);
            }

            foreach (var id in _gone)
            {
                Release(_bodies[id].Transform);
                _bodies.Remove(id);
            }
        }

        private void Handle(BattleEvent battleEvent)
        {
            if (battleEvent.Kind == EventKind.SoldierDied)
                _effects.Debris(WorldOf(battleEvent.X, battleEvent.Y), _view.SoldierColor(battleEvent.Side), 6, 0.6f);
            else if (battleEvent.Kind == EventKind.Strike && _bodies.TryGetValue(battleEvent.Id, out var body))
                body.Pose.Blow();
        }

        /// <summary>The pose of a pooled body is made once and kept with it.</summary>
        private UnitPose Pose(Transform body)
        {
            if (!_poses.TryGetValue(body, out var pose))
            {
                pose = new UnitPose(body);
                _poses[body] = pose;
            }

            return pose;
        }

        private Transform Take(Side side)
        {
            var body = _free.Count > 0 ? _free.Pop() : _world.Copy(_world.SoldierTemplate);
            body.gameObject.SetActive(true);
            body.localRotation = Quaternion.identity;
            _materials.Tint(body, _view.SoldierColor(side));
            return body;
        }

        private void Release(Transform body)
        {
            body.gameObject.SetActive(false);
            _free.Push(body);
        }

        private Vector3 WorldOf(int x, int y) => new Vector3(x * _rules.MetersPerUnit, 0f, y * _rules.MetersPerUnit);

        private sealed class Body
        {
            public Transform Transform;
            public UnitPose Pose;
            public Vector3 Previous;
            public Vector3 Current;
            public Vector3 Facing;
            public Side Side;
        }
    }
}
