using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Services;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The health of the hero of the player as a bar over its head (stage 1 spec 3.5); gone while the hero is dead. Lives right
    /// under the canvas, anchored to its lower left corner; <see cref="fill"/> is anchored to the left of the bar.
    /// </summary>
    public sealed class HeroBarView : MonoBehaviour
    {
        private const float Height = 1.5f;

        [SerializeField] private RectTransform bar;
        [SerializeField] private RectTransform fill;

        private IBattleDriver _battle;
        private HeroView _hero;
        private Camera _camera;
        private float _maxHealth;

        [Inject]
        public void Construct(IBattleDriver battle, HeroView hero, Camera mainCamera, BattleConfig rules)
        {
            _battle = battle;
            _hero = hero;
            _camera = mainCamera;
            _maxHealth = rules.HeroHealth * 10f;
        }

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            var shown = false;
            if (simulation != null)
            {
                foreach (var hero in simulation.Heroes)
                {
                    if (hero.Side != Side.Player || !hero.Alive)
                        continue;

                    shown = true;
                    fill.anchorMax = new Vector2(Mathf.Clamp01(hero.Health / _maxHealth), 1f);
                }
            }

            bar.gameObject.SetActive(shown);
            if (!shown)
                return;

            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            bar.anchoredPosition = (Vector2)_camera.WorldToScreenPoint(_hero.Position + Vector3.up * Height) / scale;
        }
    }
}
