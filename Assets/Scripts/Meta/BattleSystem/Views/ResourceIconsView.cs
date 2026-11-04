using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// An icon over every pile of gold and bundle of wood (battle-marks card of the UI, «Значки над картой»): it says what lies
    /// there before the model is made out, the same in any language. 48 units, facing the camera (it is on the canvas), 0.6 m over
    /// the top of the pile, still — the pile hops, the icon does not. Gone with the pickup; half seen (0.4) closer than 1.5 m to the
    /// hero, so the hero is never covered; out of the frame or under the HUD — hidden. Under a window the whole layer hides
    /// (<see cref="BattleLayersView"/>). Lives right under the canvas, anchored to its lower left corner.
    /// </summary>
    public sealed class ResourceIconsView : MonoBehaviour
    {
        private const float Lift = 0.6f;
        // the tops of the models (a883021): the vein of gold 0.69 m, with its hop, and the woodpile 0.81 m
        private const float GoldTop = 0.8f;
        private const float WoodTop = 0.85f;
        private const float NearHero = 1.5f;
        private const float NearAlpha = 0.4f;
        private const float HudGap = 8f;

        [Tooltip("An icon: an Image 48 × 48 with the pivot at its bottom middle.")]
        [SerializeField] private Image template;

        [Tooltip("The lowest block of the HUD at the top of the screen: icons under it are hidden.")]
        [SerializeField] private RectTransform hudBottom;

        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite woodIcon;

        [Tooltip("The 64 px renders, for a window with less than a pixel per unit of the canvas.")]
        [SerializeField] private Sprite goldIconSmall;
        [SerializeField] private Sprite woodIconSmall;

        private readonly Vector3[] _corners = new Vector3[4];
        private readonly List<Image> _icons = new List<Image>();
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
            var used = 0;
            if (simulation != null)
            {
                var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
                var safe = Screen.safeArea;
                var bounds = new Rect(safe.position / scale, safe.size / scale);
                var ceiling = bounds.yMax;
                if (hudBottom != null)
                {
                    // an overlay canvas: world corners are screen pixels
                    hudBottom.GetWorldCorners(_corners);
                    ceiling = Mathf.Min(ceiling, _corners[0].y / scale - HudGap);
                }

                var hero = Vector2.positiveInfinity;
                foreach (var state in simulation.Heroes)
                {
                    if (state.Side == Side.Player && state.Alive)
                        hero = new Vector2(state.X, state.Y) * _rules.MetersPerUnit;
                }

                foreach (var resource in simulation.Resources)
                {
                    var gold = resource.Kind == ResourceKind.Gold;
                    var x = resource.X * _rules.MetersPerUnit;
                    var z = resource.Y * _rules.MetersPerUnit;
                    var point = (Vector2)_camera.WorldToScreenPoint(new Vector3(x, (gold ? GoldTop : WoodTop) + Lift, z)) / scale;
                    if (!bounds.Contains(point) || point.y > ceiling)
                        continue;

                    if (used == _icons.Count)
                        _icons.Add(Instantiate(template, transform));

                    var icon = _icons[used++];
                    icon.gameObject.SetActive(true);
                    var small = scale < 1f;
                    icon.sprite = gold ? small && goldIconSmall != null ? goldIconSmall : goldIcon
                        : small && woodIconSmall != null ? woodIconSmall : woodIcon;
                    icon.rectTransform.anchoredPosition = point;
                    var near = (new Vector2(x, z) - hero).sqrMagnitude < NearHero * NearHero;
                    icon.color = new Color(1f, 1f, 1f, near ? NearAlpha : 1f);
                }
            }

            for (var i = used; i < _icons.Count; i++)
                _icons[i].gameObject.SetActive(false);
        }
    }
}
