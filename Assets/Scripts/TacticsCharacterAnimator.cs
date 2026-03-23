using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TacticsCharacterAnimator : MonoBehaviour
{
    private const string SelectionRingObjectName = "SelectionRing";
    private const string SelectionCarrotObjectName = "SelectionCarrot";

    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private Transform impactPivot;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float walkFramesPerSecond = 7f;
    [SerializeField, Min(0.01f)] private float attackFramesPerSecond = 10f;
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

    [Header("Damage Impact")]
    [SerializeField, Min(0.01f)] private float damageImpactOutDuration = 0.05f;
    [SerializeField, Min(0.01f)] private float damageImpactReturnDuration = 0.11f;
    [SerializeField, Min(0f)] private float damageImpactDistance = 0.15f;
    [SerializeField, Min(0f)] private float damageImpactOvershoot = 0.04f;

    [Header("Selection Indicator")]
    [SerializeField] private Color friendlySelectionColor = new Color(0.35f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color enemySelectionColor = new Color(1f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color targetedCarrotColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField, Min(0.01f)] private float selectionRingWidth = 0.6f;
    [SerializeField, Min(0.01f)] private float selectionRingIsoHeightMultiplier = 2f;
    [SerializeField, Min(0.01f)] private float selectionRingVerticalSquish = 0.9f;
    [SerializeField, Min(0.001f)] private float selectionRingThickness = 0.05f;
    [SerializeField] private float selectionRingVerticalOffset = 0.09f;
    [SerializeField, Min(0.01f)] private float selectionCarrotWidth = 0.2f;
    [SerializeField, Min(0.01f)] private float selectionCarrotHeight = 0.1f;
    [SerializeField] private float selectionCarrotVerticalOffset = 0.25f;
    [SerializeField, Min(0f)] private float selectionCarrotBobAmplitude = 0.05f;
    [SerializeField, Min(0f)] private float selectionCarrotBobFrequency = 1.5f;
    [SerializeField] private int selectionRingSortingOrder = -10;
    [SerializeField] private int selectionCarrotSortingOrder = 10;

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
    [SerializeField] private Sprite attackSouthWestA;
    [SerializeField] private Sprite attackSouthWestB;
    [SerializeField] private Sprite attackSouthWestC;
    [SerializeField] private Sprite attackNorthWestA;
    [SerializeField] private Sprite attackNorthWestB;
    [SerializeField] private Sprite attackNorthWestC;

    private TacticsMovementDirection currentDirection = TacticsMovementDirection.SouthWest;
    private float walkFrameTime;
    private Vector2 sourceFrameSizeUnits;
    private Vector2 sourceFrameSizePixels;
    private TacticsCharacterDefinition characterDefinition;
    private SpriteRenderer occlusionOverlayRenderer;
    private SpriteRenderer selectionRingRenderer;
    private SpriteRenderer selectionCarrotRenderer;
    private SortingGroup sortingGroup;
    private bool isTurnHighlighted;
    private bool isSelected;
    private bool isTargeted;
    private int occlusionDetectedFrameCount;
    private float occlusionHideTimer;
    private int activeOcclusionSortingOrder;
    private Coroutine damageImpactRoutine;
    private Sprite selectionRingSprite;
    private Texture2D selectionRingTexture;
    private float cachedSelectionRingThickness = -1f;
    private static Sprite selectionCarrotSprite;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public int CurrentSortingLayerId => sortingGroup != null ? sortingGroup.sortingLayerID : (targetRenderer != null ? targetRenderer.sortingLayerID : 0);
    public int CurrentSortingOrder => sortingGroup != null ? sortingGroup.sortingOrder : (targetRenderer != null ? targetRenderer.sortingOrder : 0);

    public void Initialize(
        SpriteRenderer spriteRenderer,
        TacticsCharacterDefinition definition,
        ProceduralIsometricMapGenerator generator = null,
        Transform impactRoot = null)
    {
        targetRenderer = spriteRenderer;
        characterDefinition = definition;
        mapGenerator = generator;
        impactPivot = impactRoot;
        ResolveSortingGroup();
        EnsureImpactPivot();
        EnsureOcclusionOverlayRenderer();
        EnsureSelectionIndicatorObjects();

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
        UpdateSelectionIndicatorVisuals();
    }

    private void Awake()
    {
        ResolveSortingGroup();
        EnsureImpactPivot();
        EnsureOcclusionOverlayRenderer();
        EnsureSelectionIndicatorObjects();
        ResetOcclusionState();
        HideOcclusionOverlay();
        UpdateSelectionIndicatorVisuals();
    }

    private void OnEnable()
    {
        EnsureSelectionIndicatorObjects();
        UpdateSelectionIndicatorVisuals();
    }

    private void OnDisable()
    {
        HideSelectionIndicator();
    }

    private void OnDestroy()
    {
        DestroyGeneratedSelectionRingAssets();
    }

    private void OnValidate()
    {
        ResolveSortingGroup();
        EnsureImpactPivot();
        EnsureOcclusionOverlayRenderer();
        EnsureSelectionIndicatorObjects();
        UpdateRendererColor();
        UpdateSelectionIndicatorVisuals();
    }

    private void LateUpdate()
    {
        if (targetRenderer == null)
        {
            ResetOcclusionState();
            HideOcclusionOverlay();
            HideSelectionIndicator();
            return;
        }

        UpdateSelectionIndicatorVisuals();

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
        int currentSortingOrder = CurrentSortingOrder;
        int currentSortingLayerId = CurrentSortingLayerId;
        int highestOccluderSortingOrder = currentSortingOrder;
        for (int i = 0; i < occluders.Count; i++)
        {
            ProceduralIsometricMapGenerator.OcclusionVolume occluder = occluders[i];
            if (occluder.SortingLayerId != currentSortingLayerId ||
                occluder.SortingOrder <= currentSortingOrder)
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

        occlusionOverlayRenderer.sortingLayerID = currentSortingLayerId;
        occlusionOverlayRenderer.sortingOrder = activeOcclusionSortingOrder + occlusionSortingOrderOffset;
        occlusionOverlayRenderer.enabled = true;
    }

    public void SetSorting(int sortingLayerId, int sortingOrder)
    {
        if (sortingGroup != null)
        {
            sortingGroup.sortingLayerID = sortingLayerId;
            sortingGroup.sortingOrder = sortingOrder;
        }
        else if (targetRenderer != null)
        {
            targetRenderer.sortingLayerID = sortingLayerId;
            targetRenderer.sortingOrder = sortingOrder;
        }
    }

    public void SetSelected(bool isSelected)
    {
        this.isSelected = isSelected;
        UpdateRendererColor();
        UpdateSelectionIndicatorVisuals();
    }

    public void SetTargeted(bool isTargeted)
    {
        this.isTargeted = isTargeted;
        UpdateSelectionIndicatorVisuals();
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

    public IEnumerator PlayAttack(TacticsMovementDirection direction)
    {
        currentDirection = direction;

        Sprite[] frames = GetAttackFrames(direction);
        bool hasVisibleFrame = false;
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                hasVisibleFrame = true;
                break;
            }
        }

        if (!hasVisibleFrame)
        {
            SetIdle(direction);
            yield break;
        }

        float secondsPerFrame = 1f / Mathf.Max(0.01f, attackFramesPerSecond);
        bool flipX = IsEastFacing(direction);

        for (int i = 0; i < frames.Length; i++)
        {
            Sprite frame = frames[i];
            if (frame != null)
            {
                ApplySprite(frame, flipX);
            }

            yield return new WaitForSeconds(secondsPerFrame);
        }

        SetIdle(direction);
    }

    public void PlayDamageImpact(Vector3? damageSourceWorldPosition = null)
    {
        EnsureImpactPivot();
        if (impactPivot == null)
        {
            return;
        }

        Vector3 recoilDirection = ResolveDamageImpactDirection(damageSourceWorldPosition);
        if (recoilDirection.sqrMagnitude <= 0.0001f)
        {
            recoilDirection = Vector3.left;
        }

        if (damageImpactRoutine != null)
        {
            StopCoroutine(damageImpactRoutine);
        }

        impactPivot.localPosition = Vector3.zero;
        damageImpactRoutine = StartCoroutine(PlayDamageImpactRoutine(recoilDirection.normalized));
    }

    private void AssignSprites(IReadOnlyList<Sprite> sprites)
    {
        if (sprites == null || sprites.Count < 10)
        {
            Debug.LogWarning("TacticsCharacterAnimator requires at least 10 sliced sprites.");
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

        if (sprites.Count >= 16)
        {
            attackSouthWestA = sprites[10];
            attackSouthWestB = sprites[11];
            attackSouthWestC = sprites[12];
            attackNorthWestA = sprites[13];
            attackNorthWestB = sprites[14];
            attackNorthWestC = sprites[15];
        }

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

    private Sprite[] GetAttackFrames(TacticsMovementDirection direction)
    {
        if (direction == TacticsMovementDirection.NorthWest || direction == TacticsMovementDirection.NorthEast)
        {
            return new[] { attackNorthWestA, attackNorthWestB, attackNorthWestC };
        }

        return new[] { attackSouthWestA, attackSouthWestB, attackSouthWestC };
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

    private void UpdateSelectionIndicatorVisuals()
    {
        EnsureSelectionIndicatorObjects();
        bool shouldShowRing = isSelected;
        bool shouldShowCarrot = isSelected || isTargeted;
        if ((!shouldShowRing && !shouldShowCarrot) ||
            targetRenderer == null ||
            selectionRingRenderer == null ||
            selectionCarrotRenderer == null)
        {
            HideSelectionIndicator();
            return;
        }

        Color selectionColor = GetSelectionIndicatorColor();
        int sortingLayerId = CurrentSortingLayerId;
        int localBaseSortingOrder = targetRenderer != null ? targetRenderer.sortingOrder : 0;

        float xRadius = Mathf.Max(0.01f, selectionRingWidth * 0.5f);
        float yRadius = xRadius / Mathf.Max(0.01f, selectionRingIsoHeightMultiplier);
        yRadius *= Mathf.Max(0.01f, selectionRingVerticalSquish);

        selectionRingRenderer.sprite = GetSelectionRingSprite();
        selectionRingRenderer.color = selectionColor;
        selectionRingRenderer.sortingLayerID = sortingLayerId;
        selectionRingRenderer.sortingOrder = localBaseSortingOrder + selectionRingSortingOrder;
        selectionRingRenderer.enabled = shouldShowRing;

        selectionCarrotRenderer.sprite = GetSelectionCarrotSprite();
        selectionCarrotRenderer.color = isTargeted ? targetedCarrotColor : selectionColor;
        selectionCarrotRenderer.sortingLayerID = sortingLayerId;
        selectionCarrotRenderer.sortingOrder = localBaseSortingOrder + selectionCarrotSortingOrder;
        selectionCarrotRenderer.enabled = shouldShowCarrot;

        float bobTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float bobOffset = Mathf.Sin(bobTime * Mathf.PI * 2f * selectionCarrotBobFrequency) * selectionCarrotBobAmplitude;
        Bounds spriteBounds = targetRenderer.bounds;
        Vector3 footWorldPosition = new Vector3(spriteBounds.center.x, spriteBounds.min.y, transform.position.z);
        Vector3 topWorldPosition = new Vector3(spriteBounds.center.x, spriteBounds.max.y, transform.position.z);
        Transform indicatorParent = GetSelectionIndicatorParent();
        Vector3 localFootPosition = indicatorParent.InverseTransformPoint(footWorldPosition);
        Vector3 localTopPosition = indicatorParent.InverseTransformPoint(topWorldPosition);

        selectionRingRenderer.transform.localPosition = new Vector3(
            localFootPosition.x,
            localFootPosition.y + selectionRingVerticalOffset,
            0f);
        selectionRingRenderer.transform.localScale = new Vector3(xRadius * 2f, yRadius * 2f, 1f);
        selectionCarrotRenderer.transform.localPosition = new Vector3(
            localTopPosition.x,
            localTopPosition.y + selectionCarrotVerticalOffset + bobOffset,
            0f);
        selectionCarrotRenderer.transform.localScale = new Vector3(
            selectionCarrotWidth,
            -selectionCarrotHeight,
            1f);
    }

    private Color GetSelectionIndicatorColor()
    {
        bool isEnemy = characterDefinition != null && characterDefinition.Team == TacticsUnitTeam.Enemy;
        return isEnemy ? enemySelectionColor : friendlySelectionColor;
    }

    private void HideSelectionIndicator()
    {
        if (selectionRingRenderer != null)
        {
            selectionRingRenderer.enabled = false;
        }

        if (selectionCarrotRenderer != null)
        {
            selectionCarrotRenderer.enabled = false;
        }
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

    private void EnsureSelectionIndicatorObjects()
    {
        Transform indicatorParent = GetSelectionIndicatorParent();

        if (selectionRingRenderer == null)
        {
            Transform existingRing = transform.Find(SelectionRingObjectName);
            if (existingRing == null && indicatorParent != null)
            {
                existingRing = indicatorParent.Find(SelectionRingObjectName);
            }

            if (existingRing != null)
            {
                selectionRingRenderer = existingRing.GetComponent<SpriteRenderer>();
            }

            if (selectionRingRenderer == null)
            {
                GameObject ringObject = new GameObject(SelectionRingObjectName);
                ringObject.transform.SetParent(indicatorParent, false);
                selectionRingRenderer = ringObject.AddComponent<SpriteRenderer>();
            }

            selectionRingRenderer.sprite = GetSelectionRingSprite();
            selectionRingRenderer.enabled = false;
        }
        else if (selectionRingRenderer.transform.parent != indicatorParent)
        {
            selectionRingRenderer.transform.SetParent(indicatorParent, true);
        }

        if (selectionCarrotRenderer != null)
        {
            if (selectionCarrotRenderer.transform.parent != indicatorParent)
            {
                selectionCarrotRenderer.transform.SetParent(indicatorParent, true);
            }

            return;
        }

        Transform existingCarrot = transform.Find(SelectionCarrotObjectName);
        if (existingCarrot == null && indicatorParent != null)
        {
            existingCarrot = indicatorParent.Find(SelectionCarrotObjectName);
        }

        if (existingCarrot != null)
        {
            selectionCarrotRenderer = existingCarrot.GetComponent<SpriteRenderer>();
        }

        if (selectionCarrotRenderer == null)
        {
            GameObject carrotObject = new GameObject(SelectionCarrotObjectName);
            carrotObject.transform.SetParent(indicatorParent, false);
            selectionCarrotRenderer = carrotObject.AddComponent<SpriteRenderer>();
        }

        selectionCarrotRenderer.sprite = GetSelectionCarrotSprite();
        selectionCarrotRenderer.enabled = false;
    }

    private Transform GetSelectionIndicatorParent()
    {
        if (sortingGroup != null)
        {
            return sortingGroup.transform;
        }

        if (targetRenderer != null)
        {
            return targetRenderer.transform;
        }

        return transform;
    }

    private static Sprite GetSelectionCarrotSprite()
    {
        if (selectionCarrotSprite != null)
        {
            return selectionCarrotSprite;
        }

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SelectionCarrotTexture"
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        for (int y = 0; y < size; y++)
        {
            float normalizedY = y / (float)(size - 1);
            int halfWidth = Mathf.Max(0, Mathf.RoundToInt((1f - normalizedY) * (size * 0.35f)));
            int center = size / 2;
            for (int x = center - halfWidth; x <= center + halfWidth; x++)
            {
                if (x >= 0 && x < size)
                {
                    texture.SetPixel(x, y, fill);
                }
            }
        }

        texture.Apply();
        selectionCarrotSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0f),
            size);
        selectionCarrotSprite.name = "SelectionCarrotSprite";
        return selectionCarrotSprite;
    }

    private void ResolveSortingGroup()
    {
        if (sortingGroup != null)
        {
            return;
        }

        if (targetRenderer != null)
        {
            sortingGroup = targetRenderer.GetComponent<SortingGroup>();
        }

        if (sortingGroup == null)
        {
            sortingGroup = GetComponentInChildren<SortingGroup>();
        }
    }

    private void EnsureImpactPivot()
    {
        if (impactPivot != null)
        {
            return;
        }

        if (targetRenderer != null && targetRenderer.transform.parent != null)
        {
            impactPivot = targetRenderer.transform.parent;
            return;
        }

        impactPivot = transform;
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

        activeOcclusionSortingOrder = CurrentSortingOrder;
        return false;
    }

    private void ResetOcclusionState()
    {
        occlusionDetectedFrameCount = 0;
        occlusionHideTimer = 0f;
        activeOcclusionSortingOrder = CurrentSortingOrder;
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

    private IEnumerator PlayDamageImpactRoutine(Vector3 recoilDirection)
    {
        Vector3 start = Vector3.zero;
        Vector3 pushedBack = recoilDirection * damageImpactDistance;
        Vector3 overshoot = recoilDirection * -damageImpactOvershoot;

        float elapsed = 0f;
        while (elapsed < damageImpactOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageImpactOutDuration);
            impactPivot.localPosition = Vector3.LerpUnclamped(start, pushedBack, EaseOutCubic(t));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < damageImpactReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / damageImpactReturnDuration);
            float springT = EaseOutBack(t);
            impactPivot.localPosition = Vector3.LerpUnclamped(pushedBack, overshoot, springT);
            yield return null;
        }

        impactPivot.localPosition = Vector3.zero;
        damageImpactRoutine = null;
    }

    private Vector3 ResolveDamageImpactDirection(Vector3? damageSourceWorldPosition)
    {
        if (impactPivot == null)
        {
            return Vector3.zero;
        }

        if (damageSourceWorldPosition.HasValue)
        {
            Vector3 direction = impactPivot.position - damageSourceWorldPosition.Value;
            direction.z = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }
        }

        return currentDirection switch
        {
            TacticsMovementDirection.NorthEast => new Vector3(1f, 0.35f, 0f).normalized,
            TacticsMovementDirection.NorthWest => new Vector3(-1f, 0.35f, 0f).normalized,
            TacticsMovementDirection.SouthEast => new Vector3(1f, -0.2f, 0f).normalized,
            _ => new Vector3(-1f, -0.2f, 0f).normalized
        };
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - Mathf.Clamp01(t);
        return 1f - (inverse * inverse * inverse);
    }

    private static float EaseOutBack(float t)
    {
        float clamped = Mathf.Clamp01(t);
        const float overshoot = 1.70158f;
        float adjusted = clamped - 1f;
        return 1f + ((overshoot + 1f) * adjusted * adjusted * adjusted) + (overshoot * adjusted * adjusted);
    }

    private Sprite GetSelectionRingSprite()
    {
        if (selectionRingSprite != null && Mathf.Approximately(cachedSelectionRingThickness, selectionRingThickness))
        {
            return selectionRingSprite;
        }

        DestroyGeneratedSelectionRingAssets();

        const int size = 128;
        selectionRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "SelectionRingTexture"
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        float center = (size - 1) * 0.5f;
        float outerRadius = center - 1f;
        float normalizedThickness = Mathf.Clamp(selectionRingThickness / Mathf.Max(0.01f, selectionRingWidth), 0.01f, 0.95f);
        float innerRadius = outerRadius * Mathf.Clamp01(1f - normalizedThickness);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                selectionRingTexture.SetPixel(x, y, distance <= outerRadius && distance >= innerRadius ? fill : clear);
            }
        }

        selectionRingTexture.Apply();
        selectionRingSprite = Sprite.Create(
            selectionRingTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        selectionRingSprite.name = "SelectionRingSprite";
        cachedSelectionRingThickness = selectionRingThickness;
        return selectionRingSprite;
    }

    private void DestroyGeneratedSelectionRingAssets()
    {
        if (selectionRingSprite != null)
        {
            if (Application.isPlaying)
            {
                Destroy(selectionRingSprite);
            }
            else
            {
                DestroyImmediate(selectionRingSprite);
            }

            selectionRingSprite = null;
        }

        if (selectionRingTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(selectionRingTexture);
            }
            else
            {
                DestroyImmediate(selectionRingTexture);
            }

            selectionRingTexture = null;
        }
    }
}
