using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.BattleSystem.Utils;
using Meta.LocalizationSystem.Services;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The orders of the army on the screen of the battle (task 4.18, army-orders card of the UI, spec 3.8): one row of five
    /// buttons at the bottom of the safe area, inset 24 — in the middle on a tall screen, in the lower right corner on a wide or
    /// square one. Rally · Capture · Units · Barracks · Base, also by the keys Z X C V B (physical places); the letters show in
    /// the corners after the first key. A touch gives the order at once; the active order again frees the army (T17). The active
    /// button: its active frame, a strip under it, ×1.08; an order without targets is grey with a dull icon, a touch shakes it
    /// and says «Нет целей». When the order turns to «Ко мне» by itself (T18) the light moves over in 0.3 s and «Ко мне» pulses
    /// once. The name of a chosen order shows over the row for 1.2 s. The stick does not start on the row and 16 around it (a
    /// transparent catcher). Under a window the row hides with the marks (<see cref="BattleLayersView"/>).
    /// </summary>
    public sealed class OrdersView : MonoBehaviour
    {
        private const float EdgeInset = 24f;
        private const float ActiveScale = 1.08f;
        private const float PressScale = 0.94f;
        private const float PressSeconds = 0.12f;
        private const float FadeSeconds = 0.15f;
        private const float MoveSeconds = 0.3f;
        private const float PulseSeconds = 0.4f;
        private const float ShakeSeconds = 0.2f;
        private const float NameSeconds = 1.2f;
        private const float AutoNameSeconds = 1.5f;
        private const float DullIcon = 0.4f;
        private static readonly Color Grey = new Color(0.5f, 0.5f, 0.5f, 1f);

        private static readonly ArmyOrder[] Orders = { ArmyOrder.Rally, ArmyOrder.Capture, ArmyOrder.Units, ArmyOrder.Barracks, ArmyOrder.Base };
        private static readonly string[] Names = { "orders.rally", "orders.capture", "orders.units", "orders.barracks", "orders.base" };
        private static readonly string[] Letters = { "Z", "X", "C", "V", "B" };

        [SerializeField] private RectTransform row;
        [SerializeField] private OrderButtonView[] buttons;
        [SerializeField] private TMP_Text toast;

        private readonly float[] _light = new float[5];
        private readonly float[] _pressed = new float[5];
        private readonly float[] _pulse = new float[5];
        private readonly float[] _shake = new float[5];
        private IBattleDriver _battle;
        private BattleScreen _screen;
        private EventCursor _events;
        private BattleSimulation _simulation;
        private float _lightSeconds = FadeSeconds;
        private float _toastUntil;
        private bool _keysShown;

        /// <summary>The row of the buttons: the marks over the world stay over it (+ 8).</summary>
        public RectTransform Row => row;

        [Inject]
        public void Construct(IBattleDriver battle, BattleScreen screen)
        {
            _battle = battle;
            _screen = screen;
        }

        private void Awake()
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                var index = i;
                buttons[i].Pressed += () => Press(index);
                buttons[i].Key.text = Letters[i];
            }
        }

        private void Update()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation != _simulation)
            {
                _simulation = simulation;
                _events = default;
                System.Array.Clear(_light, 0, _light.Length);
            }

            Place();
            if (simulation == null)
                return;

            ReadKeys();
            _events.Read(simulation, e =>
            {
                // the order turned to «Ко мне» by itself: the light moves over, «Ко мне» pulses once and names itself
                if (e.Kind == EventKind.OrderChanged && e.Side == Side.Player && e.Id == 1)
                {
                    _lightSeconds = MoveSeconds;
                    var index = System.Array.IndexOf(Orders, (ArmyOrder)e.Value);
                    if (index >= 0)
                    {
                        _pulse[index] = PulseSeconds;
                        Say(Loc.Get(Names[index]), AutoNameSeconds);
                    }
                }
            });

            Draw(simulation);
        }

        /// <summary>The middle of the bottom on a tall screen; the lower right corner on a wide or square one.</summary>
        private void Place()
        {
            var wide = Screen.width >= Screen.height;
            var anchor = wide ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f);
            row.anchorMin = anchor;
            row.anchorMax = anchor;
            row.pivot = anchor;
            row.anchoredPosition = new Vector2(wide ? -EdgeInset : 0f, EdgeInset);
        }

        private void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.anyKey.wasPressedThisFrame)
                _keysShown = true;

            var keys = new[] { keyboard.zKey, keyboard.xKey, keyboard.cKey, keyboard.vKey, keyboard.bKey };
            for (var i = 0; i < keys.Length; i++)
            {
                if (keys[i].wasPressedThisFrame)
                    Press(i);
            }
        }

        private void Press(int index)
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null || simulation.Result != BattleResult.None || (_screen != null && _screen.Covered))
                return;

            if (!HasTargets(simulation, Orders[index]))
            {
                _shake[index] = ShakeSeconds;
                Say(Loc.Get("orders.no_targets"), NameSeconds);
                return;
            }

            _pressed[index] = PressSeconds;
            _lightSeconds = FadeSeconds;
            var choosing = simulation.OrderOf(Side.Player) != Orders[index];
            _battle.Order(Orders[index]);
            if (choosing)
                Say(Loc.Get(Names[index]), NameSeconds);
        }

        /// <summary>Has an order anything to do now: «Казармы» an enemy barracks, «Бойцы» an enemy soldier, «Штаб» a standing base.</summary>
        private static bool HasTargets(BattleSimulation simulation, ArmyOrder order)
        {
            switch (order)
            {
                case ArmyOrder.Barracks:
                    foreach (var site in simulation.Sites)
                    {
                        if (site.Barracks == Side.Enemy)
                            return true;
                    }

                    return false;
                case ArmyOrder.Units:
                    return simulation.ArmyOf(Side.Enemy) > 0;
                case ArmyOrder.Base:
                    return simulation.Bases[(int)Side.Enemy].Standing;
                default:
                    return true;
            }
        }

        private void Draw(BattleSimulation simulation)
        {
            var dt = Time.unscaledDeltaTime;
            var small = Mathf.Min(Screen.width, Screen.height) / 720f < 1f;
            var current = simulation.OrderOf(Side.Player);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                button.Fit(small);
                var targets = HasTargets(simulation, Orders[i]);
                var lit = current == Orders[i] ? 1f : 0f;
                _light[i] = Mathf.MoveTowards(_light[i], lit, dt / _lightSeconds);
                _pressed[i] = Mathf.Max(0f, _pressed[i] - dt);
                _pulse[i] = Mathf.Max(0f, _pulse[i] - dt);
                _shake[i] = Mathf.Max(0f, _shake[i] - dt);

                var light = _light[i];
                var press = _pressed[i] > 0f ? Mathf.Lerp(1f, PressScale, _pressed[i] / PressSeconds) : 1f;
                var pulse = _pulse[i] > 0f ? 1f + 0.1f * Mathf.Sin((1f - _pulse[i] / PulseSeconds) * Mathf.PI) : 1f;
                button.Body.localScale = Vector3.one * (Mathf.Lerp(1f, ActiveScale, light) * press * pulse);
                button.Body.anchoredPosition = new Vector2(_shake[i] > 0f ? Mathf.Sin(_shake[i] * 80f) * 6f * (_shake[i] / ShakeSeconds) : 0f, 0f);

                button.Frame.color = targets ? Color.white : Grey;
                button.ActiveFrame.color = new Color(1f, 1f, 1f, light);
                button.Icon.color = new Color(1f, 1f, 1f, targets ? 1f : DullIcon);
                button.Strip.color = new Color(button.Strip.color.r, button.Strip.color.g, button.Strip.color.b, light);
                button.Key.gameObject.SetActive(_keysShown);
            }

            // the name fades out over its last 0.2 s
            toast.alpha = Mathf.Clamp01((_toastUntil - Time.unscaledTime) / 0.2f);
        }

        private void Say(string text, float seconds)
        {
            toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
        }
    }
}
