using System;

namespace Core.PlatformSystem.Data
{
    /// <summary>The answer of a jslib call: {id, ok, value, error}.</summary>
    [Serializable]
    public sealed class SdkAnswer
    {
        public string id;
        public bool ok;
        public string value;
        public string error;
    }

    /// <summary>The answer of «init»: the language tag of the player and whether the sound is allowed.</summary>
    [Serializable]
    public sealed class SdkInitAnswer
    {
        public string lang;
        public bool audio = true;
    }
}
