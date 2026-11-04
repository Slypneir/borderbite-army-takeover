using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.BattleSystem.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The flight of resources (task 4.25, resource-flight card of the UI, spec 3.1, 3.9; owner: «красиво, сочно, понятно»). One
    /// unit, one icon. A mined unit pops out of the pile, hangs a moment and flies along an arc bent away from the middle of the
    /// screen into its counter, which counts it on landing; a built barracks draws 5 logs out of the wood counter (each −1 as it
    /// leaves) in a fan to the place, melting in with dust; a hired soldier draws a coin out of the gold counter that chases him,
    /// and the army counts him when it hits. A series of hires quicker than 0.8 s: the first 2 coins whole, then light ones —
    /// smaller, quicker, no ring and no «−N», at most one in the air (owner's choice B). Only the player's side; the battle never
    /// waits for a flight — the counters of <see cref="ResourceHudView"/> show «seen» numbers meanwhile. At most 8 in the air,
    /// the rest counts at once; under a window everything lands at once. Off: <see cref="BattleViewConfig.ResourceFlights"/>.
    /// </summary>
    public sealed class ResourceFlightView : MonoBehaviour
    {
        private const int MaxFlights = 8;
        private const float Pop = 0.14f;
        private const float Hover = 0.04f;
        private const float MineFly = 0.5f;
        private const float LogGap = 0.07f;
        private const float LogDrop = 0.1f;
        private const float LogFly = 0.55f;
        private const float CoinFly = 0.42f;
        private const float LightCoinFly = 0.32f;
        private const float SeriesGap = 0.8f;
        private const int WholeCoins = 2;
        private const float EdgeInset = 24f;
        private static readonly Color Cream = new Color(0.96f, 0.95f, 0.88f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.827f, 0.302f, 1f);
        private static readonly Color WoodColor = new Color(0.72f, 0.5f, 0.3f, 1f);
        private static readonly Color DustColor = new Color(0.85f, 0.78f, 0.62f, 1f);

        [SerializeField] private Image template;
        [SerializeField] private Image sparkTemplate;
        [SerializeField] private Image ringTemplate;
        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite woodIcon;
        [SerializeField] private ResourceHudView hud;

        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly List<Ring> _rings = new List<Ring>();
        private readonly Stack<Image> _freeIcons = new Stack<Image>();
        private IBattleDriver _battle;
        private BattleConfig _rules;
        private BattleViewConfig _view;
        private Camera _camera;
        private BattleScreen _screen;
        private SoldiersView _soldiers;
        private EventCursor _events;
        private BattleSimulation _simulation;
        private RectTransform _layer;
        private float _lastHire = -100f;
        private int _series;
        private int _bend;

        [Inject]
        public void Construct(IBattleDriver battle, BattleConfig rules, BattleViewConfig view, Camera mainCamera, BattleScreen screen,
            SoldiersView soldiers)
        {
            _battle = battle;
            _rules = rules;
            _view = view;
            _camera = mainCamera;
            _screen = screen;
            _soldiers = soldiers;
        }

        private void Awake()
        {
            _layer = (RectTransform)transform;
            template.gameObject.SetActive(false);
            sparkTemplate.gameObject.SetActive(false);
            ringTemplate.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation != _simulation)
            {
                _simulation = simulation;
                _events = default;
                if (simulation != null)
                    _events.Read(simulation, _ => { });
                LandAll(true);
            }

            if (simulation == null)
                return;

            _events.Read(simulation, Handle);
            if (_screen != null && _screen.Covered)
                LandAll(false);

            var dt = Time.deltaTime;
            for (var i = _flights.Count - 1; i >= 0; i--)
            {
                if (Step(_flights[i], dt))
                    continue;

                Release(_flights[i].Image);
                _flights.RemoveAt(i);
            }

            StepSparks(dt);
            Offsets();
        }

        private void Handle(BattleEvent e)
        {
            if (e.Side != Side.Player)
                return;

            switch (e.Kind)
            {
                case EventKind.Pickup:
                {
                    var gold = e.Detail == (int)ResourceKind.Gold;
                    Add(new Flight
                    {
                        Kind = FlightKind.Mine, Gold = gold, World = World(e.X, e.Y, 0.8f),
                        Jitter = new Vector2(Random.Range(-24f, 24f), 0f),
                    });
                    Sparks(Screen(World(e.X, e.Y, 0.6f)), Cream, 5, 7f);
                    break;
                }
                case EventKind.Built:
                    for (var i = 0; i < 5; i++)
                        Add(new Flight { Kind = FlightKind.Log, Gold = false, World = World(e.X, e.Y, 0.2f), Delay = i * LogGap, Index = i });
                    break;
                case EventKind.Hired:
                {
                    var now = Time.time;
                    _series = now - _lastHire < SeriesGap ? _series + 1 : 0;
                    _lastHire = now;
                    var light = _series >= WholeCoins;
                    if (light && _flights.Exists(f => f.Kind == FlightKind.Coin && f.Light))
                    {
                        // one light coin in the air: the rest counts at once
                        hud.Change(0, -1, true);
                        hud.Change(2, 1, true);
                        break;
                    }

                    _bend = -_bend == 0 ? 1 : -_bend;
                    Add(new Flight { Kind = FlightKind.Coin, Gold = true, World = World(e.X, e.Y, 0.5f), Target = e.Id, Light = light, Bend = _bend });
                    break;
                }
            }
        }

        /// <summary>A new flight, or — off, under a window, or with 8 in the air — its counting at once.</summary>
        private void Add(Flight flight)
        {
            if (!_view.ResourceFlights || (_screen != null && _screen.Covered) || _flights.Count >= MaxFlights)
            {
                Leave(flight);
                Land(flight, true);
                return;
            }

            flight.Image = Take(flight.Gold ? goldIcon : woodIcon);
            flight.Start = flight.Kind == FlightKind.Mine ? Screen(flight.World) : Counter(flight.Gold ? 0 : 1);
            _flights.Add(flight);
        }

        /// <summary>One frame of a flight; false when it is over.</summary>
        private bool Step(Flight f, float dt)
        {
            f.Age += dt;
            var t = f.Age - f.Delay;
            if (t < 0f)
            {
                f.Image.enabled = false;
                return true;
            }

            if (!f.Left)
                Leave(f);

            f.Image.enabled = true;
            var rect = f.Image.rectTransform;
            switch (f.Kind)
            {
                case FlightKind.Mine:
                {
                    var target = Counter(f.Gold ? 0 : 1);
                    if (t < Pop)
                    {
                        var p = t / Pop;
                        rect.anchoredPosition = f.Start + (Vector2.up * 40f + f.Jitter) * Ease(p);
                        rect.sizeDelta = Vector2.one * 60f * (p < 0.7f ? Mathf.Lerp(0.4f, 1.15f, p / 0.7f) : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f));
                        return true;
                    }

                    var from = f.Start + Vector2.up * 40f + f.Jitter;
                    if (t < Pop + Hover)
                    {
                        rect.anchoredPosition = from;
                        return true;
                    }

                    var q = Mathf.Clamp01((t - Pop - Hover) / MineFly);
                    rect.anchoredPosition = Arc(from, target, q * q, 0.35f);
                    var size = Mathf.Lerp(60f, 48f, q);
                    // a coin spins, a log tumbles
                    var spin = f.Gold ? Mathf.Abs(Mathf.Cos(q * Mathf.PI * 3f)) : 1f;
                    rect.sizeDelta = new Vector2(size * Mathf.Max(0.2f, spin), size);
                    rect.localRotation = f.Gold ? Quaternion.identity : Quaternion.Euler(0f, 0f, q * 360f);
                    if (q < 1f)
                        return true;

                    Land(f, false);
                    return false;
                }
                case FlightKind.Log:
                {
                    var place = Screen(f.World) + Vector2.right * ((f.Index - 2) * 18f);
                    var from = f.Start + Vector2.down * 22f;
                    if (t < LogDrop)
                    {
                        rect.anchoredPosition = Vector2.Lerp(f.Start, from, t / LogDrop);
                        rect.sizeDelta = Vector2.one * 48f;
                        return true;
                    }

                    var q = Mathf.Clamp01((t - LogDrop) / LogFly);
                    float[] bends = { -1f, 1f, -0.5f, 0.5f, 0f };
                    var to = Inside(place, out var outside);
                    rect.anchoredPosition = Arc(from, to, q * q * (3f - 2f * q), bends[f.Index] * 150f / Mathf.Max(1f, Vector2.Distance(from, to)));
                    var melt = q > 0.85f ? 1f - (q - 0.85f) / 0.15f : 1f;
                    rect.sizeDelta = Vector2.one * Mathf.Lerp(48f, 44f, q) * melt;
                    f.Image.color = new Color(1f, 1f, 1f, outside && q > 0.7f ? 1f - (q - 0.7f) / 0.3f : 1f);
                    if (q < 1f)
                        return true;

                    Land(f, false);
                    return false;
                }
                default:
                {
                    var duration = f.Light ? LightCoinFly : CoinFly;
                    var q = Mathf.Clamp01(t / duration);
                    // it chases the soldier, who already walks to attack
                    if (_soldiers.TryGetPosition(f.Target, out var soldier))
                        f.World = soldier + Vector3.up * 0.6f;
                    var to = Inside(Screen(f.World), out var outside);
                    rect.anchoredPosition = Arc(f.Start, to, q * q * (3f - 2f * q), f.Bend * 60f / Mathf.Max(1f, Vector2.Distance(f.Start, to)));
                    rect.sizeDelta = Vector2.one * 48f * (f.Light ? 0.75f : 1f);
                    f.Image.color = new Color(1f, 1f, 1f, outside && q > 0.7f ? 1f - (q - 0.7f) / 0.3f : 1f);
                    if (q < 1f)
                        return true;

                    Land(f, false);
                    return false;
                }
            }
        }

        /// <summary>A spent unit leaves its counter: −1 there now.</summary>
        private void Leave(Flight f)
        {
            if (f.Left)
                return;

            f.Left = true;
            if (f.Kind == FlightKind.Log)
                hud.Change(1, -1, false);
            else if (f.Kind == FlightKind.Coin)
                hud.Change(0, -1, f.Light);
        }

        /// <summary>A unit lands: a mined one counts, a coin counts its soldier; a ring and sparks unless at once.</summary>
        private void Land(Flight f, bool atOnce)
        {
            f.Landed = true;
            switch (f.Kind)
            {
                case FlightKind.Mine:
                    hud.Change(f.Gold ? 0 : 1, 1, false);
                    if (atOnce)
                        return;

                    var counter = Counter(f.Gold ? 0 : 1);
                    Rings(counter, 26f, 64f, 0.28f);
                    Sparks(counter, f.Gold ? GoldColor : WoodColor, 6, 8f);
                    break;
                case FlightKind.Log:
                    if (!atOnce)
                        Sparks(Screen(f.World), DustColor, 7, 6f);
                    break;
                default:
                    hud.Change(2, 1, true);
                    if (atOnce)
                        return;

                    var at = Screen(f.World);
                    Sparks(at, GoldColor, 8, 7f);
                    if (!f.Light)
                        Rings(at, 16f, 40f, 0.24f);
                    break;
            }
        }

        /// <summary>A window or a new battle: everything in the air counts at once.</summary>
        private void LandAll(bool silent)
        {
            foreach (var f in _flights)
            {
                if (!silent)
                {
                    Leave(f);
                    if (!f.Landed)
                        Land(f, true);
                }

                Release(f.Image);
            }

            _flights.Clear();
            if (silent)
                hud.GoldOffset = hud.WoodOffset = hud.ArmyOffset = 0;
        }

        /// <summary>The «seen» numbers: units still in the air or not yet gone.</summary>
        private void Offsets()
        {
            int gold = 0, wood = 0, army = 0;
            foreach (var f in _flights)
            {
                if (f.Landed)
                    continue;

                if (f.Kind == FlightKind.Mine)
                {
                    if (f.Gold)
                        gold--;
                    else
                        wood--;
                }
                else if (f.Kind == FlightKind.Log && !f.Left)
                {
                    wood++;
                }
                else if (f.Kind == FlightKind.Coin)
                {
                    army--;
                    if (!f.Left)
                        gold++;
                }
            }

            hud.GoldOffset = gold;
            hud.WoodOffset = wood;
            hud.ArmyOffset = army;
        }

        /// <summary>A point between two, along an arc bent away from the middle of the screen by <paramref name="bend"/> of the way.</summary>
        private Vector2 Arc(Vector2 from, Vector2 to, float t, float bend)
        {
            var middle = (from + to) / 2f;
            var normal = new Vector2(-(to - from).y, (to - from).x);
            var centre = _layer.rect.center;
            if (Vector2.Dot(normal, middle - centre) < 0f)
                normal = -normal;
            var control = middle + normal * bend;
            var a = Vector2.Lerp(from, control, t);
            var b = Vector2.Lerp(control, to, t);
            return Vector2.Lerp(a, b, t);
        }

        /// <summary>A point kept inside the safe area with the inset; <paramref name="outside"/> when it had to move.</summary>
        private Vector2 Inside(Vector2 point, out bool outside)
        {
            var r = _layer.rect;
            var kept = new Vector2(Mathf.Clamp(point.x, r.xMin + EdgeInset, r.xMax - EdgeInset), Mathf.Clamp(point.y, r.yMin + EdgeInset, r.yMax - EdgeInset));
            outside = kept != point;
            return kept;
        }

        private Vector3 World(int x, int y, float height) => new Vector3(x * _rules.MetersPerUnit, height, y * _rules.MetersPerUnit);

        /// <summary>A point of the world in the local units of this layer.</summary>
        private Vector2 Screen(Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, _camera.WorldToScreenPoint(world), null, out var local);
            return local;
        }

        /// <summary>The icon of a counter in the local units of this layer (an overlay canvas: its position is in pixels).</summary>
        private Vector2 Counter(int counter)
        {
            var icon = hud.IconOf(counter);
            var centre = icon.TransformPoint(icon.rect.center);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, centre, null, out var local);
            return local;
        }

        private void Sparks(Vector2 at, Color color, int count, float size)
        {
            if (!_view.ResourceFlights)
                return;

            for (var i = 0; i < count; i++)
            {
                var spark = _sparks.Find(s => !s.Image.gameObject.activeSelf);
                if (spark == null)
                {
                    spark = new Spark { Image = Instantiate(sparkTemplate, _layer) };
                    _sparks.Add(spark);
                }

                spark.Image.gameObject.SetActive(true);
                spark.Image.color = color;
                spark.Image.rectTransform.sizeDelta = Vector2.one * size;
                spark.Image.rectTransform.anchoredPosition = at;
                var angle = Random.Range(0f, Mathf.PI * 2f);
                spark.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) + 0.6f) * Random.Range(120f, 220f);
                spark.Age = 0f;
            }
        }

        private void Rings(Vector2 at, float from, float to, float seconds)
        {
            var ring = _rings.Find(r => !r.Image.gameObject.activeSelf);
            if (ring == null)
            {
                ring = new Ring { Image = Instantiate(ringTemplate, _layer) };
                _rings.Add(ring);
            }

            ring.Image.gameObject.SetActive(true);
            ring.Image.rectTransform.anchoredPosition = at;
            ring.From = from;
            ring.To = to;
            ring.Seconds = seconds;
            ring.Age = 0f;
        }

        private void StepSparks(float dt)
        {
            foreach (var s in _sparks)
            {
                if (!s.Image.gameObject.activeSelf)
                    continue;

                s.Age += dt;
                s.Velocity += Vector2.down * 600f * dt;
                var rect = s.Image.rectTransform;
                rect.anchoredPosition += s.Velocity * dt;
                var color = s.Image.color;
                color.a = Mathf.Clamp01(1f - s.Age / 0.45f);
                s.Image.color = color;
                if (s.Age >= 0.45f)
                    s.Image.gameObject.SetActive(false);
            }

            foreach (var r in _rings)
            {
                if (!r.Image.gameObject.activeSelf)
                    continue;

                r.Age += dt;
                var t = Mathf.Clamp01(r.Age / r.Seconds);
                r.Image.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(r.From, r.To, Ease(t));
                // a soft cream puff growing and fading: no outline sprite yet, a filled disc kept light
                r.Image.color = new Color(Cream.r, Cream.g, Cream.b, 0.35f * (1f - t));
                if (t >= 1f)
                    r.Image.gameObject.SetActive(false);
            }
        }

        private Image Take(Sprite sprite)
        {
            var image = _freeIcons.Count > 0 ? _freeIcons.Pop() : Instantiate(template, _layer);
            image.gameObject.SetActive(true);
            image.enabled = false;
            image.sprite = sprite;
            image.color = Color.white;
            image.rectTransform.localRotation = Quaternion.identity;
            image.rectTransform.SetAsLastSibling();
            return image;
        }

        private void Release(Image image)
        {
            if (image == null)
                return;

            image.gameObject.SetActive(false);
            _freeIcons.Push(image);
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t);

        private enum FlightKind
        {
            Mine,
            Log,
            Coin,
        }

        private sealed class Flight
        {
            public FlightKind Kind;
            public bool Gold;
            public Image Image;
            public Vector3 World;
            public Vector2 Start;
            public Vector2 Jitter;
            public float Age;
            public float Delay;
            public int Index;
            public int Target;
            public bool Light;
            public float Bend;
            public bool Left;
            public bool Landed;
        }

        private sealed class Spark
        {
            public Image Image;
            public Vector2 Velocity;
            public float Age;
        }

        private sealed class Ring
        {
            public Image Image;
            public float From;
            public float To;
            public float Seconds;
            public float Age;
        }
    }
}
