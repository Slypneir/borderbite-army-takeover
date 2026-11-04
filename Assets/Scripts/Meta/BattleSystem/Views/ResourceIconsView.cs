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
    /// An icon over a pile of gold or a bundle of wood (battle-marks card of the UI, «Значки над картой»): it says what lies
    /// there before the model is made out, the same in any language. Only near the player's hero: within the reach of
    /// <see cref="BattleViewConfig.ResourceIconReach"/> (5 m), fading in and out (0.3 s; owner, 4 November). 48 units, facing the camera (it is on the canvas), 0.6 m over
    /// the top of the pile, still — the pile hops, the icon does not. Gone with the pickup; half seen (0.4) closer than 1.5 m to the
    /// hero, so the hero is never covered; out of the frame or under the HUD — hidden. Labels over buildings matter more (UI
    /// designer, 4 November): an icon that would cross a label is hidden, and of two icons that cross the nearer to the hero
    /// stays; the icons lie under the labels (earlier in the canvas, and placed after them). Under a window the whole layer hides
    /// (<see cref="BattleLayersView"/>). Lives right under the canvas, anchored to its lower left corner.
    /// </summary>
    [DefaultExecutionOrder(100)]
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

        [Tooltip("The row of the orders (task 4.18): nothing goes under it.")]
        [SerializeField] private RectTransform orderBar;

        [Tooltip("The labels over places and barracks: an icon never crosses them.")]
        [SerializeField] private WorldLabelsView labels;

        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite woodIcon;

        [Tooltip("The 64 px renders, for a window with less than a pixel per unit of the canvas.")]
        [SerializeField] private Sprite goldIconSmall;
        [SerializeField] private Sprite woodIconSmall;

        private readonly Vector3[] _corners = new Vector3[4];
        private readonly List<Image> _icons = new List<Image>();
        private readonly List<(float Near, Vector2 Point, bool Gold, float Alpha)> _wanted = new List<(float, Vector2, bool, float)>();
        private readonly Dictionary<int, float> _fades = new Dictionary<int, float>();
        private readonly List<int> _gone = new List<int>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<Rect> _taken = new List<Rect>();
        private IBattleDriver _battle;
        private BattleConfig _rules;
        private BattleViewConfig _view;
        private Camera _camera;

        [Inject]
        public void Construct(IBattleDriver battle, BattleConfig rules, BattleViewConfig view, Camera mainCamera)
        {
            _battle = battle;
            _rules = rules;
            _view = view;
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
                bounds.yMin = Floor(bounds.yMin, scale, 0f);
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

                _wanted.Clear();
                _present.Clear();
                var reach = _view.ResourceIconReach;
                var step = Time.deltaTime / Mathf.Max(0.01f, _view.ResourceIconFade);
                foreach (var resource in simulation.Resources)
                {
                    var gold = resource.Kind == ResourceKind.Gold;
                    var x = resource.X * _rules.MetersPerUnit;
                    var z = resource.Y * _rules.MetersPerUnit;
                    var near = (new Vector2(x, z) - hero).sqrMagnitude;
                    // fades in near the hero, out away from it
                    _present.Add(resource.Id);
                    _fades.TryGetValue(resource.Id, out var fade);
                    fade = Mathf.MoveTowards(fade, near < reach * reach ? 1f : 0f, step);
                    _fades[resource.Id] = fade;
                    if (fade <= 0f)
                        continue;

                    var point = (Vector2)_camera.WorldToScreenPoint(new Vector3(x, (gold ? GoldTop : WoodTop) + Lift, z)) / scale;
                    if (!bounds.Contains(point) || point.y > ceiling)
                        continue;

                    _wanted.Add((near, point, gold, fade * (near < NearHero * NearHero ? NearAlpha : 1f)));
                }

                // a picked resource takes its fade with it
                _gone.Clear();
                foreach (var id in _fades.Keys)
                {
                    if (!_present.Contains(id))
                        _gone.Add(id);
                }

                foreach (var id in _gone)
                    _fades.Remove(id);

                // the nearest to the hero first: of two icons that cross, it stays
                _wanted.Sort((a, b) => a.Near.CompareTo(b.Near));
                _taken.Clear();
                var size = template.rectTransform.sizeDelta;
                var small = scale < 1f;
                foreach (var wanted in _wanted)
                {
                    var box = new Rect(wanted.Point.x - size.x / 2f, wanted.Point.y, size.x, size.y);
                    if (Crosses(box, _taken) || (labels != null && Crosses(box, labels.Shown)))
                        continue;

                    _taken.Add(box);
                    if (used == _icons.Count)
                        _icons.Add(Instantiate(template, transform));

                    var icon = _icons[used++];
                    icon.gameObject.SetActive(true);
                    icon.sprite = wanted.Gold ? small && goldIconSmall != null ? goldIconSmall : goldIcon
                        : small && woodIconSmall != null ? woodIconSmall : woodIcon;
                    icon.rectTransform.anchoredPosition = wanted.Point;
                    icon.color = new Color(1f, 1f, 1f, wanted.Alpha);
                }
            }

            for (var i = used; i < _icons.Count; i++)
                _icons[i].gameObject.SetActive(false);
        }

        private static bool Crosses(Rect box, IReadOnlyList<Rect> others)
        {
            for (var i = 0; i < others.Count; i++)
            {
                if (box.Overlaps(others[i]))
                    return true;
            }

            return false;
        }

        /// <summary>The bottom of the free part of the screen: over the row of the orders + 8, canvas units.</summary>
        private float Floor(float bottom, float scale, float inset)
        {
            if (orderBar == null || !orderBar.gameObject.activeInHierarchy)
                return bottom;

            orderBar.GetWorldCorners(_corners);
            return Mathf.Max(bottom, _corners[1].y / scale + 8f - inset);
        }
    }
}
