using System;
using Core.ScreenSystem.Enums;

namespace Core.ScreenSystem.Utils
{
    /// <summary>The camera scale for a window; the map and its margins are never cut.</summary>
    public static class CameraFit
    {
        public static ScreenShape ShapeOf(int width, int height) => width >= height ? ScreenShape.Landscape : ScreenShape.Portrait;

        /// <summary>The orthographic size (half of the visible height) at which the map and the margins of this shape fit the window.</summary>
        public static float OrthographicSize(float mapWidth, float mapHeight, float marginAlongLongSide, ScreenShape shape, float aspect)
        {
            var neededWidth = mapWidth + (shape == ScreenShape.Landscape ? marginAlongLongSide * 2f : 0f);
            var neededHeight = mapHeight + (shape == ScreenShape.Portrait ? marginAlongLongSide * 2f : 0f);
            return Math.Max(neededHeight / 2f, neededWidth / (2f * aspect));
        }
    }
}
