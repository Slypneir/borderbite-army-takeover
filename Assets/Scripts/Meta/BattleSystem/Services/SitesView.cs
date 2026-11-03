using System.Collections.Generic;
using Core.BattleSystem.Enums;
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
    /// The places to build (stage 1 spec 3.2): a dull grey pad with a rim, glowing in the colour of the side whose colour its
    /// centre is. A barracks grows on its place, a fallen one breaks into cubes (3.3).
    /// </summary>
    public sealed class SitesView : ILateTickable
    {
        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly ViewMaterials _materials;
        private readonly EffectsView _effects;
        private readonly List<Place> _places = new List<Place>();
        private EventCursor _events;

        public SitesView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleWorld world, ViewMaterials materials,
            EffectsView effects)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _world = world;
            _materials = materials;
            _effects = effects;
        }

        /// <summary>A place in the world: the middle of its centre cell on the ground.</summary>
        public Vector3 WorldOf(SiteState site) =>
            new Vector3((site.CenterX + 0.5f) * _rules.CellSize, 0f, (site.CenterY + 0.5f) * _rules.CellSize);

        public void LateTick()
        {
            var simulation = _battle.Current?.Simulation;
            if (simulation == null)
                return;

            _events.Read(simulation, Handle);
            var sites = simulation.Sites;
            while (_places.Count < sites.Count)
                _places.Add(Create());

            for (var i = 0; i < sites.Count; i++)
                Draw(_places[i], sites[i], simulation.Grid.At(sites[i].CenterX, sites[i].CenterY));
        }

        private Place Create()
        {
            var pad = _world.Copy(_world.SiteTemplate);
            var barracks = _world.Copy(_world.BarracksTemplate);
            barracks.gameObject.SetActive(false);
            return new Place
            {
                Pad = pad,
                Rim = pad.GetChild(0),
                Top = pad.GetChild(1),
                Barracks = barracks,
                Owner = (Side)255,
                Built = (Side)255,
            };
        }

        private void Draw(Place place, SiteState site, Side owner)
        {
            var position = WorldOf(site);
            place.Pad.localPosition = position;
            place.Pad.localScale = Vector3.one * (_rules.CellSize * 3f);
            if (owner != place.Owner)
            {
                place.Owner = owner;
                _materials.Paint(place.Rim, _view.SiteRimColor);
                // a place of a side glows lighter than its land, a neutral one is dull
                var glow = owner == Side.Neutral ? _view.SiteColor : Color.Lerp(_view.SideColor(owner), Color.white, 0.35f);
                _materials.Paint(place.Top, glow);
            }

            if (site.Barracks != place.Built)
            {
                place.Built = site.Barracks;
                place.Barracks.gameObject.SetActive(site.Built);
                place.GrowAge = 0f;
                if (site.Built)
                    _materials.Paint(place.Barracks, _view.SoldierColor(site.Barracks));
            }

            if (!site.Built)
                return;

            place.Barracks.localPosition = position;
            place.GrowAge += Time.deltaTime;
            var grow = _view.GrowSeconds <= 0f ? 1f : Mathf.Clamp01(place.GrowAge / _view.GrowSeconds);
            // a short overshoot: it pops out of the ground
            var scale = grow < 1f ? Mathf.Sin(grow * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(grow * Mathf.PI)) : 1f;
            place.Barracks.localScale = new Vector3(1f, Mathf.Max(0.01f, scale), 1f);
        }

        private void Handle(BattleEvent battleEvent)
        {
            if (battleEvent.Kind != EventKind.Destroyed)
                return;

            var position = new Vector3(battleEvent.X * _rules.MetersPerUnit, 0f, battleEvent.Y * _rules.MetersPerUnit);
            _effects.Debris(position, _view.SoldierColor(battleEvent.Side), 14, 1.4f);
        }

        private sealed class Place
        {
            public Transform Pad;
            public Transform Rim;
            public Transform Top;
            public Transform Barracks;
            public Side Owner;
            public Side Built;
            public float GrowAge;
        }
    }
}
