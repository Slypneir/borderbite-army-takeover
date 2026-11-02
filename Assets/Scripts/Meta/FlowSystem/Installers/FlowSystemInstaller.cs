using Meta.FlowSystem.Services;
using VContainer;
using VContainer.Unity;

namespace Meta.FlowSystem.Installers
{
    /// <summary>
    /// The flow of the game; so far only the boot.
    /// </summary>
    public sealed class FlowSystemInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<BootFlow>(Lifetime.Singleton).As<IAsyncStartable>();
        }
    }
}
