using System;
using UnityEngine;

namespace Core.PlatformSystem.Services
{
    /// <summary>Receives answers and events of the platform SDK (SendMessage from the jslib of the platform).</summary>
    public sealed class PlatformReceiver : MonoBehaviour
    {
        public const string ObjectName = "PlatformReceiver";

        /// <summary>JSON {id, ok, value, error} of a finished call.</summary>
        public event Action<string> OnResult;

        /// <summary>See <see cref="Interfaces.ISdkBridge.OnEvent"/>.</summary>
        public event Action<string> OnEvent;

        public void Result(string json) => OnResult?.Invoke(json);

        public void Event(string type) => OnEvent?.Invoke(type);

        public static PlatformReceiver Create()
        {
            var go = new GameObject(ObjectName);
            DontDestroyOnLoad(go);
            return go.AddComponent<PlatformReceiver>();
        }
    }
}
