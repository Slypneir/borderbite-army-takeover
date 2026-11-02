using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.PlatformSystem.Interfaces
{
    /// <summary>
    /// The way to the SDK in the page: the jslib of the platform in a build, a fake in the tests.
    /// </summary>
    public interface ISdkBridge
    {
        /// <summary>
        /// pause, resume, audioOn, audioOff, focusLost, focusBack, adStart, adEnd
        /// </summary>
        event Action<string> OnEvent;

        /// <summary>
        /// Calls the SDK; the answer of the call, null when it failed or there is no SDK
        /// </summary>
        UniTask<string> CallAsync(string op, string arg, CancellationToken ct = default);
    }
}
