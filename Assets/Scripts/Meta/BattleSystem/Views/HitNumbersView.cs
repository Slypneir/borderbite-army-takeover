using System.Collections.Generic;
using Meta.BattleSystem.Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The «−1» over the bar of the player's hero when it is hurt (task 4.22, battle-marks card «Попадание по полоске»; task 4.34,
    /// spec 3.5): it shows from 0.16 s, when the lost segment breaks, rises and fades by 0.86 s, white. The enemy's units and the
    /// player's soldiers get no number (owner: the screen was cluttered); their bars still flash, jerk and break. Copies of
    /// <see cref="template"/>; lives right under the canvas, anchored to its lower left corner. Buildings get no number.
    /// </summary>
    public sealed class HitNumbersView : MonoBehaviour
    {
        private const float Appear = 0.16f;
        private const float Gone = 0.86f;
        private const float Rise = 40f;
        private static readonly Color Own = new Color(1f, 1f, 1f, 1f);

        [SerializeField] private TMP_Text template;

        private readonly List<Number> _numbers = new List<Number>();
        private BarsView _bars;
        private Camera _camera;

        [Inject]
        public void Construct(BarsView bars, Camera mainCamera)
        {
            _bars = bars;
            _camera = mainCamera;
            _bars.Struck += Add;
        }

        private void Awake() => template.gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (_bars != null)
                _bars.Struck -= Add;
        }

        private void Add(Vector3 world, bool playerHero)
        {
            // a number only over the player's hero when it is hurt (task 4.34, spec 3.5): the enemy's units and the player's
            // soldiers get none, their bars still answer the blow
            if (!playerHero)
                return;

            Number number = null;
            foreach (var free in _numbers)
            {
                if (!free.Label.gameObject.activeSelf)
                {
                    number = free;
                    break;
                }
            }

            if (number == null)
            {
                number = new Number { Label = Instantiate(template, transform) };
                _numbers.Add(number);
            }

            number.World = world;
            number.Age = 0f;
            // the static atlas of the font has the en dash, not the minus sign
            number.Label.text = "–1";
            number.Label.color = Own;
            number.Label.alpha = 0f;
            number.Label.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            foreach (var number in _numbers)
            {
                if (!number.Label.gameObject.activeSelf)
                    continue;

                number.Age += Time.deltaTime;
                if (number.Age >= Gone)
                {
                    number.Label.gameObject.SetActive(false);
                    continue;
                }

                var screen = _camera.WorldToScreenPoint(number.World);
                var t = Mathf.Clamp01((number.Age - Appear) / (Gone - Appear));
                var rect = number.Label.rectTransform;
                rect.anchoredPosition = (Vector2)screen / scale + Vector2.up * (Rise * t);
                rect.localScale = Vector3.one;
                number.Label.alpha = number.Age < Appear ? 0f : 1f - t * t;
            }
        }

        private sealed class Number
        {
            public TMP_Text Label;
            public Vector3 World;
            public float Age;
        }
    }
}
