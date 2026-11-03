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
    /// it walks or fights. Its shots leave a trace; a dead hero breaks into cubes and is gone until it comes back at home,
    /// blinking while it cannot be hurt. A placeholder: a capsule with a nose.
    /// </summary>
    public sealed class HeroView : ILateTickable, IDisposable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly EffectsView _effects;
        private BattleRunner _runner;
        private EventCursor _events;
        private Vector3 _previous;
        private Vector3 _current;
        private Material _material;

        public HeroView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleWorld world, EffectsView effects)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _world = world;
            _effects = effects;
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
            hero.localPosition = Position;
            // blinks while it cannot be hurt after coming back
            var invulnerable = state.InvulnerableUntil > _runner.Simulation.Tick;
            _world.HeroBody.enabled = !invulnerable || Mathf.Repeat(Time.time * 8f, 1f) < 0.6f;
            var facing = new Vector3(state.FacingX, 0f, state.FacingY);
            if (facing.sqrMagnitude > 0f)
                hero.localRotation = Quaternion.RotateTowards(hero.localRotation, Quaternion.LookRotation(facing), 900f * Time.deltaTime);
        }

        public void Dispose()
        {
            _battle.OnStarted -= Take;
            if (_runner != null)
                _runner.OnStep -= Step;
            if (_material != null)
                UnityEngine.Object.Destroy(_material);
        }

        private void Take(BattleRunner runner)
        {
            if (_runner != null)
                _runner.OnStep -= Step;

            _runner = runner;
            _runner.OnStep += Step;
            _current = WorldOf(Player());
            _previous = _current;
            _world.Hero.localPosition = _current;
            _world.Hero.localRotation = Quaternion.identity;

            if (_material == null)
            {
                _material = new Material(_view.FlatMaterial);
                _material.SetColor(BaseColorId, _view.PlayerHeroColor);
                _world.HeroBody.sharedMaterial = _material;
                _world.HeroNose.sharedMaterial = _material;
            }

            var height = _view.HeroHeight;
            var width = _view.HeroWidth;
            // the built-in capsule mesh is 2 × 4 × 2 units
            _world.HeroBody.transform.localScale = new Vector3(width / 2f, height / 4f, width / 2f);
            _world.HeroBody.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            _world.HeroNose.transform.localScale = Vector3.one * width * 0.4f;
            _world.HeroNose.transform.localPosition = new Vector3(0f, height * 0.7f, width * 0.5f);
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
                case EventKind.Shot:
                    _effects.Shot(Meters(battleEvent.X, battleEvent.Y), Meters(battleEvent.ToX, battleEvent.ToY));
                    break;
                case EventKind.HeroDied when battleEvent.Side == Side.Player:
                    _effects.Debris(Meters(battleEvent.X, battleEvent.Y), _view.PlayerHeroColor, 10, 1f);
                    break;
                case EventKind.HeroRespawned when battleEvent.Side == Side.Player:
                    // back at home at once, not a slide from where it fell
                    _current = Meters(battleEvent.X, battleEvent.Y);
                    _previous = _current;
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
