using System;
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
    /// Draws the heroes between the last two ticks, so they move smoothly at any frame rate, and turns them to where they walk
    /// or fight: legs step, the sword arm swings a blow, the gun arm aims and the trace of a shot leaves the muzzle. A dead hero
    /// breaks into cubes and is gone until it comes back at its gate, blinking while it cannot be hurt. The player's hero is the
    /// one in the scene; the enemy's is a copy of it, with the colour and the rim of its side.
    /// </summary>
    public sealed class HeroView : ILateTickable, IDisposable
    {

        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly EffectsView _effects;
        private readonly ViewMaterials _materials;
        private readonly List<Body> _bodies = new List<Body>();
        private BattleRunner _runner;
        private EventCursor _events;
        private Body _player;

        public HeroView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleWorld world, EffectsView effects,
            ViewMaterials materials)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _world = world;
            _effects = effects;
            _materials = materials;
            _battle.OnStarted += Take;
        }

        /// <summary>The hero of the player now, in the world, between ticks; the camera reads it in the same frame, whatever the order.</summary>
        public Vector3 Position => _player == null ? Vector3.zero : PositionOf(_player);

        /// <summary>The hero of a side now, between ticks; zero when it has none.</summary>
        public Vector3 PositionOf(Side side)
        {
            foreach (var body in _bodies)
            {
                if (body.Side == side)
                    return PositionOf(body);
            }

            return Vector3.zero;
        }

        public void LateTick()
        {
            if (_runner == null || _battle.Current != _runner)
                return;

            _events.Read(_runner.Simulation, Handle);
            foreach (var body in _bodies)
                Draw(body);
        }

        public void Dispose()
        {
            _battle.OnStarted -= Take;
            if (_runner != null)
                _runner.OnStep -= Step;
        }

        private void Take(BattleRunner runner)
        {
            if (_runner != null)
                _runner.OnStep -= Step;

            _runner = runner;
            _runner.OnStep += Step;
            _events = default;
            var heroes = runner.Simulation.Heroes;
            for (var i = 0; i < heroes.Count; i++)
            {
                var body = BodyOf(heroes[i].Side);
                body.Index = i;
                body.Current = WorldOf(heroes[i]);
                body.Previous = body.Current;
                body.Drawn = body.Current;
                body.AttackUntil = 0f;
                body.Transform.localPosition = body.Current;
                body.Transform.localRotation = Quaternion.LookRotation(new Vector3(heroes[i].FacingX, 0f, heroes[i].FacingY));
                body.Transform.gameObject.SetActive(true);
            }

            // a battle without the enemy hero hides the copy left from the last one
            foreach (var body in _bodies)
            {
                if (body.Index >= heroes.Count || heroes[body.Index].Side != body.Side)
                    body.Transform.gameObject.SetActive(false);
            }
        }

        private Body BodyOf(Side side)
        {
            foreach (var body in _bodies)
            {
                if (body.Side == side)
                    return body;
            }

            var transform = side == Side.Player ? _world.Hero : _world.Copy(_world.Hero);
            transform.name = side == Side.Player ? "Hero" : "EnemyHero";
            var made = new Body
            {
                Side = side,
                Transform = transform,
                Pose = new UnitPose(transform),
                Flash = new HitFlash(transform),
                Health = -1,
                Renderers = transform.GetComponentsInChildren<MeshRenderer>(true),
            };
            _materials.Tint(transform, _view.HeroColor(side));
            _materials.Outline(transform, side);
            _bodies.Add(made);
            if (side == Side.Player)
                _player = made;
            return made;
        }

        private void Draw(Body body)
        {
            var heroes = _runner.Simulation.Heroes;
            if (body.Index >= heroes.Count)
                return;

            var state = heroes[body.Index];
            var transform = body.Transform;
            transform.gameObject.SetActive(state.Alive);
            var position = PositionOf(body);
            body.Pose.Update(Vector3.Distance(position, body.Drawn), Time.deltaTime);
            body.Flash.Update(Time.deltaTime);
            body.Drawn = position;
            transform.localPosition = position;

            // blinks while it cannot be hurt after coming back
            var invulnerable = state.InvulnerableUntil > _runner.Simulation.Tick;
            var shown = !invulnerable || Mathf.Repeat(Time.time * 8f, 1f) < 0.6f;
            foreach (var renderer in body.Renderers)
                renderer.enabled = shown;

            // smoothly, never a jump: to the target for a moment after a blow or a shot, else to where it walks
            var facing = Time.time < body.AttackUntil ? body.Attack
                : state.Moving ? new Vector3(state.DirectionX, 0f, state.DirectionY)
                : new Vector3(state.FacingX, 0f, state.FacingY);
            if (facing.sqrMagnitude > 0f)
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(facing), _view.TurnSpeed * Time.deltaTime);
        }

        private void Step()
        {
            var heroes = _runner.Simulation.Heroes;
            foreach (var body in _bodies)
            {
                if (body.Index >= heroes.Count)
                    continue;

                body.Previous = body.Current;
                body.Current = WorldOf(heroes[body.Index]);
                // less health than a tick ago: a hit, the hero flashes red (task 4.20)
                var health = heroes[body.Index].Health;
                if (body.Health >= 0 && health < body.Health && _view.HitFlash)
                    body.Flash.Hit();
                body.Health = health;
            }
        }

        private void Handle(BattleEvent battleEvent)
        {
            var body = HeroEvent(battleEvent) ? Find(battleEvent.Side) : null;
            switch (battleEvent.Kind)
            {
                case EventKind.Shot when body != null:
                    // aim first, so the trace leaves the muzzle of the raised gun
                    body.Pose.Aim();
                    Face(body, battleEvent);
                    body.Pose.Update(0f, 0f);
                    var muzzle = body.Pose.Muzzle != null ? body.Pose.Muzzle.position : Meters(battleEvent.X, battleEvent.Y) + Vector3.up * 0.6f;
                    _effects.Shot(muzzle, Meters(battleEvent.ToX, battleEvent.ToY) + Vector3.up * 0.5f);
                    break;
                case EventKind.Shot:
                    _effects.Shot(Meters(battleEvent.X, battleEvent.Y) + Vector3.up * 0.6f, Meters(battleEvent.ToX, battleEvent.ToY) + Vector3.up * 0.5f);
                    break;
                case EventKind.Strike when body != null:
                    body.Pose.Blow();
                    Face(body, battleEvent);
                    break;
                case EventKind.HeroDied when body != null:
                    _effects.Death(_world.HeroDeathTemplate, Meters(battleEvent.X, battleEvent.Y), body.Transform.localRotation,
                        _view.HeroColor(battleEvent.Side), 10, 1f);
                    break;
                case EventKind.HeroRespawned when body != null:
                    // back at home at once, not a slide from where it fell
                    body.Current = Meters(battleEvent.X, battleEvent.Y);
                    body.Previous = body.Current;
                    body.Drawn = body.Current;
                    break;
            }
        }

        /// <summary>Blows and shots of a hero carry Id -1; deaths and returns are always a hero's.</summary>
        private static bool HeroEvent(BattleEvent battleEvent) => battleEvent.Kind switch
        {
            EventKind.Shot or EventKind.Strike => battleEvent.Id < 0,
            EventKind.HeroDied or EventKind.HeroRespawned => true,
            _ => false,
        };

        private Body Find(Side side)
        {
            foreach (var body in _bodies)
            {
                if (body.Side == side && body.Transform.gameObject.activeSelf)
                    return body;
            }

            return null;
        }

        /// <summary>The hero turns to the target of its blow or shot for a moment, at the usual turn speed.</summary>
        private void Face(Body body, BattleEvent battleEvent)
        {
            var aim = Meters(battleEvent.ToX - battleEvent.X, battleEvent.ToY - battleEvent.Y);
            if (aim.sqrMagnitude <= 0f)
                return;

            body.Attack = aim;
            body.AttackUntil = Time.time + _view.AttackFaceSeconds;
        }

        private Vector3 PositionOf(Body body) =>
            _runner == null ? body.Current : Vector3.Lerp(body.Previous, body.Current, (float)_runner.Alpha);

        private Vector3 Meters(int x, int y) => new Vector3(x * _rules.MetersPerUnit, 0f, y * _rules.MetersPerUnit);

        private Vector3 WorldOf(HeroState hero) => Meters(hero.X, hero.Y);

        private sealed class Body
        {
            public Side Side;
            public int Index;
            public Transform Transform;
            public UnitPose Pose;
            public HitFlash Flash;
            public int Health;
            public MeshRenderer[] Renderers;
            public Vector3 Previous;
            public Vector3 Current;
            public Vector3 Drawn;
            public Vector3 Attack;
            public float AttackUntil;
        }
    }
}
