using Core.BattleSystem.Enums;
using Meta.BattleSystem.Services;
using TMPro;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The «−N» over the bar of the player's hero when it is hurt (task 4.22, battle-marks card «Попадание по полоске»; task 4.34,
    /// spec 3.5; UI review of 4.34): 32 bold, white with a dark rim of 3, over the middle of the bar (its top + 8) wherever the
    /// hero walks; it shows from 0.16 s, when the lost segment breaks, rises 24 and fades by 0.86 s. Blows less than 0.5 s apart
    /// join it: «−2», «−3». The enemy's units and the player's soldiers get no number (owner: the screen was cluttered); their
    /// bars still flash, jerk and break. Buildings get no number. Lives right under the canvas, anchored to its lower left
    /// corner.
    /// </summary>
    public sealed class HitNumbersView : MonoBehaviour
    {
        private const float Appear = 0.16f;
        private const float Gone = 0.86f;
        private const float Rise = 24f;
        private const float Above = 8f;
        private const float JoinSeconds = 0.5f;
        // the bar of a hero (BarsView): its middle over the feet and its half height, meters
        private const float BarHeight = 2.15f;
        private const float BarHalf = 0.1f;

        [SerializeField] private TMP_Text template;

        private BarsView _bars;
        private HeroView _heroes;
        private Camera _camera;
        private TMP_Text _label;
        private float _age = Gone;
        private float _lastAt = -10f;
        private int _sum;

        [Inject]
        public void Construct(BarsView bars, HeroView heroes, Camera mainCamera)
        {
            _bars = bars;
            _heroes = heroes;
            _camera = mainCamera;
            _bars.Struck += Add;
        }

        private void Awake()
        {
            _label = template;
            _label.fontSize = 32f;
            _label.fontStyle = FontStyles.Bold;
            _label.color = Color.white;
            _label.outlineColor = new Color32(0x17, 0x14, 0x1a, 0xff);
            // a rim of 3 at a size of 32
            _label.outlineWidth = 0.3f;
            _label.gameObject.SetActive(false);
        }

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

            var now = Time.time;
            var joins = _label.gameObject.activeSelf && now - _lastAt < JoinSeconds;
            _sum = joins ? _sum + 1 : 1;
            // a joined blow shows the sum at once and starts the rise again; a first one waits for its segment to break
            _age = joins ? Appear : 0f;
            _lastAt = now;
            // the static atlas of the font has the en dash, not the minus sign
            _label.text = "–" + _sum;
            _label.alpha = joins ? 1f : 0f;
            _label.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!_label.gameObject.activeSelf)
                return;

            _age += Time.deltaTime;
            if (_age >= Gone)
            {
                _label.gameObject.SetActive(false);
                return;
            }

            var scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)) / 720f;
            var top = _heroes.PositionOf(Side.Player) + Vector3.up * (BarHeight + BarHalf);
            var t = Mathf.Clamp01((_age - Appear) / (Gone - Appear));
            _label.rectTransform.anchoredPosition = (Vector2)_camera.WorldToScreenPoint(top) / scale + Vector2.up * (Above + Rise * t);
            _label.alpha = _age < Appear ? 0f : 1f - t * t;
        }
    }
}
