using System.Collections.Generic;
using Meta.BattleSystem.Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The «−1» over a hit bar (task 4.22, battle-marks card «Попадание по полоске»): it shows from 0.16 s, when the lost segment
    /// breaks, rises and fades by 0.86 s. By the player's units it is white and small; by the enemy's golden (#ffe08a), larger,
    /// with a pop at the start. Copies of <see cref="template"/>; lives right under the canvas, anchored to its lower left corner.
    /// Buildings get no number: their blows are frequent and would be noise.
    /// </summary>
    public sealed class HitNumbersView : MonoBehaviour
    {
        private const float Appear = 0.16f;
        private const float Gone = 0.86f;
        private const float Rise = 40f;
        private static readonly Color Own = new Color(1f, 1f, 1f, 1f);
        private static readonly Color Enemy = new Color(1f, 0.878f, 0.541f, 1f);

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

        private void Add(Vector3 world, bool enemy)
        {
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
            number.Enemy = enemy;
            // the static atlas of the font has the en dash, not the minus sign
            number.Label.text = "–1";
            number.Label.color = enemy ? Enemy : Own;
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
                var pop = number.Enemy && t < 0.2f ? 1f + 0.4f * Mathf.Sin(t / 0.2f * Mathf.PI) : 1f;
                rect.localScale = Vector3.one * ((number.Enemy ? 1.3f : 1f) * pop);
                number.Label.alpha = number.Age < Appear ? 0f : 1f - t * t;
            }
        }

        private sealed class Number
        {
            public TMP_Text Label;
            public Vector3 World;
            public float Age;
            public bool Enemy;
        }
    }
}
