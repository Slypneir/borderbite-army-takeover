using Meta.BattleSystem.Services;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// Hides the layers of the battle under a window (UI review of layers 3–4): the marks over the world under any window, the
    /// pause button too, and the HUD of the battle between levels. Each layer is a <see cref="CanvasGroup"/>.
    /// </summary>
    public sealed class BattleLayersView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] overWorld;
        [SerializeField] private CanvasGroup[] hud;
        [SerializeField] private CanvasGroup pauseButton;

        private BattleScreen _screen;

        [Inject]
        public void Construct(BattleScreen screen) => _screen = screen;

        private void LateUpdate()
        {
            if (_screen == null)
                return;

            foreach (var layer in overWorld)
                Show(layer, !_screen.Covered);
            foreach (var layer in hud)
                Show(layer, !_screen.Between);
            Show(pauseButton, !_screen.Covered);
        }

        private static void Show(CanvasGroup layer, bool shown)
        {
            if (layer == null)
                return;

            layer.alpha = shown ? 1f : 0f;
            layer.blocksRaycasts = shown;
            layer.interactable = shown;
        }
    }
}
