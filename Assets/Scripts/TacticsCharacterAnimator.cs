using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsCharacterAnimator : MonoBehaviour
{
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
    [SerializeField, Min(1)] private int occlusionVerticalSamples = 6;
    [SerializeField, Min(1)] private int occlusionHorizontalSamples = 3;
    [SerializeField, Min(0f)] private float occlusionFootWidth = 0.18f;
    [SerializeField, Min(0f)] private float occlusionFootVerticalOffset = 0.02f;
    [SerializeField, Range(0.1f, 1f)] private float occlusionRevealHeightFactor = 0.72f;
    [SerializeField, Min(1)] private int occlusionFramesToShow = 2;
    [SerializeField, Min(0f)] private float occlusionHideDelay = 0.08f;

    [Header("Damage Impact")]
    [SerializeField, Min(0.01f)] private float damageImpactOutDuration = 0.05f;
    [SerializeField, Min(0.01f)] private float damageImpactReturnDuration = 0.11f;
    [SerializeField, Min(0f)] private float damageImpactDistance = 0.15f;
    [SerializeField, Min(0f)] private float damageImpactOvershoot = 0.04f;

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

    private static readonly List<Vector2> OcclusionSampleBuffer = new List<Vector2>(32);
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
    private Coroutine damageImpactRoutine;

    public SpriteRenderer TargetRenderer => targetRenderer;

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
        EnsureImpactPivot();
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
        EnsureImpactPivot();
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
        IReadOnlyList<Vector2> occlusionSamples = BuildOcclusionSamples(characterBounds);

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

            if (!OccludesCharacter(occluder, occlusionSamples))
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

    private IReadOnlyList<Vector2> BuildOcclusionSamples(Bounds characterBounds)
    {
        OcclusionSampleBuffer.Clear();

        Vector3 footWorld = transform.position;
        float minX = footWorld.x - (occlusionFootWidth * 0.5f);
        float maxX = footWorld.x + (occlusionFootWidth * 0.5f);
        float topY = Mathf.Lerp(
            footWorld.y,
            characterBounds.max.y,
            Mathf.Clamp01(occlusionRevealHeightFactor));
        topY = Mathf.Max(footWorld.y + occlusionFootVerticalOffset, topY);

        int horizontalSampleCount = Mathf.Max(1, occlusionHorizontalSamples);
        int verticalSampleCount = Mathf.Max(1, occlusionVerticalSamples);

        for (int verticalIndex = 0; verticalIndex < verticalSampleCount; verticalIndex++)
        {
            float verticalT = verticalSampleCount == 1
                ? 0f
                : verticalIndex / (float)(verticalSampleCount - 1);
            float sampleY = Mathf.Lerp(footWorld.y + occlusionFootVerticalOffset, topY, verticalT);

            for (int horizontalIndex = 0; horizontalIndex < horizontalSampleCount; horizontalIndex++)
            {
                float horizontalT = horizontalSampleCount == 1
                    ? 0.5f
                    : horizontalIndex / (float)(horizontalSampleCount - 1);
                float sampleX = Mathf.Lerp(minX, maxX, horizontalT);
                OcclusionSampleBuffer.Add(new Vector2(sampleX, sampleY));
            }
        }

        return OcclusionSampleBuffer;
    }

    private bool OccludesCharacter(
        ProceduralIsometricMapGenerator.OcclusionVolume occluder,
        IReadOnlyList<Vector2> samples)
    {
        if (samples == null || samples.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < samples.Count; i++)
        {
            Vector2 sample = samples[i];
            if ((occluder.HasLeftFace && IsPointInsidePolygon(sample, occluder.LeftFacePoints)) ||
                (occluder.HasRightFace && IsPointInsidePolygon(sample, occluder.RightFacePoints)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        bool isInside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            bool intersects = ((a.y > point.y) != (b.y > point.y)) &&
                              (point.x < ((b.x - a.x) * (point.y - a.y) / Mathf.Max(0.0001f, b.y - a.y)) + a.x);
            if (intersects)
            {
                isInside = !isInside;
            }
        }

        return isInside;
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
