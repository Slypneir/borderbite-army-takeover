using System;
using System.Collections.Generic;
using Meta.BattleSystem.Configuration;
using UnityEngine;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Materials of the views, shared: one flat placeholder material per colour, and one tinted copy of a model's material per
    /// colour for its `paint` parts — the white parts of the 3D artist that the game colours by side (Docs/ART_REQUESTS.md).
    /// </summary>
    public sealed class ViewMaterials : IDisposable
    {
        public const string PaintPart = "paint";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly BattleViewConfig _view;
        private readonly Dictionary<Color, Material> _byColor = new Dictionary<Color, Material>();
        private readonly Dictionary<(Material, Color), Material> _tinted = new Dictionary<(Material, Color), Material>();
        private readonly Dictionary<Material, Material> _sourceOf = new Dictionary<Material, Material>();

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

        /// <summary>Gives every renderer under <paramref name="root"/> the flat material of a colour: placeholders without texture.</summary>
        public void Paint(Transform root, Color color)
        {
            var material = Flat(color);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = material;
        }

        /// <summary>Colours the `paint` parts under <paramref name="root"/>; the other parts keep the texture of the model.</summary>
        public void Tint(Transform root, Color color)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name == PaintPart)
                    renderer.sharedMaterial = Tinted(renderer.sharedMaterial, color);
            }
        }

        public void Dispose()
        {
            foreach (var material in _byColor.Values)
                UnityEngine.Object.Destroy(material);
            foreach (var material in _tinted.Values)
                UnityEngine.Object.Destroy(material);
            _byColor.Clear();
            _tinted.Clear();
            _sourceOf.Clear();
        }

        private Material Tinted(Material current, Color color)
        {
            if (current == null)
                return Flat(color);

            // a renderer tinted before holds a copy: tint its source again, not the copy
            var source = _sourceOf.TryGetValue(current, out var original) ? original : current;
            if (_tinted.TryGetValue((source, color), out var tinted))
                return tinted;

            tinted = new Material(source);
            tinted.SetColor(BaseColorId, color);
            _tinted[(source, color)] = tinted;
            _sourceOf[tinted] = source;
            return tinted;
        }
    }
}
