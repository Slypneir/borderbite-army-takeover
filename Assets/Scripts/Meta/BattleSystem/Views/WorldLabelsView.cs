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
    /// <see cref="EdgeInset"/> and kept <see cref="HudGap"/> under the bottom of the HUD, and hidden while the place itself is out of
    /// the frame, under the HUD or no room is left for it (UI reviews of f1d3ccb and 3752fe1: the HUD is always on top and always reads).
    /// </summary>
    public sealed class WorldLabelsView : MonoBehaviour
    {
        // over the roof of the barracks model (2.5 m)
        private const float Height = 2.8f;
        private const float DullAlpha = 0.45f;
        // the inset from the edges of the safe area, canvas units (STYLE.md 1 of the interface, EdgeInset)
        private const float EdgeInset = 24f;
        // the gap between a label and the bottom of the HUD, canvas units
        private const float HudGap = 8f;

        [SerializeField] private RectTransform template;

        [Tooltip("The lowest block of the HUD at the top of the screen (the row of counters): labels stay under it.")]
        [SerializeField] private RectTransform hudBottom;

        private readonly Vector3[] _corners = new Vector3[4];

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
            var ceiling = bounds.yMax - EdgeInset;
            if (hudBottom != null)
            {
                // an overlay canvas: world corners are screen pixels
                hudBottom.GetWorldCorners(_corners);
                ceiling = Mathf.Min(ceiling, _corners[0].y / scale - HudGap);
            }
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
                // the place itself must be in the frame and not under the HUD
                var ground = (Vector2)_camera.WorldToScreenPoint(new Vector3(x, 0f, z)) / scale;
                var shown = (site.Barracks == Side.Player || (!site.Built && owner == Side.Player))
                            && bounds.Contains(ground) && ground.y < ceiling;
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
                if (Inside(point, mark, bounds, ceiling, out var place))
                    mark.Root.anchoredPosition = place;
                else
                    mark.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The bottom middle of a label, moved so the text and the ring stay in the safe area with the inset and under the
        /// <paramref name="ceiling"/>; false when no room is left for the label.
        /// </summary>
        private static bool Inside(Vector2 point, Mark mark, Rect bounds, float ceiling, out Vector2 place)
        {
            var half = Mathf.Max(mark.Label.preferredWidth, mark.Ring.rectTransform.rect.width) / 2f;
            var left = bounds.xMin + EdgeInset + half;
            var right = bounds.xMax - EdgeInset - half;
            var bottom = bounds.yMin + EdgeInset;
            var top = ceiling - mark.Root.rect.height;
            place = new Vector2(left <= right ? Mathf.Clamp(point.x, left, right) : bounds.center.x, Mathf.Clamp(point.y, bottom, top));
            return bottom <= top;
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
