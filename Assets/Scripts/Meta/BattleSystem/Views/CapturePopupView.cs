using System.Collections.Generic;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using TMPro;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// «+N%» over an enclosed area, rimmed in the colour of the side that took it: rises and fades, kept on the screen below the
    /// HUD. A capture of the same side closer than 120 units on the screen to a label born or grown less than 0.6 s ago joins it:
    /// one label with the sum («+2%», «+3%») starting again, not a stack (UI review of 4.33, B030). Labels are copies of
    /// <see cref="template"/>, kept for reuse. Lives right under the canvas, anchored to its lower left corner.
    /// </summary>
    public sealed class CapturePopupView : MonoBehaviour
    {
        private const float EdgeMargin = 120f;
        private const float TopMargin = 220f;
        private const float MergeDistance = 120f;
        private const float MergeSeconds = 0.6f;

        [SerializeField] private TMP_Text template;

        private readonly List<Popup> _popups = new List<Popup>();
        private IBattleDriver _battle;
        private BattleConfig _rules;
        private BattleViewConfig _view;
        private Camera _camera;
        private BattleSimulation _simulation;
        private int _seen;

        [Inject]
        public void Construct(IBattleDriver battle, BattleConfig rules, BattleViewConfig view, Camera mainCamera)
        {
            _battle = battle;
            _rules = rules;
            _view = view;
            _camera = mainCamera;
        }

        private void Awake() => template.gameObject.SetActive(false);

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation != _simulation)
            {
                _simulation = simulation;
                _seen = simulation?.Captures.Count ?? 0;
                foreach (var popup in _popups)
                    popup.Label.gameObject.SetActive(false);
            }

            if (simulation == null)
                return;

            for (; _seen < simulation.Captures.Count; _seen++)
            {
                // cut off land going back to grass adds nothing to anyone: no «+N%»
                if (simulation.Captures[_seen].Side != Side.Neutral)
                    Show(simulation.Captures[_seen]);
            }

            Animate();
        }

        private void Show(Core.BattleSystem.Data.Capture capture)
        {
            var world = new Vector3(capture.CenterX * _rules.MetersPerUnit, 0f, capture.CenterY * _rules.MetersPerUnit);
            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            var place = Place(world, scale);
            foreach (var near in _popups)
            {
                if (!near.Label.gameObject.activeSelf || near.Side != capture.Side || near.Age >= MergeSeconds ||
                    (Place(near.World, scale) - place).sqrMagnitude > MergeDistance * MergeDistance)
                    continue;

                near.Percent += capture.Percent;
                near.Label.text = "+" + near.Percent + "%";
                near.Age = 0f;
                return;
            }

            var popup = Free();
            popup.Side = capture.Side;
            popup.Percent = capture.Percent;
            popup.Label.text = "+" + capture.Percent + "%";
            // white on the colour of the side would vanish on its own area: white with a dark rim of the side
            var side = capture.Side == Side.Player ? _view.PlayerColor : _view.EnemyColor;
            popup.Label.color = Color.white;
            popup.Label.outlineColor = Color.Lerp(side, Color.black, 0.55f);
            popup.World = world;
            popup.Age = 0f;
            popup.Label.gameObject.SetActive(true);
        }

        private void Animate()
        {
            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            foreach (var popup in _popups)
            {
                if (!popup.Label.gameObject.activeSelf)
                    continue;

                popup.Age += Time.deltaTime;
                var t = popup.Age / _view.PopupSeconds;
                if (t >= 1f)
                {
                    popup.Label.gameObject.SetActive(false);
                    continue;
                }

                ((RectTransform)popup.Label.transform).anchoredPosition = Place(popup.World, scale) + Vector2.up * (_view.PopupRise * t);
                popup.Label.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            }
        }

        /// <summary>A large area may have its middle off the screen or under the HUD: the label stays where it is seen.</summary>
        private Vector2 Place(Vector3 world, float scale)
        {
            var canvas = new Vector2(Screen.width, Screen.height) / scale;
            var screen = (Vector2)_camera.WorldToScreenPoint(world) / scale;
            screen.x = Mathf.Clamp(screen.x, EdgeMargin, Mathf.Max(EdgeMargin, canvas.x - EdgeMargin));
            screen.y = Mathf.Clamp(screen.y, EdgeMargin, Mathf.Max(EdgeMargin, canvas.y - TopMargin - _view.PopupRise));
            return screen;
        }

        private Popup Free()
        {
            foreach (var popup in _popups)
            {
                if (!popup.Label.gameObject.activeSelf)
                    return popup;
            }

            var label = Instantiate(template, transform);
            var created = new Popup { Label = label };
            _popups.Add(created);
            return created;
        }

        private sealed class Popup
        {
            public TMP_Text Label;
            public Vector3 World;
            public float Age;
            public Side Side;
            public int Percent;
        }
    }
}
