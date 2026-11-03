using System;
using Core.ScreenSystem.Configuration;
using Core.ScreenSystem.Enums;
using Core.ScreenSystem.Interfaces;
using Core.ScreenSystem.Utils;
using UnityEngine;
using VContainer.Unity;

namespace Core.ScreenSystem.Services
{
    /// <summary>Watches the size of the window and scales the camera; the game state does not care about it.</summary>
    public sealed class ScreenLayoutService : IScreenLayout, IStartable, ITickable
    {
        private readonly ScreenLayoutConfig _config;
        private readonly Camera _camera;
        private int _width;
        private int _height;

        public ScreenLayoutService(ScreenLayoutConfig config, Camera camera)
        {
            _config = config;
            _camera = camera;
        }

        public ScreenShape Shape { get; private set; }

        public event Action OnChanged;

        public void Start() => Refresh();

        public void Tick()
        {
            if (Screen.width != _width || Screen.height != _height)
                Refresh();
        }

        private void Refresh()
        {
            _width = Screen.width;
            _height = Screen.height;
            Shape = CameraFit.ShapeOf(_width, _height);
            _camera.orthographicSize = CameraFit.OrthographicSize(_config.VisibleShortSide, _config.Tilt, (float)_width / Mathf.Max(1, _height));
            Debug.Log($"[ScreenLayoutService] {_width}x{_height} {Shape}, camera size {_camera.orthographicSize:0.00}");
            OnChanged?.Invoke();
        }
    }
}
