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

        [Tooltip("The hero of the enemy: dark red, as the player's is dark blue. Starting value: dark red.")]
        [SerializeField] private Color enemyHeroColor = new Color(0.62f, 0.1f, 0.08f);

        [Tooltip("Soldiers of the player, placeholders. Starting value: blue.")]
        [SerializeField] private Color playerSoldierColor = new Color(0.2f, 0.4f, 0.88f);

        [Tooltip("Soldiers of the enemy, placeholders. Starting value: red.")]
        [SerializeField] private Color enemySoldierColor = new Color(0.8f, 0.2f, 0.17f);

        [Tooltip("Piles of gold. Starting value: gold yellow.")]
        [SerializeField] private Color goldColor = new Color(0.98f, 0.8f, 0.25f);

        [Tooltip("Bundles of wood. Starting value: wood brown.")]
        [SerializeField] private Color woodColor = new Color(0.55f, 0.36f, 0.2f);

        [Tooltip("An empty place to build that no side owns: a dull grey pad. Starting value: grey.")]
        [SerializeField] private Color siteColor = new Color(0.5f, 0.5f, 0.48f);


        [Tooltip("Shots and the flash of a gun. Starting value: warm white.")]
        [SerializeField] private Color shotColor = new Color(1f, 0.93f, 0.7f);

        [Tooltip("The water around the island, to the horizon; teal, so the blue of the player never reads as water (voxel-forge profile). Starting value: #2F8487.")]
        [SerializeField] private Color waterColor = new Color(0.184f, 0.518f, 0.529f);

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

        [Header("Units")]
        [Tooltip("The hero and soldiers turn to where they walk or fight this fast, degrees a second; 180° takes 0.2 s (stage 1 " +
                 "spec 2.2). Starting value: 900.")]
        [Min(1f)] [SerializeField] private float turnSpeed = 900f;

        [Tooltip("The rim around the units of the player: light, so the side reads not only by colour (owner, 4 November). " +
                 "Starting value: warm white.")]
        [SerializeField] private Color playerOutline = new Color(0.96f, 0.95f, 0.88f);

        [Tooltip("The rim around the units of the enemy: dark. Starting value: near black.")]
        [SerializeField] private Color enemyOutline = new Color(0.09f, 0.08f, 0.1f);

        [Tooltip("The width of the rim, a part of the screen height: 0.0025 is about 2.7 pixels at 1080. Starting value: 0.0025.")]
        [Range(0f, 0.01f)] [SerializeField] private float outlineWidth = 0.0025f;

        [Tooltip("After a blow or a shot the hero faces its target this long, seconds, then turns back to where it walks. " +
                 "Starting value: 0.35.")]
        [Min(0f)] [SerializeField] private float attackFaceSeconds = 0.35f;

        [Header("World")]
        [Tooltip("Depth of the island under the cells, meters. Starting value: 1.5.")]
        [Min(0.1f)] [SerializeField] private float islandDepth = 1.5f;

        [Tooltip("The water lies this far under the top of the cells, meters (voxel-forge profile). Starting value: 0.5.")]
        [Min(0f)] [SerializeField] private float waterDepth = 0.5f;



        [Tooltip("How softly the camera follows the hero: the time to close most of the gap, seconds. Starting value: 0.12.")]
        [Min(0f)] [SerializeField] private float cameraSmoothing = 0.12f;

        [Header("Life of the battle")]
        [Tooltip("A picked resource flies to the hero this long, seconds. Starting value: 0.25.")]
        [Min(0f)] [SerializeField] private float pickupSeconds = 0.25f;

        [Tooltip("A new barracks grows this long, seconds. Starting value: 0.3.")]
        [Min(0f)] [SerializeField] private float growSeconds = 0.3f;

        [Tooltip("The trace of a shot stays this long, seconds. Starting value: 0.12.")]
        [Min(0.01f)] [SerializeField] private float shotSeconds = 0.12f;

        [Tooltip("The cubes of a fallen soldier, hero or barracks fly this long, seconds. Starting value: 0.8.")]
        [Min(0.05f)] [SerializeField] private float debrisSeconds = 0.8f;

        [Header("Capture")]
        [Tooltip("«+N%» over a captured area lives this long, seconds. Starting value: 1.2.")]
        [Min(0.1f)] [SerializeField] private float popupSeconds = 1.2f;

        [Tooltip("«+N%» rises this much while it lives, canvas units. Starting value: 60.")]
        [SerializeField] private float popupRise = 60f;

        [Header("Surface")]
        [Tooltip("The grass of the island and how the colour of a side lies over it.")]
        [SerializeField] private SurfaceConfig surface;

        [Header("Materials")]
        [Tooltip("The material of the cells: shader Borderbite/Map/Cells.")]
        [SerializeField] private Material cellsMaterial;

        [Tooltip("The material of placeholders: shader Borderbite/World/Flat Lit; each view takes a copy in its colour.")]
        [SerializeField] private Material flatMaterial;

        [Tooltip("The sea: shader Borderbite/World/Water, its numbers from Assets/Textures/Water/water.json (ProjectSetup).")]
        [SerializeField] private Material waterMaterial;

        [Tooltip("Shader Borderbite/World/Flat Lit Outlined: units take copies of their materials with it and the rim of their side.")]
        [SerializeField] private Shader outlinedShader;

        public Color NeutralColor => neutralColor;
        public Color PlayerColor => playerColor;

        public Color SideColor(Core.BattleSystem.Enums.Side side) => side switch
        {
            Core.BattleSystem.Enums.Side.Player => playerColor,
            Core.BattleSystem.Enums.Side.Enemy => enemyColor,
            _ => neutralColor,
        };

        public Color SoldierColor(Core.BattleSystem.Enums.Side side) =>
            side == Core.BattleSystem.Enums.Side.Player ? playerSoldierColor : enemySoldierColor;
        public Color EnemyColor => enemyColor;
        public Color PlayerHeroColor => playerHeroColor;

        public Color HeroColor(Core.BattleSystem.Enums.Side side) =>
            side == Core.BattleSystem.Enums.Side.Player ? playerHeroColor : enemyHeroColor;
        public Color PlayerSoldierColor => playerSoldierColor;
        public Color EnemySoldierColor => enemySoldierColor;
        public Color GoldColor => goldColor;
        public Color WoodColor => woodColor;
        public Color SiteColor => siteColor;
        public Color ShotColor => shotColor;
        public Color WaterColor => waterColor;
        public Color IslandColor => islandColor;
        public float FlashSeconds => flashSeconds;
        public float FlashStrength => flashStrength;
        public float WaveSeconds => waveSeconds;
        public Color OutlineColor(Core.BattleSystem.Enums.Side side) =>
            side == Core.BattleSystem.Enums.Side.Player ? playerOutline : enemyOutline;
        public float OutlineWidth => outlineWidth;
        public Shader OutlinedShader => outlinedShader;
        public Material WaterMaterial => waterMaterial;
        public float TurnSpeed => turnSpeed;
        public float AttackFaceSeconds => attackFaceSeconds;
        public float IslandDepth => islandDepth;
        public float WaterDepth => waterDepth;
        public float CameraSmoothing => cameraSmoothing;
        public float PickupSeconds => pickupSeconds;
        public float GrowSeconds => growSeconds;
        public float ShotSeconds => shotSeconds;
        public float DebrisSeconds => debrisSeconds;
        public float PopupSeconds => popupSeconds;
        public float PopupRise => popupRise;
        public Material CellsMaterial => cellsMaterial;
        public Material FlatMaterial => flatMaterial;
        public SurfaceConfig Surface => surface;

#if UNITY_EDITOR
        public void InitForEditor(Material cells, Material flat)
        {
            cellsMaterial = cells;
            flatMaterial = flat;
        }

        public void SetSurfaceForEditor(SurfaceConfig config) => surface = config;

        public void SetOutlinedShaderForEditor(Shader shader) => outlinedShader = shader;

        public void SetWaterForEditor(Material material) => waterMaterial = material;
#endif
    }
}
