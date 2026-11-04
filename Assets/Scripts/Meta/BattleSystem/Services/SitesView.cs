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
    /// The places to build (stage 1 spec 3.2): a pad whose `paint` rags and flag are dull grey, or glow in the colour of the side
    /// whose colour its centre is; the flag of three parts waves, each 18°·sin(2π·t/1.6 − k·0.9) around its own Y (the 3D
    /// artist, a883021). A barracks grows on its place, its `paint` roof and flag in the colour of its side, and the pad hides
    /// under it — its posts would stick out at the corners; a fallen one breaks into cubes and the pad is back (3.3).
    /// </summary>
    public sealed class SitesView : ILateTickable
    {
        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly ViewMaterials _materials;
        private readonly EffectsView _effects;
        private const float FlagDegrees = 18f;
        private const float FlagPeriod = 1.6f;
        private const float FlagLag = 0.9f;

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

        /// <summary>
        /// A log of the player's building landed at its place (task 4.25): the barracks grows by a fifth of its height a log, so it
        /// is seen to be built by these logs. Without flights it grows by time.
        /// </summary>
        public void LogLanded(int site)
        {
            if (site >= 0 && site < _places.Count)
                _places[site].Logs++;
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
            var flags = new List<Transform>();
            var rests = new List<Quaternion>();
            for (var k = 1; k <= 3; k++)
            {
                var flag = Find(pad, "paint_flag_" + k);
                if (flag == null)
                    break;

                flags.Add(flag);
                rests.Add(flag.localRotation);
            }

            return new Place
            {
                Pad = pad,
                Barracks = barracks,
                Owner = (Side)255,
                Built = (Side)255,
                Flags = flags.ToArray(),
                FlagRests = rests.ToArray(),
            };
        }

        private void Draw(Place place, SiteState site, Side owner)
        {
            var position = WorldOf(site);
            place.Pad.localPosition = position;
            if (owner != place.Owner)
            {
                place.Owner = owner;
                // a place of a side glows lighter than its land, a neutral one is dull
                var glow = owner == Side.Neutral ? _view.SiteColor : Color.Lerp(_view.SideColor(owner), Color.white, 0.35f);
                _materials.Tint(place.Pad, glow);
            }

            if (site.Barracks != place.Built)
            {
                place.Built = site.Barracks;
                place.Barracks.gameObject.SetActive(site.Built);
                place.Pad.gameObject.SetActive(!site.Built);
                place.GrowAge = 0f;
                place.Logs = 0;
                place.ByLogs = site.Barracks == Side.Player && _view.ResourceFlights;
                if (site.Built)
                    _materials.Tint(place.Barracks, _view.SoldierColor(site.Barracks));
            }

            if (!site.Built)
            {
                Wave(place);
                return;
            }

            place.Barracks.localPosition = position;
            // the door to the south, where the hired soldiers come out and the camera looks from
            place.Barracks.localRotation = Quaternion.Euler(0f, 180f, 0f);
            place.GrowAge += Time.deltaTime;
            var grow = _view.GrowSeconds <= 0f ? 1f : Mathf.Clamp01(place.GrowAge / _view.GrowSeconds);
            if (place.ByLogs)
            {
                // towards the logs landed so far, smoothly; a stuck flight never leaves it half built (2 s at most)
                place.Shown = Mathf.MoveTowards(place.Shown, place.Logs / 5f, Time.deltaTime * 4f);
                grow = place.GrowAge > 2f ? 1f : place.Shown;
            }
            // a short overshoot: it pops out of the ground
            var scale = grow < 1f ? Mathf.Sin(grow * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(grow * Mathf.PI)) : 1f;
            place.Barracks.localScale = new Vector3(1f, Mathf.Max(0.01f, scale), 1f);
        }

        /// <summary>The flag of a pad waves, each part a little later than the one it hangs on.</summary>
        private static void Wave(Place place)
        {
            var t = Time.time;
            for (var i = 0; i < place.Flags.Length; i++)
            {
                var k = i + 1;
                var angle = FlagDegrees * Mathf.Sin(2f * Mathf.PI * t / FlagPeriod - k * FlagLag);
                place.Flags[i].localRotation = place.FlagRests[i] * Quaternion.Euler(0f, angle, 0f);
            }
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
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
            public Transform Barracks;
            public Side Owner;
            public Side Built;
            public float GrowAge;
            public int Logs;
            public bool ByLogs;
            public float Shown;
            public Transform[] Flags;
            public Quaternion[] FlagRests;
        }
    }
}
