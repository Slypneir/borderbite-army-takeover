using Meta.MapEditorSystem.Interfaces;
using Meta.MapEditorSystem.Services;
using Meta.MapEditorSystem.Views;
using VContainer;
using VContainer.Unity;

namespace Meta.MapEditorSystem.Installers
{
    /// <summary>The map editor of layer 5: the map files of the project, the map open for editing and its screen.</summary>
    public sealed class MapEditorSystemInstaller : IInstaller
    {
        private readonly MapEditorView _view;

        public MapEditorSystemInstaller(MapEditorView view) => _view = view;

        public void Install(IContainerBuilder builder)
        {
            builder.Register<MapLibrary>(Lifetime.Singleton).As<IMapLibrary>();
            builder.Register<MapEditing>(Lifetime.Singleton).AsSelf();
            builder.RegisterInstance(_view);
        }
    }
}
