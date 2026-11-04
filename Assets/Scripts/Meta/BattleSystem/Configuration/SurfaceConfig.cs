using System;
using UnityEngine;

namespace Meta.BattleSystem.Configuration
{
    /// <summary>
    /// The grass of the island (stage 1 spec 2.1): the base of the 3D artist, its decor stamps and how many of each kind to strew,
    /// and how the colour of a side lies over the grass. Filled from <c>Assets/Textures/Surface/grass_decor.json</c> by
    /// <c>ProjectSetup</c>; the numbers of the paint stay as the owner set them.
    /// </summary>
    [CreateAssetMenu(fileName = "SurfaceConfig", menuName = "Borderbite/Battle/Surface Config")]
    public sealed class SurfaceConfig : ScriptableObject
    {
        [Header("Grass")]
        [Tooltip("The grass of the whole island, seamless, a pixel a voxel (1/8 m). Readable: the decor is stamped into a copy.")]
        [SerializeField] private Texture2D grassBase;

        [Tooltip("The decor stamps in a row, transparent background. Readable.")]
        [SerializeField] private Texture2D grassDecor;

        [Tooltip("Pixels of the grass in a meter. Starting value: 8.")]
        [Min(1)] [SerializeField] private int pixelsPerMeter = 8;

        [Tooltip("Where each stamp lies in the decor texture (pixels, from its lower left) and which kind it is.")]
        [SerializeField] private Stamp[] stamps = Array.Empty<Stamp>();

        [Tooltip("How each kind of decor is strewn, in this order.")]
        [SerializeField] private Kind[] kinds = Array.Empty<Kind>();

        [Header("Paint over the grass")]
        [Tooltip("The colour of a side is first lightened to white this much, 0..1. Starting value: 0.55 (owner, 4 November: " +
                 "red land must differ from grass in brightness for colour-blind players).")]
        [Range(0f, 1f)] [SerializeField] private float lighten = 0.55f;

        [Tooltip("How much the lightened colour covers the grass, 0..1; the grass shows through the rest. Starting value: 0.7.")]
        [Range(0f, 1f)] [SerializeField] private float paintAlpha = 0.7f;

        [Tooltip("The mean brightness of the grass: the paint keeps the light and dark of the grass around it. Starting value: 0.491.")]
        [Range(0.05f, 1f)] [SerializeField] private float lumMean = 0.491f;

        public Texture2D GrassBase => grassBase;
        public Texture2D GrassDecor => grassDecor;
        public int PixelsPerMeter => pixelsPerMeter;
        public Stamp[] Stamps => stamps;
        public Kind[] Kinds => kinds;
        public float Lighten => lighten;
        public float PaintAlpha => paintAlpha;
        public float LumMean => lumMean;

        [Serializable]
        public struct Stamp
        {
            public string Id;
            public RectInt Rect;
            public int Kind;
        }

        [Serializable]
        public struct Kind
        {
            public string Id;
            [Tooltip("Pieces of this decor on a square meter.")]
            public float Density;
            [Tooltip("Centres of two groups are no closer, pixels.")]
            public float Spacing;
            public int GroupMin;
            public int GroupMax;
            [Tooltip("The pieces of a group lie this far from its centre at most, pixels.")]
            public float Radius;
        }

#if UNITY_EDITOR
        public void InitForEditor(Texture2D baseTexture, Texture2D decor, int pixels, Stamp[] stampList, Kind[] kindList)
        {
            grassBase = baseTexture;
            grassDecor = decor;
            pixelsPerMeter = pixels;
            stamps = stampList;
            kinds = kindList;
        }
#endif
    }
}
