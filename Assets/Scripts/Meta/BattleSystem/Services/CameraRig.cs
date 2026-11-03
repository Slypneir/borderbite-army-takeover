using Core.ScreenSystem.Configuration;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// The camera looks down at the hero from the tilt of the screen config and follows it softly. Past the edge of the island
    /// is water to the horizon, so the camera is never held back at the edge (stage 1 spec 2.3).
    /// </summary>
    public sealed class CameraRig : ILateTickable
    {
        private const float Distance = 60f;

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

            _focus = Vector3.SmoothDamp(_focus, _hero.Position, ref _velocity, _view.CameraSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            var rotation = Quaternion.Euler(_screen.Tilt, 0f, 0f);
            var transform = _camera.transform;
            transform.rotation = rotation;
            transform.position = _focus - rotation * Vector3.forward * Distance;
        }

        private void ShowWholeMap()
        {
            if (_normalSize < 0f)
                _normalSize = _camera.orthographicSize;

            var size = _rules.MapSize;
            var center = new Vector3(size / 2f, 0f, size / 2f);
            var rotation = Quaternion.Euler(90f, 0f, 0f);
            _camera.transform.rotation = rotation;
            _camera.transform.position = center - rotation * Vector3.forward * Distance;
            _camera.orthographicSize = size * 0.6f * Mathf.Max(1f, (float)Screen.height / Mathf.Max(1, Screen.width));
        }
    }
}
