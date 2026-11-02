using UnityEngine;

namespace Core.ScreenSystem.Views
{
    /// <summary>Keeps this RectTransform inside the safe area; everything tied to an edge lives under it.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _area;
        private Vector2Int _size;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _area || Screen.width != _size.x || Screen.height != _size.y)
                Apply();
        }

        private void Apply()
        {
            _area = Screen.safeArea;
            _size = new Vector2Int(Screen.width, Screen.height);
            if (_size.x <= 0 || _size.y <= 0)
                return;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(_area.xMin / _size.x, _area.yMin / _size.y);
            rect.anchorMax = new Vector2(_area.xMax / _size.x, _area.yMax / _size.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
