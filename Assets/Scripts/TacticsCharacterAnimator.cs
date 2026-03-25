using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TacticsCharacterAnimator : MonoBehaviour
{
    private const string SelectionCarrotObjectName = "SelectionCarrot";
    private const string SelectionCarrotBorderObjectName = "SelectionCarrotBorder";
    private const int SelectionCarrotBorderThicknessPixels = 3;

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
    [SerializeField] private Color activeCharacterCarrotColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private Color selectedCharacterCarrotColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color locallyOwnedCharacterCarrotColor = new Color(0.22f, 0.84f, 0.3f, 1f);
    [SerializeField, Min(0.01f)] private float selectionCarrotWidth = 0.2f;
    [SerializeField, Min(0.01f)] private float selectionCarrotHeight = 0.1f;
    [SerializeField, Range(0.5f, 1f)] private float selectionCarrotInnerScale = 0.72f;
    [SerializeField] private float selectionCarrotVerticalOffset = 0.25f;
    [SerializeField, Min(0f)] private float selectionCarrotBobAmplitude = 0.05f;
    [SerializeField, Min(0f)] private float selectionCarrotBobFrequency = 1.5f;
    [SerializeField] private int selectionCarrotSortingOrder = 10;
    [SerializeField] private int selectionCarrotBorderSortingOrderOffset = -1;

    [Header("Target Hover Preview")]
    [SerializeField] private Color targetHoverFlashColor = new Color(0.98f, 0.98f, 0.98f, 1f);
    [SerializeField, Min(0.01f)] private float targetHoverFlashFrequency = 1f;
    [SerializeField, Range(0f, 1f)] private float targetHoverFlashStrength = 0.3f;
    [SerializeField, Min(0)] private int targetHoverFlashSortingOrderOffset = 1;

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
    private TacticsCharacterData characterData;
    private SpriteRenderer occlusionOverlayRenderer;
    private SpriteRenderer targetHoverOverlayRenderer;
    private SpriteRenderer selectionCarrotRenderer;
    private SpriteRenderer selectionCarrotBorderRenderer;
    private SortingGroup sortingGroup;
    private bool isTurnHighlighted;
    private bool isSelected;
    private bool isTargeted;
    private bool isLocallyOwned;
    private bool isTargetHoverPreviewActive;
    private bool isPresentationVisible = true;
    private int occlusionDetectedFrameCount;
    private float occlusionHideTimer;
    private int activeOcclusionSortingOrder;
    private Coroutine damageImpactRoutine;
    private static Sprite selectionCarrotSprite;
    private static Sprite selectionCarrotBorderSprite;
    private static Shader solidTintOverlayShader;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public int CurrentSortingLayerId => sortingGroup != null ? sortingGroup.sortingLayerID : (targetRenderer != null ? targetRenderer.sortingLayerID : 0);
    public int CurrentSortingOrder => sortingGroup != null ? sortingGroup.sortingOrder : (targetRenderer != null ? targetRenderer.sortingOrder : 0);

    public void Initialize(
        SpriteRenderer spriteRenderer,
        TacticsCharacterData definition,
        ProceduralIsometricMapGenerator generator = null,
        Transform impactRoot = null)
    {
        targetRenderer = spriteRenderer;
        characterData = definition;
        mapGenerator = generator;
        impactPivot = impactRoot;
        ResolveSortingGroup();
        EnsureImpactPivot();
        EnsureOcclusionOverlayRenderer();
        EnsureTargetHoverOverlayRenderer();
        EnsureSelectionIndicatorObjects();

        if (characterData != null)
        {
            walkFramesPerSecond = characterData.WalkFramesPerSecond;
            neutralColor = characterData.BaseColor;
        }

        if (characterData == null || !characterData.TryGetOrderedSprites(out IReadOnlyList<Sprite> sprites))
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
        EnsureTargetHoverOverlayRenderer();
        EnsureSelectionIndicatorObjects();
        ResetOcclusionState();
        HideOcclusionOverlay();
        HideTargetHoverOverlay();
        UpdateSelectionIndicatorVisuals();
    }

    private void OnEnable()
    {
        EnsureSelectionIndicatorObjects();
        UpdateSelectionIndicatorVisuals();
    }

    private void OnDisable()
    {
        HideTargetHoverOverlay();
        HideSelectionIndicator();
    }

    private void OnDestroy()
    {
    }

    private void OnValidate()
    {
        ResolveSortingGroup();
        EnsureImpactPivot();
        EnsureOcclusionOverlayRenderer();
        EnsureTargetHoverOverlayRenderer();
        EnsureSelectionIndicatorObjects();
        UpdateRendererColor();
        SyncTargetHoverOverlayVisual();
        UpdateSelectionIndicatorVisuals();
    }

    private void LateUpdate()
    {
        if (targetRenderer == null)
        {
            ResetOcclusionState();
            HideOcclusionOverlay();
            HideTargetHoverOverlay();
            HideSelectionIndicator();
            return;
        }

        UpdateRendererColor();
        SyncTargetHoverOverlayVisual();
        UpdateSelectionIndicatorVisuals();

        if (!isPresentationVisible)
        {
            ResetOcclusionState();
            HideOcclusionOverlay();
            HideTargetHoverOverlay();
            HideSelectionIndicator();
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

    public void SetLocallyOwned(bool isLocallyOwned)
    {
        this.isLocallyOwned = isLocallyOwned;
        UpdateSelectionIndicatorVisuals();
    }

    public void SetTargetHoverPreview(bool isActive)
    {
        isTargetHoverPreviewActive = isActive;
        UpdateRendererColor();
    }

    public void SetTurnHighlight(bool isActiveTurn)
    {
        isTurnHighlighted = isActiveTurn;
        UpdateRendererColor();
    }

    public void SetVisualVisibility(bool isVisible)
    {
        isPresentationVisible = isVisible;

        if (!isPresentationVisible)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = false;
            }

            HideOcclusionOverlay();
            HideTargetHoverOverlay();
            HideSelectionIndicator();
            return;
        }

        if (targetRenderer != null)
        {
            targetRenderer.enabled = targetRenderer.sprite != null;
        }

        UpdateRendererColor();
        UpdateSelectionIndicatorVisuals();
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

        if (!isPresentationVisible)
        {
            targetRenderer.enabled = false;
            return;
        }

        Color baseColor;
        if (!isTurnHighlighted)
        {
            baseColor = neutralColor;
        }
        else
        {
            bool isEnemy = characterData != null && characterData.Team == TacticsUnitTeam.Enemy;
            baseColor = isEnemy ? enemyTurnColor : playerTurnColor;
        }

        if (!isTargetHoverPreviewActive)
        {
            targetRenderer.color = baseColor;
            targetRenderer.enabled = targetRenderer.sprite != null;
            return;
        }

        targetRenderer.color = baseColor;
        targetRenderer.enabled = targetRenderer.sprite != null;
    }

    private void UpdateSelectionIndicatorVisuals()
    {
        EnsureSelectionIndicatorObjects();
        if (!isPresentationVisible)
        {
            HideSelectionIndicator();
            return;
        }

        bool isActiveCharacter = isTurnHighlighted;
        bool isSelectedCharacter = isSelected || isTargeted;
        bool shouldShowCarrot = isActiveCharacter || isSelectedCharacter || isLocallyOwned;
        if (!shouldShowCarrot ||
            targetRenderer == null ||
            selectionCarrotRenderer == null ||
            selectionCarrotBorderRenderer == null)
        {
            HideSelectionIndicator();
            return;
        }

        int sortingLayerId = CurrentSortingLayerId;
        int localBaseSortingOrder = targetRenderer != null ? targetRenderer.sortingOrder : 0;

        selectionCarrotRenderer.sprite = GetSelectionCarrotSprite();
        selectionCarrotBorderRenderer.sprite = GetSelectionCarrotBorderSprite();
        Color fillColor = isActiveCharacter
            ? activeCharacterCarrotColor
            : isSelectedCharacter
                ? selectedCharacterCarrotColor
                : locallyOwnedCharacterCarrotColor;
        Color borderColor = selectedCharacterCarrotColor;
        bool shouldShowBorder = isSelectedCharacter;

        selectionCarrotRenderer.color = fillColor;
        selectionCarrotBorderRenderer.color = borderColor;
        selectionCarrotRenderer.sortingLayerID = sortingLayerId;
        selectionCarrotRenderer.sortingOrder = localBaseSortingOrder + selectionCarrotSortingOrder;
        selectionCarrotBorderRenderer.sortingLayerID = sortingLayerId;
        selectionCarrotBorderRenderer.sortingOrder = localBaseSortingOrder + selectionCarrotSortingOrder + selectionCarrotBorderSortingOrderOffset;
        selectionCarrotRenderer.enabled = shouldShowCarrot;
        selectionCarrotBorderRenderer.enabled = shouldShowBorder;

        float bobTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float bobOffset = Mathf.Sin(bobTime * Mathf.PI * 2f * selectionCarrotBobFrequency) * selectionCarrotBobAmplitude;
        Bounds spriteBounds = targetRenderer.bounds;
        Vector3 topWorldPosition = new Vector3(spriteBounds.center.x, spriteBounds.max.y, transform.position.z);
        Transform indicatorParent = GetSelectionIndicatorParent();
        Vector3 localTopPosition = indicatorParent.InverseTransformPoint(topWorldPosition);

        Vector3 carrotLocalPosition = new Vector3(
            localTopPosition.x,
            localTopPosition.y + selectionCarrotVerticalOffset + bobOffset,
            0f);
        Vector3 carrotOuterScale = new Vector3(
            selectionCarrotWidth,
            -selectionCarrotHeight,
            1f);
        Vector3 carrotInnerScale = new Vector3(
            selectionCarrotWidth * selectionCarrotInnerScale,
            -selectionCarrotHeight * selectionCarrotInnerScale,
            1f);
        selectionCarrotRenderer.transform.localPosition = carrotLocalPosition;
        selectionCarrotBorderRenderer.transform.localPosition = carrotLocalPosition;
        selectionCarrotRenderer.transform.localScale = shouldShowBorder ? carrotInnerScale : carrotOuterScale;
        selectionCarrotBorderRenderer.transform.localScale = carrotOuterScale;
    }

    private void HideSelectionIndicator()
    {
        if (selectionCarrotRenderer != null)
        {
            selectionCarrotRenderer.enabled = false;
        }

        if (selectionCarrotBorderRenderer != null)
        {
            selectionCarrotBorderRenderer.enabled = false;
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

    private void EnsureTargetHoverOverlayRenderer()
    {
        if (targetHoverOverlayRenderer != null)
        {
            return;
        }

        Transform existingOverlay = transform.Find("TargetHoverOverlay");
        if (existingOverlay != null)
        {
            targetHoverOverlayRenderer = existingOverlay.GetComponent<SpriteRenderer>();
        }

        if (targetHoverOverlayRenderer == null)
        {
            GameObject overlayObject = new GameObject("TargetHoverOverlay");
            overlayObject.transform.SetParent(transform, false);
            targetHoverOverlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
        }

        Material overlayMaterial = CreateSolidTintOverlayMaterial();
        if (overlayMaterial != null)
        {
            targetHoverOverlayRenderer.sharedMaterial = overlayMaterial;
        }

        targetHoverOverlayRenderer.enabled = false;
    }

    private void EnsureSelectionIndicatorObjects()
    {
        Transform indicatorParent = GetSelectionIndicatorParent();
        RemoveLegacySelectionRingObject(indicatorParent);

        if (selectionCarrotRenderer != null && selectionCarrotBorderRenderer != null)
        {
            if (selectionCarrotRenderer.transform.parent != indicatorParent)
            {
                selectionCarrotRenderer.transform.SetParent(indicatorParent, true);
            }

            if (selectionCarrotBorderRenderer.transform.parent != indicatorParent)
            {
                selectionCarrotBorderRenderer.transform.SetParent(indicatorParent, true);
            }

            return;
        }

        Transform existingCarrot = transform.Find(SelectionCarrotObjectName);
        Transform existingCarrotBorder = transform.Find(SelectionCarrotBorderObjectName);
        if (existingCarrot == null && indicatorParent != null)
        {
            existingCarrot = indicatorParent.Find(SelectionCarrotObjectName);
        }

        if (existingCarrotBorder == null && indicatorParent != null)
        {
            existingCarrotBorder = indicatorParent.Find(SelectionCarrotBorderObjectName);
        }

        if (existingCarrot != null)
        {
            selectionCarrotRenderer = existingCarrot.GetComponent<SpriteRenderer>();
        }

        if (existingCarrotBorder != null)
        {
            selectionCarrotBorderRenderer = existingCarrotBorder.GetComponent<SpriteRenderer>();
        }

        if (selectionCarrotRenderer == null)
        {
            GameObject carrotObject = new GameObject(SelectionCarrotObjectName);
            carrotObject.transform.SetParent(indicatorParent, false);
            selectionCarrotRenderer = carrotObject.AddComponent<SpriteRenderer>();
        }

        if (selectionCarrotBorderRenderer == null)
        {
            GameObject carrotBorderObject = new GameObject(SelectionCarrotBorderObjectName);
            carrotBorderObject.transform.SetParent(indicatorParent, false);
            selectionCarrotBorderRenderer = carrotBorderObject.AddComponent<SpriteRenderer>();
        }

        selectionCarrotRenderer.sprite = GetSelectionCarrotSprite();
        selectionCarrotBorderRenderer.sprite = GetSelectionCarrotBorderSprite();
        selectionCarrotRenderer.enabled = false;
        selectionCarrotBorderRenderer.enabled = false;
    }

    private void RemoveLegacySelectionRingObject(Transform indicatorParent)
    {
        Transform existingRing = transform.Find("SelectionRing");
        if (existingRing == null && indicatorParent != null)
        {
            existingRing = indicatorParent.Find("SelectionRing");
        }

        if (existingRing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existingRing.gameObject);
        }
        else
        {
            DestroyImmediate(existingRing.gameObject);
        }
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

    private static Sprite GetSelectionCarrotBorderSprite()
    {
        if (selectionCarrotBorderSprite != null)
        {
            return selectionCarrotBorderSprite;
        }

        Sprite fillSprite = GetSelectionCarrotSprite();
        Rect rect = fillSprite.rect;
        Texture2D sourceTexture = fillSprite.texture;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SelectionCarrotBorderTexture"
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isFilled = sourceTexture.GetPixel((int)rect.x + x, (int)rect.y + y).a > 0.5f;
                if (!isFilled)
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                bool hasTransparentNeighbor = false;
                for (int offsetY = -SelectionCarrotBorderThicknessPixels; offsetY <= SelectionCarrotBorderThicknessPixels && !hasTransparentNeighbor; offsetY++)
                {
                    for (int offsetX = -SelectionCarrotBorderThicknessPixels; offsetX <= SelectionCarrotBorderThicknessPixels; offsetX++)
                    {
                        int sampleX = x + offsetX;
                        int sampleY = y + offsetY;
                        if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= height)
                        {
                            hasTransparentNeighbor = true;
                            break;
                        }

                        bool neighborFilled = sourceTexture.GetPixel((int)rect.x + sampleX, (int)rect.y + sampleY).a > 0.5f;
                        if (!neighborFilled)
                        {
                            hasTransparentNeighbor = true;
                            break;
                        }
                    }
                }

                texture.SetPixel(x, y, hasTransparentNeighbor ? fill : clear);
            }
        }

        texture.Apply();
        selectionCarrotBorderSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0f),
            fillSprite.pixelsPerUnit);
        selectionCarrotBorderSprite.name = "SelectionCarrotBorderSprite";
        return selectionCarrotBorderSprite;
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

    private void SyncTargetHoverOverlayVisual()
    {
        if (targetHoverOverlayRenderer == null || targetRenderer == null)
        {
            return;
        }

        if (!isPresentationVisible || !ShouldShowTargetHoverFlashFrame())
        {
            HideTargetHoverOverlay();
            return;
        }

        targetHoverOverlayRenderer.sprite = targetRenderer.sprite;
        targetHoverOverlayRenderer.flipX = targetRenderer.flipX;
        targetHoverOverlayRenderer.flipY = targetRenderer.flipY;
        targetHoverOverlayRenderer.drawMode = targetRenderer.drawMode;
        targetHoverOverlayRenderer.size = targetRenderer.size;
        targetHoverOverlayRenderer.maskInteraction = targetRenderer.maskInteraction;
        targetHoverOverlayRenderer.sortingLayerID = CurrentSortingLayerId;
        targetHoverOverlayRenderer.sortingOrder = CurrentSortingOrder + targetHoverFlashSortingOrderOffset;
        targetHoverOverlayRenderer.color = new Color(
            targetHoverFlashColor.r,
            targetHoverFlashColor.g,
            targetHoverFlashColor.b,
            Mathf.Clamp01(targetHoverFlashStrength));
        targetHoverOverlayRenderer.transform.localPosition = targetRenderer.transform.localPosition;
        targetHoverOverlayRenderer.transform.localRotation = targetRenderer.transform.localRotation;
        targetHoverOverlayRenderer.transform.localScale = targetRenderer.transform.localScale;
        targetHoverOverlayRenderer.enabled = targetHoverOverlayRenderer.sprite != null;
    }

    private bool ShouldShowTargetHoverFlashFrame()
    {
        if (!isTargetHoverPreviewActive)
        {
            return false;
        }

        float flashTime = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        float blinkWave = Mathf.Sin(flashTime * Mathf.PI * 2f * targetHoverFlashFrequency);
        return blinkWave >= 0f;
    }

    private void HideTargetHoverOverlay()
    {
        if (targetHoverOverlayRenderer != null)
        {
            targetHoverOverlayRenderer.enabled = false;
        }
    }

    private static Material CreateSolidTintOverlayMaterial()
    {
        solidTintOverlayShader ??= Shader.Find("Hidden/Tactics/SolidTintSprite");
        if (solidTintOverlayShader == null)
        {
            return null;
        }

        Material material = new Material(solidTintOverlayShader);
        material.hideFlags = HideFlags.HideAndDontSave;
        return material;
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

}
