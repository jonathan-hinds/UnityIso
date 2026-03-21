using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsCharacterAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float walkFramesPerSecond = 7f;
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color playerTurnColor = new Color(1f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color enemyTurnColor = new Color(1f, 0.38f, 0.38f, 1f);

    [Header("Occlusion Highlight")]
    [SerializeField] private Color occlusionHighlightColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField, Min(1)] private int occlusionSortingOrderOffset = 1;
    [SerializeField, Min(0f)] private float occlusionOverlapPadding = 0.01f;
    [SerializeField, Min(0f)] private float occlusionBoundsPadding = 0.04f;
    [SerializeField, Min(1)] private int occlusionFramesToShow = 2;
    [SerializeField, Min(0f)] private float occlusionHideDelay = 0.08f;

    [Header("Sprites")]
    [SerializeField] private Sprite walkSouthWestA;
    [SerializeField] private Sprite walkSouthWestB;
    [SerializeField] private Sprite walkSouthWestC;
    [SerializeField] private Sprite walkNorthWestA;
    [SerializeField] private Sprite walkNorthWestB;
    [SerializeField] private Sprite walkNorthWestC;
    [SerializeField] private Sprite jumpSouthWest;
    [SerializeField] private Sprite jumpNorthWest;
    [SerializeField] private Sprite idleSouthWest;
    [SerializeField] private Sprite idleNorthWest;

    private TacticsMovementDirection currentDirection = TacticsMovementDirection.SouthWest;
    private float walkFrameTime;
    private Vector2 sourceFrameSizeUnits;
    private Vector2 sourceFrameSizePixels;
    private TacticsCharacterDefinition characterDefinition;
    private SpriteRenderer occlusionOverlayRenderer;
    private bool isTurnHighlighted;
    private int occlusionDetectedFrameCount;
    private float occlusionHideTimer;
    private int activeOcclusionSortingOrder;

    public SpriteRenderer TargetRenderer => targetRenderer;

    public void Initialize(SpriteRenderer spriteRenderer, TacticsCharacterDefinition definition, ProceduralIsometricMapGenerator generator = null)
    {
        targetRenderer = spriteRenderer;
        characterDefinition = definition;
        mapGenerator = generator;
        EnsureOcclusionOverlayRenderer();

        if (characterDefinition != null)
        {
            walkFramesPerSecond = characterDefinition.WalkFramesPerSecond;
        }

        if (characterDefinition == null || !characterDefinition.TryGetOrderedSprites(out IReadOnlyList<Sprite> sprites))
        {
            Debug.LogWarning($"TacticsCharacterAnimator could not resolve sprites for '{name}'.");
            return;
        }

        AssignSprites(sprites);
        UpdateRendererColor();
        SetIdle(currentDirection);
        ResetOcclusionState();
        HideOcclusionOverlay();
    }

    private void Awake()
    {
        EnsureOcclusionOverlayRenderer();
        ResetOcclusionState();
        HideOcclusionOverlay();
    }

    private void LateUpdate()
    {
        if (targetRenderer == null)
        {
            ResetOcclusionState();
            HideOcclusionOverlay();
            return;
        }

        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        }

        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            ResetOcclusionState();
            HideOcclusionOverlay();
            return;
        }

        SyncOcclusionOverlayVisual();

        IReadOnlyList<ProceduralIsometricMapGenerator.OcclusionVolume> occluders = mapGenerator.OcclusionVolumes;
        if (occluders == null || occluders.Count == 0)
        {
            ResetOcclusionState();
            HideOcclusionOverlay();
            return;
        }

        Bounds characterBounds = targetRenderer.bounds;
        characterBounds.Expand(new Vector3(
            occlusionOverlapPadding + occlusionBoundsPadding,
            occlusionOverlapPadding + occlusionBoundsPadding,
            0f));

        bool isOccluded = false;
        int highestOccluderSortingOrder = targetRenderer.sortingOrder;
        for (int i = 0; i < occluders.Count; i++)
        {
            ProceduralIsometricMapGenerator.OcclusionVolume occluder = occluders[i];
            if (occluder.SortingLayerId != targetRenderer.sortingLayerID ||
                occluder.SortingOrder <= targetRenderer.sortingOrder)
            {
                continue;
            }

            if (!occluder.Bounds.Intersects(characterBounds))
            {
                continue;
            }

            isOccluded = true;
            highestOccluderSortingOrder = Mathf.Max(highestOccluderSortingOrder, occluder.SortingOrder);
        }

        bool shouldDisplayOverlay = UpdateOcclusionState(isOccluded, highestOccluderSortingOrder);
        if (!shouldDisplayOverlay)
        {
            HideOcclusionOverlay();
            return;
        }

        occlusionOverlayRenderer.sortingLayerID = targetRenderer.sortingLayerID;
        occlusionOverlayRenderer.sortingOrder = activeOcclusionSortingOrder + occlusionSortingOrderOffset;
        occlusionOverlayRenderer.enabled = true;
    }

    public void SetSelected(bool isSelected)
    {
        UpdateRendererColor();
    }

    public void SetTurnHighlight(bool isActiveTurn)
    {
        isTurnHighlighted = isActiveTurn;
        UpdateRendererColor();
    }

    public void SetIdle(TacticsMovementDirection direction)
    {
        currentDirection = direction;
        ApplySprite(GetIdleSprite(direction), IsEastFacing(direction));
    }

    public void SetWalk(TacticsMovementDirection direction, float deltaTime)
    {
        currentDirection = direction;
        walkFrameTime += deltaTime * walkFramesPerSecond;

        Sprite[] frames = GetWalkFrames(direction);
        int frameIndex = Mathf.FloorToInt(walkFrameTime) % frames.Length;
        ApplySprite(frames[frameIndex], IsEastFacing(direction));
    }

    public void SetJump(TacticsMovementDirection direction)
    {
        currentDirection = direction;
        ApplySprite(GetJumpSprite(direction), IsEastFacing(direction));
    }

    public void ResetWalkCycle()
    {
        walkFrameTime = 0f;
    }

    private void AssignSprites(IReadOnlyList<Sprite> sprites)
    {
        if (sprites == null || sprites.Count < 10)
        {
            Debug.LogWarning("TacticsCharacterAnimator requires 10 sliced sprites.");
            return;
        }

        walkSouthWestA = sprites[0];
        walkSouthWestB = sprites[1];
        walkSouthWestC = sprites[2];
        walkNorthWestA = sprites[3];
        walkNorthWestB = sprites[4];
        walkNorthWestC = sprites[5];
        jumpSouthWest = sprites[6];
        jumpNorthWest = sprites[7];
        idleSouthWest = sprites[8];
        idleNorthWest = sprites[9];
        sourceFrameSizePixels = InferSourceFrameSizePixels(sprites[0]);
        float pixelsPerUnit = Mathf.Max(0.0001f, sprites[0].pixelsPerUnit);
        sourceFrameSizeUnits = sourceFrameSizePixels / pixelsPerUnit;
    }

    private Sprite[] GetWalkFrames(TacticsMovementDirection direction)
    {
        if (direction == TacticsMovementDirection.NorthWest || direction == TacticsMovementDirection.NorthEast)
        {
            return new[] { walkNorthWestA, walkNorthWestB, walkNorthWestC };
        }

        return new[] { walkSouthWestA, walkSouthWestB, walkSouthWestC };
    }

    private Sprite GetJumpSprite(TacticsMovementDirection direction)
    {
        if (direction == TacticsMovementDirection.NorthWest || direction == TacticsMovementDirection.NorthEast)
        {
            return jumpNorthWest;
        }

        return jumpSouthWest;
    }

    private Sprite GetIdleSprite(TacticsMovementDirection direction)
    {
        if (direction == TacticsMovementDirection.NorthWest || direction == TacticsMovementDirection.NorthEast)
        {
            return idleNorthWest;
        }

        return idleSouthWest;
    }

    private bool IsEastFacing(TacticsMovementDirection direction)
    {
        return direction == TacticsMovementDirection.SouthEast || direction == TacticsMovementDirection.NorthEast;
    }

    private void ApplySprite(Sprite sprite, bool flipX)
    {
        if (targetRenderer == null || sprite == null)
        {
            return;
        }

        targetRenderer.sprite = sprite;
        targetRenderer.flipX = flipX;

        Vector2 anchorSize = sourceFrameSizeUnits == Vector2.zero ? sprite.bounds.size : sourceFrameSizeUnits;
        Vector2 trimOffsetUnits = GetTrimOffsetUnits(sprite);
        Vector3 localPosition = new Vector3(
            -(anchorSize.x * 0.5f) + trimOffsetUnits.x,
            trimOffsetUnits.y,
            0f);
        targetRenderer.transform.localPosition = SnapToSpritePixelGrid(localPosition, sprite);
    }

    private void UpdateRendererColor()
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (!isTurnHighlighted)
        {
            targetRenderer.color = neutralColor;
            return;
        }

        bool isEnemy = characterDefinition != null && characterDefinition.Team == TacticsUnitTeam.Enemy;
        targetRenderer.color = isEnemy ? enemyTurnColor : playerTurnColor;
    }

    private Vector3 SnapToSpritePixelGrid(Vector3 localPosition, Sprite sprite)
    {
        if (sprite == null)
        {
            return localPosition;
        }

        float pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
        float pixelStep = 1f / pixelsPerUnit;

        localPosition.x = Mathf.Round(localPosition.x / pixelStep) * pixelStep;
        localPosition.y = Mathf.Round(localPosition.y / pixelStep) * pixelStep;
        return localPosition;
    }

    private void EnsureOcclusionOverlayRenderer()
    {
        if (occlusionOverlayRenderer != null)
        {
            return;
        }

        Transform existingOverlay = transform.Find("OcclusionHighlight");
        if (existingOverlay != null)
        {
            occlusionOverlayRenderer = existingOverlay.GetComponent<SpriteRenderer>();
        }

        if (occlusionOverlayRenderer != null)
        {
            return;
        }

        GameObject overlayObject = new GameObject("OcclusionHighlight");
        overlayObject.transform.SetParent(transform, false);
        occlusionOverlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
        occlusionOverlayRenderer.color = occlusionHighlightColor;
        occlusionOverlayRenderer.enabled = false;
    }

    private void SyncOcclusionOverlayVisual()
    {
        if (occlusionOverlayRenderer == null)
        {
            return;
        }

        occlusionOverlayRenderer.sprite = targetRenderer.sprite;
        occlusionOverlayRenderer.flipX = targetRenderer.flipX;
        occlusionOverlayRenderer.drawMode = targetRenderer.drawMode;
        occlusionOverlayRenderer.size = targetRenderer.size;
        occlusionOverlayRenderer.color = occlusionHighlightColor;
        occlusionOverlayRenderer.maskInteraction = targetRenderer.maskInteraction;
        occlusionOverlayRenderer.transform.localPosition = targetRenderer.transform.localPosition;
        occlusionOverlayRenderer.transform.localRotation = targetRenderer.transform.localRotation;
        occlusionOverlayRenderer.transform.localScale = targetRenderer.transform.localScale;
    }

    private void HideOcclusionOverlay()
    {
        if (occlusionOverlayRenderer != null)
        {
            occlusionOverlayRenderer.enabled = false;
        }
    }

    private bool UpdateOcclusionState(bool isCurrentlyOccluded, int highestOccluderSortingOrder)
    {
        if (isCurrentlyOccluded)
        {
            occlusionDetectedFrameCount++;
            occlusionHideTimer = occlusionHideDelay;
            activeOcclusionSortingOrder = Mathf.Max(activeOcclusionSortingOrder, highestOccluderSortingOrder);
            return occlusionDetectedFrameCount >= occlusionFramesToShow;
        }

        occlusionDetectedFrameCount = 0;
        if (occlusionHideTimer > 0f)
        {
            occlusionHideTimer = Mathf.Max(0f, occlusionHideTimer - Time.deltaTime);
            return true;
        }

        activeOcclusionSortingOrder = targetRenderer != null ? targetRenderer.sortingOrder : 0;
        return false;
    }

    private void ResetOcclusionState()
    {
        occlusionDetectedFrameCount = 0;
        occlusionHideTimer = 0f;
        activeOcclusionSortingOrder = targetRenderer != null ? targetRenderer.sortingOrder : 0;
    }

    private Vector2 InferSourceFrameSizePixels(Sprite referenceSprite)
    {
        if (referenceSprite == null)
        {
            return Vector2.zero;
        }

        string spriteName = referenceSprite.name;
        int sizeSeparatorIndex = spriteName.LastIndexOf('_');
        if (sizeSeparatorIndex <= 0)
        {
            return referenceSprite.rect.size;
        }

        string prefix = spriteName[..sizeSeparatorIndex];
        int dimensionSeparatorIndex = prefix.LastIndexOf('_');
        if (dimensionSeparatorIndex <= 0)
        {
            return referenceSprite.rect.size;
        }

        string dimensionToken = prefix[(dimensionSeparatorIndex + 1)..];
        string[] parts = dimensionToken.Split('x');
        if (parts.Length != 2 ||
            !float.TryParse(parts[0], out float widthPixels) ||
            !float.TryParse(parts[1], out float heightPixels))
        {
            return referenceSprite.rect.size;
        }

        return new Vector2(widthPixels, heightPixels);
    }

    private Vector2 GetTrimOffsetUnits(Sprite sprite)
    {
        if (sprite == null || sourceFrameSizePixels == Vector2.zero)
        {
            return Vector2.zero;
        }

        float pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
        return new Vector2(
            PositiveModulo(sprite.rect.x, sourceFrameSizePixels.x) / pixelsPerUnit,
            PositiveModulo(sprite.rect.y, sourceFrameSizePixels.y) / pixelsPerUnit);
    }

    private float PositiveModulo(float value, float modulus)
    {
        if (Mathf.Approximately(modulus, 0f))
        {
            return 0f;
        }

        float result = value % modulus;
        return result < 0f ? result + modulus : result;
    }
}
