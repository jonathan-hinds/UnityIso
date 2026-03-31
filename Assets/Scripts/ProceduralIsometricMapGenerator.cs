using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class ProceduralIsometricMapGenerator : MonoBehaviour
{
    [Serializable]
    public struct TileVisualProfile
    {
        public Sprite topSprite;
        public Sprite leftSideSprite;
        public Sprite rightSideSprite;
        public Color defaultTopColor;
        public Color defaultLeftSideColor;
        public Color defaultRightSideColor;
        public Color defaultOutlineColor;
        public bool addCliffEdgeShadows;
        public float cliffShadowWidth;
        public Color cliffShadowColor;
        public float tileWidth;
        public float tileHeight;
        public float elevationStep;
        public bool renderSideFaces;
        public FakeShadowSettings fakeShadowSettings;
    }

    public readonly struct OcclusionVolume
    {
        public OcclusionVolume(Bounds bounds, int sortingOrder, int sortingLayerId)
        {
            Bounds = bounds;
            SortingOrder = sortingOrder;
            SortingLayerId = sortingLayerId;
        }

        public Bounds Bounds { get; }
        public int SortingOrder { get; }
        public int SortingLayerId { get; }
    }

    [Serializable]
    public sealed class DebrisSettings
    {
        [FormerlySerializedAs("intensity")]
        [Range(0f, 1f)]
        public float amount = 0.18f;

        [FormerlySerializedAs("noiseScale")]
        [Range(0f, 1f)]
        public float dispersion = 0.5f;

        public List<Sprite> topSprites = new();

        public bool HasSprites => topSprites != null && topSprites.Count > 0;
        public bool IsEnabled => amount > 0f && HasSprites;

        public DebrisSettings Clone()
        {
            DebrisSettings clone = new DebrisSettings
            {
                amount = amount,
                dispersion = dispersion,
                topSprites = new List<Sprite>()
            };

            if (topSprites != null)
            {
                for (int i = 0; i < topSprites.Count; i++)
                {
                    if (topSprites[i] != null)
                    {
                        clone.topSprites.Add(topSprites[i]);
                    }
                }
            }

            return clone;
        }

        public void Sanitize()
        {
            amount = Mathf.Clamp01(amount);
            dispersion = Mathf.Clamp01(dispersion);
            topSprites ??= new List<Sprite>();

            for (int i = topSprites.Count - 1; i >= 0; i--)
            {
                if (topSprites[i] == null)
                {
                    topSprites.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class FakeShadowSettings
    {
        public bool enabled = true;

        [Range(0f, 1f)]
        public float elevationOpacityPerLevel = 0.04f;

        [Range(0f, 1f)]
        public float adjacentBlockerOpacityPerLevel = 0.08f;

        [Range(0f, 1f)]
        public float diagonalBlockerOpacityPerLevel = 0.04f;

        [Range(0f, 1f)]
        public float frontExposureReductionPerLevel = 0.08f;

        [Range(0f, 1f)]
        public float maxOpacity = 1f;

        [FormerlySerializedAs("frontGradientOpacity")]
        [Range(0f, 1f)]
        public float nearDepthOpacity = 0f;

        [FormerlySerializedAs("backGradientOpacity")]
        [Range(0f, 1f)]
        public float farDepthOpacity = 1f;

        [Range(0.5f, 4f)]
        public float depthFalloffExponent = 1f;

        [Range(2, 16)]
        public int opacityBandCount = 10;

        public Color tint = new Color(0f, 0f, 0f, 1f);

        public bool IsEnabled => enabled && maxOpacity > 0f && tint.a > 0f;

        public FakeShadowSettings Clone()
        {
            return new FakeShadowSettings
            {
                enabled = enabled,
                elevationOpacityPerLevel = elevationOpacityPerLevel,
                adjacentBlockerOpacityPerLevel = adjacentBlockerOpacityPerLevel,
                diagonalBlockerOpacityPerLevel = diagonalBlockerOpacityPerLevel,
                frontExposureReductionPerLevel = frontExposureReductionPerLevel,
                maxOpacity = maxOpacity,
                nearDepthOpacity = nearDepthOpacity,
                farDepthOpacity = farDepthOpacity,
                depthFalloffExponent = depthFalloffExponent,
                opacityBandCount = opacityBandCount,
                tint = tint
            };
        }

        public void Sanitize()
        {
            elevationOpacityPerLevel = Mathf.Clamp01(elevationOpacityPerLevel);
            adjacentBlockerOpacityPerLevel = Mathf.Clamp01(adjacentBlockerOpacityPerLevel);
            diagonalBlockerOpacityPerLevel = Mathf.Clamp01(diagonalBlockerOpacityPerLevel);
            frontExposureReductionPerLevel = Mathf.Clamp01(frontExposureReductionPerLevel);
            maxOpacity = Mathf.Clamp01(maxOpacity);
            nearDepthOpacity = Mathf.Clamp01(nearDepthOpacity);
            farDepthOpacity = Mathf.Clamp01(farDepthOpacity);
            farDepthOpacity = Mathf.Max(nearDepthOpacity, farDepthOpacity);
            depthFalloffExponent = Mathf.Clamp(depthFalloffExponent, 0.5f, 4f);
            opacityBandCount = Mathf.Clamp(opacityBandCount, 2, 16);
        }
    }

    [Serializable]
    public struct ChestSpawnSettings
    {
        [Range(0f, 1f)] public float spawnChance;
        [Range(0f, 1f)] public float mimicChance;
        [Min(0)] public int maxChestCount;
        [Min(0)] public int minGoldReward;
        [Min(0)] public int maxGoldReward;
        [Min(0)] public int minItemDrops;
        [Min(0)] public int maxItemDrops;
        public List<TacticsChestItemPoolEntry> itemPool;

        public bool IsEnabled => spawnChance > 0f && maxChestCount > 0 && (maxGoldReward > 0 || maxItemDrops > 0);

        public ChestSpawnSettings Clone()
        {
            ChestSpawnSettings clone = this;
            clone.itemPool = new List<TacticsChestItemPoolEntry>();
            if (itemPool != null)
            {
                for (int i = 0; i < itemPool.Count; i++)
                {
                    TacticsChestItemPoolEntry entry = itemPool[i];
                    if (entry != null)
                    {
                        clone.itemPool.Add(entry.Clone());
                    }
                }
            }

            return clone;
        }

        public void Sanitize()
        {
            spawnChance = Mathf.Clamp01(spawnChance);
            mimicChance = Mathf.Clamp01(mimicChance);
            maxChestCount = Mathf.Max(0, maxChestCount);
            minGoldReward = Mathf.Max(0, minGoldReward);
            maxGoldReward = Mathf.Max(minGoldReward, maxGoldReward);
            minItemDrops = Mathf.Max(0, minItemDrops);
            maxItemDrops = Mathf.Max(minItemDrops, maxItemDrops);
            itemPool ??= new List<TacticsChestItemPoolEntry>();

            for (int i = itemPool.Count - 1; i >= 0; i--)
            {
                TacticsChestItemPoolEntry entry = itemPool[i];
                if (entry == null)
                {
                    itemPool.RemoveAt(i);
                    continue;
                }

                entry.Sanitize();
                if (!entry.IsValid)
                {
                    itemPool.RemoveAt(i);
                }
            }
        }
    }

    public readonly struct ChestSpawnPlan
    {
        public ChestSpawnPlan(
            string runtimeChestId,
            Vector2Int tile,
            TacticsChestController.ChestFacing facing,
            bool containsMimic)
        {
            RuntimeChestId = runtimeChestId;
            Tile = tile;
            Facing = facing;
            ContainsMimic = containsMimic;
        }

        public string RuntimeChestId { get; }
        public Vector2Int Tile { get; }
        public TacticsChestController.ChestFacing Facing { get; }
        public bool ContainsMimic { get; }
    }

    public readonly struct StairsSpawnPlan
    {
        public StairsSpawnPlan(string runtimeStairsId, Vector2Int tile)
        {
            RuntimeStairsId = runtimeStairsId;
            Tile = tile;
        }

        public string RuntimeStairsId { get; }
        public Vector2Int Tile { get; }
    }

    public event Action MapGenerated;

    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int length = 10;
    [SerializeField, Min(1)] private int width = 10;
    [SerializeField, Min(0)] private int minElevation = 0;
    [SerializeField, Min(0)] private int maxElevation = 4;

    [Header("Noise")]
    [SerializeField, Min(0.01f)] private float noiseScale = 5f;
    [SerializeField, Range(1, 6)] private int noiseOctaves = 3;
    [SerializeField, Range(0.1f, 1f)] private float persistence = 0.5f;
    [SerializeField, Min(1f)] private float lacunarity = 2f;
    [SerializeField, Range(0, 4)] private int smoothingPasses = 1;

    [Header("Debris")]
    [SerializeField] private DebrisSettings debrisSettings = new DebrisSettings();

    [Header("Fake Shadows")]
    [SerializeField] private FakeShadowSettings fakeShadowSettings = new FakeShadowSettings();

    [Header("Tile Art")]
    [SerializeField] private Sprite topSprite;
    [SerializeField] private Sprite leftSideSprite;
    [SerializeField] private Sprite rightSideSprite;
    [SerializeField] private Color defaultTopColor = new Color(0.40f, 0.74f, 0.33f, 1f);
    [SerializeField] private Color defaultLeftSideColor = new Color(0.26f, 0.49f, 0.22f, 1f);
    [SerializeField] private Color defaultRightSideColor = new Color(0.20f, 0.38f, 0.17f, 1f);
    [SerializeField] private Color defaultOutlineColor = new Color(0.12f, 0.20f, 0.10f, 1f);

    [Header("Depth Cues")]
    [SerializeField] private bool addCliffEdgeShadows = true;
    [SerializeField, Range(0.05f, 0.5f)] private float cliffShadowWidth = 0.22f;
    [SerializeField] private Color cliffShadowColor = new Color(0.05f, 0.08f, 0.04f, 0.45f);

    [Header("Layout")]
    [SerializeField, Min(0.1f)] private float tileWidth = 1f;
    [SerializeField, Min(0.1f)] private float tileHeight = 0.5f;
    [SerializeField, Min(0.05f)] private float elevationStep = 0.5f;
    [SerializeField] private bool renderSideFaces = true;
    [SerializeField] private Vector3 mapOffset = Vector3.zero;

    [Header("Scene")]
    [SerializeField] private bool autoFrameCamera = true;
    [SerializeField, Min(0f)] private float cameraPadding = 1f;

    [Header("Enemy Spawns")]
    [SerializeField] private List<TacticsEnemySpawnEntry> enemySpawnEntries = new();

    [Header("Chest Spawns")]
    [SerializeField] private ChestSpawnSettings chestSpawnSettings = new ChestSpawnSettings
    {
        spawnChance = 0.08f,
        mimicChance = 0.2f,
        maxChestCount = 4,
        minGoldReward = 5,
        maxGoldReward = 100,
        minItemDrops = 0,
        maxItemDrops = 1,
        itemPool = null
    };

    private const string GeneratedRootName = "Generated Isometric Map";
    private const string GeneratedAttachmentRootPrefix = "Generated Runtime - ";
    private const int DefaultSpritePixels = 128;
    private const float DefaultPixelsPerUnit = 128f;
    private const int TopFaceSortBias = 2;
    private const int FakeShadowSortBias = 3;
    private const int DebrisSortBias = 4;
    private const int CharacterSortBias = 8;
    private const int DebrisSeedOffset = 8191;
    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1)
    };

    private static Sprite cachedDefaultTopSprite;
    private static Sprite cachedDefaultLeftSideSprite;
    private static Sprite cachedDefaultRightSideSprite;
    private static Sprite cachedStairsTopSprite;
    private Sprite cachedLeftVisibleDebrisMaskSprite;
    private Sprite cachedRightVisibleDebrisMaskSprite;
    private Sprite cachedTopLeftEdgeShadowSprite;
    private Sprite cachedTopRightEdgeShadowSprite;
    private Sprite cachedBottomLeftEdgeShadowSprite;
    private Sprite cachedBottomRightEdgeShadowSprite;
    private Sprite cachedFakeShadowFillSprite;

    private readonly List<SpriteRenderer> spawnedRenderers = new List<SpriteRenderer>();
    private readonly List<OcclusionVolume> occlusionVolumes = new List<OcclusionVolume>();
    private readonly HashSet<Vector2Int> currentDebrisTiles = new HashSet<Vector2Int>();
    private int[,] currentHeights;
    private int maximumGeneratedElevation;
    private System.Random spawnPlacementRandom;
    private Vector2Int? currentStairsTile;
    private string currentStairsRuntimeId = string.Empty;

    public int Length => length;
    public int Width => width;
    public float TileWidth => tileWidth;
    public float TileHeight => tileHeight;
    public float ElevationStep => elevationStep;
    public bool HasGeneratedMap => currentHeights != null;
    public int MaximumElevation => maximumGeneratedElevation;
    public IReadOnlyList<TacticsEnemySpawnEntry> EnemySpawnEntries => enemySpawnEntries;
    public IReadOnlyList<OcclusionVolume> OcclusionVolumes => occlusionVolumes;
    public ChestSpawnSettings ChestSettings => chestSpawnSettings;

    public TacticsMatchGenerationSettings CreateMatchGenerationSettings()
    {
        TacticsMatchGenerationSettings settings = new TacticsMatchGenerationSettings
        {
            seed = seed,
            width = width,
            length = length,
            noiseScale = noiseScale,
            noiseOctaves = noiseOctaves,
            minElevation = minElevation,
            maxElevation = maxElevation,
            debris = debrisSettings != null ? debrisSettings.Clone() : new DebrisSettings(),
            fakeShadows = fakeShadowSettings != null ? fakeShadowSettings.Clone() : new FakeShadowSettings(),
            chestSpawns = chestSpawnSettings.Clone(),
            enemies = new List<TacticsMatchEnemySettings>(enemySpawnEntries.Count)
        };

        for (int i = 0; i < enemySpawnEntries.Count; i++)
        {
            TacticsEnemySpawnEntry entry = enemySpawnEntries[i];
            if (!entry.IsValid)
            {
                continue;
            }

            settings.enemies.Add(new TacticsMatchEnemySettings
            {
                enemyId = entry.EnemyId,
                count = entry.Count
            });
        }

        settings.Sanitize();
        return settings;
    }

    public void ApplyMatchGenerationSettings(TacticsMatchGenerationSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        TacticsMatchGenerationSettings sanitized = settings.Clone();
        sanitized.Sanitize();

        seed = sanitized.seed;
        width = sanitized.width;
        length = sanitized.length;
        noiseScale = sanitized.noiseScale;
        noiseOctaves = sanitized.noiseOctaves;
        minElevation = sanitized.minElevation;
        maxElevation = sanitized.maxElevation;
        debrisSettings = sanitized.debris != null ? sanitized.debris.Clone() : new DebrisSettings();
        debrisSettings.Sanitize();
        fakeShadowSettings = sanitized.fakeShadows != null ? sanitized.fakeShadows.Clone() : new FakeShadowSettings();
        fakeShadowSettings.Sanitize();
        chestSpawnSettings = sanitized.chestSpawns.Clone();
        chestSpawnSettings.Sanitize();

        enemySpawnEntries = new List<TacticsEnemySpawnEntry>(sanitized.enemies.Count);
        for (int i = 0; i < sanitized.enemies.Count; i++)
        {
            TacticsMatchEnemySettings enemy = sanitized.enemies[i];
            if (enemy == null || !enemy.IsValid)
            {
                continue;
            }

            enemySpawnEntries.Add(new TacticsEnemySpawnEntry(enemy.enemyId, enemy.count));
        }
    }

    public TileVisualProfile CreateTileVisualProfile()
    {
        return new TileVisualProfile
        {
            topSprite = topSprite,
            leftSideSprite = leftSideSprite,
            rightSideSprite = rightSideSprite,
            defaultTopColor = defaultTopColor,
            defaultLeftSideColor = defaultLeftSideColor,
            defaultRightSideColor = defaultRightSideColor,
            defaultOutlineColor = defaultOutlineColor,
            addCliffEdgeShadows = addCliffEdgeShadows,
            cliffShadowWidth = cliffShadowWidth,
            cliffShadowColor = cliffShadowColor,
            tileWidth = tileWidth,
            tileHeight = tileHeight,
            elevationStep = elevationStep,
            renderSideFaces = renderSideFaces,
            fakeShadowSettings = fakeShadowSettings != null ? fakeShadowSettings.Clone() : new FakeShadowSettings()
        };
    }

    public void ApplyTileVisualProfile(TileVisualProfile profile)
    {
        topSprite = profile.topSprite;
        leftSideSprite = profile.leftSideSprite;
        rightSideSprite = profile.rightSideSprite;
        defaultTopColor = profile.defaultTopColor;
        defaultLeftSideColor = profile.defaultLeftSideColor;
        defaultRightSideColor = profile.defaultRightSideColor;
        defaultOutlineColor = profile.defaultOutlineColor;
        addCliffEdgeShadows = profile.addCliffEdgeShadows;
        cliffShadowWidth = Mathf.Max(0.05f, profile.cliffShadowWidth);
        cliffShadowColor = profile.cliffShadowColor;
        tileWidth = Mathf.Max(0.1f, profile.tileWidth);
        tileHeight = Mathf.Max(0.1f, profile.tileHeight);
        elevationStep = Mathf.Max(0.05f, profile.elevationStep);
        renderSideFaces = profile.renderSideFaces;
        fakeShadowSettings = profile.fakeShadowSettings != null ? profile.fakeShadowSettings.Clone() : new FakeShadowSettings();
        fakeShadowSettings.Sanitize();
    }

    public void ConfigureSingleTilePreview(TileVisualProfile profile)
    {
        ApplyTileVisualProfile(profile);
        ConfigureSingleTilePreview();
    }

    public void ConfigureSingleTilePreview()
    {
        generateOnStart = false;
        seed = 0;
        length = 1;
        width = 1;
        minElevation = 1;
        maxElevation = 1;
        noiseScale = 1f;
        noiseOctaves = 1;
        persistence = 0.5f;
        lacunarity = 2f;
        smoothingPasses = 0;
        autoFrameCamera = false;
        cameraPadding = 0f;
        mapOffset = Vector3.zero;
        renderSideFaces = false;
        enemySpawnEntries.Clear();
        debrisSettings = new DebrisSettings();
        debrisSettings.topSprites.Clear();
        debrisSettings.amount = 0f;
        fakeShadowSettings = new FakeShadowSettings { enabled = false };
        chestSpawnSettings = new ChestSpawnSettings();
    }

    private void Start()
    {
        if (!generateOnStart || !TacticsRuntimeStartupState.GameplayStartRequested)
        {
            return;
        }

        GenerateMap();
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ValidateSettings();
        EnsureDefaultSprites();
        ClearGeneratedMap();
        spawnedRenderers.Clear();
        occlusionVolumes.Clear();

        currentHeights = GenerateHeights();
        maximumGeneratedElevation = GetMaximumHeight(currentHeights);
        spawnPlacementRandom = CreateSpawnPlacementRandom();
        bool hasStairsSpawnPlan = TryCreateStairsSpawnPlan(out StairsSpawnPlan stairsSpawnPlan);
        currentStairsTile = hasStairsSpawnPlan ? stairsSpawnPlan.Tile : null;
        currentStairsRuntimeId = hasStairsSpawnPlan ? stairsSpawnPlan.RuntimeStairsId : string.Empty;
        currentDebrisTiles.Clear();
        CreateDebrisPlacementSet(currentDebrisTiles);
        Transform generatedRoot = CreateGeneratedRoot();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                int height = currentHeights[x, y];
                if (height <= 0)
                {
                    continue;
                }

                Bounds? tileOcclusionBounds = null;
                int tileOcclusionSortingOrder = int.MinValue;
                int tileOcclusionSortingLayerId = 0;
                bool isStairsTile = currentStairsTile.HasValue && currentStairsTile.Value.x == x && currentStairsTile.Value.y == y;
                Sprite resolvedTopSprite = isStairsTile
                    ? GetResolvedStairsTopSprite()
                    : (topSprite != null ? topSprite : cachedDefaultTopSprite);

                Vector3 topPosition = GridToWorld(x, y, height);
                CreateTilePart(
                    generatedRoot,
                    resolvedTopSprite,
                    topPosition,
                    $"Top_{x}_{y}_{height}",
                    x,
                    y,
                    height,
                    TopFaceSortBias,
                    IsometricMapElevationElementType.TopFace,
                    ref tileOcclusionBounds,
                    ref tileOcclusionSortingOrder,
                    ref tileOcclusionSortingLayerId);
                ConfigureTopTile(
                    partName: $"Top_{x}_{y}_{height}",
                    generatedRoot: generatedRoot,
                    gridX: x,
                    gridY: y,
                    height: height,
                    isStairsTile: isStairsTile,
                    runtimeStairsId: isStairsTile ? currentStairsRuntimeId : string.Empty);

                int lowerLeftNeighborHeight = GetHeight(currentHeights, x - 1, y);
                int lowerRightNeighborHeight = GetHeight(currentHeights, x, y - 1);
                int upperLeftNeighborHeight = GetHeight(currentHeights, x, y + 1);
                int upperRightNeighborHeight = GetHeight(currentHeights, x + 1, y);

                if (fakeShadowSettings != null && fakeShadowSettings.IsEnabled)
                {
                    CreateTilePart(
                        generatedRoot,
                        cachedFakeShadowFillSprite,
                        topPosition,
                        $"FakeShadow_{x}_{y}_{height}",
                        x,
                        y,
                        height,
                        FakeShadowSortBias,
                        IsometricMapElevationElementType.TopOverlay);
                    ConfigureFakeShadowOverlay(
                        partName: $"FakeShadow_{x}_{y}_{height}",
                        generatedRoot: generatedRoot,
                        gridX: x,
                        gridY: y,
                        elevation: height,
                        leftBlockingElevation: lowerLeftNeighborHeight,
                        rightBlockingElevation: lowerRightNeighborHeight,
                        upperLeftBlockingElevation: upperLeftNeighborHeight,
                        upperRightBlockingElevation: upperRightNeighborHeight);
                }

                CreateCutawaySideFaces(
                    generatedRoot,
                    topPosition,
                    x,
                    y,
                    height);

                if (!isStairsTile &&
                    currentDebrisTiles.Contains(new Vector2Int(x, y)) &&
                    TryGetDebrisSpriteForTile(x, y, height, out Sprite debrisSprite))
                {
                    // Debris sits one tie-break step above its own top face so the overlay
                    // is visible on the tile, but it still remains far below the next
                    // screen-depth bucket, so nearer columns continue to occlude it.
                    CreateTilePart(
                        generatedRoot,
                        debrisSprite,
                        topPosition,
                        $"Debris_{x}_{y}_{height}",
                        x,
                        y,
                        height,
                        DebrisSortBias,
                        IsometricMapElevationElementType.TopOverlay);
                    ConfigureDebrisOverlay(
                        partName: $"Debris_{x}_{y}_{height}",
                        generatedRoot: generatedRoot,
                        elevation: height,
                        leftBlockingElevation: lowerLeftNeighborHeight,
                        rightBlockingElevation: lowerRightNeighborHeight);
                }

                // Treat the terrain as stacked isometric columns. The vertical side faces
                // are still only visible on the two screen-facing edges, but the top-edge
                // cliff shadow should behave like a perimeter outline around each plateau.
                // That means every diamond edge checks the neighboring column that shares
                // that edge and draws a shadow band only when the neighbor is lower. Equal
                // heights therefore merge into one platform with no shadow seam between
                // tiles, while lower surrounding tiles get a continuous border around the
                // raised area.
                if (renderSideFaces)
                {
                    CreateTilePart(
                        generatedRoot,
                        leftSideSprite != null ? leftSideSprite : cachedDefaultLeftSideSprite,
                        GridToWorld(x, y, height),
                        $"Left_{x}_{y}_{height}",
                        x,
                        y,
                        height,
                        0,
                        IsometricMapElevationElementType.SideFace,
                        ref tileOcclusionBounds,
                        ref tileOcclusionSortingOrder,
                        ref tileOcclusionSortingLayerId);

                    CreateTilePart(
                        generatedRoot,
                        rightSideSprite != null ? rightSideSprite : cachedDefaultRightSideSprite,
                        GridToWorld(x, y, height),
                        $"Right_{x}_{y}_{height}",
                        x,
                        y,
                        height,
                        1,
                        IsometricMapElevationElementType.SideFace,
                        ref tileOcclusionBounds,
                        ref tileOcclusionSortingOrder,
                        ref tileOcclusionSortingLayerId);
                }

                if (renderSideFaces && addCliffEdgeShadows)
                {
                    if (upperLeftNeighborHeight < height)
                    {
                        CreateTilePart(
                            generatedRoot,
                            cachedTopLeftEdgeShadowSprite,
                            topPosition,
                            $"TopLeftShadow_{x}_{y}_{height}",
                            x,
                            y,
                            height,
                            3,
                            IsometricMapElevationElementType.Shadow);
                    }

                    if (upperRightNeighborHeight < height)
                    {
                        CreateTilePart(
                            generatedRoot,
                            cachedTopRightEdgeShadowSprite,
                            topPosition,
                            $"TopRightShadow_{x}_{y}_{height}",
                            x,
                            y,
                            height,
                            4,
                            IsometricMapElevationElementType.Shadow);
                    }

                    if (lowerLeftNeighborHeight < height)
                    {
                        CreateTilePart(
                            generatedRoot,
                            cachedBottomLeftEdgeShadowSprite,
                            topPosition,
                            $"BottomLeftShadow_{x}_{y}_{height}",
                            x,
                            y,
                            height,
                            5,
                            IsometricMapElevationElementType.Shadow);
                    }

                    if (lowerRightNeighborHeight < height)
                    {
                        CreateTilePart(
                            generatedRoot,
                            cachedBottomRightEdgeShadowSprite,
                            topPosition,
                            $"BottomRightShadow_{x}_{y}_{height}",
                            x,
                            y,
                            height,
                            6,
                            IsometricMapElevationElementType.Shadow);
                    }
                }

                for (int layer = 1; layer < height; layer++)
                {
                    Vector3 sidePosition = GridToWorld(x, y, layer);
                    string sliceCapName = $"SliceCap_{x}_{y}_{layer}";
                    CreateTilePart(
                        generatedRoot,
                        topSprite != null ? topSprite : cachedDefaultTopSprite,
                        sidePosition,
                        sliceCapName,
                        x,
                        y,
                        layer,
                        TopFaceSortBias,
                        IsometricMapElevationElementType.SliceCap);

                    if (renderSideFaces && lowerLeftNeighborHeight < layer)
                    {
                        CreateTilePart(
                            generatedRoot,
                            leftSideSprite != null ? leftSideSprite : cachedDefaultLeftSideSprite,
                            sidePosition,
                            $"Left_{x}_{y}_{layer}",
                            x,
                            y,
                            layer,
                            0,
                            IsometricMapElevationElementType.SideFace,
                            ref tileOcclusionBounds,
                            ref tileOcclusionSortingOrder,
                            ref tileOcclusionSortingLayerId);
                    }

                    if (renderSideFaces && lowerRightNeighborHeight < layer)
                    {
                        CreateTilePart(
                            generatedRoot,
                            rightSideSprite != null ? rightSideSprite : cachedDefaultRightSideSprite,
                            sidePosition,
                            $"Right_{x}_{y}_{layer}",
                            x,
                            y,
                            layer,
                            1,
                            IsometricMapElevationElementType.SideFace,
                            ref tileOcclusionBounds,
                            ref tileOcclusionSortingOrder,
                            ref tileOcclusionSortingLayerId);
                    }
                }

                if (tileOcclusionBounds.HasValue)
                {
                    occlusionVolumes.Add(new OcclusionVolume(
                        tileOcclusionBounds.Value,
                        tileOcclusionSortingOrder,
                        tileOcclusionSortingLayerId));
                }
            }
        }

        if (autoFrameCamera)
        {
            FrameMainCamera();
        }

        MapGenerated?.Invoke();
    }

    private void ValidateSettings()
    {
        if (maxElevation < minElevation)
        {
            maxElevation = minElevation;
        }

        debrisSettings ??= new DebrisSettings();
        debrisSettings.Sanitize();
        fakeShadowSettings ??= new FakeShadowSettings();
        fakeShadowSettings.Sanitize();
        chestSpawnSettings.Sanitize();
    }

    private Transform CreateGeneratedRoot()
    {
        GameObject root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        return root.transform;
    }

    private void ClearGeneratedMap()
    {
        Transform existingRoot = transform.Find(GeneratedRootName);
        if (existingRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existingRoot.gameObject);
            }
            else
            {
                DestroyImmediate(existingRoot.gameObject);
            }
        }

        List<Transform> attachmentRoots = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith(GeneratedAttachmentRootPrefix, StringComparison.Ordinal))
            {
                attachmentRoots.Add(child);
            }
        }

        for (int i = 0; i < attachmentRoots.Count; i++)
        {
            if (Application.isPlaying)
            {
                Destroy(attachmentRoots[i].gameObject);
            }
            else
            {
                DestroyImmediate(attachmentRoots[i].gameObject);
            }
        }
    }

    private int[,] GenerateHeights()
    {
        int[,] heights = new int[width, length];
        float[,] samples = new float[width, length];
        System.Random random = new System.Random(seed);
        Vector2 seedOffset = new Vector2(random.Next(-100000, 100000), random.Next(-100000, 100000));

        float minSample = float.MaxValue;
        float maxSample = float.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                float sample = SampleFractalNoise(x, y, seedOffset, noiseScale, noiseOctaves, persistence, lacunarity);
                samples[x, y] = sample;
                minSample = Mathf.Min(minSample, sample);
                maxSample = Mathf.Max(maxSample, sample);
            }
        }

        for (int pass = 0; pass < smoothingPasses; pass++)
        {
            samples = SmoothSamples(samples);
        }

        if (Mathf.Approximately(maxSample, minSample))
        {
            maxSample = minSample + 0.0001f;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                float normalized = Mathf.InverseLerp(minSample, maxSample, samples[x, y]);
                heights[x, y] = Mathf.RoundToInt(Mathf.Lerp(minElevation, maxElevation, normalized));
            }
        }

        return heights;
    }

    private float[,] SmoothSamples(float[,] source)
    {
        float[,] smoothed = new float[width, length];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                float total = 0f;
                int count = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int sampleX = x + dx;
                        int sampleY = y + dy;
                        if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= length)
                        {
                            continue;
                        }

                        total += source[sampleX, sampleY];
                        count++;
                    }
                }

                smoothed[x, y] = count > 0 ? total / count : source[x, y];
            }
        }

        return smoothed;
    }

    public bool IsWithinBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < length;
    }

    public int GetTileElevation(int x, int y)
    {
        if (currentHeights == null)
        {
            return 0;
        }

        return GetHeight(currentHeights, x, y);
    }

    public bool IsTraversable(int x, int y)
    {
        return GetTileElevation(x, y) > 0;
    }

    public Transform GetOrCreateGeneratedAttachmentRoot(string rootName)
    {
        string normalizedRootName = string.IsNullOrWhiteSpace(rootName) ? "Objects" : rootName.Trim();
        string objectName = $"{GeneratedAttachmentRootPrefix}{normalizedRootName}";
        Transform root = transform.Find(objectName);
        if (root != null)
        {
            return root;
        }

        GameObject rootObject = new GameObject(objectName);
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        return rootObject.transform;
    }

    public bool TryGetTileWorldPosition(int x, int y, out Vector3 worldPosition)
    {
        int elevation = GetTileElevation(x, y);
        if (elevation <= 0)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = GridToWorld(x, y, elevation);
        return true;
    }

    public Vector3 GridToWorldPosition(int x, int y, int elevation)
    {
        return GridToWorld(x, y, elevation);
    }

    public int GetCharacterSortingOrder(int x, int y, int elevation)
    {
        // Keep characters just above the top face of their current tile while still
        // allowing nearer columns to occlude them.
        return CalculateSortingOrder(x, y, elevation, CharacterSortBias);
    }

    private bool TryGetDebrisSpriteForTile(int x, int y, int elevation, out Sprite debrisSprite)
    {
        debrisSprite = null;
        if (debrisSettings == null)
        {
            return false;
        }

        debrisSettings.Sanitize();
        if (!debrisSettings.IsEnabled)
        {
            return false;
        }

        int spriteIndex = GetDeterministicTileHash(seed + DebrisSeedOffset, x, y, elevation);
        spriteIndex = Mathf.Abs(spriteIndex) % debrisSettings.topSprites.Count;
        debrisSprite = debrisSettings.topSprites[spriteIndex];
        return debrisSprite != null;
    }

    private void CreateDebrisPlacementSet(HashSet<Vector2Int> placementSet)
    {
        if (placementSet == null)
        {
            return;
        }

        placementSet.Clear();
        if (currentHeights == null || debrisSettings == null)
        {
            return;
        }

        debrisSettings.Sanitize();
        if (!debrisSettings.IsEnabled)
        {
            return;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                if (currentHeights[x, y] <= 0)
                {
                    continue;
                }

                Vector2Int tile = new Vector2Int(x, y);
                if (currentStairsTile.HasValue && currentStairsTile.Value == tile)
                {
                    continue;
                }

                candidates.Add(tile);
            }
        }

        int targetCount = Mathf.Clamp(
            Mathf.RoundToInt(candidates.Count * debrisSettings.amount),
            0,
            candidates.Count);
        if (targetCount <= 0)
        {
            return;
        }

        Vector2 seedOffset = CreateNoiseSeedOffset(seed + DebrisSeedOffset);
        float clumpScale = ResolveDebrisClumpScale();
        List<Vector2Int> chosenTiles = new List<Vector2Int>(targetCount);

        while (chosenTiles.Count < targetCount)
        {
            int bestIndex = -1;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int tile = candidates[i];
                if (placementSet.Contains(tile))
                {
                    continue;
                }

                float clumpScore = SampleFractalNoise(
                    tile.x,
                    tile.y,
                    seedOffset,
                    clumpScale,
                    octaves: 1,
                    persistenceValue: 0.5f,
                    lacunarityValue: 2f);
                float spreadScore = CalculateDebrisSpreadScore(tile, chosenTiles);
                float tieBreaker = GetDeterministicTileValue(seed + DebrisSeedOffset, tile.x, tile.y, currentHeights[tile.x, tile.y]) * 0.001f;
                float score = Mathf.Lerp(clumpScore, spreadScore, debrisSettings.dispersion) + tieBreaker;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                break;
            }

            Vector2Int selectedTile = candidates[bestIndex];
            placementSet.Add(selectedTile);
            chosenTiles.Add(selectedTile);
        }
    }

    public Vector2Int GetCenterTile()
    {
        return new Vector2Int(width / 2, length / 2);
    }

    public List<Vector2Int> GetRandomSpawnTiles(int count, IReadOnlyCollection<Vector2Int> blockedTiles = null)
    {
        List<Vector2Int> spawnTiles = new List<Vector2Int>();
        if (count <= 0 || currentHeights == null)
        {
            return spawnTiles;
        }

        spawnPlacementRandom ??= CreateSpawnPlacementRandom();

        HashSet<Vector2Int> blocked = blockedTiles != null
            ? new HashSet<Vector2Int>(blockedTiles)
            : new HashSet<Vector2Int>();
        if (currentStairsTile.HasValue)
        {
            blocked.Add(currentStairsTile.Value);
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                Vector2Int tile = new Vector2Int(x, y);
                if (IsTraversable(x, y) && !blocked.Contains(tile))
                {
                    candidates.Add(tile);
                }
            }
        }

        ShuffleCandidates(candidates, spawnPlacementRandom);

        int spawnCount = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            spawnTiles.Add(candidates[i]);
        }

        return spawnTiles;
    }

    public List<ChestSpawnPlan> CreateChestSpawnPlans(IReadOnlyCollection<Vector2Int> blockedTiles = null)
    {
        List<ChestSpawnPlan> results = new List<ChestSpawnPlan>();
        if (!HasGeneratedMap)
        {
            return results;
        }

        ChestSpawnSettings settings = chestSpawnSettings;
        settings.Sanitize();
        if (!settings.IsEnabled)
        {
            return results;
        }

        HashSet<Vector2Int> blocked = blockedTiles != null
            ? new HashSet<Vector2Int>(blockedTiles)
            : new HashSet<Vector2Int>();

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                Vector2Int tile = new Vector2Int(x, y);
                if (IsTraversable(x, y) && !blocked.Contains(tile))
                {
                    candidates.Add(tile);
                }
            }
        }

        System.Random random = CreateChestPlacementRandom();
        ShuffleCandidates(candidates, random);

        for (int i = 0; i < candidates.Count && results.Count < settings.maxChestCount; i++)
        {
            if (random.NextDouble() > settings.spawnChance)
            {
                continue;
            }

            Vector2Int tile = candidates[i];
            blocked.Add(tile);
            results.Add(new ChestSpawnPlan(
                runtimeChestId: $"chest_{tile.x}_{tile.y}_{results.Count}",
                tile: tile,
                facing: (TacticsChestController.ChestFacing)random.Next(0, 4),
                containsMimic: random.NextDouble() <= settings.mimicChance));
        }

        return results;
    }

    public bool TryCreateStairsSpawnPlan(
        out StairsSpawnPlan spawnPlan,
        IReadOnlyCollection<Vector2Int> blockedTiles = null)
    {
        spawnPlan = default;
        if (!HasGeneratedMap)
        {
            return false;
        }

        HashSet<Vector2Int> blocked = blockedTiles != null
            ? new HashSet<Vector2Int>(blockedTiles)
            : new HashSet<Vector2Int>();
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                Vector2Int tile = new Vector2Int(x, y);
                if (!IsTraversable(x, y) ||
                    blocked.Contains(tile) ||
                    !HasAdjacentInteractionTile(tile, blocked))
                {
                    continue;
                }

                candidates.Add(tile);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        System.Random random = CreateStairsPlacementRandom();
        ShuffleCandidates(candidates, random);
        Vector2Int selectedTile = candidates[0];
        spawnPlan = new StairsSpawnPlan($"stairs_{selectedTile.x}_{selectedTile.y}", selectedTile);
        return true;
    }

    public int RollChestGoldReward()
    {
        ChestSpawnSettings settings = chestSpawnSettings;
        settings.Sanitize();
        if (settings.maxGoldReward <= 0)
        {
            return 0;
        }

        return UnityEngine.Random.Range(settings.minGoldReward, settings.maxGoldReward + 1);
    }

    public List<TacticsInventoryItemSaveData> RollChestItems()
    {
        ChestSpawnSettings settings = chestSpawnSettings;
        settings.Sanitize();
        List<TacticsInventoryItemSaveData> results = new List<TacticsInventoryItemSaveData>();
        if (settings.maxItemDrops <= 0 || settings.itemPool == null || settings.itemPool.Count == 0)
        {
            return results;
        }

        int dropCount = UnityEngine.Random.Range(settings.minItemDrops, settings.maxItemDrops + 1);
        if (dropCount <= 0)
        {
            return results;
        }

        int totalWeight = 0;
        for (int i = 0; i < settings.itemPool.Count; i++)
        {
            totalWeight += Mathf.Max(1, settings.itemPool[i].weight);
        }

        for (int dropIndex = 0; dropIndex < dropCount && totalWeight > 0; dropIndex++)
        {
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            for (int entryIndex = 0; entryIndex < settings.itemPool.Count; entryIndex++)
            {
                TacticsChestItemPoolEntry entry = settings.itemPool[entryIndex];
                cumulative += Mathf.Max(1, entry.weight);
                if (roll >= cumulative)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.itemId) && TacticsItemCatalogResources.TryGetItem(entry.itemId, out _))
                {
                    results.Add(new TacticsInventoryItemSaveData
                    {
                        instanceId = Guid.NewGuid().ToString("N"),
                        itemId = entry.itemId
                    });
                }

                break;
            }
        }

        return results;
    }

    private int GetHeight(int[,] heights, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= length)
        {
            return 0;
        }

        return heights[x, y];
    }

    private int GetMaximumHeight(int[,] heights)
    {
        if (heights == null)
        {
            return 0;
        }

        int maximumHeight = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                maximumHeight = Mathf.Max(maximumHeight, heights[x, y]);
            }
        }

        return maximumHeight;
    }

    private Vector3 GridToWorld(int x, int y, int elevation)
    {
        float worldX = (x - y) * (tileWidth * 0.5f);
        float worldY = (x + y) * (tileHeight * 0.5f) + (elevation * elevationStep);
        return new Vector3(worldX, worldY, 0f) + mapOffset;
    }

    private void CreateTilePart(
        Transform parent,
        Sprite sprite,
        Vector3 position,
        string objectName,
        int gridX,
        int gridY,
        int layer,
        int sortBias,
        IsometricMapElevationElementType elementType,
        ref Bounds? occlusionBounds,
        ref int occlusionSortingOrder,
        ref int occlusionSortingLayerId)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position + GetSpriteOffset(sprite, sortBias);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.sortingOrder = CalculateSortingOrder(gridX, gridY, layer, sortBias);

        IsometricMapElevationElement elevationElement = part.AddComponent<IsometricMapElevationElement>();
        elevationElement.Initialize(layer, elementType, renderer);

        spawnedRenderers.Add(renderer);
        if (occlusionBounds.HasValue)
        {
            Bounds bounds = occlusionBounds.Value;
            bounds.Encapsulate(renderer.bounds);
            occlusionBounds = bounds;
        }
        else
        {
            occlusionBounds = renderer.bounds;
        }

        occlusionSortingOrder = Mathf.Max(occlusionSortingOrder, renderer.sortingOrder);
        occlusionSortingLayerId = renderer.sortingLayerID;
    }

    private void CreateTilePart(
        Transform parent,
        Sprite sprite,
        Vector3 position,
        string objectName,
        int gridX,
        int gridY,
        int layer,
        int sortBias,
        IsometricMapElevationElementType elementType)
    {
        Bounds? ignoredBounds = null;
        int ignoredSortingOrder = int.MinValue;
        int ignoredSortingLayerId = 0;
        CreateTilePart(
            parent,
            sprite,
            position,
            objectName,
            gridX,
            gridY,
            layer,
            sortBias,
            elementType,
            ref ignoredBounds,
            ref ignoredSortingOrder,
            ref ignoredSortingLayerId);
    }

    private Vector3 GetSpriteOffset(Sprite sprite, int sortBias)
    {
        if (sprite == null)
        {
            return Vector3.zero;
        }

        bool isLeftFace = sortBias == 0;
        bool isRightFace = sortBias == 1;
        if (!isLeftFace && !isRightFace)
        {
            return Vector3.zero;
        }

        float pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
        float canvasWidth = sprite.rect.width / pixelsPerUnit;
        float canvasHeight = sprite.rect.height / pixelsPerUnit;

        float expectedFaceWidth = tileWidth * 0.5f;
        float expectedFaceHeight = elevationStep + (tileHeight * 0.5f);
        float widthTolerance = Mathf.Max(0.01f, expectedFaceWidth * 0.15f);
        float heightTolerance = Mathf.Max(0.01f, expectedFaceHeight * 0.15f);
        bool looksLikeTightSideSprite =
            Mathf.Abs(canvasWidth - expectedFaceWidth) <= widthTolerance &&
            Mathf.Abs(canvasHeight - expectedFaceHeight) <= heightTolerance;

        if (!looksLikeTightSideSprite)
        {
            return Vector3.zero;
        }

        float localCenterX = ((sprite.rect.width * 0.5f) - sprite.pivot.x) / pixelsPerUnit;
        float localCenterY = ((sprite.rect.height * 0.5f) - sprite.pivot.y) / pixelsPerUnit;

        float targetCenterX = isLeftFace ? (-tileWidth * 0.25f) : (tileWidth * 0.25f);
        float targetCenterY = -(elevationStep * 0.5f) - (tileHeight * 0.25f);

        return new Vector3(targetCenterX - localCenterX, targetCenterY - localCenterY, 0f);
    }

    private void ConfigureTopTile(
        string partName,
        Transform generatedRoot,
        int gridX,
        int gridY,
        int height,
        bool isStairsTile,
        string runtimeStairsId)
    {
        Transform topTransform = generatedRoot.Find(partName);
        if (topTransform == null)
        {
            return;
        }

        PolygonCollider2D collider = topTransform.gameObject.AddComponent<PolygonCollider2D>();
        collider.points = new[]
        {
            new Vector2(0f, tileHeight * 0.5f),
            new Vector2(tileWidth * 0.5f, 0f),
            new Vector2(0f, -tileHeight * 0.5f),
            new Vector2(-tileWidth * 0.5f, 0f)
        };

        IsometricTileHoverInfo hoverInfo = topTransform.gameObject.AddComponent<IsometricTileHoverInfo>();
        hoverInfo.Initialize(gridX, gridY, height, tileWidth, tileHeight);

        IsometricMapElevationElement elevationElement = topTransform.GetComponent<IsometricMapElevationElement>();
        if (elevationElement != null)
        {
            elevationElement.AttachInteraction(collider, hoverInfo);
        }

        if (isStairsTile)
        {
            TacticsStairsController stairs = topTransform.gameObject.AddComponent<TacticsStairsController>();
            stairs.Initialize(this, runtimeStairsId, new Vector2Int(gridX, gridY));
        }
    }

    private void ConfigureDebrisOverlay(
        string partName,
        Transform generatedRoot,
        int elevation,
        int leftBlockingElevation,
        int rightBlockingElevation)
    {
        Transform debrisTransform = generatedRoot.Find(partName);
        if (debrisTransform == null)
        {
            return;
        }

        SpriteRenderer renderer = debrisTransform.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }

        GameObject maskObject = new GameObject("DebrisMask");
        maskObject.transform.SetParent(debrisTransform, false);
        maskObject.transform.localPosition = Vector3.zero;

        SpriteMask spriteMask = maskObject.AddComponent<SpriteMask>();
        spriteMask.isCustomRangeActive = true;
        spriteMask.frontSortingLayerID = renderer.sortingLayerID;
        spriteMask.backSortingLayerID = renderer.sortingLayerID;
        spriteMask.frontSortingOrder = renderer.sortingOrder;
        spriteMask.backSortingOrder = renderer.sortingOrder;

        IsometricDebrisOverlayController controller = debrisTransform.gameObject.AddComponent<IsometricDebrisOverlayController>();
        controller.Initialize(
            elevation,
            leftBlockingElevation,
            rightBlockingElevation,
            renderer,
            spriteMask,
            cachedLeftVisibleDebrisMaskSprite,
            cachedRightVisibleDebrisMaskSprite);
        controller.ApplyVisibleElevation(maximumGeneratedElevation);
    }

    private void ConfigureFakeShadowOverlay(
        string partName,
        Transform generatedRoot,
        int gridX,
        int gridY,
        int elevation,
        int leftBlockingElevation,
        int rightBlockingElevation,
        int upperLeftBlockingElevation,
        int upperRightBlockingElevation)
    {
        Transform shadowTransform = generatedRoot.Find(partName);
        if (shadowTransform == null)
        {
            return;
        }

        SpriteRenderer renderer = shadowTransform.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }

        IsometricFakeShadowOverlayController controller = shadowTransform.gameObject.AddComponent<IsometricFakeShadowOverlayController>();
        int maxDepth = Mathf.Max(1, (width - 1) + (length - 1) + maximumGeneratedElevation);
        float normalizedDepth = Mathf.Clamp01((gridX + gridY + Mathf.Max(0, elevation - 1)) / (float)maxDepth);
        controller.Initialize(
            normalizedDepth,
            elevation,
            leftBlockingElevation,
            rightBlockingElevation,
            upperLeftBlockingElevation,
            upperRightBlockingElevation,
            renderer,
            fakeShadowSettings);
        controller.ApplyVisibilityContext(maximumGeneratedElevation, maximumGeneratedElevation);
    }

    private void CreateCutawaySideFaces(
        Transform generatedRoot,
        Vector3 topPosition,
        int gridX,
        int gridY,
        int height)
    {
        if (!renderSideFaces)
        {
            return;
        }

        CreateTilePart(
            generatedRoot,
            leftSideSprite != null ? leftSideSprite : cachedDefaultLeftSideSprite,
            topPosition,
            $"CutLeft_{gridX}_{gridY}_{height}",
            gridX,
            gridY,
            height,
            0,
            IsometricMapElevationElementType.CutawaySideFace);

        CreateTilePart(
            generatedRoot,
            rightSideSprite != null ? rightSideSprite : cachedDefaultRightSideSprite,
            topPosition,
            $"CutRight_{gridX}_{gridY}_{height}",
            gridX,
            gridY,
            height,
            1,
            IsometricMapElevationElementType.CutawaySideFace);
    }

    private int CalculateSortingOrder(int gridX, int gridY, int layer, int sortBias)
    {
        // Keep the depth bucket stride much larger than any tie-break contribution so
        // objects on a nearer screen-depth bucket always render in front.
        int maxDepth = width + length + maxElevation + 2;
        int tileDepth = gridX + gridY + layer;
        int depthStride = Mathf.Max(256, ((length + 1) * 10) + ((width + 1) * 2) + 16);
        int backToFrontOrder = (maxDepth - tileDepth) * depthStride;
        int rowTieBreak = (length - gridY) * 10;
        int columnTieBreak = (width - gridX) * 2;
        return backToFrontOrder + rowTieBreak + columnTieBreak + sortBias;
    }

    private static Vector2 CreateNoiseSeedOffset(int sourceSeed)
    {
        System.Random random = new System.Random(sourceSeed);
        return new Vector2(random.Next(-100000, 100000), random.Next(-100000, 100000));
    }

    private static float SampleFractalNoise(
        int x,
        int y,
        Vector2 seedOffset,
        float scale,
        int octaves,
        float persistenceValue,
        float lacunarityValue)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float sample = 0f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            float sampleX = ((x + seedOffset.x) / scale) * frequency;
            float sampleY = ((y + seedOffset.y) / scale) * frequency;
            float perlin = Mathf.PerlinNoise(sampleX, sampleY);
            sample += perlin * amplitude;
            amplitudeSum += amplitude;
            amplitude *= persistenceValue;
            frequency *= lacunarityValue;
        }

        return amplitudeSum > 0f ? sample / amplitudeSum : 0f;
    }

    private float ResolveDebrisClumpScale()
    {
        float mapSpan = Mathf.Max(width, length);
        return Mathf.Max(2f, mapSpan * 0.85f);
    }

    private float CalculateDebrisSpreadScore(Vector2Int tile, IReadOnlyList<Vector2Int> chosenTiles)
    {
        if (chosenTiles == null || chosenTiles.Count == 0)
        {
            return GetDeterministicTileValue(seed + DebrisSeedOffset, tile.x, tile.y, currentHeights[tile.x, tile.y]);
        }

        float nearestDistanceSquared = float.MaxValue;
        for (int i = 0; i < chosenTiles.Count; i++)
        {
            Vector2Int chosenTile = chosenTiles[i];
            int dx = tile.x - chosenTile.x;
            int dy = tile.y - chosenTile.y;
            float distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
            }
        }

        float maxDx = Mathf.Max(1, width - 1);
        float maxDy = Mathf.Max(1, length - 1);
        float maxDistanceSquared = (maxDx * maxDx) + (maxDy * maxDy);
        return Mathf.Clamp01(nearestDistanceSquared / maxDistanceSquared);
    }

    private static int GetDeterministicTileHash(int sourceSeed, int x, int y, int elevation)
    {
        unchecked
        {
            int hash = sourceSeed;
            hash = (hash * 397) ^ x;
            hash = (hash * 397) ^ y;
            hash = (hash * 397) ^ elevation;
            return hash;
        }
    }

    private static float GetDeterministicTileValue(int sourceSeed, int x, int y, int elevation)
    {
        uint hash = unchecked((uint)GetDeterministicTileHash(sourceSeed, x, y, elevation));
        return hash / (float)uint.MaxValue;
    }

    private void FrameMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || spawnedRenderers.Count == 0)
        {
            return;
        }

        Bounds bounds = spawnedRenderers[0].bounds;
        for (int i = 1; i < spawnedRenderers.Count; i++)
        {
            bounds.Encapsulate(spawnedRenderers[i].bounds);
        }

        mainCamera.orthographic = true;
        Vector3 position = mainCamera.transform.position;
        position.x = bounds.center.x;
        position.y = bounds.center.y;
        mainCamera.transform.position = position;

        float verticalSize = bounds.extents.y + cameraPadding;
        float horizontalSize = (bounds.extents.x + cameraPadding) / Mathf.Max(0.01f, mainCamera.aspect);
        mainCamera.orthographicSize = Mathf.Max(verticalSize, horizontalSize, 1f);
    }

    private void EnsureDefaultSprites()
    {
        if (cachedDefaultTopSprite == null)
        {
            cachedDefaultTopSprite = CreateTopSprite();
        }

        if (cachedDefaultLeftSideSprite == null)
        {
            cachedDefaultLeftSideSprite = CreateLeftSideSprite();
        }

        if (cachedDefaultRightSideSprite == null)
        {
            cachedDefaultRightSideSprite = CreateRightSideSprite();
        }

        cachedTopLeftEdgeShadowSprite = CreateTopEdgeShadowSprite(TopShadowEdge.TopLeft);
        cachedTopRightEdgeShadowSprite = CreateTopEdgeShadowSprite(TopShadowEdge.TopRight);
        cachedBottomLeftEdgeShadowSprite = CreateTopEdgeShadowSprite(TopShadowEdge.BottomLeft);
        cachedBottomRightEdgeShadowSprite = CreateTopEdgeShadowSprite(TopShadowEdge.BottomRight);
        cachedFakeShadowFillSprite = CreateFakeShadowFillSprite();
        cachedLeftVisibleDebrisMaskSprite = CreateTopHalfMaskSprite(TopHalf.Left);
        cachedRightVisibleDebrisMaskSprite = CreateTopHalfMaskSprite(TopHalf.Right);
        cachedStairsTopSprite = cachedStairsTopSprite != null
            ? cachedStairsTopSprite
            : Resources.Load<Sprite>("Sprites/stairs");
    }

    private Sprite GetResolvedStairsTopSprite()
    {
        return cachedStairsTopSprite != null ? cachedStairsTopSprite : (topSprite != null ? topSprite : cachedDefaultTopSprite);
    }


    private enum TopShadowEdge
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private enum TopHalf
    {
        Left,
        Right
    }

    private Sprite CreateTopSprite()
    {
        Texture2D texture = CreateTransparentTexture();
        Vector2[] diamond =
        {
            new Vector2(64f, 96f),
            new Vector2(127f, 64f),
            new Vector2(64f, 32f),
            new Vector2(0f, 64f)
        };

        FillPolygon(texture, diamond, defaultTopColor);
        return FinalizeSprite(texture, "Default_Isometric_Top");
    }

    private Sprite CreateLeftSideSprite()
    {
        Texture2D texture = CreateTransparentTexture(128, 192);
        Vector2[] leftFace =
        {
            new Vector2(0f, 96f),
            new Vector2(64f, 64f),
            new Vector2(64f, 0f),
            new Vector2(0f, 32f)
        };

        FillPolygon(texture, leftFace, defaultLeftSideColor);
        StrokePolygon(texture, leftFace, defaultOutlineColor);
        return FinalizeSprite(texture, "Default_Isometric_Left");
    }

    private Sprite CreateRightSideSprite()
    {
        Texture2D texture = CreateTransparentTexture(128, 192);
        Vector2[] rightFace =
        {
            new Vector2(64f, 64f),
            new Vector2(127f, 96f),
            new Vector2(127f, 32f),
            new Vector2(64f, 0f)
        };

        FillPolygon(texture, rightFace, defaultRightSideColor);
        StrokePolygon(texture, rightFace, defaultOutlineColor);
        return FinalizeSprite(texture, "Default_Isometric_Right");
    }

    private Sprite CreateTopEdgeShadowSprite(TopShadowEdge edge)
    {
        Texture2D texture = CreateTransparentTexture();
        Vector2 center = new Vector2(64f, 64f);
        float inset = Mathf.Clamp01(cliffShadowWidth);

        Vector2 edgeStart;
        Vector2 edgeEnd;
        string spriteName;

        switch (edge)
        {
            case TopShadowEdge.TopLeft:
                edgeStart = new Vector2(64f, 96f);
                edgeEnd = new Vector2(0f, 64f);
                spriteName = "Default_Isometric_TopLeftShadow";
                break;
            case TopShadowEdge.TopRight:
                edgeStart = new Vector2(127f, 64f);
                edgeEnd = new Vector2(64f, 96f);
                spriteName = "Default_Isometric_TopRightShadow";
                break;
            case TopShadowEdge.BottomLeft:
                edgeStart = new Vector2(0f, 64f);
                edgeEnd = new Vector2(64f, 32f);
                spriteName = "Default_Isometric_BottomLeftShadow";
                break;
            default:
                edgeStart = new Vector2(64f, 32f);
                edgeEnd = new Vector2(127f, 64f);
                spriteName = "Default_Isometric_BottomRightShadow";
                break;
        }

        Vector2 innerEnd = Vector2.Lerp(edgeEnd, center, inset);
        Vector2 innerStart = Vector2.Lerp(edgeStart, center, inset);

        Vector2[] shadowBand =
        {
            edgeStart,
            edgeEnd,
            innerEnd,
            innerStart
        };

        FillPolygon(texture, shadowBand, cliffShadowColor);
        return FinalizeSprite(texture, spriteName);
    }

    private Sprite CreateFakeShadowFillSprite()
    {
        Texture2D texture = CreateTransparentTexture();
        Vector2[] diamond =
        {
            new Vector2(64f, 96f),
            new Vector2(127f, 64f),
            new Vector2(64f, 32f),
            new Vector2(0f, 64f)
        };

        FillPolygon(texture, diamond, Color.white);
        return FinalizeSprite(texture, "Default_Isometric_FakeShadowFill");
    }

    private Sprite CreateTopHalfMaskSprite(TopHalf half)
    {
        Texture2D texture = CreateTransparentTexture();
        Vector2[] polygon = half == TopHalf.Left
            ? new[]
            {
                new Vector2(64f, 96f),
                new Vector2(64f, 32f),
                new Vector2(0f, 64f)
            }
            : new[]
            {
                new Vector2(64f, 96f),
                new Vector2(127f, 64f),
                new Vector2(64f, 32f)
            };

        FillPolygon(texture, polygon, Color.white);
        return FinalizeSprite(texture, half == TopHalf.Left ? "DebrisMask_LeftVisible" : "DebrisMask_RightVisible");
    }

    private Texture2D CreateTransparentTexture()
    {
        return CreateTransparentTexture(DefaultSpritePixels, DefaultSpritePixels);
    }

    private Texture2D CreateTransparentTexture(int widthPixels, int heightPixels)
    {
        Texture2D texture = new Texture2D(widthPixels, heightPixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Generated_Isometric_Texture"
        };

        Color[] pixels = new Color[widthPixels * heightPixels];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        texture.SetPixels(pixels);
        return texture;
    }

    private Sprite FinalizeSprite(Texture2D texture, string spriteName)
    {
        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            DefaultPixelsPerUnit);
        sprite.name = spriteName;
        return sprite;
    }

    private void FillPolygon(Texture2D texture, IReadOnlyList<Vector2> points, Color fillColor)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(GetMin(points, point => point.x)), 0, texture.width - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(GetMax(points, point => point.x)), 0, texture.width - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(GetMin(points, point => point.y)), 0, texture.height - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(GetMax(points, point => point.y)), 0, texture.height - 1);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (IsPointInsidePolygon(new Vector2(x + 0.5f, y + 0.5f), points))
                {
                    texture.SetPixel(x, y, fillColor);
                }
            }
        }
    }

    private void StrokePolygon(Texture2D texture, IReadOnlyList<Vector2> points, Color strokeColor)
    {
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Count];
            DrawLine(texture, start, end, strokeColor);
        }
    }

    private void DrawLine(Texture2D texture, Vector2 start, Vector2 end, Color color)
    {
        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);

        int deltaX = Mathf.Abs(x1 - x0);
        int deltaY = Mathf.Abs(y1 - y0);
        int stepX = x0 < x1 ? 1 : -1;
        int stepY = y0 < y1 ? 1 : -1;
        int error = deltaX - deltaY;

        while (true)
        {
            if (x0 >= 0 && x0 < texture.width && y0 >= 0 && y0 < texture.height)
            {
                texture.SetPixel(x0, y0, color);
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int doubledError = error * 2;
            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x0 += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    private bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool isInside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            bool crosses = ((a.y > point.y) != (b.y > point.y)) &&
                           (point.x < ((b.x - a.x) * (point.y - a.y) / ((b.y - a.y) + 0.0001f)) + a.x);
            if (crosses)
            {
                isInside = !isInside;
            }
        }

        return isInside;
    }

    private float GetMin(IReadOnlyList<Vector2> points, Func<Vector2, float> selector)
    {
        float value = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            value = Mathf.Min(value, selector(points[i]));
        }

        return value;
    }

    private float GetMax(IReadOnlyList<Vector2> points, Func<Vector2, float> selector)
    {
        float value = float.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            value = Mathf.Max(value, selector(points[i]));
        }

        return value;
    }

    private System.Random CreateSpawnPlacementRandom()
    {
        int mapHash = seed;
        mapHash = (mapHash * 397) ^ width;
        mapHash = (mapHash * 397) ^ length;
        mapHash = (mapHash * 397) ^ minElevation;
        mapHash = (mapHash * 397) ^ maxElevation;
        return new System.Random(mapHash);
    }

    private System.Random CreateChestPlacementRandom()
    {
        ChestSpawnSettings settings = chestSpawnSettings;
        settings.Sanitize();

        int chestHash = seed;
        chestHash = (chestHash * 397) ^ width;
        chestHash = (chestHash * 397) ^ length;
        chestHash = (chestHash * 397) ^ Mathf.RoundToInt(settings.spawnChance * 1000f);
        chestHash = (chestHash * 397) ^ Mathf.RoundToInt(settings.mimicChance * 1000f);
        chestHash = (chestHash * 397) ^ settings.maxChestCount;
        chestHash = (chestHash * 397) ^ settings.minGoldReward;
        chestHash = (chestHash * 397) ^ settings.maxGoldReward;
        return new System.Random(chestHash);
    }

    private System.Random CreateStairsPlacementRandom()
    {
        int stairsHash = seed;
        stairsHash = (stairsHash * 397) ^ width;
        stairsHash = (stairsHash * 397) ^ length;
        stairsHash = (stairsHash * 397) ^ minElevation;
        stairsHash = (stairsHash * 397) ^ maxElevation;
        stairsHash = (stairsHash * 397) ^ 1789;
        return new System.Random(stairsHash);
    }

    private bool HasAdjacentInteractionTile(Vector2Int tile, IReadOnlyCollection<Vector2Int> blockedTiles)
    {
        int sourceElevation = GetTileElevation(tile.x, tile.y);
        if (sourceElevation <= 0)
        {
            return false;
        }

        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            Vector2Int neighbor = tile + NeighborOffsets[i];
            if (!IsWithinBounds(neighbor.x, neighbor.y) ||
                !IsTraversable(neighbor.x, neighbor.y) ||
                GetTileElevation(neighbor.x, neighbor.y) != sourceElevation ||
                IsTileReserved(neighbor, blockedTiles))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsTileReserved(Vector2Int tile, IReadOnlyCollection<Vector2Int> blockedTiles)
    {
        if (blockedTiles == null)
        {
            return false;
        }

        foreach (Vector2Int blockedTile in blockedTiles)
        {
            if (blockedTile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private static void ShuffleCandidates(List<Vector2Int> candidates, System.Random random)
    {
        if (candidates == null || random == null)
        {
            return;
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
        }
    }
}
