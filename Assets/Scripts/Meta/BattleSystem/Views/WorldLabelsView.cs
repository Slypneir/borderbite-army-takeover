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
    /// Short wood is dull, never red: red is the enemy. A label never leaves the safe area: it is pushed inside it by
    /// <see cref="EdgeInset"/>, and hidden while the place itself is out of the frame (UI review of f1d3ccb).
    /// </summary>
    public sealed class WorldLabelsView : MonoBehaviour
    {
        // over the roof of the barracks model (2.5 m)
        private const float Height = 2.8f;
        private const float DullAlpha = 0.45f;
        // the inset from the edges of the safe area, canvas units (STYLE.md 1 of the interface, EdgeInset)
        private const float EdgeInset = 24f;

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
            var safe = Screen.safeArea;
            var bounds = new Rect(safe.position / scale, safe.size / scale);
            var purse = simulation.SideOf(Side.Player);
            var army = simulation.ArmyOf(Side.Player);
            var buildTicks = Mathf.Max(1, simulation.Settings.BuildTicks);
            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                var owner = simulation.Grid.At(site.CenterX, site.CenterY);
                var mark = _marks[i];
                var x = (site.CenterX + 0.5f) * _rules.CellSize;
                var z = (site.CenterY + 0.5f) * _rules.CellSize;
                var shown = (site.Barracks == Side.Player || (!site.Built && owner == Side.Player))
                            && bounds.Contains((Vector2)_camera.WorldToScreenPoint(new Vector3(x, 0f, z)) / scale);
                mark.Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                if (site.Built)
                {
                    mark.Label.text = string.Format(Loc.Get("hud.hire"), _rules.SoldierGold, army, _rules.ArmyMax);
                    mark.Label.alpha = purse.Gold >= _rules.SoldierGold && army < _rules.ArmyMax ? 1f : DullAlpha;
                    mark.Ring.enabled = false;
                }
                else
                {
                    mark.Label.text = string.Format(Loc.Get("hud.build"), _rules.BarracksWood);
                    mark.Label.alpha = purse.Wood >= _rules.BarracksWood ? 1f : DullAlpha;
                    mark.Ring.enabled = site.BuildProgress > 0;
                    mark.Ring.fillAmount = (float)site.BuildProgress / buildTicks;
                }

                var point = (Vector2)_camera.WorldToScreenPoint(new Vector3(x, Height, z)) / scale;
                mark.Root.anchoredPosition = Inside(point, mark, bounds);
            }
        }

        /// <summary>The bottom middle of a label, moved so the text and the ring stay in the safe area with the inset.</summary>
        private static Vector2 Inside(Vector2 point, Mark mark, Rect bounds)
        {
            var half = Mathf.Max(mark.Label.preferredWidth, mark.Ring.rectTransform.rect.width) / 2f;
            var height = mark.Root.rect.height;
            var left = bounds.xMin + EdgeInset + half;
            var right = bounds.xMax - EdgeInset - half;
            var bottom = bounds.yMin + EdgeInset;
            var top = bounds.yMax - EdgeInset - height;
            return new Vector2(left <= right ? Mathf.Clamp(point.x, left, right) : bounds.center.x,
                bottom <= top ? Mathf.Clamp(point.y, bottom, top) : bottom);
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
