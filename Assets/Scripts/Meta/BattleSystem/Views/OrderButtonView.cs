using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// One button of the orders (army-orders card of the UI): the frame of its group (normal, and the active one over it, faded
    /// in by <see cref="OrdersView"/>), the icon on top, a light strip under it while active, the letter of its key in the corner.
    /// The order goes at the touch, not at the release (<see cref="IPointerDownHandler"/>): a finger that holds the stick and
    /// slides over the row presses nothing — a touch belongs to where it began.
    /// </summary>
    public sealed class OrderButtonView : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private RectTransform body;
        [SerializeField] private Image frame;
        [SerializeField] private Image activeFrame;
        [SerializeField] private Image icon;
        [SerializeField] private Image strip;
        [SerializeField] private TMP_Text key;

        [Tooltip("256 / 128 frames and 128 / 64 icons: the small ones for a window with less than a pixel per unit of the canvas.")]
        [SerializeField] private Sprite frameNormal;
        [SerializeField] private Sprite frameActive;
        [SerializeField] private Sprite frameNormalSmall;
        [SerializeField] private Sprite frameActiveSmall;
        [SerializeField] private Sprite iconLarge;
        [SerializeField] private Sprite iconSmall;

        private bool _small;
        private bool _drawn;

        public event Action Pressed;

        public RectTransform Body => body;
        public Image Frame => frame;
        public Image ActiveFrame => activeFrame;
        public Image Icon => icon;
        public Image Strip => strip;
        public TMP_Text Key => key;

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();

        /// <summary>The sprites for the density of the window: the 256 frames and 128 icons, or the halves.</summary>
        public void Fit(bool small)
        {
            if (_drawn && small == _small)
                return;

            _drawn = true;
            _small = small;
            frame.sprite = small && frameNormalSmall != null ? frameNormalSmall : frameNormal;
            activeFrame.sprite = small && frameActiveSmall != null ? frameActiveSmall : frameActive;
            icon.sprite = small && iconSmall != null ? iconSmall : iconLarge;
        }
    }
}
