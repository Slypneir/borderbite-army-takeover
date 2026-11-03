using System.Collections.Generic;
using Core.InputManagement.Configuration;
using Core.InputManagement.Interfaces;
using Core.PlatformSystem.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VContainer.Unity;

namespace Core.InputManagement.Services
{
    /// <summary>
    /// The floating stick: the first finger (or the held left button) that touches the screen away from the edges and the
    /// interface puts the base under itself; the knob follows it up to the travel, further the base follows. A second finger is
    /// ignored. The stick gives only a direction; WASD and the arrows give one without a stick. Released, paused, turned or
    /// unfocused — the hero stands. Positions are units of the 720 × 720 Expand canvas.
    /// </summary>
    public sealed class MoveInput : IMoveInput, ITickable
    {
        private const float CanvasReference = 720f;
        private const int MousePointer = -1;
        private const int NoPointer = -2;

        private readonly MoveStickConfig _config;
        private readonly IPlaybackGate _gate;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private int _pointer = NoPointer;
        private bool _enabled = true;
        private Vector2Int _screen;

        public MoveInput(MoveStickConfig config, IPlaybackGate gate)
        {
            _config = config;
            _gate = gate;
        }

        public Vector2 Direction { get; private set; }

        public bool StickShown => _pointer != NoPointer;

        public Vector2 StickCenter { get; private set; }

        public Vector2 KnobOffset { get; private set; }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value)
                    Release();
            }
        }

        public void Tick()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!_enabled || _gate.Frozen || screen != _screen)
            {
                _screen = screen;
                Release();
                Direction = Vector2.zero;
                return;
            }

            var touching = ReadTouches();
            if (!touching)
                ReadMouse();

            Direction = StickShown ? StickDirection() : KeysDirection();
        }

        /// <summary>True when the touch screen is in use this frame: the mouse that browsers make of a finger is then ignored.</summary>
        private bool ReadTouches()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return false;

            var any = false;
            foreach (var touch in touchscreen.touches)
            {
                var phase = touch.phase.ReadValue();
                // an ended touch keeps its phase until the next one: only a touch on the glass or just lifted counts
                if (!touch.isInProgress && !touch.press.wasReleasedThisFrame)
                    continue;

                any = true;
                var id = touch.touchId.ReadValue();
                if (_pointer == id)
                {
                    if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                        Release();
                    else
                        Follow(touch.position.ReadValue());
                }
                else if (_pointer == NoPointer && touch.press.wasPressedThisFrame)
                {
                    TryStart(id, touch.position.ReadValue());
                }
            }

            if (_pointer >= 0 && !Tracked(touchscreen))
                Release();

            return any || _pointer >= 0;
        }

        private bool Tracked(Touchscreen touchscreen)
        {
            foreach (var touch in touchscreen.touches)
            {
                if (touch.touchId.ReadValue() == _pointer && touch.isInProgress)
                    return true;
            }

            return false;
        }

        private void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (_pointer == MousePointer)
            {
                if (mouse.leftButton.isPressed)
                    Follow(mouse.position.ReadValue());
                else
                    Release();
            }
            else if (_pointer == NoPointer && mouse.leftButton.wasPressedThisFrame)
            {
                TryStart(MousePointer, mouse.position.ReadValue());
            }
        }

        private void TryStart(int pointer, Vector2 pixels)
        {
            if (OverInterface(pixels))
                return;

            var scale = Scale();
            var point = pixels / scale;
            var safe = Screen.safeArea;
            var min = safe.min / scale;
            var max = safe.max / scale;
            var guard = _config.EdgeGuard;
            if (point.x < min.x + guard || point.y < min.y + guard || point.x > max.x - guard || point.y > max.y - guard)
                return;

            // the base that does not fit at the edge moves inside
            var radius = _config.BaseDiameter / 2f;
            StickCenter = new Vector2(
                Mathf.Clamp(point.x, min.x + radius, Mathf.Max(min.x + radius, max.x - radius)),
                Mathf.Clamp(point.y, min.y + radius, Mathf.Max(min.y + radius, max.y - radius)));
            _pointer = pointer;
            Follow(pixels);
        }

        private void Follow(Vector2 pixels)
        {
            var offset = pixels / Scale() - StickCenter;
            var travel = _config.Travel;
            if (offset.magnitude > travel)
            {
                // the finger went past the travel: the base follows it
                StickCenter += offset.normalized * (offset.magnitude - travel);
                offset = offset.normalized * travel;
            }

            KnobOffset = offset;
        }

        private void Release()
        {
            _pointer = NoPointer;
            KnobOffset = Vector2.zero;
        }

        private Vector2 StickDirection()
        {
            var deadZone = _config.DeadZone * _config.BaseDiameter / 2f;
            return KnobOffset.magnitude > deadZone ? KnobOffset.normalized : Vector2.zero;
        }

        /// <summary>WASD and the arrows by their physical place, whatever the layout.</summary>
        private static Vector2 KeysDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            var x = Axis(keyboard.dKey, keyboard.rightArrowKey) - Axis(keyboard.aKey, keyboard.leftArrowKey);
            var y = Axis(keyboard.wKey, keyboard.upArrowKey) - Axis(keyboard.sKey, keyboard.downArrowKey);
            return new Vector2(x, y).normalized;
        }

        private static float Axis(KeyControl key, KeyControl arrow) => key.isPressed || arrow.isPressed ? 1f : 0f;

        private bool OverInterface(Vector2 pixels)
        {
            var events = EventSystem.current;
            if (events == null)
                return false;

            _hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = pixels }, _hits);
            return _hits.Count > 0;
        }

        private static float Scale() => Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / CanvasReference;
    }
}
