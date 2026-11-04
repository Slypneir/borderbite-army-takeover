using UnityEngine;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The iron plate of the top HUD (battle-hud card «Вид в стиле игры», art of the 3D artist 3a3c4f0): one framed plate with no
    /// gap between the territory bar and the counters. On a tall screen two rows — the bar on top, the counters under it with the
    /// pause on the right (≈ 156 of HudTop 200), one rivet in the middle of the top and the bottom; on a wide or square one a
    /// single row «pause · counters · bar» (≈ 104 of 120), three rivets on each long side. Rebuilt only when the shape of the
    /// window changes.
    /// </summary>
    public sealed class HudLayoutView : MonoBehaviour
    {
        private const float EdgeInset = 24f;
        private const float TallHeight = 156f;
        private const float WideHeight = 104f;
        // the rim of the frame at its scale of 0.75 (25 px), and the room inside it
        private const float Inside = 20f;
        private const float Gap = 8f;
        private const float BarHeight = 64f;
        private const float RowHeight = 56f;
        private const float PauseSide = 64f;
        private const float CountersWidth = 540f;
        // a rivet sits on the middle line of the rim: 14 px of the frame at 0.75
        private const float RivetInset = 10.5f;

        [SerializeField] private RectTransform plate;
        [SerializeField] private RectTransform bar;
        [SerializeField] private RectTransform counters;
        [SerializeField] private RectTransform pause;
        [SerializeField] private RectTransform[] topRivets;
        [SerializeField] private RectTransform[] bottomRivets;

        private int _shape = -1;

        private void LateUpdate()
        {
            var shape = Screen.width >= Screen.height ? 1 : 0;
            if (shape == _shape)
                return;

            _shape = shape;
            if (shape == 1)
                Wide();
            else
                Tall();
        }

        private void Tall()
        {
            Place(plate, TallHeight);
            // the bar across the top, inside the rim
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(Inside, -Inside - BarHeight);
            bar.offsetMax = new Vector2(-Inside, -Inside);
            // the counters under it on the left, the pause on the right, 8 inside the rim
            Corner(counters, new Vector2(0f, 0f), new Vector2(Inside, Inside), new Vector2(CountersWidth, RowHeight));
            Corner(pause, new Vector2(1f, 0f), new Vector2(-Inside - Gap - PauseSide / 2f, Inside + RowHeight / 2f), null);
            Rivets(1);
        }

        private void Wide()
        {
            Place(plate, WideHeight);
            var middle = WideHeight / 2f;
            Corner(pause, new Vector2(0f, 0f), new Vector2(Inside + Gap + PauseSide / 2f, middle), null);
            var countersLeft = Inside + Gap + PauseSide + Gap * 2f;
            Corner(counters, new Vector2(0f, 0f), new Vector2(countersLeft, middle - RowHeight / 2f), new Vector2(CountersWidth, RowHeight));
            // the bar takes the rest, 20 under the top rim (≥ 12, the flags of the pins stay off the rim)
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.offsetMin = new Vector2(countersLeft + CountersWidth + Gap, -BarHeight / 2f);
            bar.offsetMax = new Vector2(-Inside, BarHeight / 2f);
            Rivets(3);
        }

        private static void Place(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(EdgeInset, -EdgeInset - height);
            rect.offsetMax = new Vector2(-EdgeInset, -EdgeInset);
        }

        /// <summary>A rect anchored to a corner of the plate; its size stays when <paramref name="size"/> is null.</summary>
        private static void Corner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2? size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            if (size.HasValue)
            {
                rect.pivot = Vector2.zero;
                rect.sizeDelta = size.Value;
                rect.anchoredPosition = new Vector2(anchor.x > 0.5f ? position.x - size.Value.x : position.x, position.y);
            }
            else
            {
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
            }
        }

        /// <summary>Rivets on the middle of the long sides: one each on a tall screen, three each on a wide one.</summary>
        private void Rivets(int count)
        {
            for (var i = 0; i < topRivets.Length; i++)
            {
                var shown = i < count;
                topRivets[i].gameObject.SetActive(shown);
                bottomRivets[i].gameObject.SetActive(shown);
                if (!shown)
                    continue;

                var x = (i + 1f) / (count + 1f);
                Set(topRivets[i], new Vector2(x, 1f), -RivetInset);
                Set(bottomRivets[i], new Vector2(x, 0f), RivetInset);
            }
        }

        private static void Set(RectTransform rivet, Vector2 anchor, float y)
        {
            rivet.anchorMin = anchor;
            rivet.anchorMax = anchor;
            rivet.pivot = new Vector2(0.5f, 0.5f);
            rivet.anchoredPosition = new Vector2(0f, y);
        }
    }
}
