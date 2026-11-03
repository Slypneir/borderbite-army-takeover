using System;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Views;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Draws the hero of the player between the last two ticks, so it moves smoothly at any frame rate, and turns it to where
    /// it walks. A placeholder: a capsule with a nose.
    /// </summary>
    public sealed class HeroView : ILateTickable, IDisposable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private BattleRunner _runner;
        private Vector3 _previous;
        private Vector3 _current;
        private Material _material;

        public HeroView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleWorld world)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _world = world;
            _battle.OnStarted += Take;
        }

        /// <summary>The hero now, in the world, between ticks; the camera reads it in the same frame, whatever the order.</summary>
        public Vector3 Position => _runner == null ? _current : Vector3.Lerp(_previous, _current, (float)_runner.Alpha);

        public void LateTick()
        {
            if (_runner == null || _battle.Current != _runner)
                return;

            var hero = _world.Hero;
            hero.localPosition = Position;
            var state = Player();
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
            _world.HeroBody.transform.localScale = new Vector3(width, height / 2f, width);
            _world.HeroBody.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            _world.HeroNose.transform.localScale = Vector3.one * width * 0.4f;
            _world.HeroNose.transform.localPosition = new Vector3(0f, height * 0.7f, width * 0.5f);
        }

        private void Step()
        {
            _previous = _current;
            _current = WorldOf(Player());
        }

        private Core.BattleSystem.Structs.HeroState Player()
        {
            foreach (var hero in _runner.Simulation.Heroes)
            {
                if (hero.Side == Side.Player)
                    return hero;
            }

            return default;
        }

        private Vector3 WorldOf(Core.BattleSystem.Structs.HeroState hero) =>
            new Vector3(hero.X * _rules.MetersPerUnit, 0f, hero.Y * _rules.MetersPerUnit);
    }
}
