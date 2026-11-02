using System.Linq;
using FluentAssertions;
using Meta.LocalizationSystem.Services;
using NUnit.Framework;

namespace Tests.LocalizationSystem.Editor
{
    public sealed class LocTests
    {
        [TearDown]
        public void TearDown() => Loc.Set(Loc.English);

        [TestCase("ru", "ru")]
        [TestCase("ru-RU", "ru")]
        [TestCase("RU", "ru")]
        [TestCase("en-US", "en")]
        [TestCase("de", "en")]
        [TestCase("tr-TR", "en")]
        [TestCase("", "en")]
        [TestCase(null, "en")]
        public void FromPlatform_Tag_LanguageOfTheGame(string tag, string expected)
        {
            Loc.FromPlatform(tag).Should().Be(expected);
        }

        [Test]
        public void Tables_EnglishAndRussian_SameKeys()
        {
            Loc.KeysOf(Loc.English).Should().NotBeEmpty();
            Loc.KeysOf(Loc.Russian).OrderBy(k => k).Should().Equal(Loc.KeysOf(Loc.English).OrderBy(k => k));
        }

        [Test]
        public void Get_LanguageChanged_TextAndEvent()
        {
            var changed = 0;
            void Count() => changed++;
            Loc.Changed += Count;

            Loc.Set(Loc.Russian);
            var russian = Loc.Get("boot.status");
            Loc.Set(Loc.English);
            var english = Loc.Get("boot.status");
            Loc.Changed -= Count;

            russian.Should().Be("Готово к бою");
            english.Should().Be("Ready for battle");
            changed.Should().Be(2);
        }

        [Test]
        public void Get_UnknownKey_TheKey()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, "[Loc] no string nope");

            Loc.Get("nope").Should().Be("nope");
        }
    }
}
