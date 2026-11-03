using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Core.EditorTools
{
    /// <summary>
    /// Input for self-checks through the editor bridge in Play Mode: a finger on a virtual touch screen, the mouse and keys go
    /// to the Input System as events, as a real device would send them. Positions are pixels of the Game view.
    /// </summary>
    public static class InputScript
    {
        private const int Finger = 1;

        private static Touchscreen _touchscreen;

        [MenuItem("Tools/Dev/Game View 1080x1920")]
        public static void Portrait() => SetGameView(1080, 1920);

        [MenuItem("Tools/Dev/Game View 1920x1080")]
        public static void Landscape() => SetGameView(1920, 1080);

        /// <summary>The resolution of the Game view, as a phone or a monitor of that size would give.</summary>
        public static string SetGameView(int width, int height)
        {
            PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, $"{width}x{height}");
            return $"game view {width}x{height}";
        }

        public static string TouchDown(float x, float y) => Touch(UnityEngine.InputSystem.TouchPhase.Began, x, y);

        public static string TouchMove(float x, float y) => Touch(UnityEngine.InputSystem.TouchPhase.Moved, x, y);

        public static string TouchUp(float x, float y) => Touch(UnityEngine.InputSystem.TouchPhase.Ended, x, y);

        public static string Mouse(float x, float y, bool pressed)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(x, y), buttons = (ushort)(pressed ? 1 : 0) });
            return $"mouse {x},{y} {(pressed ? "down" : "up")}";
        }

        /// <summary>Holds the named keys (W, A, UpArrow…) and releases all others; no names releases everything.</summary>
        public static string Keys(string names)
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            var state = new KeyboardState();
            foreach (var name in (names ?? "").Split(','))
            {
                if (!string.IsNullOrWhiteSpace(name))
                    state.Set((Key)System.Enum.Parse(typeof(Key), name.Trim()), true);
            }

            InputSystem.QueueStateEvent(keyboard, state);
            return "keys " + names;
        }

        private static string Touch(UnityEngine.InputSystem.TouchPhase phase, float x, float y)
        {
            if (_touchscreen == null || !_touchscreen.added)
                _touchscreen = InputSystem.AddDevice<Touchscreen>();

            InputSystem.QueueStateEvent(_touchscreen, new TouchState { touchId = Finger, phase = phase, position = new Vector2(x, y) });
            return $"touch {phase} {x},{y}";
        }
    }
}
