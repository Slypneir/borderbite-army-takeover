using Core.ScreenSystem.Enums;
using Core.ScreenSystem.Utils;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.ScreenSystem.Editor
{
    public sealed class CameraFitTests
    {
        private const float Visible = 16f;
        private const float Tilt = 60f;

        [TestCase(1080, 1920, ScreenShape.Portrait)]
        [TestCase(1920, 1080, ScreenShape.Landscape)]
        [TestCase(907, 510, ScreenShape.Landscape)]
        [TestCase(800, 800, ScreenShape.Landscape)]
        public void ShapeOf_Size_Shape(int width, int height, ScreenShape expected)
        {
            CameraFit.ShapeOf(width, height).Should().Be(expected);
        }

        [TestCase(1080, 1920)]
        [TestCase(720, 1600)]
        [TestCase(600, 1800)]
        public void OrthographicSize_TallWindow_SixteenMetersAcross(int width, int height)
        {
            var aspect = (float)width / height;

            var size = CameraFit.OrthographicSize(Visible, Tilt, aspect);

            CameraFit.VisibleGround(size, Tilt, aspect).Width.Should().BeApproximately(Visible, 0.001f);
        }

        [TestCase(1920, 1080)]
        [TestCase(1366, 768)]
        [TestCase(907, 510)]
        [TestCase(2560, 1080)]
        public void OrthographicSize_WideWindow_SixteenMetersOfGroundUpTheScreen(int width, int height)
        {
            var aspect = (float)width / height;

            var size = CameraFit.OrthographicSize(Visible, Tilt, aspect);

            CameraFit.VisibleGround(size, Tilt, aspect).Depth.Should().BeApproximately(Visible, 0.001f);
        }

        [Test]
        public void OrthographicSize_StraightDown_GroundEqualsTheView()
        {
            var size = CameraFit.OrthographicSize(Visible, 90f, 16f / 9f);

            (size * 2f).Should().BeApproximately(Visible, 0.001f);
        }
    }
}
