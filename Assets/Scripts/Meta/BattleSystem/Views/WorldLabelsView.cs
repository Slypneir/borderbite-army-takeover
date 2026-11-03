using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.LocalizationSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// Labels over the places of the player (stage 1 spec 3.3): over an empty one the price «10 wood» and a ring of the progress
    /// of building, dull while wood is short; over a barracks the price of a soldier and the army «N/30». Copies of
    /// <see cref="template"/> (a label with a ring), one per place. Lives right under the canvas, anchored to its lower left corner.
    /// Short wood is dull, never red: red is the enemy.
    /// </summary>
    public sealed class WorldLabelsView : MonoBehaviour
    {
        private const float Height = 1.8f;
        private const float DullAlpha = 0.45f;

        [SerializeField] private RectTransform template;

        private readonly List<Mark> _marks = new List<Mark>();
        private IBattleDriver _battle;
        private BattleConfig _rules;
        private Camera _camera;

        [Inject]
        public void Construct(IBattleDriver battle, BattleConfig rules, Camera mainCamera)
        {
            _battle = battle;
            _rules = rules;
            _camera = mainCamera;
        }

        private void Awake() => template.gameObject.SetActive(false);

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
                return;

            var sites = simulation.Sites;
            while (_marks.Count < sites.Count)
                _marks.Add(Create());

            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            var purse = simulation.SideOf(Side.Player);
            var army = simulation.ArmyOf(Side.Player);
            var buildTicks = Mathf.Max(1, simulation.Settings.BuildTicks);
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                var owner = simulation.Grid.At(site.CenterX, site.CenterY);
                var mark = _marks[i];
                var shown = site.Barracks == Side.Player || (!site.Built && owner == Side.Player);
                mark.Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                var world = new Vector3((site.CenterX + 0.5f) * _rules.CellSize, Height, (site.CenterY + 0.5f) * _rules.CellSize);
                mark.Root.anchoredPosition = (Vector2)_camera.WorldToScreenPoint(world) / scale;
                if (site.Built)
                {
                    mark.Label.text = string.Format(Loc.Get("hud.hire"), _rules.SoldierGold, army, _rules.ArmyMax);
                    mark.Label.alpha = purse.Gold >= _rules.SoldierGold && army < _rules.ArmyMax ? 1f : DullAlpha;
                    mark.Ring.enabled = false;
                    continue;
                }

                mark.Label.text = string.Format(Loc.Get("hud.build"), _rules.BarracksWood);
                mark.Label.alpha = purse.Wood >= _rules.BarracksWood ? 1f : DullAlpha;
                mark.Ring.enabled = site.BuildProgress > 0;
                mark.Ring.fillAmount = (float)site.BuildProgress / buildTicks;
            }
        }

        private Mark Create()
        {
            var root = Instantiate(template, transform);
            root.gameObject.SetActive(true);
            return new Mark { Root = root, Label = root.GetComponentInChildren<TMP_Text>(true), Ring = root.GetComponentInChildren<Image>(true) };
        }

        private sealed class Mark
        {
            public RectTransform Root;
            public TMP_Text Label;
            public Image Ring;
        }
    }
}
