using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// Marks of layer 3 over the world (stage 1 spec 4.1, 4.3; brief of the card battle-marks):
    /// - the health of a building while it is hit and 2 s after: 120 wide over a barracks, 160 over a base, over the roof; a
    ///   light rim on the player's, a dark one on the enemy's; over the player's barracks above its price label;
    /// - signs at the edge of the frame when a building of the player is hit or the enemy takes a piece of the player's land by
    ///   enclosure out of sight: a point to the event with a mark inside (a building or land), red with a dark rim, at the edge
    ///   of the safe area (inset 24, under the HUD + 8 at the top); it pulses twice a second for 3 s, then stays, and goes 2 s
    ///   after the attack or at once when the place comes into the frame; no more than 3, those within 15° merge, the base first;
    /// - the same sign with crossed swords while a wave of the enemy gathers to leave (task 4.27): it points where the squads of
    ///   the wave are, from 5 s before they leave, and stays 2 s after;
    /// - «3, 2, 1» over the player's base while its hero waits to come back.
    /// Lives right under the canvas, anchored to its lower left corner; canvas units are pixels / (short side / 720).
    /// </summary>
    public sealed class BattleMarksView : MonoBehaviour
    {
        private const float BarracksHeight = 2.8f;
        private const float BaseHeight = 4.3f;
        private const float SignHoldSeconds = 2f;
        private const float PulseSeconds = 3f;
        private const float PulseRate = 2f;
        private const float MergeDegrees = 15f;
        private const int MaxSigns = 3;
        private const float EdgeInset = 24f;
        private const float HudGap = 8f;
        private const float LabelGap = 8f;
        // the height of a price label over the player's barracks (WorldLabelsView), canvas units
        private const float LabelHeight = 120f;
        private const int WaveKey = -1000;

        [SerializeField] private RectTransform barTemplate;
        [SerializeField] private RectTransform signTemplate;
        [SerializeField] private TMP_Text countdown;
        [SerializeField] private RectTransform hudBottom;

        [Tooltip("The row of the orders (task 4.18): nothing goes under it.")]
        [SerializeField] private RectTransform orderBar;

        [SerializeField] private float barracksBarWidth = 120f;
        [SerializeField] private float baseBarWidth = 160f;
        [SerializeField] private float signSize = 96f;
        [SerializeField] private float baseSignSize = 120f;
        [SerializeField] private Color lightRim = new Color(0.96f, 0.95f, 0.88f);
        [SerializeField] private Color darkRim = new Color(0.09f, 0.08f, 0.1f);
        [SerializeField] private Color barFill = new Color(0.96f, 0.95f, 0.88f);
        [SerializeField] private Color threat = new Color(0.9f, 0.2f, 0.17f);

        private readonly Dictionary<int, Bar> _bars = new Dictionary<int, Bar>();
        private readonly List<Bar> _barPool = new List<Bar>();
        private readonly List<Threat> _threats = new List<Threat>();
        private readonly List<Sign> _signs = new List<Sign>();
        private readonly Vector3[] _corners = new Vector3[4];
        private EventCursor _events;
        private int _captures;
        private object _simulation;
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

        private void Awake()
        {
            barTemplate.gameObject.SetActive(false);
            signTemplate.gameObject.SetActive(false);
            countdown.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
                return;

            if (_simulation != simulation)
            {
                _simulation = simulation;
                _captures = simulation.Captures.Count;
                _threats.Clear();
                foreach (var bar in _bars.Values)
                    bar.Root.gameObject.SetActive(false);
                _bars.Clear();
            }

            var now = Time.time;
            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            var safe = Screen.safeArea;
            var bounds = new Rect(safe.position / scale, safe.size / scale);
            bounds.yMin = Floor(bounds.yMin, scale, EdgeInset);
            var ceiling = bounds.yMax - EdgeInset;
            if (hudBottom != null)
            {
                hudBottom.GetWorldCorners(_corners);
                ceiling = Mathf.Min(ceiling, _corners[0].y / scale - HudGap);
            }

            _events.Read(simulation, e => Handle(e, now));
            for (; _captures < simulation.Captures.Count; _captures++)
            {
                var capture = simulation.Captures[_captures];
                if (capture.Side == Side.Enemy && capture.FromEnemy > 0)
                    Threaten(-100 - _captures, capture.CenterX, capture.CenterY, false, true, now);
            }

            if (simulation.WaveComing(out var waveX, out var waveY))
                Threaten(WaveKey, waveX, waveY, false, false, now, true);

            DrawBars(simulation, scale, bounds, ceiling, now);
            DrawSigns(scale, bounds, ceiling, now);
            DrawCountdown(simulation, scale);
        }

        private void Handle(BattleEvent battleEvent, float now)
        {
            if (battleEvent.Kind != EventKind.BuildingHit)
                return;

            // a key per building: places 0.., bases -1 and -2 by side
            var key = battleEvent.Id >= 0 ? battleEvent.Id : -1 - (int)battleEvent.Side;
            var isBase = battleEvent.Id < 0;
            if (!_bars.TryGetValue(key, out var bar))
            {
                bar = TakeBar();
                _bars[key] = bar;
            }

            bar.Side = battleEvent.Side;
            bar.IsBase = isBase;
            bar.Health = battleEvent.Value;
            bar.X = battleEvent.X;
            bar.Y = battleEvent.Y;
            // spec T12: a bar after a blow and 5 s more
            bar.Until = now + _view.BarHoldSeconds;
            bar.Max = isBase ? _rules.BaseHealth : _rules.BarracksHealth;
            bar.HitAt = now;
            if (battleEvent.Side == Side.Player)
                Threaten(key, battleEvent.X, battleEvent.Y, isBase, false, now);
        }

        private void Threaten(int key, int x, int y, bool isBase, bool land, float now, bool wave = false)
        {
            foreach (var known in _threats)
            {
                if (known.Key != key)
                    continue;

                known.Until = now + SignHoldSeconds;
                known.X = x;
                known.Y = y;
                return;
            }

            _threats.Add(new Threat { Key = key, X = x, Y = y, IsBase = isBase, Land = land, Wave = wave, Since = now, Until = now + SignHoldSeconds });
        }

        private void DrawBars(Core.BattleSystem.Services.BattleSimulation simulation, float scale, Rect bounds, float ceiling, float now)
        {
            var gone = ListPool.Keys;
            gone.Clear();
            foreach (var pair in _bars)
            {
                var bar = pair.Value;
                var standing = bar.IsBase ? simulation.Bases[(int)bar.Side].Standing : simulation.Sites[pair.Key].Barracks == bar.Side;
                if (now > bar.Until || !standing || bar.Health <= 0)
                {
                    bar.Root.gameObject.SetActive(false);
                    _barPool.Add(bar);
                    gone.Add(pair.Key);
                    continue;
                }

                var height = bar.IsBase ? BaseHeight : BarracksHeight;
                var screen = _camera.WorldToScreenPoint(new Vector3(bar.X * _rules.MetersPerUnit, height, bar.Y * _rules.MetersPerUnit));
                var point = (Vector2)screen / scale;
                // over the player's own barracks the price label stays; the bar goes over it
                if (!bar.IsBase && bar.Side == Side.Player)
                    point.y += LabelHeight + LabelGap;
                var width = bar.IsBase ? baseBarWidth : barracksBarWidth;
                // inside the safe area with the inset and under the HUD, as the labels (UI review)
                var half = width / 2f;
                point.x = Mathf.Clamp(point.x, bounds.xMin + EdgeInset + half, Mathf.Max(bounds.xMin + EdgeInset + half, bounds.xMax - EdgeInset - half));
                point.y = Mathf.Clamp(point.y, bounds.yMin + EdgeInset, Mathf.Max(bounds.yMin + EdgeInset, ceiling - bar.Root.sizeDelta.y));
                // a building out of the frame shows no bar: its sign at the edge tells of it, as a label hides (UI review)
                var ground = (Vector2)_camera.WorldToScreenPoint(new Vector3(bar.X * _rules.MetersPerUnit, 0f, bar.Y * _rules.MetersPerUnit)) / scale;
                bar.Root.gameObject.SetActive(screen.z > 0f && bounds.Contains(ground) && ground.y < ceiling);
                // a hit (task 4.22): the end of the fill flashes white, the bar jerks sideways; no «−1» for buildings
                var since = now - bar.HitAt;
                if (_view.BarShake && since < 0.15f)
                    point.x += Mathf.Sin(since * 90f) * 3f * (1f - since / 0.15f);
                bar.Flash.color = new Color(1f, 0.98f, 0.92f, Mathf.Clamp01(1f - since / 0.12f));
                bar.Root.anchoredPosition = point;
                bar.Root.sizeDelta = new Vector2(width, bar.Root.sizeDelta.y);
                bar.Rim.color = bar.Side == Side.Player ? lightRim : darkRim;
                bar.Fill.anchorMax = new Vector2(Mathf.Clamp01(bar.Health / Mathf.Max(1f, bar.Max)), 1f);
            }

            foreach (var key in gone)
                _bars.Remove(key);
        }

        private void DrawSigns(float scale, Rect bounds, float ceiling, float now)
        {
            var area = new Rect(bounds.xMin + EdgeInset, bounds.yMin + EdgeInset, bounds.width - 2f * EdgeInset, ceiling - bounds.yMin - EdgeInset);
            var centre = area.center;
            var shown = 0;
            _threats.RemoveAll(t => now > t.Until);
            // the base first, then the newest
            _threats.Sort((a, b) => a.IsBase != b.IsBase ? (a.IsBase ? -1 : 1) : b.Since.CompareTo(a.Since));
            var angles = ListPool.Angles;
            angles.Clear();
            foreach (var threat in _threats)
            {
                var screen = _camera.WorldToScreenPoint(new Vector3(threat.X * _rules.MetersPerUnit, 0f, threat.Y * _rules.MetersPerUnit));
                var point = (Vector2)screen / scale;
                if (screen.z > 0f && area.Contains(point))
                    continue;

                var direction = point - centre;
                if (screen.z <= 0f)
                    direction = -direction;
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                var merged = false;
                foreach (var other in angles)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(other, angle)) < MergeDegrees)
                        merged = true;
                }

                if (merged || shown >= MaxSigns)
                    continue;

                angles.Add(angle);
                var sign = SignAt(shown++);
                var size = threat.IsBase ? baseSignSize : signSize;
                sign.Root.sizeDelta = Vector2.one * size;
                sign.Root.anchoredPosition = Edge(centre, direction, area, size / 2f);
                sign.Point.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                sign.Building.SetActive(!threat.Land && !threat.Wave);
                sign.Land.SetActive(threat.Land && !threat.Wave);
                if (sign.Wave != null)
                    sign.Wave.SetActive(threat.Wave);
                var age = now - threat.Since;
                var pulse = age < PulseSeconds ? 0.5f + 0.5f * Mathf.Cos(age * PulseRate * 2f * Mathf.PI) : 1f;
                sign.Group.alpha = Mathf.Lerp(0.35f, 1f, pulse);
                sign.Root.gameObject.SetActive(true);
            }

            for (var i = shown; i < _signs.Count; i++)
                _signs[i].Root.gameObject.SetActive(false);
        }

        /// <summary>Where the ray from the centre crosses the edge of the area, pulled in by half the sign.</summary>
        private static Vector2 Edge(Vector2 centre, Vector2 direction, Rect area, float half)
        {
            if (direction.sqrMagnitude < 1e-4f)
                direction = Vector2.up;
            var inner = new Rect(area.xMin + half, area.yMin + half, Mathf.Max(0f, area.width - 2f * half), Mathf.Max(0f, area.height - 2f * half));
            var tx = direction.x > 0f ? (inner.xMax - centre.x) / direction.x : direction.x < 0f ? (inner.xMin - centre.x) / direction.x : float.MaxValue;
            var ty = direction.y > 0f ? (inner.yMax - centre.y) / direction.y : direction.y < 0f ? (inner.yMin - centre.y) / direction.y : float.MaxValue;
            return centre + direction * Mathf.Max(0f, Mathf.Min(tx, ty));
        }

        private void DrawCountdown(Core.BattleSystem.Services.BattleSimulation simulation, float scale)
        {
            var waiting = 0;
            foreach (var hero in simulation.Heroes)
            {
                if (hero.Side == Side.Player && !hero.Alive)
                    waiting = hero.RespawnTick - simulation.Tick;
            }

            var home = simulation.Bases[(int)Side.Player];
            countdown.gameObject.SetActive(waiting > 0 && home.Standing);
            if (waiting <= 0)
                return;

            var seconds = Mathf.CeilToInt(waiting / (float)simulation.Settings.TicksPerSecond);
            countdown.text = seconds.ToString();
            var screen = _camera.WorldToScreenPoint(new Vector3(home.X * _rules.MetersPerUnit, BaseHeight + 0.5f, home.Y * _rules.MetersPerUnit));
            ((RectTransform)countdown.transform).anchoredPosition = (Vector2)screen / scale;
        }

        private Bar TakeBar()
        {
            if (_barPool.Count > 0)
            {
                var pooled = _barPool[_barPool.Count - 1];
                _barPool.RemoveAt(_barPool.Count - 1);
                return pooled;
            }

            var root = Instantiate(barTemplate, transform);
            var fill = (RectTransform)root.Find("Fill");
            // the end of the fill flashes white at a hit (task 4.22): a narrow strip at its right edge
            var flash = new GameObject("HitFlash", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            flash.SetParent(fill, false);
            flash.anchorMin = new Vector2(1f, 0f);
            flash.anchorMax = new Vector2(1f, 1f);
            flash.pivot = new Vector2(1f, 0.5f);
            flash.sizeDelta = new Vector2(10f, 0f);
            var flashImage = flash.GetComponent<Image>();
            flashImage.raycastTarget = false;
            flashImage.color = new Color(1f, 0.98f, 0.92f, 0f);
            return new Bar
            {
                Root = root,
                Rim = root.GetComponent<Image>(),
                Fill = fill,
                Flash = flashImage,
                HitAt = -1000f,
            };
        }

        private Sign SignAt(int index)
        {
            while (_signs.Count <= index)
            {
                var root = Instantiate(signTemplate, transform);
                _signs.Add(new Sign
                {
                    Root = root,
                    Group = root.GetComponent<CanvasGroup>(),
                    Point = (RectTransform)root.Find("Point"),
                    Building = root.Find("Building").gameObject,
                    Land = root.Find("Land").gameObject,
                    Wave = root.Find("Wave")?.gameObject,
                });
            }

            return _signs[index];
        }

        private sealed class Bar
        {
            public RectTransform Root;
            public Image Rim;
            public RectTransform Fill;
            public Side Side;
            public bool IsBase;
            public int Health;
            public float Max;
            public int X;
            public int Y;
            public float Until;
            public float HitAt;
            public Image Flash;
        }

        private sealed class Threat
        {
            public int Key;
            public int X;
            public int Y;
            public bool IsBase;
            public bool Land;
            public bool Wave;
            public float Since;
            public float Until;
        }

        private sealed class Sign
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Point;
            public GameObject Building;
            public GameObject Land;
            public GameObject Wave;
        }

        private static class ListPool
        {
            public static readonly List<int> Keys = new List<int>();
            public static readonly List<float> Angles = new List<float>();
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
