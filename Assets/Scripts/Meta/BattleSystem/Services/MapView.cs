using System;
using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Utils;
using Meta.BattleSystem.Views;
using UnityEngine;
using VContainer.Unity;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// Draws the cells: grass with decor strewn by the seed of the level, and a texel per cell over it on one quad (the colour of
    /// the side and how much it is painted), the island under it, its shore and rocks and the water around. A cell that changed
    /// side turns to its colour with a short flash; the cells of an enclosed area wait for a wave that runs from where the ring
    /// closed. The top of the cells is the plane y = 0.
    /// A map with water (layer 5): the surface is cut away over it, the island is a cliff along every shore of the cells, the
    /// waving sea comes into the lakes and bays, its depth and foam go by the distance to the land, and the shore of the 3D
    /// artist stands where the coast runs straight along whole pieces; the square island of stage 1 is drawn as before.
    /// </summary>
    public sealed class MapView : ILateTickable, IDisposable
    {
        private static readonly int CellTexId = Shader.PropertyToID("_CellTex");
        private static readonly int CellsId = Shader.PropertyToID("_Cells");
        private static readonly int FieldTexId = Shader.PropertyToID("_FieldTex");
        private static readonly int FieldRangeId = Shader.PropertyToID("_FieldRange");
        private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
        private static readonly int PlayerColorId = Shader.PropertyToID("_PlayerColor");
        private static readonly int EnemyColorId = Shader.PropertyToID("_EnemyColor");
        // the borders are rebuilt at most this often, seconds (README of the 3D artist: 5–10 times a second)
        private const float FieldEvery = 0.1f;
        private static readonly int GridStrengthId = Shader.PropertyToID("_GridStrength");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int GrassTexId = Shader.PropertyToID("_GrassTex");
        private static readonly int GrassTilesId = Shader.PropertyToID("_GrassTiles");
        private static readonly int LightenId = Shader.PropertyToID("_Lighten");
        private static readonly int PaintAlphaId = Shader.PropertyToID("_PaintAlpha");
        private static readonly int LumMeanId = Shader.PropertyToID("_LumMean");
        private static readonly int MapSizeId = Shader.PropertyToID("_MapSize");
        private static readonly int RocksId = Shader.PropertyToID("_Rocks");
        private static readonly int RockCountId = Shader.PropertyToID("_RockCount");
        private static readonly int RockRadiiId = Shader.PropertyToID("_RockRadii");
        private const int MaxRocks = 16;
        private static readonly int WavesId = Shader.PropertyToID("_Waves");
        // the waving sea reaches this far off the island, meters; past it the water is flat (README of the 3D artist)
        private const float SeaReach = 15f;
        private const float SeaStep = 1f;
        private const float SeaJitter = 0.3f;
        private static readonly int LandDistId = Shader.PropertyToID("_LandDist");
        private static readonly int LandDistRectId = Shader.PropertyToID("_LandDistRect");
        private static readonly int LandDistOnId = Shader.PropertyToID("_LandDistOn");
        private static readonly int LandDistRangeId = Shader.PropertyToID("_LandDistRange");
        // the distance to the land is kept up to this many meters, at this many texels a meter: past it the sea is deep anyway
        private const float LandDistRange = 4f;
        private const int LandDistPerMeter = 4;
        // a straight piece of the shore of the 3D artist is 2 m: 4 cells
        private const int ShoreCells = 4;

        private readonly BattleViewConfig _view;
        private readonly BattleConfig _rules;
        private readonly IBattleDriver _battle;
        private readonly BattleDebug _debug;
        private readonly BattleWorld _world;
        private readonly List<int> _active = new List<int>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Transform> _shore = new List<Transform>();

        private BattleSimulation _simulation;
        private Texture2D _texture;
        private Texture2D _grass;
        private MeshRenderer _nearSea;
        private Mesh _seaGrid;
        private Mesh _farSea;
        private uint _grassSeed;
        private Material _cells;
        private ulong _mapKey;
        private Mesh _cube;
        private Mesh _cliffs;
        private Texture2D _landDist;
        // 255 on land, 0 on water: the red of a texel of the cells, the surface is cut where it is 0
        private byte[] _land;
        private Color32[] _pixels;
        private Side[] _shown;
        // the owner each cell shows now: it changes when the flash of the cell starts, so the borders follow the wave
        private Side[] _visible;
        private TerritoryField _field;
        private bool _fieldDirty;
        private float _fieldAt;
        private Color[] _from;
        private float[] _start;
        private float[] _delay;
        private bool[] _animating;
        private int _version;
        private int _captures;

        public MapView(BattleViewConfig view, BattleConfig rules, IBattleDriver battle, BattleDebug debug, BattleWorld world)
        {
            _view = view;
            _rules = rules;
            _battle = battle;
            _debug = debug;
            _world = world;
            _battle.OnStarted += Take;
        }

        public void LateTick()
        {
            if (_simulation == null || _battle.Current?.Simulation != _simulation)
                return;

            var now = Time.time;
            QueueWave(now);
            if (_simulation.Grid.Version != _version)
                StartChanged(now);

            if (_active.Count > 0)
                Animate(now);

            if (_fieldDirty && now >= _fieldAt)
            {
                _field.Build(_visible);
                _fieldDirty = false;
                _fieldAt = now + FieldEvery;
            }

            _cells.SetFloat(GridStrengthId, _debug.ShowDebug ? 0.6f : 0f);
        }

        public void Dispose()
        {
            _battle.OnStarted -= Take;
            foreach (var material in _materials)
                UnityEngine.Object.Destroy(material);
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
            if (_field != null)
                UnityEngine.Object.Destroy(_field.Texture);
            if (_grass != null)
                UnityEngine.Object.Destroy(_grass);
            if (_seaGrid != null)
                UnityEngine.Object.Destroy(_seaGrid);
            if (_farSea != null)
                UnityEngine.Object.Destroy(_farSea);
            if (_cliffs != null)
                UnityEngine.Object.Destroy(_cliffs);
            if (_landDist != null)
                UnityEngine.Object.Destroy(_landDist);
        }

        private void Take(BattleRunner runner)
        {
            _simulation = runner.Simulation;
            var width = _simulation.Grid.Width;
            var height = _simulation.Grid.Height;
            var key = KeyOf(_simulation.Settings.Map);
            if (_texture == null || _texture.width != width || _texture.height != height || key != _mapKey)
                Build(width, height, _simulation.Settings.Map, key);
            Grass(_simulation.Settings.Seed);

            for (var i = 0; i < _shown.Length; i++)
            {
                _shown[i] = _simulation.Grid[i];
                _visible[i] = _shown[i];
                _pixels[i] = Pixel(i, 0);
                _delay[i] = -1f;
                _animating[i] = false;
            }

            _active.Clear();
            _version = _simulation.Grid.Version;
            _captures = _simulation.Captures.Count;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
            _field.Build(_visible);
            _fieldDirty = false;
        }

        private Color32 Pixel(int cell, byte flash) => new Color32(_land[cell], 255, 255, flash);

        /// <summary>What the drawing of a map depends on beyond its size: its water and rocks. The same map again is not rebuilt.</summary>
        private static ulong KeyOf(BattleMap map)
        {
            var hash = 14695981039346656037UL;
            void Mix(int value) => hash = (hash ^ (uint)value) * 1099511628211UL;
            if (map?.Water != null)
            {
                for (var i = 0; i < map.Water.Length; i++)
                {
                    if (map.Water[i])
                        Mix(i);
                }
            }

            if (map != null)
            {
                foreach (var rock in map.Rocks)
                {
                    Mix(rock.X);
                    Mix(rock.Y * 7 + rock.Size);
                    Mix(rock.Yaw);
                }
            }

            return hash;
        }

        private void Build(int width, int height, BattleMap map, ulong key)
        {
            _mapKey = key;
            var cells = width * height;
            _land = new byte[cells];
            for (var i = 0; i < cells; i++)
                _land[i] = map != null && map.Water != null && map.Water[i] ? (byte)0 : (byte)255;
            _pixels = new Color32[cells];
            _shown = new Side[cells];
            _visible = new Side[cells];
            _from = new Color[cells];
            _start = new float[cells];
            _delay = new float[cells];
            _animating = new bool[cells];
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Cells",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var mapW = width * _rules.CellSize;
            var mapH = height * _rules.CellSize;
            _cells = Copy(_view.CellsMaterial);
            _cells.SetTexture(CellTexId, _texture);
            _cells.SetVector(CellsId, new Vector4(width, height, 0f, 0f));
            if (_field != null)
                UnityEngine.Object.Destroy(_field.Texture);
            _field = new TerritoryField(width, height);
            _cells.SetTexture(FieldTexId, _field.Texture);
            _cells.SetFloat(FieldRangeId, TerritoryField.Range);
            _cells.SetFloat(CellSizeId, _rules.CellSize);
            _cells.SetColor(PlayerColorId, _view.PlayerColor);
            _cells.SetColor(EnemyColorId, _view.EnemyColor);
            _cells.SetFloat(Shader.PropertyToID("_FlashStrength"), _view.FlashStrength);
            _world.Surface.sharedMaterial = _cells;
            _world.Surface.transform.localPosition = new Vector3(mapW / 2f, 0f, mapH / 2f);
            _world.Surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _world.Surface.transform.localScale = new Vector3(mapW, mapH, 1f);

            var depth = _view.IslandDepth;
            _world.Island.sharedMaterial = Flat(_view.IslandColor);
            var islandMesh = _world.Island.GetComponent<MeshFilter>();
            _cube ??= islandMesh.sharedMesh;
            if (_cliffs != null)
                UnityEngine.Object.Destroy(_cliffs);
            _cliffs = null;
            var wet = map != null && map.Water != null;
            if (wet)
            {
                // the island of a map with water: a cliff along every shore of the cells
                islandMesh.sharedMesh = _cliffs = Cliffs(map, _rules.CellSize, depth);
                _world.Island.transform.localPosition = Vector3.zero;
                _world.Island.transform.localScale = Vector3.one;
            }
            else
            {
                islandMesh.sharedMesh = _cube;
                _world.Island.transform.localPosition = new Vector3(mapW / 2f, -depth / 2f - 0.001f, mapH / 2f);
                _world.Island.transform.localScale = new Vector3(mapW, depth, mapH);
            }

            LandDistance(wet ? map : null, mapW, mapH);
            BuildSea(mapW, mapH, wet ? map : null);
            if (wet)
                BuildCoast(map, mapW, mapH);
            else
                BuildShore(mapW, mapH);
            PlaceMapRocks(map);
        }

        /// <summary>The walls of the island of a map with water: a quad from the grass down to the bottom on every side of a cell of land that looks at water or off the map.</summary>
        private static Mesh Cliffs(BattleMap map, float cell, float depth)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var up = Vector3.up;
            void Wall(Vector3 middle, Vector3 normal)
            {
                var right = Vector3.Cross(normal, up) * (cell / 2f);
                var down = up * depth;
                var s = vertices.Count;
                vertices.Add(middle - right - down);
                vertices.Add(middle - right);
                vertices.Add(middle + right);
                vertices.Add(middle + right - down);
                triangles.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            bool Wet(int x, int y) => map.IsWaterOrOff(x, y);
            for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
            {
                if (Wet(x, y))
                    continue;

                var cx = (x + 0.5f) * cell;
                var cz = (y + 0.5f) * cell;
                if (Wet(x, y - 1))
                    Wall(new Vector3(cx, 0f, y * cell), Vector3.back);
                if (Wet(x, y + 1))
                    Wall(new Vector3(cx, 0f, (y + 1) * cell), Vector3.forward);
                if (Wet(x - 1, y))
                    Wall(new Vector3(x * cell, 0f, cz), Vector3.left);
                if (Wet(x + 1, y))
                    Wall(new Vector3((x + 1) * cell, 0f, cz), Vector3.right);
            }

            var mesh = new Mesh { name = "Cliffs", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The distance from the sea to the land for the water (global, so the waving sea and the flat ring read it): texels over
        /// the map and the waving sea round it, meters up to <see cref="LandDistRange"/>. Off for the square island: the water then
        /// measures from the square itself, as before.
        /// </summary>
        private void LandDistance(BattleMap map, float mapW, float mapH)
        {
            Shader.SetGlobalFloat(LandDistOnId, map != null ? 1f : 0f);
            if (map == null)
                return;

            var cell = _rules.CellSize;
            var texelsX = Mathf.CeilToInt((mapW + SeaReach * 2f) * LandDistPerMeter);
            var texelsZ = Mathf.CeilToInt((mapH + SeaReach * 2f) * LandDistPerMeter);
            var reach = Mathf.CeilToInt(LandDistRange / cell) + 1;
            var bytes = new byte[texelsX * texelsZ];
            for (var j = 0; j < texelsZ; j++)
            for (var i = 0; i < texelsX; i++)
            {
                var px = -SeaReach + (i + 0.5f) / LandDistPerMeter;
                var pz = -SeaReach + (j + 0.5f) / LandDistPerMeter;
                var cx = Mathf.FloorToInt(px / cell);
                var cz = Mathf.FloorToInt(pz / cell);
                var best = LandDistRange;
                for (var y = Mathf.Max(0, cz - reach); y <= Mathf.Min(map.Height - 1, cz + reach); y++)
                for (var x = Mathf.Max(0, cx - reach); x <= Mathf.Min(map.Width - 1, cx + reach); x++)
                {
                    if (map.IsWater(x, y))
                        continue;

                    var dx = Mathf.Max(0f, Mathf.Max(x * cell - px, px - (x + 1) * cell));
                    var dz = Mathf.Max(0f, Mathf.Max(y * cell - pz, pz - (y + 1) * cell));
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
                }

                bytes[j * texelsX + i] = (byte)Mathf.RoundToInt(best / LandDistRange * 255f);
            }

            if (_landDist == null || _landDist.width != texelsX || _landDist.height != texelsZ)
            {
                if (_landDist != null)
                    UnityEngine.Object.Destroy(_landDist);
                _landDist = new Texture2D(texelsX, texelsZ, TextureFormat.R8, false, true)
                {
                    name = "LandDistance",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
            }

            _landDist.SetPixelData(bytes, 0);
            _landDist.Apply(false);
            Shader.SetGlobalTexture(LandDistId, _landDist);
            Shader.SetGlobalVector(LandDistRectId, new Vector4(-SeaReach, -SeaReach, texelsX / (float)LandDistPerMeter, texelsZ / (float)LandDistPerMeter));
            Shader.SetGlobalFloat(LandDistRangeId, LandDistRange);
        }

        /// <summary>
        /// The sea (task 4.10): near the island a mesh of uneven triangles a meter apart, its vertices moved by a constant hash
        /// up to 0.3 m and the diagonals of the squares alternating, which the shader raises into waves; around it, to the
        /// horizon, a flat ring of the same water without waves. The ring leaves a hole for the mesh: translucent water must not
        /// lie twice. The outer vertices of the mesh do not move, so it meets the ring without a gap.
        /// </summary>
        private void BuildSea(float mapW, float mapH, BattleMap map)
        {
            var source = _view.WaterMaterial;
            var water = _world.Water;
            water.transform.localPosition = new Vector3(0f, -_view.WaterDepth, 0f);
            water.transform.localRotation = Quaternion.identity;
            water.transform.localScale = Vector3.one;
            var far = source != null ? Copy(source) : Flat(_view.WaterColor);
            far.SetVector(MapSizeId, new Vector4(mapW, mapH, 0f, 0f));
            far.SetFloat(WavesId, 0f);
            water.sharedMaterial = far;
            if (_farSea != null)
                UnityEngine.Object.Destroy(_farSea);
            water.GetComponent<MeshFilter>().sharedMesh = _farSea = SeaRing(mapW, mapH, SeaReach, Mathf.Max(mapW, mapH) * 10f);

            if (_nearSea == null)
            {
                var near = new GameObject("NearSea", typeof(MeshFilter), typeof(MeshRenderer));
                near.transform.SetParent(water.transform.parent, false);
                _nearSea = near.GetComponent<MeshRenderer>();
                _nearSea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _nearSea.receiveShadows = false;
            }

            _nearSea.transform.localPosition = water.transform.localPosition;
            var waves = source != null ? Copy(source) : Flat(_view.WaterColor);
            waves.SetVector(MapSizeId, new Vector4(mapW, mapH, 0f, 0f));
            waves.SetFloat(WavesId, 1f);
            _nearSea.sharedMaterial = waves;
            if (_seaGrid != null)
                UnityEngine.Object.Destroy(_seaGrid);
            _nearSea.GetComponent<MeshFilter>().sharedMesh = _seaGrid = SeaGrid(mapW, mapH, SeaReach, SeaStep, SeaJitter, map, _rules.CellSize);
        }

        /// <summary>
        /// The squares of the grid from <paramref name="reach"/> off the island inwards, leaving out those under it: under the
        /// square island, or, on a map with water, those with no water within half a meter.
        /// </summary>
        private static Mesh SeaGrid(float mapW, float mapH, float reach, float step, float jitter, BattleMap map, float cell)
        {
            var cellsX = Mathf.RoundToInt((mapW + reach * 2f) / step);
            var cellsZ = Mathf.RoundToInt((mapH + reach * 2f) / step);
            var origin = -reach;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var index = new int[(cellsX + 1) * (cellsZ + 1)];
            for (var j = 0; j <= cellsZ; j++)
            for (var i = 0; i <= cellsX; i++)
            {
                var edge = i == 0 || j == 0 || i == cellsX || j == cellsZ;
                var x = origin + i * step + (edge ? 0f : (Hash(i, j, 71) - 0.5f) * 2f * jitter);
                var z = origin + j * step + (edge ? 0f : (Hash(i, j, 72) - 0.5f) * 2f * jitter);
                index[j * (cellsX + 1) + i] = vertices.Count;
                vertices.Add(new Vector3(x, 0f, z));
            }

            for (var j = 0; j < cellsZ; j++)
            for (var i = 0; i < cellsX; i++)
            {
                // a square wholly under the island is never seen
                var cx = origin + (i + 0.5f) * step;
                var cz = origin + (j + 0.5f) * step;
                if (map == null ? cx > step && cx < mapW - step && cz > step && cz < mapH - step : AllLand(map, cell, cx, cz, step))
                    continue;

                var a = index[j * (cellsX + 1) + i];
                var b = index[j * (cellsX + 1) + i + 1];
                var c = index[(j + 1) * (cellsX + 1) + i + 1];
                var d = index[(j + 1) * (cellsX + 1) + i];
                if ((i + j) % 2 == 0)
                    triangles.AddRange(new[] { a, d, c, a, c, b });
                else
                    triangles.AddRange(new[] { a, d, b, d, c, b });
            }

            var mesh = new Mesh { name = "SeaGrid" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            // the waves rise up to a few tenths of a meter: bounds a little taller than the flat grid
            mesh.bounds = new Bounds(new Vector3(mapW / 2f, 0f, mapH / 2f), new Vector3(mapW + reach * 2f, 1f, mapH + reach * 2f));
            return mesh;
        }

        /// <summary>No water under a square of the sea grid and half a meter round it (the vertices move up to 0.3 m).</summary>
        private static bool AllLand(BattleMap map, float cell, float cx, float cz, float step)
        {
            var half = step / 2f + 0.5f;
            var x0 = Mathf.FloorToInt((cx - half) / cell);
            var x1 = Mathf.FloorToInt((cx + half) / cell);
            var z0 = Mathf.FloorToInt((cz - half) / cell);
            var z1 = Mathf.FloorToInt((cz + half) / cell);
            for (var y = z0; y <= z1; y++)
            for (var x = x0; x <= x1; x++)
            {
                if (map.IsWaterOrOff(x, y))
                    return false;
            }

            return true;
        }

        /// <summary>Four flat strips round the grid out to the horizon.</summary>
        private static Mesh SeaRing(float mapW, float mapH, float reach, float horizon)
        {
            var lo = -reach;
            var hiX = mapW + reach;
            var hiZ = mapH + reach;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Strip(float x0, float z0, float x1, float z1)
            {
                var s = vertices.Count;
                vertices.Add(new Vector3(x0, 0f, z0));
                vertices.Add(new Vector3(x0, 0f, z1));
                vertices.Add(new Vector3(x1, 0f, z1));
                vertices.Add(new Vector3(x1, 0f, z0));
                triangles.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }

            Strip(-horizon, -horizon, lo, mapH + horizon);
            Strip(hiX, -horizon, mapW + horizon, mapH + horizon);
            Strip(lo, -horizon, hiX, lo);
            Strip(lo, hiZ, hiX, mapH + horizon);
            var mesh = new Mesh { name = "SeaRing" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A constant hash of a vertex of the grid, 0..1: the same sea every time.</summary>
        private static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                var h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 982451653u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296f;
            }
        }

        /// <summary>
        /// The shore of the 3D artist around the island: 2 m pieces along each edge, a corner piece at each corner, all facing
        /// out to the water (their +z), and a few rocks in the water, bottoms 0.5 m under it. After import the corner piece
        /// covers -x, +z of its origin.
        /// </summary>
        private void BuildShore(float mapW, float mapH)
        {
            foreach (var piece in _shore)
                UnityEngine.Object.Destroy(piece.gameObject);
            _shore.Clear();
            if (_world.ShoreTemplate == null)
                return;

            const float length = 2f;
            var last = new int[4];
            for (var k = 0; k < 4; k++)
                last[k] = -1;
            var step = 0;
            for (var along = length / 2f; along < Mathf.Max(mapW, mapH); along += length, step++)
            {
                if (along < mapW)
                {
                    Place(Shore(0, step, last), new Vector3(along, 0f, 0f), 180f);
                    Place(Shore(1, step, last), new Vector3(along, 0f, mapH), 0f);
                }

                if (along < mapH)
                {
                    Place(Shore(2, step, last), new Vector3(0f, 0f, along), -90f);
                    Place(Shore(3, step, last), new Vector3(mapW, 0f, along), 90f);
                }
            }

            if (_world.ShoreCornerTemplate != null)
            {
                Place(_world.ShoreCornerTemplate, new Vector3(0f, 0f, mapH), 0f);
                Place(_world.ShoreCornerTemplate, new Vector3(mapW, 0f, mapH), 90f);
                Place(_world.ShoreCornerTemplate, new Vector3(mapW, 0f, 0f), 180f);
                Place(_world.ShoreCornerTemplate, new Vector3(0f, 0f, 0f), -90f);
            }

            SeaRocks(mapW, mapH);
        }

        /// <summary>A few rocks in the sea off the square, bottoms 0.5 m under the water, with foam round them.</summary>
        private void SeaRocks(float mapW, float mapH)
        {
            var rocks = _world.RockTemplates;
            if (rocks == null || rocks.Length == 0)
                return;

            // the same rocks every time, a few meters off the shore on each side
            var spots = new[]
            {
                new Vector2(-3f, 6f), new Vector2(-2.6f, 21f), new Vector2(9f, -3.2f), new Vector2(24f, -2.6f),
                new Vector2(mapW + 3f, 26f), new Vector2(mapW + 2.6f, 11f), new Vector2(23f, mapH + 3.2f),
                new Vector2(8f, mapH + 2.6f), new Vector2(-3.4f, mapH + 3.4f), new Vector2(mapW + 3.4f, -3.4f),
            };
            var bottom = -_view.WaterDepth - 0.5f;
            var water = _world.Water.sharedMaterial;
            var radii = water.HasProperty(RockRadiiId) ? water.GetVector(RockRadiiId) : Vector4.zero;
            // global: the waving sea and the flat ring read the same rocks
            var circles = new Vector4[MaxRocks];
            var count = Mathf.Min(spots.Length, MaxRocks);
            for (var i = 0; i < spots.Length; i++)
            {
                var template = rocks[i % rocks.Length];
                var scale = 0.8f + 0.1f * (i % 4);
                var rock = Place(template, new Vector3(spots[i].x, bottom, spots[i].y), i * 47f);
                rock.localScale = Vector3.one * scale;
                if (i < MaxRocks)
                    circles[i] = new Vector4(spots[i].x, spots[i].y, RockRadius(template.name, radii) * scale, 0f);
            }

            // the sea foams around the rocks: circles at the water line, radii of the 3D artist (water.json)
            Shader.SetGlobalVectorArray(RocksId, circles);
            Shader.SetGlobalFloat(RockCountId, count);
        }

        private static float RockRadius(string name, Vector4 radii)
        {
            if (name.Contains("large"))
                return radii.y;
            if (name.Contains("tall"))
                return radii.z;
            return radii.x;
        }

        /// <summary>
        /// The shore of the 3D artist on a map with water: a straight piece wherever the coast runs straight along a whole 2 m
        /// block of 4 × 4 cells of land with 4 cells of water before it, a corner piece where two such sides of a block meet
        /// round the outside; the rest of the coast is the cliff of the cells. The rocks in the sea off the square stay.
        /// </summary>
        private void BuildCoast(BattleMap map, float mapW, float mapH)
        {
            foreach (var piece in _shore)
                UnityEngine.Object.Destroy(piece.gameObject);
            _shore.Clear();
            if (_world.ShoreTemplate != null)
            {
                var cell = _rules.CellSize;
                var length = ShoreCells * cell;
                for (var by = 0; by < map.Height / ShoreCells; by++)
                for (var bx = 0; bx < map.Width / ShoreCells; bx++)
                {
                    if (!Solid(map, bx, by))
                        continue;

                    var x0 = bx * ShoreCells;
                    var y0 = by * ShoreCells;
                    var south = Open(map, x0, y0 - 1, 1, 0);
                    var north = Open(map, x0, y0 + ShoreCells, 1, 0);
                    var west = Open(map, x0 - 1, y0, 0, 1);
                    var east = Open(map, x0 + ShoreCells, y0, 0, 1);
                    var along = new Vector2((bx + 0.5f) * length, (by + 0.5f) * length);
                    if (south)
                        Place(Variant(0, bx, by), new Vector3(along.x, 0f, by * length), 180f);
                    if (north)
                        Place(Variant(1, bx, by), new Vector3(along.x, 0f, (by + 1) * length), 0f);
                    if (west)
                        Place(Variant(2, bx, by), new Vector3(bx * length, 0f, along.y), -90f);
                    if (east)
                        Place(Variant(3, bx, by), new Vector3((bx + 1) * length, 0f, along.y), 90f);

                    var corner = _world.ShoreCornerTemplate;
                    if (corner == null)
                        continue;

                    if (north && west && map.IsWaterOrOff(x0 - 1, y0 + ShoreCells))
                        Place(corner, new Vector3(bx * length, 0f, (by + 1) * length), 0f);
                    if (north && east && map.IsWaterOrOff(x0 + ShoreCells, y0 + ShoreCells))
                        Place(corner, new Vector3((bx + 1) * length, 0f, (by + 1) * length), 90f);
                    if (south && east && map.IsWaterOrOff(x0 + ShoreCells, y0 - 1))
                        Place(corner, new Vector3((bx + 1) * length, 0f, by * length), 180f);
                    if (south && west && map.IsWaterOrOff(x0 - 1, y0 - 1))
                        Place(corner, new Vector3(bx * length, 0f, by * length), -90f);
                }
            }

            SeaRocks(mapW, mapH);
        }

        /// <summary>A block of 4 × 4 cells all land.</summary>
        private static bool Solid(BattleMap map, int bx, int by)
        {
            for (var y = by * ShoreCells; y < (by + 1) * ShoreCells; y++)
            for (var x = bx * ShoreCells; x < (bx + 1) * ShoreCells; x++)
            {
                if (map.IsWater(x, y))
                    return false;
            }

            return true;
        }

        /// <summary>4 cells in a row from (x, y) by (dx, dy) all water or off the map.</summary>
        private static bool Open(BattleMap map, int x, int y, int dx, int dy)
        {
            for (var k = 0; k < ShoreCells; k++)
            {
                if (!map.IsWaterOrOff(x + dx * k, y + dy * k))
                    return false;
            }

            return true;
        }

        /// <summary>A straight piece for a side of a block: a variant by a hash of the place, the same coast every time.</summary>
        private Transform Variant(int edge, int bx, int by)
        {
            var variants = _world.ShoreVariants;
            if (variants == null || variants.Length < 2)
                return _world.ShoreTemplate;

            var hash = (uint)(edge * 73856093) ^ (uint)(bx * 19349663) ^ (uint)(by * 83492791);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return variants[(int)((hash ^ (hash >> 16)) % (uint)variants.Length)];
        }

        /// <summary>
        /// The rocks of the map (Т-Р3, decor): small, medium and large by the models of the 3D artist, on the grass or, in the
        /// water, their bottoms under it with foam round them as the rocks of the sea.
        /// </summary>
        private void PlaceMapRocks(BattleMap map)
        {
            var rocks = _world.RockTemplates;
            if (map == null || map.Rocks.Count == 0 || rocks == null || rocks.Length == 0)
                return;

            var cell = _rules.CellSize;
            var water = _world.Water.sharedMaterial;
            var radii = water.HasProperty(RockRadiiId) ? water.GetVector(RockRadiiId) : Vector4.zero;
            var circles = new List<Vector4>(Shader.GetGlobalVectorArray(RocksId) ?? Array.Empty<Vector4>());
            var count = Mathf.RoundToInt(Shader.GetGlobalFloat(RockCountId));
            foreach (var spot in map.Rocks)
            {
                var template = RockOf(rocks, spot.Size, out var scale);
                var wet = map.IsWater(spot.X, spot.Y);
                var position = new Vector3((spot.X + 0.5f) * cell, wet ? -_view.WaterDepth - 0.5f : -0.05f, (spot.Y + 0.5f) * cell);
                var rock = Place(template, position, spot.Yaw);
                rock.localScale = Vector3.one * scale;
                if (!wet || count >= MaxRocks)
                    continue;

                while (circles.Count < MaxRocks)
                    circles.Add(Vector4.zero);
                circles[count++] = new Vector4(position.x, position.z, RockRadius(template.name, radii) * scale, 0f);
            }

            if (circles.Count < MaxRocks)
                return;

            Shader.SetGlobalVectorArray(RocksId, circles);
            Shader.SetGlobalFloat(RockCountId, count);
        }

        /// <summary>Small — the small rock, medium — the tall one, large — the large one, by the names of the 3D artist.</summary>
        private static Transform RockOf(Transform[] rocks, int size, out float scale)
        {
            scale = size == 0 ? 0.8f : 1f;
            var name = size == 0 ? "small" : size == 1 ? "tall" : "large";
            foreach (var rock in rocks)
            {
                if (rock.name.Contains(name))
                    return rock;
            }

            return rocks[Mathf.Clamp(size, 0, rocks.Length - 1)];
        }

        /// <summary>
        /// A piece of straight shore for a place along an edge: a variant by a hash of the place (the same island every time),
        /// never the one just placed on that edge.
        /// </summary>
        private Transform Shore(int edge, int step, int[] last)
        {
            var variants = _world.ShoreVariants;
            if (variants == null || variants.Length < 2)
                return _world.ShoreTemplate;

            var hash = (uint)(edge * 73856093) ^ (uint)(step * 19349663);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            var pick = (int)((hash ^ (hash >> 16)) % (uint)variants.Length);
            if (pick == last[edge])
                pick = (pick + 1) % variants.Length;
            last[edge] = pick;
            return variants[pick];
        }

        private Transform Place(Transform template, Vector3 position, float yaw)
        {
            var piece = _world.Copy(template);
            piece.localPosition = position;
            piece.localRotation = Quaternion.Euler(0f, yaw, 0f);
            _shore.Add(piece);
            return piece;
        }

        /// <summary>The cells of new enclosures wait for the wave: the farther from the closing of the ring, the later.</summary>
        private void QueueWave(float now)
        {
            var captures = _simulation.Captures;
            for (; _captures < captures.Count; _captures++)
            {
                var capture = captures[_captures];
                var size = _simulation.Grid.Width;
                var originX = capture.OriginX / (float)BattleSettings.UnitsPerCell;
                var originY = capture.OriginY / (float)BattleSettings.UnitsPerCell;
                var farthest = 0.001f;
                foreach (var cell in capture.Cells)
                    farthest = Mathf.Max(farthest, Distance(cell, size, originX, originY));

                foreach (var cell in capture.Cells)
                    _delay[cell] = Distance(cell, size, originX, originY) / farthest * _view.WaveSeconds;
            }
        }

        private void StartChanged(float now)
        {
            var grid = _simulation.Grid;
            for (var i = 0; i < _shown.Length; i++)
            {
                if (grid[i] == _shown[i])
                    continue;

                if (!_animating[i])
                    _active.Add(i);

                _animating[i] = true;

                _from[i] = _pixels[i];
                _shown[i] = grid[i];
                _start[i] = now + Mathf.Max(0f, _delay[i]);
                _delay[i] = -1f;
            }

            _version = grid.Version;
        }

        private void Animate(float now)
        {
            var flash = _view.FlashSeconds;
            for (var a = _active.Count - 1; a >= 0; a--)
            {
                var i = _active[a];
                var t = (now - _start[i]) / flash;
                if (t < 0f)
                    continue;

                // the cell shows its new owner from the start of its flash: the borders move with the wave
                if (_visible[i] != _shown[i])
                {
                    _visible[i] = _shown[i];
                    _fieldDirty = true;
                }

                if (t >= 1f)
                {
                    _pixels[i] = Pixel(i, 0);
                    _animating[i] = false;
                    // the last one was already seen in this backward pass: it takes the free place
                    _active[a] = _active[_active.Count - 1];
                    _active.RemoveAt(_active.Count - 1);
                    continue;
                }

                _pixels[i] = Pixel(i, (byte)(255f * (1f - t)));
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        /// <summary>The grass of this level: the base of the 3D artist with its decor strewn by the seed; once per seed.</summary>
        private void Grass(uint seed)
        {
            var surface = _view.Surface;
            if (surface == null)
                return;

            int size;
            if (_grass == null || _grassSeed != seed)
            {
                var pixels = GrassScatter.Island(surface, seed, out size);
                if (pixels == null)
                    return;

                if (_grass == null || _grass.width != size)
                {
                    if (_grass != null)
                        UnityEngine.Object.Destroy(_grass);
                    _grass = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    {
                        name = "Grass",
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Repeat,
                    };
                }

                _grass.SetPixels32(pixels);
                _grass.Apply(false, true);
                _grassSeed = seed;
            }

            // a material made anew for another map gets the grass too
            size = _grass.width;
            _cells.SetTexture(GrassTexId, _grass);
            // the grass covers this many meters once; a larger map repeats it
            var tile = (float)size / surface.PixelsPerMeter;
            _cells.SetVector(GrassTilesId, new Vector4(_simulation.Grid.Width * _rules.CellSize / tile, _simulation.Grid.Height * _rules.CellSize / tile, 0f, 0f));
            _cells.SetFloat(LightenId, surface.Lighten);
            _cells.SetFloat(PaintAlphaId, surface.PaintAlpha);
            _cells.SetFloat(LumMeanId, surface.LumMean);
        }

        private static float Distance(int cell, int size, float x, float y)
        {
            var dx = cell % size + 0.5f - x;
            var dy = cell / size + 0.5f - y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private Material Flat(Color color)
        {
            var material = Copy(_view.FlatMaterial);
            material.SetColor(BaseColorId, color);
            return material;
        }

        private Material Copy(Material source)
        {
            var material = new Material(source);
            _materials.Add(material);
            return material;
        }
    }
}
