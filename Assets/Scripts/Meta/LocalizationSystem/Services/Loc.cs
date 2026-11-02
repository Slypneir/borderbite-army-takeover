using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meta.LocalizationSystem.Services
{
    /// <summary>
    /// The languages of the game: a table per language in Resources/Localization/&lt;code&gt;.json. A string missing in the
    /// language falls back to English, then to the key itself; a third language is one more file and one more code in
    /// <see cref="Languages"/>. The change of the language is seen at once through <see cref="Changed"/>.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Russian = "ru";

        public static readonly string[] Languages = { English, Russian };

        private const string Folder = "Localization/";
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>The language of the game now; English until the platform answers.</summary>
        public static string Language { get; private set; } = English;

        /// <summary>The language changed: open screens and labels draw their text again.</summary>
        public static event Action Changed;

        /// <summary>Switches the language; a language the game does not have is English.</summary>
        public static void Set(string language)
        {
            language = FromPlatform(language);
            if (language == Language)
                return;

            Language = language;
            Changed?.Invoke();
        }

        /// <summary>The language of the game for a tag from the SDK (ru, en-US, de): its own when the game has it, else English.</summary>
        public static string FromPlatform(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag.Length < 2)
                return English;

            var code = tag.Substring(0, 2).ToLowerInvariant();
            return Array.IndexOf(Languages, code) >= 0 ? code : English;
        }

        /// <summary>The string of <paramref name="key"/> in the language, else in English, else the key.</summary>
        public static string Get(string key)
        {
            if (Table(Language).TryGetValue(key, out var text) || Table(English).TryGetValue(key, out text))
                return text;

            Debug.LogWarning($"[Loc] no string {key}");
            return key;
        }

        /// <summary>All keys of a language, for the check that the tables match.</summary>
        public static IReadOnlyCollection<string> KeysOf(string language) => Table(language).Keys;

        private static Dictionary<string, string> Table(string language)
        {
            if (Tables.TryGetValue(language, out var table))
                return table;

            table = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>(Folder + language);
            if (asset != null)
            {
                foreach (var entry in JsonUtility.FromJson<LocTable>(asset.text).strings)
                    table[entry.key] = entry.text;
            }

            Tables[language] = table;
            return table;
        }

        [Serializable]
        private sealed class LocTable
        {
            public LocEntry[] strings = Array.Empty<LocEntry>();
        }

        [Serializable]
        private sealed class LocEntry
        {
            public string key;
            public string text;
        }
    }
}
