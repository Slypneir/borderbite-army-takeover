using Core.BattleSystem.Enums;
using Core.ScreenSystem.Configuration;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// The camera looks down at the hero from the tilt of the screen config and follows it softly. Past the edge of the island
    /// is water to the horizon, so the camera is never held back at the edge (stage 1 spec 2.3). A hero gone for good — fallen
    /// without a base (spec 2.6a) — leaves the camera to the middle of the player's army.
    /// </summary>
    public sealed class CameraRig : ILateTickable
    {
        private const float Distance = 60f;
        // about 0.8 s to the army of a hero gone for good (SmoothDamp reaches the target in some 3 times its smoothing)
        private const float ArmySmoothing = 0.27f;

        private readonly Camera _camera;
        private readonly ScreenLayoutConfig _screen;
        private readonly BattleViewConfig _view;
        private readonly IBattleDriver _battle;
        private readonly HeroView _hero;
        private readonly BattleConfig _rules;
        private readonly BattleDebug _debug;
        private float _normalSize = -1f;
        private Vector3 _focus;
        private Vector3 _velocity;

        public CameraRig(Camera camera, ScreenLayoutConfig screen, BattleViewConfig view, IBattleDriver battle, HeroView hero,
            BattleConfig rules, BattleDebug debug)
        {
            _camera = camera;
            _screen = screen;
            _view = view;
            _battle = battle;
            _hero = hero;
            _rules = rules;
            _debug = debug;
            _battle.OnStarted += _ =>
            {
                _focus = _hero.Position;
                _velocity = Vector3.zero;
            };
        }

        public void LateTick()
        {
            if (_battle.Current == null)
                return;

            if (_debug.Overview)
            {
                ShowWholeMap();
                return;
            }

            if (_normalSize > 0f)
            {
                // back from the overview: the screen layout owns the size
                _camera.orthographicSize = _normalSize;
                _normalSize = -1f;
            }

            var target = _hero.Position;
            var smoothing = _view.CameraSmoothing;
            if (ArmyInstead(out var army))
            {
                // the hero is gone for good (task 4.36, spec 2.6a): the camera goes softly, in about 0.8 s, to the middle of the army
                target = army;
                smoothing = Mathf.Max(smoothing, ArmySmoothing);
            }

            _focus = Vector3.SmoothDamp(_focus, target, ref _velocity, smoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            var rotation = Quaternion.Euler(_screen.Tilt, 0f, 0f);
            var transform = _camera.transform;
            transform.rotation = rotation;
            transform.position = _focus - rotation * Vector3.forward * Distance;
        }

        /// <summary>
        /// The middle of the player's army when its hero has fallen without a base to come back to; false while the hero lives,
        /// can still come back, or no soldier is left.
        /// </summary>
        private bool ArmyInstead(out Vector3 middle)
        {
            middle = default;
            var simulation = _battle.Current.Simulation;
            if (simulation.Bases[(int)Side.Player].Standing)
                return false;

            foreach (var hero in simulation.Heroes)
            {
                if (hero.Side == Side.Player && hero.Alive)
                    return false;
            }

            long x = 0;
            long y = 0;
            var count = 0;
            foreach (var soldier in simulation.Soldiers)
            {
                if (soldier.Side != Side.Player)
                    continue;

                x += soldier.X;
                y += soldier.Y;
                count++;
            }

            if (count == 0)
                return false;

            middle = new Vector3(x / (float)count * _rules.MetersPerUnit, 0f, y / (float)count * _rules.MetersPerUnit);
            return true;
        }

        private void ShowWholeMap()
        {
            if (_normalSize < 0f)
                _normalSize = _camera.orthographicSize;

            var grid = _battle.Current.Simulation.Grid;
            var width = grid.Width * _rules.CellSize;
            var height = grid.Height * _rules.CellSize;
            var center = new Vector3(width / 2f, 0f, height / 2f);
            var rotation = Quaternion.Euler(90f, 0f, 0f);
            _camera.transform.rotation = rotation;
            _camera.transform.position = center - rotation * Vector3.forward * Distance;
            // both sides in the frame: the height, and the width at the aspect of the screen
            _camera.orthographicSize = 0.6f * Mathf.Max(height, width * Screen.height / Mathf.Max(1f, Screen.width));
        }
    }
}
