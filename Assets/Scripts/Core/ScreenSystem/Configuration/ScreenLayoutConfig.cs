using UnityEngine;

namespace Core.ScreenSystem.Configuration
{
    /// <summary>What the camera shows, whatever the shape of the window (stage 1 spec 2.3).</summary>
    [CreateAssetMenu(fileName = "ScreenLayoutConfig", menuName = "Borderbite/Screen/Layout")]
    public sealed class ScreenLayoutConfig : ScriptableObject
    {
        [Tooltip("Meters of the ground seen along the shorter side of the window, in portrait and landscape. Starting value: 16.")]
        [Min(1f)] [SerializeField] private float visibleShortSide = 16f;

        [Tooltip("Tilt of the camera from the horizon, degrees: 90 looks straight down. Starting value: 50 (owner, 4 November).")]
        [Range(10f, 90f)] [SerializeField] private float tilt = 50f;

        public float VisibleShortSide => visibleShortSide;
        public float Tilt => tilt;
    }
}
