using System;
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
    /// Draws the hero of the player between the last two ticks, so it moves smoothly at any frame rate, and turns it to where
    /// it walks or fights: legs step, the sword arm swings a blow, the gun arm aims and the trace of a shot leaves the muzzle.
    /// A dead hero breaks into cubes and is gone until it comes back at home, blinking while it cannot be hurt. Its `paint`
    /// parts take the colour of the hero of the player.
    /// </summary>
    public sealed class HeroView : ILateTickable, IDisposable
    {
        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly EffectsView _effects;
        private readonly ViewMaterials _materials;
        private BattleRunner _runner;
        private EventCursor _events;
        private UnitPose _pose;
        private MeshRenderer[] _renderers;
        private Vector3 _previous;
        private Vector3 _current;
        private Vector3 _drawn;

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

        /// <summary>The hero now, in the world, between ticks; the camera reads it in the same frame, whatever the order.</summary>
        public Vector3 Position => _runner == null ? _current : Vector3.Lerp(_previous, _current, (float)_runner.Alpha);

        public void LateTick()
        {
            if (_runner == null || _battle.Current != _runner)
                return;

            _events.Read(_runner.Simulation, Handle);
            var hero = _world.Hero;
            var state = Player();
            hero.gameObject.SetActive(state.Alive);
            var position = Position;
            _pose.Update(Vector3.Distance(position, _drawn), Time.deltaTime);
            _drawn = position;
            hero.localPosition = position;

            // blinks while it cannot be hurt after coming back
            var invulnerable = state.InvulnerableUntil > _runner.Simulation.Tick;
            var shown = !invulnerable || Mathf.Repeat(Time.time * 8f, 1f) < 0.6f;
            foreach (var renderer in _renderers)
                renderer.enabled = shown;

            var facing = new Vector3(state.FacingX, 0f, state.FacingY);
            if (facing.sqrMagnitude > 0f)
                hero.localRotation = Quaternion.RotateTowards(hero.localRotation, Quaternion.LookRotation(facing), 900f * Time.deltaTime);
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
            _current = WorldOf(Player());
            _previous = _current;
            _drawn = _current;
            _world.Hero.localPosition = _current;
            _world.Hero.localRotation = Quaternion.identity;

            if (_pose != null)
                return;

            _pose = new UnitPose(_world.Hero);
            _renderers = _world.Hero.GetComponentsInChildren<MeshRenderer>(true);
            _materials.Tint(_world.Hero, _view.PlayerHeroColor);
        }

        private void Step()
        {
            _previous = _current;
            _current = WorldOf(Player());
        }

        private void Handle(BattleEvent battleEvent)
        {
            switch (battleEvent.Kind)
            {
                case EventKind.Shot when battleEvent.Side == Side.Player:
                    // aim first, so the trace leaves the muzzle of the raised gun
                    _pose.Aim();
                    var aim = Meters(battleEvent.ToX - battleEvent.X, battleEvent.ToY - battleEvent.Y);
                    if (aim.sqrMagnitude > 0f)
                        _world.Hero.rotation = Quaternion.LookRotation(aim);
                    _pose.Update(0f, 0f);
                    var muzzle = _pose.Muzzle != null ? _pose.Muzzle.position : Meters(battleEvent.X, battleEvent.Y) + Vector3.up * 0.6f;
                    _effects.Shot(muzzle, Meters(battleEvent.ToX, battleEvent.ToY) + Vector3.up * 0.5f);
                    break;
                case EventKind.Shot:
                    _effects.Shot(Meters(battleEvent.X, battleEvent.Y) + Vector3.up * 0.6f, Meters(battleEvent.ToX, battleEvent.ToY) + Vector3.up * 0.5f);
                    break;
                case EventKind.Strike when battleEvent.Side == Side.Player && battleEvent.Id < 0:
                    _pose.Blow();
                    break;
                case EventKind.HeroDied when battleEvent.Side == Side.Player:
                    _effects.Debris(Meters(battleEvent.X, battleEvent.Y), _view.PlayerHeroColor, 10, 1f);
                    break;
                case EventKind.HeroRespawned when battleEvent.Side == Side.Player:
                    // back at home at once, not a slide from where it fell
                    _current = Meters(battleEvent.X, battleEvent.Y);
                    _previous = _current;
                    _drawn = _current;
                    break;
            }
        }

        private Vector3 Meters(int x, int y) => new Vector3(x * _rules.MetersPerUnit, 0f, y * _rules.MetersPerUnit);

        private HeroState Player()
        {
            foreach (var hero in _runner.Simulation.Heroes)
            {
                if (hero.Side == Side.Player)
                    return hero;
            }

            return default;
        }

        private Vector3 WorldOf(HeroState hero) => Meters(hero.X, hero.Y);
    }
}
