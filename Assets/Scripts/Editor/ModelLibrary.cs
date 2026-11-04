using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Core.EditorTools
{
    /// <summary>
    /// The models of the 3D artist (voxel-forge, export/borderbite/models; Docs/ART_REQUESTS.md) as prefabs of the game: the
    /// .glb stays as it came, glTFast imports it; a prefab takes its meshes, parts and markers by their names and gives every
    /// part a material of the shader Borderbite/World/Flat Lit with the texture of that part. The views tint the parts named
    /// `paint` with the colour of a side. Tools/Setup/Build Models; Build Everything runs it before the scene.
    /// </summary>
    public static class ModelLibrary
    {
        public const string PrefabFolder = "Assets/Prefabs/World/";

        private const string MaterialFolder = "Assets/Materials/Models/";
        private const string ShaderName = "Borderbite/World/Flat Lit";
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        /// <summary>Every model of the game: id → the .glb under Assets/Models.</summary>
        public static readonly Dictionary<string, string> Models = new Dictionary<string, string>
        {
            { "ground_cell", "Assets/Models/World/ground_cell.glb" },
            { "shore_straight", "Assets/Models/World/shore_straight.glb" },
            { "shore_straight_b", "Assets/Models/World/shore_straight_b.glb" },
            { "shore_straight_c", "Assets/Models/World/shore_straight_c.glb" },
            { "shore_corner", "Assets/Models/World/shore_corner.glb" },
            { "rock_small", "Assets/Models/World/rock_small.glb" },
            { "rock_large", "Assets/Models/World/rock_large.glb" },
            { "rock_tall", "Assets/Models/World/rock_tall.glb" },
            { "hero", "Assets/Models/Units/hero.glb" },
            { "soldier", "Assets/Models/Units/soldier.glb" },
            { "barracks", "Assets/Models/Buildings/barracks.glb" },
            { "build_spot", "Assets/Models/Buildings/build_spot.glb" },
            { "base", "Assets/Models/Buildings/base.glb" },
            { "gold_pile", "Assets/Models/Pickups/gold_pile.glb" },
            { "wood_bundle", "Assets/Models/Pickups/wood_bundle.glb" },
        };

        /// <summary>The prefab of a model, or null when it was not built (then the scene keeps its placeholder).</summary>
        public static GameObject Prefab(string id) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + id + ".prefab");

        [MenuItem("Tools/Setup/Build Models")]
        public static string Build()
        {
            var report = new StringBuilder();
            var shader = Shader.Find(ShaderName);
            Folder(PrefabFolder);
            Folder(MaterialFolder);
            foreach (var pair in Models)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
                if (source == null)
                {
                    report.Append($"{pair.Key}: no model at {pair.Value}; ");
                    continue;
                }

                // an empty root keeps the offset and scale of the model's own node when a view places the prefab
                var copy = new GameObject(pair.Key);
                var model = Object.Instantiate(source, copy.transform, false);
                model.name = source.name;
                var triangles = 0;
                foreach (var renderer in copy.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterial = MaterialOf(pair.Key, renderer, shader);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    triangles += mesh == null ? 0 : (int)(mesh.GetIndexCount(0) / 3);
                }

                PrefabUtility.SaveAsPrefabAsset(copy, PrefabFolder + pair.Key + ".prefab");
                Object.DestroyImmediate(copy);
                report.Append($"{pair.Key}: {triangles} tris; ");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ModelLibrary] " + report);
            return report.ToString();
        }

        /// <summary>The names of the nodes of a model with their parents, local positions and scales, for the check of a delivery.</summary>
        public static string Describe(string id)
        {
            var prefab = Prefab(id);
            if (prefab == null)
                return "no prefab " + id;

            var text = new StringBuilder();
            foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
            {
                var renderer = node.GetComponent<MeshRenderer>();
                var bounds = renderer == null ? "" : $" bounds {renderer.bounds.size}";
                text.Append($"{node.name}<{(node.parent == null ? "-" : node.parent.name)}> {node.localPosition} s{node.lossyScale}{bounds}; ");
            }

            return text.ToString();
        }

        private static Material MaterialOf(string id, MeshRenderer renderer, Shader shader)
        {
            var path = MaterialFolder + id + "_" + renderer.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture(BaseMapId, TextureOf(renderer.sharedMaterial));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The base colour texture of a material of glTFast, whatever its shader calls it.</summary>
        private static Texture TextureOf(Material material)
        {
            if (material == null)
                return null;

            foreach (var name in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(name);
                if (texture != null && name.ToLowerInvariant().Contains("base"))
                    return texture;
            }

            return material.mainTexture;
        }

        private static void Folder(string folder)
        {
            folder = folder.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            Folder(Path.GetDirectoryName(folder)?.Replace('\\', '/') + "/");
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
