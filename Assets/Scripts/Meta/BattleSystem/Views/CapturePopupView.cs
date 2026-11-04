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
    /// HUD. Labels are copies of <see cref="template"/>, kept for reuse. Lives right under the canvas, anchored to its lower left
    /// corner.
    /// </summary>
    public sealed class CapturePopupView : MonoBehaviour
    {
        private const float EdgeMargin = 120f;
        private const float TopMargin = 220f;

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
            var popup = Free();
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

                // a large area may have its middle off the screen or under the HUD: the label stays where it is seen
                var canvas = new Vector2(Screen.width, Screen.height) / scale;
                var screen = (Vector2)_camera.WorldToScreenPoint(popup.World) / scale;
                screen.x = Mathf.Clamp(screen.x, EdgeMargin, Mathf.Max(EdgeMargin, canvas.x - EdgeMargin));
                screen.y = Mathf.Clamp(screen.y, EdgeMargin, Mathf.Max(EdgeMargin, canvas.y - TopMargin - _view.PopupRise));
                ((RectTransform)popup.Label.transform).anchoredPosition = screen + Vector2.up * (_view.PopupRise * t);
                popup.Label.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            }
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
        }
    }
}
