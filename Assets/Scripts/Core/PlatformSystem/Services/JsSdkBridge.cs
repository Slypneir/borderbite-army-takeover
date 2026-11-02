using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Core.PlatformSystem.Data;
using Core.PlatformSystem.Interfaces;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.PlatformSystem.Services
{
    /// <summary>
    /// Calls go through the jslib of the platform (Plugins/WebGL/Platforms; a build links exactly one) and come back by SendMessage.
    /// </summary>
    public sealed class JsSdkBridge : ISdkBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GamePlatformCall(string op, string id, string arg);
#endif

        private readonly Dictionary<string, UniTaskCompletionSource<string>> _pending = new Dictionary<string, UniTaskCompletionSource<string>>();
        private PlatformReceiver _receiver;
        private int _nextId;

        public event Action<string> OnEvent;

        /// <summary>The receiver exists before the first call: the SDK may send events right after init.</summary>
        public void Listen()
        {
            if (_receiver != null)
                return;

            _receiver = PlatformReceiver.Create();
            _receiver.OnResult += HandleResult;
            _receiver.OnEvent += type => OnEvent?.Invoke(type);
        }

        public async UniTask<string> CallAsync(string op, string arg, CancellationToken ct = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Listen();
            var id = (++_nextId).ToString();
            var source = new UniTaskCompletionSource<string>();
            _pending[id] = source;
            GamePlatformCall(op, id, arg ?? "");
            try
            {
                return await source.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _pending.Remove(id);
            }
#else
            await UniTask.CompletedTask;
            return null;
#endif
        }

        private void HandleResult(string json)
        {
            var answer = JsonUtility.FromJson<SdkAnswer>(json);
            if (answer.id == null || !_pending.TryGetValue(answer.id, out var source))
                return;

            if (answer.ok)
            {
                source.TrySetResult(answer.value ?? "");
            }
            else
            {
                Debug.LogWarning($"[JsSdkBridge] SDK: {answer.error}");
                source.TrySetResult(null);
            }
        }
    }
}
