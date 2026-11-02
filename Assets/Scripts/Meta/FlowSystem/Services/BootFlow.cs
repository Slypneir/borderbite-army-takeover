using System.Threading;
using Core.PlatformSystem.Interfaces;
using Cysharp.Threading.Tasks;
using Meta.LocalizationSystem.Services;
using UnityEngine;
using VContainer.Unity;

namespace Meta.FlowSystem.Services
{
    /// <summary>
    /// From the first frame to the moment the player can act: the SDK, the language from the SDK, the save, the signals of the
    /// platform in the order it asks for. The check of a build waits for the line «[BootFlow] ready».
    /// </summary>
    public sealed class BootFlow : IAsyncStartable
    {
        private readonly IPlatform _platform;

        public BootFlow(IPlatform platform)
        {
            _platform = platform;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            await UniTask.NextFrame(ct);
            await _platform.InitializeAsync(ct);
            _platform.SignalFirstFrame();
            Debug.Log("[BootFlow] first frame");

            Loc.Set(_platform.Language);
            await _platform.LoadAsync(ct);

            _platform.SignalReady();
            Debug.Log($"[BootFlow] ready, language {Loc.Language}");
        }
    }
}
