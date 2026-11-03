using Core.InputManagement.Configuration;
using Core.InputManagement.Interfaces;
using UnityEngine;
using VContainer;

namespace Core.InputManagement.Views
{
    /// <summary>
    /// Draws the floating stick of <see cref="IMoveInput"/>: the base under the finger, the knob at its offset, both
    /// semi-transparent and only while held. Lives right under the canvas, anchored to its lower left corner.
    /// </summary>
    public sealed class MoveStickView : MonoBehaviour
    {
        [SerializeField] private RectTransform stickBase;
        [SerializeField] private RectTransform knob;
        [SerializeField] private CanvasGroup group;

        private IMoveInput _input;
        private MoveStickConfig _config;

        [Inject]
        public void Construct(IMoveInput input, MoveStickConfig config)
        {
            _input = input;
            _config = config;
        }

        private void LateUpdate()
        {
            if (_input == null)
                return;

            var shown = _input.StickShown;
            group.alpha = shown ? _config.Opacity : 0f;
            if (!shown)
                return;

            stickBase.sizeDelta = Vector2.one * _config.BaseDiameter;
            knob.sizeDelta = Vector2.one * _config.KnobDiameter;
            stickBase.anchoredPosition = _input.StickCenter;
            knob.anchoredPosition = _input.KnobOffset;
        }
    }
}
