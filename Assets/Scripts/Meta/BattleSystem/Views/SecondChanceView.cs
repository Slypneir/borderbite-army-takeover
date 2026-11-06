using Core.BattleSystem.Enums;
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
    /// The second chance on the territory bar (task 4.36, spec 2.6a; battle-hud card «Второй шанс на полосе», accepted by the
    /// owner on 5 November), on placeholders until the art of the 3D artist:
    /// - the middle of the bar: two triangles 34 × 26 on the rims over and under it, tips to the groove, and a sunk dashed line
    ///   in the groove (yellow and dark strokes 7 / 5). Neither side holds it — yellow-black stripes; held — the solid colour of
    ///   the side with a cream rim and a solid line of that colour;
    /// - the chance passes (0.45 s): the triangles move 6 out and turn over (width 1 → 0 → 1), change colour on the edge with a
    ///   white flash of 0.08 s, 8 voxels; the line fills with the new colour from the top down;
    /// - the banner «Второй шанс» under the plate on two ropes from its lower rim at the middle of the bar: while nobody holds the
    ///   chance only the ends of the ropes show, 22 long, swaying; the first to take it — the banner drops out (0.3 s), bounces
    ///   and swings 6° dying out in 1.2 s; the chance passes while both bases stand — it turns over in 0.45 s and the shield
    ///   changes colour; taken from the player without a base — the ropes break and it falls turning and fades in 0.7 s;
    /// - a side without a base holding the chance: its triangles beat like a heart, a double push of 1.18 every 1.2 s;
    /// - «Возрождений нет» from the fall of the player's base: a plaque at the left under the HUD, an iron rim on dark, a grey
    ///   hero with a crossed arrow and the text in calm scarlet with a dark rim breathing (#c23d34 ↔ #e0574b, scale 1 ↔ 1.03 in
    ///   1.6 s); it slides in from the left in 0.35 s with a bounce and a white flash. On a tall screen the banner is at 0.85 and
    ///   the plaque centred under it, as wide (sizes: battle-hud card, «Размер знамени»).
    /// </summary>
    public sealed class SecondChanceView : MonoBehaviour
    {
        private const float PassSeconds = 0.45f;
        private const float DropSeconds = 0.3f;
        private const float SwingSeconds = 1.2f;
        private const float BreakSeconds = 0.7f;
        private const float BeatEvery = 1.2f;
        private const float PlaqueSeconds = 0.35f;
        // the banner hangs this far under the lower rim (owner, 6 November: was 22–30)
        private const float RopeLength = 12f;
        private const float RopeEnds = 22f;
        // the ropes hang this far from the middle on a wide screen
        private const float RopeApart = 110f;
        // the teeth hang under the cloth this far; labels keep this far under them
        private const float Teeth = 12f;
        private const float Gap = 8f;
        private static readonly Color Cream = new Color(0.96f, 0.95f, 0.88f, 1f);
        private readonly Vector3[] _corners = new Vector3[4];
        private static readonly Color Dark = new Color(0.09f, 0.08f, 0.1f, 1f);

        [Tooltip("The territory bar: the triangles and the banner hang at its middle.")]
        [SerializeField] private RectTransform bar;
        [Tooltip("The iron plate of the HUD: the banner hangs from its lower rim.")]
        [SerializeField] private RectTransform plate;
        [Tooltip("Top and Bottom: each with Rim, Fill, Hazard and Flash.")]
        [SerializeField] private RectTransform[] triangles;
        [Tooltip("The line in the groove: Dashes and Solid (a filled image from the top).")]
        [SerializeField] private Image solidLine;
        [SerializeField] private RectTransform voxels;
        [Tooltip("The banner: Ropes (Left, Right), Cloth with Shield and Label.")]
        [SerializeField] private RectTransform banner;
        [Tooltip("«Возрождений нет»: the plaque with Flash and Label.")]
        [SerializeField] private RectTransform plaque;

        private IBattleDriver _battle;
        private BattleViewConfig _view;
        private object _simulation;
        private Side _holder;
        private Side _from;
        private float _passAt = -10f;
        private float _dropAt = -10f;
        private float _turnAt = -10f;
        private float _breakAt = -10f;
        private float _plaqueAt = -10f;
        private bool _plaqueShown;
        private Image _shield;
        private TMP_Text _bannerLabel;
        private TMP_Text _plaqueLabel;
        private Image _plaqueFlash;
        private RectTransform _cloth;
        private RectTransform _ropes;
        private float _plaqueWidth;
        private float _plaqueHeight;

        // a tall screen keeps the stack of the HUD, the banner and the plaque within a quarter of its height (UI review of 4.36)
        private static float Hang => RopeLength;
        private const float NarrowPlaqueHeight = 48f;

        /// <summary>A tall screen: the banner at 0.85 and the plaque centred under it, the stack within a quarter of the height.</summary>
        private static bool Narrow => Screen.height > Screen.width;

        private static float BannerScale => Narrow ? 0.85f : 1f;

        [Inject]
        public void Construct(IBattleDriver battle, BattleViewConfig view)
        {
            _battle = battle;
            _view = view;
        }

        private void Awake()
        {
            _cloth = (RectTransform)banner.Find("Cloth");
            _ropes = (RectTransform)banner.Find("Ropes");
            _shield = _cloth.Find("Shield").GetComponent<Image>();
            _bannerLabel = _cloth.Find("Label").GetComponent<TMP_Text>();
            _plaqueLabel = plaque.Find("Label").GetComponent<TMP_Text>();
            _plaqueFlash = plaque.Find("Flash").GetComponent<Image>();
            // thick by the face, not by bold: a dilate of 0.25 and a dark rim of 2 at 36 (UI review of 4.36)
            _plaqueLabel.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.25f);
            _plaqueLabel.outlineColor = Dark;
            _plaqueLabel.outlineWidth = 0.2f;
            _plaqueWidth = plaque.sizeDelta.x;
            _plaqueHeight = plaque.sizeDelta.y;
        }

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
                return;

            var now = Time.time;
            if (_simulation != simulation)
            {
                _simulation = simulation;
                _holder = simulation.SecondChance;
                _from = _holder;
                _passAt = _dropAt = _turnAt = _breakAt = _plaqueAt = -10f;
                _plaqueShown = false;
            }

            var holder = simulation.SecondChance;
            if (holder != _holder)
            {
                _from = _holder;
                _holder = holder;
                _passAt = now;
                if (_from == Side.Neutral)
                    _dropAt = now;
                else if (_from == Side.Player && !simulation.Bases[(int)Side.Player].Standing)
                    _breakAt = now;
                else
                    _turnAt = now;
            }

            var playerFell = !simulation.Bases[(int)Side.Player].Standing;
            if (playerFell && !_plaqueShown)
            {
                _plaqueShown = true;
                _plaqueAt = now;
            }

            var beating = holder != Side.Neutral && !simulation.Bases[(int)holder].Standing;
            HideMiddleRivets();
            DrawTriangles(now, beating);
            DrawLine(now);
            DrawBanner(now);
            DrawPlaque(now);
        }

        /// <summary>
        /// The banner is a part of the HUD (UI review of 4.36): a label or a mark of the world <paramref name="half"/> wide at
        /// <paramref name="x"/> keeps under its lower edge (its teeth) + 8, canvas units; elsewhere the ceiling stays.
        /// </summary>
        public float CeilingAt(float x, float half, float ceiling, float scale)
        {
            if (!Span(scale, out var left, out var right, out var bottom) || x + half < left || x - half > right)
                return ceiling;

            return Mathf.Min(ceiling, bottom);
        }

        /// <summary>
        /// The room the banner takes, canvas units, with the gap of 8 round it: its sides and the line under its teeth; false
        /// while it does not hang.
        /// </summary>
        public bool Span(float scale, out float left, out float right, out float bottom) => Span(scale, out left, out right, out bottom, out _);

        /// <summary>The room of the banner as above and the top of its cloth.</summary>
        public bool Span(float scale, out float left, out float right, out float bottom, out float top)
        {
            left = right = bottom = top = 0f;
            if (_cloth == null || !_cloth.gameObject.activeInHierarchy)
                return false;

            // an overlay canvas: world corners are screen pixels
            _cloth.GetWorldCorners(_corners);
            left = Mathf.Min(_corners[0].x, _corners[2].x) / scale - Gap;
            right = Mathf.Max(_corners[0].x, _corners[2].x) / scale + Gap;
            bottom = Mathf.Min(_corners[0].y, _corners[2].y) / scale - Teeth * BannerScale - Gap;
            top = Mathf.Max(_corners[0].y, _corners[2].y) / scale;
            return true;
        }

        private Color ColorOf(Side side) => side == Side.Player ? _view.PlayerColor : _view.EnemyColor;

        /// <summary>The triangles stand in place of the rivets of the rims at the middle of the bar: those rivets hide.</summary>
        private void HideMiddleRivets()
        {
            var middle = bar.TransformPoint(bar.rect.center).x;
            var reach = bar.TransformVector(new Vector3(40f, 0f, 0f)).x;
            foreach (Transform child in plate)
            {
                if (!child.name.StartsWith("Rivet"))
                    continue;

                var image = child.GetComponent<Image>();
                if (image != null)
                    image.enabled = Mathf.Abs(child.position.x - middle) > reach;
            }
        }

        private void DrawTriangles(float now, bool beating)
        {
            var t = (now - _passAt) / PassSeconds;
            var passing = t < 1f;
            // on the edge the colour changes: before it the old one shows
            var shown = passing && t < 0.5f ? _from : _holder;
            var width = passing ? Mathf.Abs(Mathf.Cos(t * Mathf.PI)) : 1f;
            var away = passing ? 6f * Mathf.Sin(t * Mathf.PI) : 0f;
            var beat = 1f;
            if (beating)
            {
                var phase = now % BeatEvery;
                beat = 1f + 0.18f * Mathf.Max(Bump(phase, 0f, 0.14f), Bump(phase, 0.22f, 0.14f));
            }

            for (var i = 0; i < triangles.Length; i++)
            {
                var triangle = triangles[i];
                var up = i == 0 ? 1f : -1f;
                triangle.anchoredPosition = new Vector2(0f, up * (13f + away));
                triangle.localScale = new Vector3(width * beat, beat, 1f);
                var held = shown != Side.Neutral;
                triangle.Find("Rim").GetComponent<Image>().color = held ? Cream : Dark;
                var fill = triangle.Find("Fill").GetComponent<Image>();
                fill.enabled = held;
                fill.color = held ? ColorOf(shown) : Color.white;
                triangle.Find("Hazard").GetComponent<Image>().enabled = !held;
                var flash = now - (_passAt + PassSeconds / 2f);
                triangle.Find("Flash").GetComponent<Image>().color = new Color(1f, 1f, 1f, passing && flash >= 0f && flash < 0.08f ? 1f : 0f);
            }

            // 8 voxels of the colour of the side fly out of the middle while it passes
            for (var i = 0; i < voxels.childCount; i++)
            {
                var voxel = (RectTransform)voxels.GetChild(i);
                voxel.gameObject.SetActive(passing && _holder != Side.Neutral);
                if (!voxel.gameObject.activeSelf)
                    continue;

                var angle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                voxel.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (8f + 34f * t);
                var color = ColorOf(_holder);
                color.a = 1f - t;
                voxel.GetComponent<Image>().color = color;
            }
        }

        private void DrawLine(float now)
        {
            if (_holder == Side.Neutral)
            {
                solidLine.enabled = false;
                return;
            }

            solidLine.enabled = true;
            solidLine.color = ColorOf(_holder);
            solidLine.fillAmount = Mathf.Clamp01((now - _passAt) / PassSeconds);
        }

        private void DrawBanner(float now)
        {
            // the banner hangs from the lower rim of the plate at the middle of the bar
            var corners = new Vector3[4];
            plate.GetWorldCorners(corners);
            var rim = bar.InverseTransformPoint(corners[0]).y - bar.rect.yMin;
            banner.anchoredPosition = new Vector2(0f, rim + 6f);

            var broken = now - _breakAt;
            if (_holder == Side.Neutral && broken >= BreakSeconds)
            {
                // only the ends of the ropes peep out from under the plate, swaying
                _cloth.gameObject.SetActive(false);
                _ropes.gameObject.SetActive(true);
                SetRopes(RopeEnds, 4f * Mathf.Sin(now * 2.4f));
                banner.localRotation = Quaternion.identity;
                return;
            }

            _cloth.gameObject.SetActive(true);
            _ropes.gameObject.SetActive(true);
            if (_breakAt > 0f && broken < BreakSeconds)
            {
                // the ropes break: the banner falls turning and fades
                var t = broken / BreakSeconds;
                _ropes.gameObject.SetActive(false);
                _cloth.anchoredPosition = new Vector2(0f, -Hang - 140f * t * t);
                _cloth.localRotation = Quaternion.Euler(0f, 0f, -40f * t);
                SetAlpha(_cloth, 1f - t);
                return;
            }

            if (_holder == Side.Neutral)
                return;

            SetAlpha(_cloth, 1f);
            var drop = now - _dropAt;
            var fall = Mathf.Clamp01(drop / DropSeconds);
            // the fall with a bounce on the ropes at its end
            var bounce = drop > DropSeconds && drop < DropSeconds + 0.25f ? 8f * Mathf.Sin((drop - DropSeconds) / 0.25f * Mathf.PI) : 0f;
            var hang = Mathf.Lerp(0f, Hang, fall * fall) + bounce;
            SetRopes(Mathf.Max(RopeEnds, hang), 0f);
            _cloth.anchoredPosition = new Vector2(0f, -hang);
            var swing = drop > DropSeconds ? 6f * Mathf.Exp(-3f * (drop - DropSeconds) / SwingSeconds) *
                                             Mathf.Sin((drop - DropSeconds) * 2f * Mathf.PI / 0.6f) : 0f;
            _cloth.localRotation = Quaternion.Euler(0f, 0f, swing);

            // the chance passes while both bases stand: the banner turns over, the shield changes colour on its edge
            var turn = (now - _turnAt) / PassSeconds;
            var turning = turn < 1f;
            _cloth.localScale = new Vector3((turning ? Mathf.Abs(Mathf.Cos(turn * Mathf.PI)) : 1f) * BannerScale, BannerScale, 1f);
            _shield.color = ColorOf(turning && turn < 0.5f && _from != Side.Neutral ? _from : _holder);
            _bannerLabel.text = Loc.Get("battle.second_chance");
        }

        private void SetRopes(float length, float sway)
        {
            foreach (RectTransform rope in _ropes)
            {
                rope.anchoredPosition = new Vector2(Mathf.Sign(rope.anchoredPosition.x) * RopeApart * BannerScale, 0f);
                rope.sizeDelta = new Vector2(rope.sizeDelta.x, length);
                rope.localRotation = Quaternion.Euler(0f, 0f, sway);
            }
        }

        private void DrawPlaque(float now)
        {
            plaque.gameObject.SetActive(_plaqueShown);
            if (!_plaqueShown)
                return;

            var t = Mathf.Clamp01((now - _plaqueAt) / PlaqueSeconds);
            // out from the left with a bounce: past the place by 10%, then back
            var slide = t < 0.75f ? Mathf.Lerp(-1.1f, 0.06f, t / 0.75f) : Mathf.Lerp(0.06f, 0f, (t - 0.75f) / 0.25f);
            // on a tall screen the plaque stands centred under the teeth of the banner, as wide, one block with it; on a wide one
            // at the left under the HUD
            var y = -10f;
            var left = 16f;
            var width = _plaqueWidth;
            if (Narrow && _cloth.gameObject.activeInHierarchy)
            {
                _cloth.GetWorldCorners(_corners);
                var a = plate.InverseTransformPoint(_corners[0]);
                var b = plate.InverseTransformPoint(_corners[2]);
                width = Mathf.Abs(b.x - a.x);
                left = Mathf.Min(a.x, b.x) - plate.rect.xMin;
                y = Mathf.Min(a.y, b.y) - plate.rect.yMin - Teeth * BannerScale - Gap;
            }

            plaque.sizeDelta = new Vector2(width, Narrow ? NarrowPlaqueHeight : _plaqueHeight);
            plaque.anchoredPosition = new Vector2(left + slide * width, y);
            var flash = now - _plaqueAt - PlaqueSeconds;
            _plaqueFlash.color = new Color(1f, 1f, 1f, flash >= 0f && flash < 0.12f ? 0.8f * (1f - flash / 0.12f) : 0f);
            var breath = 0.5f - 0.5f * Mathf.Cos(now / 1.6f * 2f * Mathf.PI);
            // the pulse is of colour, not of alpha: a paler letter reads worse (UI review of 4.36)
            _plaqueLabel.color = Color.Lerp(new Color32(0xc2, 0x3d, 0x34, 0xff), new Color32(0xe0, 0x57, 0x4b, 0xff), breath);
            _plaqueLabel.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.03f, breath);
            _plaqueLabel.text = Loc.Get("battle.no_respawns");
        }

        private static float Bump(float phase, float start, float length)
        {
            var t = (phase - start) / length;
            return t < 0f || t > 1f ? 0f : Mathf.Sin(t * Mathf.PI);
        }

        private static void SetAlpha(RectTransform root, float alpha)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = alpha;
        }
    }
}
