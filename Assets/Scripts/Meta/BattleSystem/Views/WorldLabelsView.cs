using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// Labels over the places of the player (stage 1 spec 3.3), icons and numbers, no words (battle-marks card of the UI,
    /// «Значки над картой»): over an empty one «[barracks][wood] 10» and a ring of the progress of building, dull while wood is
    /// short; over a barracks «[gold] 5 · [soldier] N/30». Copies of <see cref="template"/> (a row of icons and numbers with a
    /// ring), one per place. Lives right under the canvas, anchored to its lower left corner. Short wood is dull, never red: red is
    /// the enemy. A label never leaves the safe area: it is pushed inside it by <see cref="EdgeInset"/> and kept
    /// <see cref="HudGap"/> under the bottom of the HUD, and hidden while the place itself is out of the frame, under the HUD or no
    /// room is left for it (UI reviews of f1d3ccb and 3752fe1: the HUD is always on top and always reads).
    /// </summary>
    /// <remarks>
    /// The mark of a place to build (task 4.26, battle-marks card «Метка места под казарму»; owner: seen always): a pin over every
    /// empty place in the frame at any distance from the hero, its tip 1.2 m over the middle of the place — blue where the player
    /// may build (its own or neutral land, owner 5 November) with «[wood] 5» under it and the ring of building round the
    /// medallion, grey on enemy land with no label. A built barracks has no pin. Off the frame no pin: the edges belong to the
    /// signs of threats.
    /// </remarks>
    public sealed class WorldLabelsView : MonoBehaviour
    {
        // the tip of the pin over the middle of a place, meters
        private const float PinHeight = 1.2f;
        // the price right under the tip of the pin (UI review of 4.26)
        private const float RowGap = 4f;
        // short of a resource: the number in text/disabled, the icon half seen — not the whole label (UI review of 4.26)
        private static readonly Color DisabledText = new Color(0.55f, 0.56f, 0.6f, 1f);
        private const float DullIcon = 0.6f;
        // the hero closer than this to the middle of a place sees through its pin and price (UI review of 4.26)
        private const float NearHero = 1.5f;
        private const float NearAlpha = 0.4f;
        // the top of the health bar over the hero, meters (BarsView: 2.15 m, 0.2 high)
        private const float HeroBar = 2.25f;
        private static readonly Color Grey = new Color(0.55f, 0.56f, 0.6f, 1f);
        // over the roof of the barracks model (2.5 m)
        private const float Height = 2.8f;
        // the inset from the edges of the safe area, canvas units (STYLE.md 1 of the interface, EdgeInset)
        private const float EdgeInset = 24f;
        // the gap between a label and the bottom of the HUD, canvas units
        private const float HudGap = 8f;

        [Tooltip("A label: a CanvasGroup with a ring (Image) and a row (HorizontalLayoutGroup) of Icon1, Icon2, Text1, Dot, Icon3, Text2.")]
        [SerializeField] private RectTransform template;

        [Tooltip("The lowest block of the HUD at the top of the screen (the row of counters): labels stay under it.")]
        [SerializeField] private RectTransform hudBottom;
        [Tooltip("The banner of the second chance: a part of the HUD, labels under it keep under its edge (task 4.36).")]
        [SerializeField] private SecondChanceView banner;

        [Tooltip("The row of the orders (task 4.18): nothing goes under it.")]
        [SerializeField] private RectTransform orderBar;

        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite woodIcon;
        [SerializeField] private Sprite barracksIcon;
        [SerializeField] private Sprite armyIcon;

        [Tooltip("The medallion of a place on the land of the player, and on any other; while empty the placeholder disc is tinted.")]
        [SerializeField] private Sprite siteOwn;
        [SerializeField] private Sprite siteOther;
        [SerializeField] private Color ownColor = new Color(0.25f, 0.5f, 0.95f, 1f);

        [Tooltip("The 64 px renders, for a window with less than a pixel per unit of the canvas.")]
        [SerializeField] private Sprite goldIconSmall;
        [SerializeField] private Sprite woodIconSmall;
        [SerializeField] private Sprite barracksIconSmall;

        private readonly Vector3[] _corners = new Vector3[4];

        private readonly List<Mark> _marks = new List<Mark>();
        private readonly List<Rect> _shown = new List<Rect>();
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

        /// <summary>The rectangles of the labels shown this frame, canvas units: the icons of resources give way to them.</summary>
        public IReadOnlyList<Rect> Shown => _shown;

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
                return;

            _shown.Clear();
            var sites = simulation.Sites;
            while (_marks.Count < sites.Count)
                _marks.Add(Create());

            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            var safe = Screen.safeArea;
            var bounds = new Rect(safe.position / scale, safe.size / scale);
            bounds.yMin = Floor(bounds.yMin, scale, EdgeInset);
            var ceiling = bounds.yMax - EdgeInset;
            if (hudBottom != null)
            {
                // an overlay canvas: world corners are screen pixels
                hudBottom.GetWorldCorners(_corners);
                ceiling = Mathf.Min(ceiling, _corners[0].y / scale - HudGap);
            }
            var small = scale < 1f;
            var gold = small && goldIconSmall != null ? goldIconSmall : goldIcon;
            var wood = small && woodIconSmall != null ? woodIconSmall : woodIcon;
            var barracks = small && barracksIconSmall != null ? barracksIconSmall : barracksIcon;
            var purse = simulation.SideOf(Side.Player);
            var army = simulation.ArmyOf(Side.Player);
            var hero = Vector2.positiveInfinity;
            var heroBarTop = float.NegativeInfinity;
            foreach (var state in simulation.Heroes)
            {
                if (state.Side != Side.Player || !state.Alive)
                    continue;

                hero = new Vector2(state.X, state.Y) * _rules.MetersPerUnit;
                // the top of the hero's bar (2.15 m, 0.2 high) on the screen, + 8 units
                heroBarTop = _camera.WorldToScreenPoint(new Vector3(hero.x, HeroBar, hero.y)).y / scale + 8f;
            }

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
                var shown = (site.Barracks == Side.Player || !site.Built) && bounds.Contains(ground) && ground.y < ceiling;
                mark.Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                // the player may build on its own and on neutral land: the pin is its
                var own = owner != Side.Enemy;
                // over the hero building there: the medallion and the price see-through, the ring stays solid
                var near = (new Vector2(x, z) - hero).sqrMagnitude < NearHero * NearHero;
                foreach (var graphic in mark.PinGraphics)
                    graphic.canvasRenderer.SetAlpha(near ? NearAlpha : 1f);
                mark.RowGroup.alpha = near ? NearAlpha : 1f;
                if (site.Built)
                {
                    // over a barracks of the player: the price of a soldier and the army, as before; no pin
                    mark.Pin.gameObject.SetActive(false);
                    mark.Row.gameObject.SetActive(true);
                    mark.Row.anchoredPosition = Vector2.zero;
                    Fill(mark, gold, null, _rules.SoldierGold.ToString(), true, armyIcon, $"{army}/{_rules.ArmyMax}");
                    Dull(mark, purse.Gold >= _rules.SoldierGold && army < _rules.ArmyMax);
                    mark.Ring.enabled = false;
                }
                else
                {
                    // an empty place: the pin, blue on the land of the player, grey elsewhere; the price only on its own
                    mark.Pin.gameObject.SetActive(true);
                    var sprite = own ? siteOwn : siteOther;
                    mark.Plate.sprite = sprite != null ? sprite : mark.Disc;
                    mark.Plate.color = sprite != null ? Color.white : own ? ownColor : Grey;
                    mark.Row.gameObject.SetActive(own);
                    if (own)
                    {
                        Fill(mark, wood, null, _rules.BarracksWood.ToString(), false, null, null);
                        mark.Row.anchoredPosition = new Vector2(0f, -mark.Row.rect.height - RowGap);
                        Dull(mark, purse.Wood >= _rules.BarracksWood);
                    }

                    mark.Ring.enabled = site.Builder == Side.Player && site.BuildProgress > 0;
                    mark.Ring.fillAmount = (float)site.BuildProgress / buildTicks;
                }

                var point = (Vector2)_camera.WorldToScreenPoint(new Vector3(x, site.Built ? Height : PinHeight, z)) / scale;
                // by the hero: the tip of the pin goes over the hero's health bar (+ 8), so the bar is never covered
                if (near && !site.Built && heroBarTop > point.y)
                    point.y = heroBarTop;

                var half = Mathf.Max(mark.Row.gameObject.activeSelf ? mark.Row.rect.width : 0f, mark.Pin.rect.width) / 2f;
                var under = banner != null ? banner.CeilingAt(point.x, half, ceiling, scale) : ceiling;
                if (Inside(point, mark, bounds, under, out var place))
                {
                    mark.Root.anchoredPosition = place;
                    var rowShown = mark.Row.gameObject.activeSelf;
                    var width = Mathf.Max(rowShown ? mark.Row.rect.width : 0f, mark.Pin.gameObject.activeSelf ? mark.Pin.rect.width : 0f);
                    var bottom = rowShown ? place.y + mark.Row.anchoredPosition.y : place.y;
                    var top = mark.Pin.gameObject.activeSelf ? place.y + mark.Pin.rect.height : place.y + mark.Row.rect.height;
                    _shown.Add(new Rect(place.x - width / 2f, bottom, width, top - bottom));
                }
                else
                    mark.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>Short of the resource: the numbers grey, the icons half seen; the dark backing stays as it is.</summary>
        private void Dull(Mark mark, bool enough)
        {
            var text = enough ? (Color)mark.TextColor : DisabledText;
            mark.Text1.color = text;
            mark.Text2.color = text;
            var icon = new Color(1f, 1f, 1f, enough ? 1f : DullIcon);
            mark.Icon1.color = icon;
            mark.Icon2.color = icon;
            mark.Icon3.color = icon;
        }

        /// <summary>
        /// Sets the icons and numbers of a label; a null leaves its slot out of the row. The dot between the price and the army is
        /// a small disc, not a glyph: the static atlas of the font has no «·».
        /// </summary>
        private static void Fill(Mark mark, Sprite first, Sprite second, string firstText, bool dot, Sprite third, string secondText)
        {
            var changed = Set(mark.Icon1, first) | Set(mark.Icon2, second) | Set(mark.Icon3, third) |
                          Set(mark.Text1, firstText) | Set(mark.Text2, secondText);
            if (mark.Dot.activeSelf != dot)
            {
                mark.Dot.SetActive(dot);
                changed = true;
            }

            if (changed)
                LayoutRebuilder.ForceRebuildLayoutImmediate(mark.Row);
        }

        private static bool Set(Image icon, Sprite sprite)
        {
            var shown = sprite != null;
            if (icon.gameObject.activeSelf == shown && icon.sprite == sprite)
                return false;

            icon.gameObject.SetActive(shown);
            icon.sprite = sprite;
            return true;
        }

        private static bool Set(TMP_Text label, string text)
        {
            var shown = !string.IsNullOrEmpty(text);
            if (label.gameObject.activeSelf == shown && (!shown || label.text == text))
                return false;

            label.gameObject.SetActive(shown);
            if (shown)
                label.text = text;
            return true;
        }

        /// <summary>
        /// The bottom middle of a label, moved so the row and the ring stay in the safe area with the inset and under the
        /// <paramref name="ceiling"/>; false when no room is left for the label.
        /// </summary>
        private static bool Inside(Vector2 point, Mark mark, Rect bounds, float ceiling, out Vector2 place)
        {
            var half = Mathf.Max(mark.Row.gameObject.activeSelf ? mark.Row.rect.width : 0f, mark.Pin.rect.width) / 2f;
            var left = bounds.xMin + EdgeInset + half;
            var right = bounds.xMax - EdgeInset - half;
            // a price under a pin hangs below the root: it keeps over the bottom too
            var bottom = bounds.yMin + EdgeInset - (mark.Row.gameObject.activeSelf ? Mathf.Min(0f, mark.Row.anchoredPosition.y) : 0f);
            var top = ceiling - mark.Root.rect.height;
            place = new Vector2(left <= right ? Mathf.Clamp(point.x, left, right) : bounds.center.x, Mathf.Clamp(point.y, bottom, top));
            return bottom <= top;
        }

        private Mark Create()
        {
            var root = Instantiate(template, transform);
            root.gameObject.SetActive(true);
            var row = (RectTransform)root.Find("Row");
            return new Mark
            {
                Root = root,
                Group = root.GetComponent<CanvasGroup>(),
                Pin = (RectTransform)root.Find("Pin"),
                Plate = root.Find("Pin/Plate").GetComponent<Image>(),
                Disc = root.Find("Pin/Plate").GetComponent<Image>().sprite,
                PinGraphics = System.Array.FindAll(root.Find("Pin").GetComponentsInChildren<Graphic>(true), g => g.name != "Ring"),
                RowGroup = row.GetComponent<CanvasGroup>(),
                Ring = root.Find("Pin/Ring").GetComponent<Image>(),
                Row = row,
                Icon1 = row.Find("Icon1").GetComponent<Image>(),
                Icon2 = row.Find("Icon2").GetComponent<Image>(),
                Text1 = row.Find("Text1").GetComponent<TMP_Text>(),
                Dot = row.Find("Dot").gameObject,
                Icon3 = row.Find("Icon3").GetComponent<Image>(),
                Text2 = row.Find("Text2").GetComponent<TMP_Text>(),
                TextColor = row.Find("Text1").GetComponent<TMP_Text>().color,
            };
        }

        private sealed class Mark
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Pin;
            public Image Plate;
            public Sprite Disc;
            public Graphic[] PinGraphics;
            public CanvasGroup RowGroup;
            public Image Ring;
            public RectTransform Row;
            public Image Icon1;
            public Image Icon2;
            public TMP_Text Text1;
            public GameObject Dot;
            public Image Icon3;
            public TMP_Text Text2;
            public Color TextColor;
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
