using Core.ScreenSystem.Enums;
using Core.ScreenSystem.Utils;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.ScreenSystem.Editor
{
    public sealed class CameraFitTests
    {
        private const float Map = 10f;
        private const float Margin = 2f;

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
        [TestCase(1920, 1080)]
        [TestCase(907, 510)]
        [TestCase(1366, 768)]
        [TestCase(800, 800)]
        [TestCase(2560, 1080)]
        [TestCase(600, 1800)]
        public void OrthographicSize_AnyWindow_MapIsNeverCut(int width, int height)
        {
            var shape = CameraFit.ShapeOf(width, height);
            var aspect = (float)width / height;

            var size = CameraFit.OrthographicSize(Map, Map, Margin, shape, aspect);

            (size * 2f).Should().BeGreaterOrEqualTo(Map);
            (size * 2f * aspect).Should().BeGreaterOrEqualTo(Map - 0.001f);
        }

        [Test]
        public void OrthographicSize_TallWindow_MapFillsTheWidth()
        {
            var aspect = 9f / 16f;

            var size = CameraFit.OrthographicSize(Map, Map, Margin, ScreenShape.Portrait, aspect);

            (size * 2f * aspect).Should().BeApproximately(Map, 0.001f);
        }

        [Test]
        public void OrthographicSize_WideWindow_MapFillsTheHeight()
        {
            var size = CameraFit.OrthographicSize(Map, Map, Margin, ScreenShape.Landscape, 16f / 9f);

            (size * 2f).Should().BeApproximately(Map, 0.001f);
        }

        [Test]
        public void OrthographicSize_SquareWindow_MarginsKept()
        {
            var size = CameraFit.OrthographicSize(Map, Map, Margin, ScreenShape.Landscape, 1f);

            (size * 2f).Should().BeApproximately(Map + Margin * 2f, 0.001f);
        }
    }
}
