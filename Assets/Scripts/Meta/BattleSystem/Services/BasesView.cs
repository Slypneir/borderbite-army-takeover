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
    /// The bases of the sides (stage 1 spec 4.1): the fort of the 3D artist in the middle of each start area, its `paint` merlons,
    /// roof and banners in the colour of the soldiers of its side, the gate to the middle of the map (the player's east, the
    /// enemy's west), where its hero comes back. A fallen base is gone; its cubes fly from SitesView, as for any building.
    /// </summary>
    public sealed class BasesView : ILateTickable, IDisposable
    {
        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleWorld _world;
        private readonly ViewMaterials _materials;
        private readonly Transform[] _forts = new Transform[3];
        private BattleRunner _runner;

        public BasesView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleWorld world, ViewMaterials materials)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _world = world;
            _materials = materials;
            _battle.OnStarted += Take;
        }

        public void LateTick()
        {
            if (_runner == null || _battle.Current != _runner)
                return;

            var bases = _runner.Simulation.Bases;
            foreach (var side in new[] { Side.Player, Side.Enemy })
            {
                var fort = _forts[(int)side];
                if (fort != null)
                    fort.gameObject.SetActive(bases[(int)side].Standing);
            }
        }

        public void Dispose() => _battle.OnStarted -= Take;

        private void Take(BattleRunner runner)
        {
            _runner = runner;
            if (_world.BaseTemplate == null)
                return;

            var bases = runner.Simulation.Bases;
            foreach (var side in new[] { Side.Player, Side.Enemy })
            {
                var fort = _forts[(int)side];
                if (fort == null)
                {
                    fort = _world.Copy(_world.BaseTemplate);
                    fort.name = side + "Base";
                    _materials.Tint(fort, _view.SoldierColor(side));
                    _forts[(int)side] = fort;
                }

                var place = bases[(int)side];
                fort.localPosition = new Vector3(place.X * _rules.MetersPerUnit, 0f, place.Y * _rules.MetersPerUnit);
                // the gate (+Z of the model) looks to the hero's gate
                fort.localRotation = Quaternion.LookRotation(new Vector3(place.GateX - place.X, 0f, place.GateY - place.Y));
                fort.gameObject.SetActive(place.Standing);
            }
        }
    }
}
