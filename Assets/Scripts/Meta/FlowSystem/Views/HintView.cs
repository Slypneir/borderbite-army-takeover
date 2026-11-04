using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.BattleSystem.Utils;
using Meta.LocalizationSystem.Services;
using Meta.ProgressSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Meta.FlowSystem.Views
{
    /// <summary>
    /// The hint of level 1 (stage 1 spec 5.5; screens/first-hint.md, «for layer 4»), once per player (spec T11; the step is
    /// saved with the progress): 0 — a hand «drag» low in the middle (WASD keys without a word on a computer) until the hero has
    /// walked a few meters; 1 — an arrow to the nearest gold until gold is picked up; 2 — to the player's own empty place (to
    /// wood first while it is short) until a barracks stands; 3 — to the player's barracks until a soldier is hired. The arrow
    /// stands by the hero towards the goal, or at the edge of the frame when the goal is out of it; it pulses softly, and once,
    /// after 5 s without moving, grows by 20%. Light with a dark rim: neither a side nor a threat.
    /// </summary>
    public sealed class HintView : MonoBehaviour
    {
        private const int Done = 4;
        private const float WalkMeters = 3f;
        private const float ArrowDistance = 150f;
        // clear of the hero and its health bar even when the goal is close
        private const float ArrowNear = 90f;
        private const float EdgeInset = 24f;
        private const float HudGap = 8f;
        private const float IdleSeconds = 5f;

        [SerializeField] private RectTransform hand;
        [SerializeField] private RectTransform finger;
        [SerializeField] private TMP_Text handWord;
        [SerializeField] private RectTransform keys;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private RectTransform hudBottom;

        private readonly Vector3[] _corners = new Vector3[4];
        private IBattleDriver _battle;
        private ProgressService _progress;
        private HeroView _hero;
        private BattleConfig _rules;
        private Camera _camera;
        private EventCursor _events;
        private object _simulation;
        private Vector3 _start;
        private Vector3 _lastHero;
        private float _stillSince;
        private bool _grown;
        private int _shownStep = -1;

        [Inject]
        public void Construct(IBattleDriver battle, ProgressService progress, HeroView hero, BattleConfig rules, Camera mainCamera)
        {
            _battle = battle;
            _progress = progress;
            _hero = hero;
            _rules = rules;
            _camera = mainCamera;
        }

        private void Awake() => Show(-1);

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            var step = _progress == null ? Done : _progress.Data.HintStep;
            if (simulation == null || _progress.Data.Level != 1 || step >= Done || simulation.Result != BattleResult.None)
            {
                Show(-1);
                return;
            }

            if (_simulation != simulation)
            {
                _simulation = simulation;
                _start = _hero.Position;
                _lastHero = _start;
                _stillSince = Time.time;
                _grown = false;
            }

            _events.Read(simulation, e => Advance(e, step));
            step = _progress.Data.HintStep;
            if (step == 0 && Vector3.Distance(_hero.Position, _start) >= WalkMeters)
                _progress.SetHintStep(1);

            step = _progress.Data.HintStep;
            if (step >= Done)
            {
                Show(-1);
                return;
            }

            if (Vector3.Distance(_hero.Position, _lastHero) > 0.05f)
            {
                _lastHero = _hero.Position;
                _stillSince = Time.time;
            }
            else if (!_grown && Time.time - _stillSince > IdleSeconds)
            {
                // once more visible, never a lasting blink
                _grown = true;
            }

            Show(step);
            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            var pulse = (1f + 0.08f * Mathf.Sin(Time.time * 4f)) * (_grown ? 1.2f : 1f);
            if (step == 0)
            {
                DrawHand(pulse);
                return;
            }

            if (!Goal(simulation, step, out var goal))
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            DrawArrow(goal, scale, pulse);
        }

        private void Advance(BattleEvent battleEvent, int step)
        {
            if (battleEvent.Side != Side.Player)
                return;

            if (step <= 1 && battleEvent.Kind == EventKind.Pickup && battleEvent.Detail == (int)ResourceKind.Gold)
                _progress.SetHintStep(2);
            else if (step <= 2 && battleEvent.Kind == EventKind.Built)
                _progress.SetHintStep(3);
            else if (step <= 3 && battleEvent.Kind == EventKind.Hired)
                _progress.SetHintStep(Done);
        }

        private void Show(int step)
        {
            if (step == _shownStep)
                return;

            _shownStep = step;
            var touch = Touchscreen.current != null;
            hand.gameObject.SetActive(step == 0 && touch);
            keys.gameObject.SetActive(step == 0 && !touch);
            arrow.gameObject.SetActive(step > 0);
            handWord.text = Loc.Get("hint.move");
        }

        private void DrawHand(float pulse)
        {
            hand.localScale = Vector3.one * pulse;
            keys.localScale = Vector3.one * pulse;
            // the finger slides right and back: «hold and drag»
            finger.anchoredPosition = new Vector2(Mathf.PingPong(Time.time * 120f, 160f) - 80f, 0f);
        }

        /// <summary>The goal of a step in the world: gold, then the own place (wood first while short), then the own barracks.</summary>
        private bool Goal(Core.BattleSystem.Services.BattleSimulation simulation, int step, out Vector3 goal)
        {
            goal = default;
            var hero = _hero.Position;
            var best = float.MaxValue;
            var size = simulation.Grid.Size;
            var purse = simulation.SideOf(Side.Player);
            var wantWood = step == 2 && purse.Wood < simulation.Settings.BarracksWood;
            if (step == 1 || wantWood)
            {
                var kind = step == 1 ? ResourceKind.Gold : ResourceKind.Wood;
                foreach (var pile in simulation.Resources)
                {
                    if (pile.Kind != kind)
                        continue;

                    var at = Meters(pile.X, pile.Y);
                    var distance = Vector3.Distance(hero, at);
                    if (distance < best)
                    {
                        best = distance;
                        goal = at;
                    }
                }

                return best < float.MaxValue;
            }

            foreach (var site in simulation.Sites)
            {
                var own = simulation.Grid.At(site.CenterX, site.CenterY) == Side.Player;
                var wanted = step == 2 ? !site.Built && simulation.Grid.At(site.CenterX, site.CenterY) != Side.Enemy : site.Barracks == Side.Player;
                if (!wanted)
                    continue;

                var at = Meters(BattleStateMiddle(site.CenterX), BattleStateMiddle(site.CenterY));
                // the player's own places come first
                var distance = Vector3.Distance(hero, at) - (own ? 1000f : 0f);
                if (distance < best)
                {
                    best = distance;
                    goal = at;
                }
            }

            return best < float.MaxValue && size > 0;
        }

        private void DrawArrow(Vector3 goal, float scale, float pulse)
        {
            var safe = Screen.safeArea;
            var bounds = new Rect(safe.position / scale, safe.size / scale);
            var top = bounds.yMax - EdgeInset;
            if (hudBottom != null)
            {
                hudBottom.GetWorldCorners(_corners);
                top = Mathf.Min(top, _corners[0].y / scale - HudGap);
            }

            var area = new Rect(bounds.xMin + EdgeInset, bounds.yMin + EdgeInset, bounds.width - 2f * EdgeInset, top - bounds.yMin - EdgeInset);
            var heroPoint = (Vector2)_camera.WorldToScreenPoint(_hero.Position + Vector3.up * 0.2f) / scale;
            var goalScreen = _camera.WorldToScreenPoint(goal);
            var goalPoint = (Vector2)goalScreen / scale;
            var direction = goalPoint - heroPoint;
            if (direction.sqrMagnitude < 1f)
                direction = Vector2.up;
            var inFrame = goalScreen.z > 0f && area.Contains(goalPoint);
            Vector2 at;
            if (inFrame)
            {
                // by the hero towards the goal, never past it
                at = heroPoint + direction.normalized * Mathf.Clamp(direction.magnitude * 0.5f, ArrowNear, ArrowDistance);
            }
            else
            {
                var half = 48f;
                var inner = new Rect(area.xMin + half, area.yMin + half, area.width - 2f * half, area.height - 2f * half);
                var centre = inner.center;
                var ray = goalPoint - centre;
                var tx = ray.x > 0f ? (inner.xMax - centre.x) / ray.x : ray.x < 0f ? (inner.xMin - centre.x) / ray.x : float.MaxValue;
                var ty = ray.y > 0f ? (inner.yMax - centre.y) / ray.y : ray.y < 0f ? (inner.yMin - centre.y) / ray.y : float.MaxValue;
                at = centre + ray * Mathf.Clamp01(Mathf.Min(tx, ty));
                direction = ray;
            }

            arrow.gameObject.SetActive(true);
            arrow.anchoredPosition = at;
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            arrow.localScale = Vector3.one * pulse;
        }

        private static int BattleStateMiddle(int cell) => cell * BattleSettings.UnitsPerCell + BattleSettings.UnitsPerCell / 2;

        private Vector3 Meters(int x, int y) => new Vector3(x * _rules.MetersPerUnit, 0f, y * _rules.MetersPerUnit);
    }
}
