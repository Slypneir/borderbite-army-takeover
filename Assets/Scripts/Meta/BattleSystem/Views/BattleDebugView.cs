using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using Meta.DeveloperTools.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The overlay of the developer (stage 1 spec 2.7): F1 or DevBridge.ShowDebug toggles the grid of cells and this line of the
    /// tick, the frames per second, the shares and the enemy; while it is on, keys 1–9 give the enemy the strength of that level
    /// (GDD 5.9) at once. Off for good in a build for players.
    /// </summary>
    public sealed class BattleDebugView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private IBattleDriver _battle;
        private BattleDebug _debug;
        private float _fps;

        [Inject]
        public void Construct(IBattleDriver battle, BattleDebug debug)
        {
            _battle = battle;
            _debug = debug;
        }

        private void Update()
        {
            if (!DevFlags.Enabled || _debug == null)
            {
                label.enabled = false;
                return;
            }

            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                _debug.ShowDebug = !_debug.ShowDebug;

            label.enabled = _debug.ShowDebug;
            if (!_debug.ShowDebug)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (var level = 1; level <= 9; level++)
                {
                    if (keyboard[Key.Digit1 + level - 1].wasPressedThisFrame || keyboard[Key.Numpad1 + level - 1].wasPressedThisFrame)
                        _debug.SetEnemyLevel(level);
                }
            }

            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.1f);
            var simulation = _battle.Current?.Simulation;
            label.text = simulation == null
                ? $"fps {_fps:0}"
                : $"tick {simulation.Tick}  fps {_fps:0}  blue {simulation.Grid.Permille(Core.BattleSystem.Enums.Side.Player) / 10f:0.0}%  " +
                  $"red {simulation.Grid.Permille(Core.BattleSystem.Enums.Side.Enemy) / 10f:0.0}%  enemy L{_debug.EnemyLevel} p{simulation.EnemyPriority} (1–9)";
        }
    }
}
