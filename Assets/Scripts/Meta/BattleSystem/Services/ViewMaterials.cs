using System;
using System.Collections.Generic;
using Meta.BattleSystem.Configuration;
using UnityEngine;

namespace Meta.BattleSystem.Services
{
    /// <summary>One copy of the flat placeholder material per colour, shared by every view of the battle.</summary>
    public sealed class ViewMaterials : IDisposable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly BattleViewConfig _view;
        private readonly Dictionary<Color, Material> _byColor = new Dictionary<Color, Material>();

        public ViewMaterials(BattleViewConfig view)
        {
            _view = view;
        }

        public Material Flat(Color color)
        {
            if (_byColor.TryGetValue(color, out var material))
                return material;

            material = new Material(_view.FlatMaterial);
            material.SetColor(BaseColorId, color);
            _byColor[color] = material;
            return material;
        }

        /// <summary>Gives every renderer under <paramref name="root"/> the flat material of a colour.</summary>
        public void Paint(Transform root, Color color)
        {
            var material = Flat(color);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = material;
        }

        public void Dispose()
        {
            foreach (var material in _byColor.Values)
                UnityEngine.Object.Destroy(material);
            _byColor.Clear();
        }
    }
}
