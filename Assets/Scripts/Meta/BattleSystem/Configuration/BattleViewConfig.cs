using UnityEngine;

namespace Meta.BattleSystem.Configuration
{
    /// <summary>How the battle looks: placeholder colours and materials, the recolouring of cells, the wave of an enclosure.</summary>
    [CreateAssetMenu(fileName = "BattleViewConfig", menuName = "Borderbite/Battle/Battle View Config")]
    public sealed class BattleViewConfig : ScriptableObject
    {
        [Header("Colours")]
        [Tooltip("A neutral cell. Starting value: grey.")]
        [SerializeField] private Color neutralColor = new Color(0.66f, 0.67f, 0.63f);

        [Tooltip("A cell of the player. Starting value: blue.")]
        [SerializeField] private Color playerColor = new Color(0.25f, 0.5f, 0.95f);

        [Tooltip("A cell of the enemy. Starting value: red.")]
        [SerializeField] private Color enemyColor = new Color(0.9f, 0.3f, 0.27f);

        [Tooltip("The hero of the player, a placeholder. Starting value: dark blue.")]
        [SerializeField] private Color playerHeroColor = new Color(0.1f, 0.25f, 0.7f);

        [Tooltip("The water around the island, to the horizon. Starting value: sea blue.")]
        [SerializeField] private Color waterColor = new Color(0.16f, 0.43f, 0.6f);

        [Tooltip("The sides of the island under the cells. Starting value: earth brown.")]
        [SerializeField] private Color islandColor = new Color(0.46f, 0.36f, 0.26f);

        [Header("Recolouring")]
        [Tooltip("A cell turns to its new colour with a flash this long, seconds; the spec allows up to 0.2. Starting value: 0.15.")]
        [Range(0.01f, 0.2f)] [SerializeField] private float flashSeconds = 0.15f;

        [Tooltip("How white the flash starts, 0..1. Starting value: 0.45.")]
        [Range(0f, 1f)] [SerializeField] private float flashStrength = 0.45f;

        [Tooltip("An enclosed area recolours as a wave from where the ring closed to its farthest cell, seconds; with the flash the " +
                 "spec asks for 0.3–0.5. Starting value: 0.35.")]
        [Range(0f, 1f)] [SerializeField] private float waveSeconds = 0.35f;

        [Header("World")]
        [Tooltip("Depth of the island under the cells, meters. Starting value: 1.5.")]
        [Min(0.1f)] [SerializeField] private float islandDepth = 1.5f;

        [Tooltip("The water lies this far under the cells, meters. Starting value: 0.4.")]
        [Min(0f)] [SerializeField] private float waterDepth = 0.4f;

        [Tooltip("Height of the placeholder hero, meters. Starting value: 1.")]
        [Min(0.1f)] [SerializeField] private float heroHeight = 1f;

        [Tooltip("Width of the placeholder hero, meters. Starting value: 0.6.")]
        [Min(0.1f)] [SerializeField] private float heroWidth = 0.6f;

        [Tooltip("How softly the camera follows the hero: the time to close most of the gap, seconds. Starting value: 0.12.")]
        [Min(0f)] [SerializeField] private float cameraSmoothing = 0.12f;

        [Header("Capture")]
        [Tooltip("«+N%» over a captured area lives this long, seconds. Starting value: 1.2.")]
        [Min(0.1f)] [SerializeField] private float popupSeconds = 1.2f;

        [Tooltip("«+N%» rises this much while it lives, canvas units. Starting value: 60.")]
        [SerializeField] private float popupRise = 60f;

        [Header("Materials")]
        [Tooltip("The material of the cells: shader Borderbite/Map/Cells.")]
        [SerializeField] private Material cellsMaterial;

        [Tooltip("The material of placeholders: shader Borderbite/Placeholder/Flat Shaded; each view takes a copy in its colour.")]
        [SerializeField] private Material flatMaterial;

        public Color NeutralColor => neutralColor;
        public Color PlayerColor => playerColor;
        public Color EnemyColor => enemyColor;
        public Color PlayerHeroColor => playerHeroColor;
        public Color WaterColor => waterColor;
        public Color IslandColor => islandColor;
        public float FlashSeconds => flashSeconds;
        public float FlashStrength => flashStrength;
        public float WaveSeconds => waveSeconds;
        public float IslandDepth => islandDepth;
        public float WaterDepth => waterDepth;
        public float HeroHeight => heroHeight;
        public float HeroWidth => heroWidth;
        public float CameraSmoothing => cameraSmoothing;
        public float PopupSeconds => popupSeconds;
        public float PopupRise => popupRise;
        public Material CellsMaterial => cellsMaterial;
        public Material FlatMaterial => flatMaterial;

#if UNITY_EDITOR
        public void InitForEditor(Material cells, Material flat)
        {
            cellsMaterial = cells;
            flatMaterial = flat;
        }
#endif
    }
}
