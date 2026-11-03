using System;
using Core.ScreenSystem.Enums;

namespace Core.ScreenSystem.Utils
{
    /// <summary>The scale of the tilted orthographic camera for a window: the same meters of ground along the shorter side.</summary>
    public static class CameraFit
    {
        public static ScreenShape ShapeOf(int width, int height) => width >= height ? ScreenShape.Landscape : ScreenShape.Portrait;

        /// <summary>
        /// The orthographic size (half of the visible height in the view plane). Across the screen the ground is seen at full
        /// size; along it a camera tilted from the horizon stretches the ground by 1 / sin(tilt).
        /// </summary>
        public static float OrthographicSize(float visibleShortSide, float tiltDegrees, float aspect)
        {
            if (aspect < 1f)
                return visibleShortSide / (2f * aspect);

            return visibleShortSide * (float)Math.Sin(tiltDegrees * Math.PI / 180d) / 2f;
        }

        /// <summary>Meters of ground seen across (width) and along (depth) the window for a size.</summary>
        public static (float Width, float Depth) VisibleGround(float orthographicSize, float tiltDegrees, float aspect) =>
            (2f * orthographicSize * aspect, 2f * orthographicSize / (float)Math.Sin(tiltDegrees * Math.PI / 180d));
    }
}
