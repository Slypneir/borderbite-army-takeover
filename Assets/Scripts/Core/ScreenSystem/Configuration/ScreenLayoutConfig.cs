using UnityEngine;

namespace Core.ScreenSystem.Configuration
{
    /// <summary>What the camera must always show, whatever the shape of the window.</summary>
    [CreateAssetMenu(fileName = "ScreenLayoutConfig", menuName = "Borderbite/Screen/Layout")]
    public sealed class ScreenLayoutConfig : ScriptableObject
    {
        [Tooltip("Width of the map, world units. Starting value: 10.")]
        [Min(0.1f)] [SerializeField] private float mapWidth = 10f;

        [Tooltip("Height of the map, world units. Starting value: 10.")]
        [Min(0.1f)] [SerializeField] private float mapHeight = 10f;

        [Tooltip("Free world units above and below the map in a tall window, for the interface at the edges. Starting value: 2.")]
        [Min(0f)] [SerializeField] private float portraitMargin = 2f;

        [Tooltip("Free world units left and right of the map in a wide window, for the interface at the edges. Starting value: 2.")]
        [Min(0f)] [SerializeField] private float landscapeMargin = 2f;

        public float MapWidth => mapWidth;
        public float MapHeight => mapHeight;
        public float PortraitMargin => portraitMargin;
        public float LandscapeMargin => landscapeMargin;
    }
}
