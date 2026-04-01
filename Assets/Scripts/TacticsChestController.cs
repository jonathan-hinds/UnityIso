using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class TacticsChestController : MonoBehaviour, ITacticsCombatTextAnchor, ITacticsTileBlocker
{
    public enum ChestFacing
    {
        SouthWest = 0,
        SouthEast = 1,
        NorthWest = 2,
        NorthEast = 3
    }

    private sealed class ChestSpriteLibrary
    {
        public Sprite closedSouthWest;
        public Sprite openSouthWest;
        public Sprite closedNorthWest;
        public Sprite openNorthWest;
        public bool IsValid =>
            closedSouthWest != null &&
            openSouthWest != null &&
            closedNorthWest != null &&
            openNorthWest != null;
    }

    private const string ChestSpriteResourcePath = "Sprites/chest3";
    private const string VisualObjectName = "Visual";
    private const string OcclusionOverlayObjectName = "OcclusionOverlay";
    private static ChestSpriteLibrary cachedSpriteLibrary;

    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private BoxCollider2D interactionCollider;
    [SerializeField] private Vector2 tileAnchorOffset = new Vector2(0f, 0.17f);
    [SerializeField] private Color occlusionHighlightColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField, Min(1)] private int occlusionSortingOrderOffset = 1;
    [SerializeField, Min(0f)] private float occlusionOverlapPadding = 0.01f;
    [SerializeField, Min(0f)] private float occlusionBoundsPadding = 0.04f;
    [SerializeField, Min(1)] private int occlusionFramesToShow = 2;
    [SerializeField, Min(0f)] private float occlusionHideDelay = 0.08f;

    private SpriteRenderer occlusionOverlayRenderer;
    private bool isPresentationVisible = true;
    private int occlusionDetectedFrameCount;
    private float occlusionHideTimer;
    private int activeOcclusionSortingOrder;
    private Vector2 lastAppliedTileAnchorOffset;
    private bool blocksTile = true;

    public string RuntimeChestId { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public ChestFacing Facing { get; private set; }
    public bool IsOpened { get; private set; }
    public bool ContainsMimic { get; private set; }
    public bool BlocksTile => blocksTile;
    public int CurrentElevation => mapGenerator != null ? mapGenerator.GetTileElevation(GridPosition.x, GridPosition.y) : 0;
    public int CurrentSortingOrder => sortingGroup != null ? sortingGroup.sortingOrder : (spriteRenderer != null ? spriteRenderer.sortingOrder : 0);

    public event Action<TacticsChestController> ChestOpened;

    public void Initialize(
        ProceduralIsometricMapGenerator generator,
        string runtimeChestId,
        Vector2Int gridPosition,
        ChestFacing facing,
        bool opened,
        bool containsMimic = false)
    {
        mapGenerator = generator;
        RuntimeChestId = string.IsNullOrWhiteSpace(runtimeChestId) ? $"chest_{gridPosition.x}_{gridPosition.y}" : runtimeChestId.Trim();
        GridPosition = gridPosition;
        Facing = facing;
        IsOpened = opened;
        ContainsMimic = containsMimic;
        blocksTile = !opened;

        EnsureComponents();
        ApplyVisualState();
        SnapToTile();
        TacticsTileBlockerRegistry.Refresh(this);
    }

    private void Awake()
    {
        EnsureComponents();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        TacticsTileBlockerRegistry.Register(this);
    }

    private void OnDisable()
    {
        TacticsTileBlockerRegistry.Unregister(this);
    }

    private void LateUpdate()
    {
        if (!isPresentationVisible || spriteRenderer == null)
        {
            HideOcclusionOverlay();
            ResetOcclusionState();
            return;
        }

        mapGenerator ??= FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            HideOcclusionOverlay();
            ResetOcclusionState();
            return;
        }

        IReadOnlyList<ProceduralIsometricMapGenerator.OcclusionVolume> occluders = mapGenerator.OcclusionVolumes;
        if (occluders == null || occluders.Count == 0)
        {
            HideOcclusionOverlay();
            ResetOcclusionState();
            return;
        }

        SyncOcclusionOverlayVisual();

        Bounds chestBounds = spriteRenderer.bounds;
        chestBounds.Expand(new Vector3(
            occlusionOverlapPadding + occlusionBoundsPadding,
            occlusionOverlapPadding + occlusionBoundsPadding,
            0f));

        bool isOccluded = false;
        int sortingLayerId = GetCombatTextSortingLayerId();
        int sortingOrder = GetCombatTextSortingOrder();
        int highestOccluderSortingOrder = sortingOrder;

        for (int i = 0; i < occluders.Count; i++)
        {
            ProceduralIsometricMapGenerator.OcclusionVolume occluder = occluders[i];
            if (occluder.SortingLayerId != sortingLayerId || occluder.SortingOrder <= sortingOrder)
            {
                continue;
            }

            if (!occluder.Bounds.Intersects(chestBounds))
            {
                continue;
            }

            isOccluded = true;
            highestOccluderSortingOrder = Mathf.Max(highestOccluderSortingOrder, occluder.SortingOrder);
        }

        if (!UpdateOcclusionState(isOccluded, highestOccluderSortingOrder))
        {
            HideOcclusionOverlay();
            return;
        }

        occlusionOverlayRenderer.sortingLayerID = sortingLayerId;
        occlusionOverlayRenderer.sortingOrder = activeOcclusionSortingOrder + occlusionSortingOrderOffset;
        occlusionOverlayRenderer.enabled = true;
    }

    private void Update()
    {
        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return;
        }

        if (lastAppliedTileAnchorOffset != tileAnchorOffset)
        {
            SnapToTile();
        }
    }

    public bool IsAdjacentAndInteractable(TacticsCharacterController character)
    {
        if (character == null || IsOpened || !character.IsAlive)
        {
            return false;
        }

        return Mathf.Abs(character.GridPosition.x - GridPosition.x) +
               Mathf.Abs(character.GridPosition.y - GridPosition.y) == 1 &&
               character.CurrentElevation == CurrentElevation;
    }

    public bool TryOpen(TacticsCharacterController opener, int goldReward)
    {
        if (IsOpened || !IsAdjacentAndInteractable(opener))
        {
            return false;
        }

        IsOpened = true;
        ApplyVisualState();
        ChestOpened?.Invoke(this);

        if (goldReward > 0)
        {
            TacticsCombatTextSystem.ShowGoldReward(this, goldReward);
        }

        return true;
    }

    public bool TryRevealMimic(TacticsCharacterController opener)
    {
        if (!ContainsMimic || IsOpened || !IsAdjacentAndInteractable(opener))
        {
            return false;
        }

        IsOpened = true;
        blocksTile = false;
        ApplyVisualState();
        TacticsTileBlockerRegistry.Refresh(this);
        ChestOpened?.Invoke(this);
        SetPresentationVisible(false);
        return true;
    }

    public void SetPresentationVisible(bool isVisible)
    {
        isPresentationVisible = isVisible;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = isVisible;
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = isVisible;
        }

        if (!isVisible)
        {
            HideOcclusionOverlay();
            ResetOcclusionState();
        }
    }

    public Vector3 GetCombatTextSpawnPosition(float verticalPadding = 0.18f)
    {
        if (spriteRenderer != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + Mathf.Max(verticalPadding, bounds.size.y * 0.12f), 0f);
        }

        return transform.position + new Vector3(0f, 0.75f + verticalPadding, 0f);
    }

    public int GetCombatTextSortingLayerId()
    {
        return sortingGroup != null ? sortingGroup.sortingLayerID : (spriteRenderer != null ? spriteRenderer.sortingLayerID : SortingLayer.NameToID("Default"));
    }

    public int GetCombatTextSortingOrder()
    {
        return sortingGroup != null ? sortingGroup.sortingOrder : (spriteRenderer != null ? spriteRenderer.sortingOrder : 0);
    }

    public static TacticsChestController FindByRuntimeId(string runtimeChestId)
    {
        if (string.IsNullOrWhiteSpace(runtimeChestId))
        {
            return null;
        }

        TacticsChestController[] chests = FindObjectsByType<TacticsChestController>(FindObjectsSortMode.None);
        for (int i = 0; i < chests.Length; i++)
        {
            TacticsChestController chest = chests[i];
            if (chest != null &&
                string.Equals(chest.RuntimeChestId, runtimeChestId, StringComparison.OrdinalIgnoreCase))
            {
                return chest;
            }
        }

        return null;
    }

    public static bool IsBlockingTile(Vector2Int tile)
    {
        return TacticsTileBlockerUtility.IsBlockingTile(tile);
    }

    public static TacticsChestController FindBestAdjacentClosedChest(TacticsCharacterController character)
    {
        if (character == null)
        {
            return null;
        }

        TacticsChestController[] chests = FindObjectsByType<TacticsChestController>(FindObjectsSortMode.None);
        TacticsChestController bestChest = null;
        int bestSortingOrder = int.MinValue;

        for (int i = 0; i < chests.Length; i++)
        {
            TacticsChestController chest = chests[i];
            if (chest == null || !chest.IsAdjacentAndInteractable(character))
            {
                continue;
            }

            int sortingOrder = chest.CurrentSortingOrder;
            if (bestChest == null || sortingOrder > bestSortingOrder)
            {
                bestChest = chest;
                bestSortingOrder = sortingOrder;
            }
        }

        return bestChest;
    }

    private void EnsureComponents()
    {
        EnsureSpriteLibraryLoaded();

        Transform visualTransform = transform.Find(VisualObjectName);
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject(VisualObjectName);
            visualObject.transform.SetParent(transform, false);
            visualTransform = visualObject.transform;
        }

        spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sortingLayerName = "Default";
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        sortingGroup = visualTransform.GetComponent<SortingGroup>();
        if (sortingGroup == null)
        {
            sortingGroup = visualTransform.gameObject.AddComponent<SortingGroup>();
        }

        interactionCollider = visualTransform.GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
        {
            interactionCollider = visualTransform.gameObject.AddComponent<BoxCollider2D>();
        }

        if (occlusionOverlayRenderer == null)
        {
            GameObject overlayObject = transform.Find(OcclusionOverlayObjectName)?.gameObject;
            if (overlayObject == null)
            {
                overlayObject = new GameObject(OcclusionOverlayObjectName);
                overlayObject.transform.SetParent(transform, false);
            }

            occlusionOverlayRenderer = overlayObject.GetComponent<SpriteRenderer>();
            if (occlusionOverlayRenderer == null)
            {
                occlusionOverlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
            }

            occlusionOverlayRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            occlusionOverlayRenderer.color = occlusionHighlightColor;
            occlusionOverlayRenderer.enabled = false;
        }
    }

    private void ApplyVisualState()
    {
        if (spriteRenderer == null || cachedSpriteLibrary == null || !cachedSpriteLibrary.IsValid)
        {
            return;
        }

        spriteRenderer.flipX = Facing is ChestFacing.SouthEast or ChestFacing.NorthEast;
        spriteRenderer.sprite = ResolveSprite();
        spriteRenderer.color = Color.white;

        if (interactionCollider != null && spriteRenderer.sprite != null)
        {
            interactionCollider.size = spriteRenderer.sprite.bounds.size;
            interactionCollider.offset = Vector2.zero;
        }

        SyncOcclusionOverlayVisual();
    }

    private Sprite ResolveSprite()
    {
        bool useNorthVariant = Facing is ChestFacing.NorthWest or ChestFacing.NorthEast;
        if (IsOpened)
        {
            return useNorthVariant ? cachedSpriteLibrary.openNorthWest : cachedSpriteLibrary.openSouthWest;
        }

        return useNorthVariant ? cachedSpriteLibrary.closedNorthWest : cachedSpriteLibrary.closedSouthWest;
    }

    private void SnapToTile()
    {
        if (mapGenerator == null || !mapGenerator.TryGetTileWorldPosition(GridPosition.x, GridPosition.y, out Vector3 worldPosition))
        {
            return;
        }

        transform.position = worldPosition + new Vector3(tileAnchorOffset.x, tileAnchorOffset.y, 0f);
        lastAppliedTileAnchorOffset = tileAnchorOffset;
        int sortingOrder = mapGenerator.GetCharacterSortingOrder(GridPosition.x, GridPosition.y, CurrentElevation);
        sortingGroup.sortingLayerName = "Default";
        sortingGroup.sortingOrder = sortingOrder;
    }

    private void SyncOcclusionOverlayVisual()
    {
        if (occlusionOverlayRenderer == null || spriteRenderer == null)
        {
            return;
        }

        occlusionOverlayRenderer.sprite = spriteRenderer.sprite;
        occlusionOverlayRenderer.flipX = spriteRenderer.flipX;
        occlusionOverlayRenderer.flipY = spriteRenderer.flipY;
        occlusionOverlayRenderer.drawMode = spriteRenderer.drawMode;
        occlusionOverlayRenderer.size = spriteRenderer.size;
        occlusionOverlayRenderer.color = occlusionHighlightColor;
        occlusionOverlayRenderer.maskInteraction = spriteRenderer.maskInteraction;
        occlusionOverlayRenderer.transform.localPosition = spriteRenderer.transform.localPosition;
        occlusionOverlayRenderer.transform.localRotation = spriteRenderer.transform.localRotation;
        occlusionOverlayRenderer.transform.localScale = spriteRenderer.transform.localScale;
    }

    private bool UpdateOcclusionState(bool isOccluded, int highestOccluderSortingOrder)
    {
        if (isOccluded)
        {
            occlusionDetectedFrameCount = Mathf.Min(occlusionFramesToShow, occlusionDetectedFrameCount + 1);
            occlusionHideTimer = occlusionHideDelay;
            activeOcclusionSortingOrder = highestOccluderSortingOrder;
        }
        else if (occlusionHideTimer > 0f)
        {
            occlusionHideTimer = Mathf.Max(0f, occlusionHideTimer - Time.deltaTime);
        }
        else
        {
            occlusionDetectedFrameCount = 0;
        }

        return occlusionDetectedFrameCount >= occlusionFramesToShow || occlusionHideTimer > 0f;
    }

    private void ResetOcclusionState()
    {
        occlusionDetectedFrameCount = 0;
        occlusionHideTimer = 0f;
        activeOcclusionSortingOrder = GetCombatTextSortingOrder();
    }

    private void HideOcclusionOverlay()
    {
        if (occlusionOverlayRenderer != null)
        {
            occlusionOverlayRenderer.enabled = false;
        }
    }

    private static void EnsureSpriteLibraryLoaded()
    {
        if (cachedSpriteLibrary != null && cachedSpriteLibrary.IsValid)
        {
            return;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(ChestSpriteResourcePath);
        cachedSpriteLibrary = new ChestSpriteLibrary();

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
            {
                continue;
            }

            switch (sprite.name)
            {
                case "chest3_0":
                    cachedSpriteLibrary.closedSouthWest = sprite;
                    break;
                case "chest3_1":
                    cachedSpriteLibrary.openSouthWest = sprite;
                    break;
                case "chest3_2":
                    cachedSpriteLibrary.closedNorthWest = sprite;
                    break;
                case "chest3_3":
                    cachedSpriteLibrary.openNorthWest = sprite;
                    break;
            }
        }
    }
}
