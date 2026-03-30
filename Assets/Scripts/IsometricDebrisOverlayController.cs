using UnityEngine;

[DisallowMultipleComponent]
public sealed class IsometricDebrisOverlayController : MonoBehaviour
{
    [SerializeField, Min(0)] private int elevation;
    [SerializeField, Min(0)] private int leftBlockingElevation;
    [SerializeField, Min(0)] private int rightBlockingElevation;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteMask spriteMask;
    [SerializeField] private Sprite leftVisibleMaskSprite;
    [SerializeField] private Sprite rightVisibleMaskSprite;

    public void Initialize(
        int sourceElevation,
        int sourceLeftBlockingElevation,
        int sourceRightBlockingElevation,
        SpriteRenderer sourceRenderer,
        SpriteMask sourceMask,
        Sprite sourceLeftVisibleMaskSprite,
        Sprite sourceRightVisibleMaskSprite)
    {
        elevation = Mathf.Max(0, sourceElevation);
        leftBlockingElevation = Mathf.Max(0, sourceLeftBlockingElevation);
        rightBlockingElevation = Mathf.Max(0, sourceRightBlockingElevation);
        spriteRenderer = sourceRenderer;
        spriteMask = sourceMask;
        leftVisibleMaskSprite = sourceLeftVisibleMaskSprite;
        rightVisibleMaskSprite = sourceRightVisibleMaskSprite;
    }

    public void ApplyVisibleElevation(int visibleElevation)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        bool hideLeftHalf = leftBlockingElevation > elevation && leftBlockingElevation <= visibleElevation;
        bool hideRightHalf = rightBlockingElevation > elevation && rightBlockingElevation <= visibleElevation;

        if (hideLeftHalf && hideRightHalf)
        {
            spriteRenderer.enabled = false;
            if (spriteMask != null)
            {
                spriteMask.enabled = false;
            }

            return;
        }

        spriteRenderer.enabled = true;
        if (spriteMask == null)
        {
            return;
        }

        if (!hideLeftHalf && !hideRightHalf)
        {
            spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            spriteMask.enabled = false;
            return;
        }

        spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        spriteMask.sprite = hideRightHalf ? leftVisibleMaskSprite : rightVisibleMaskSprite;
        spriteMask.enabled = spriteMask.sprite != null;
    }
}

[DisallowMultipleComponent]
public sealed class IsometricFakeShadowOverlayController : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float normalizedDepth;
    [SerializeField, Min(0)] private int elevation;
    [SerializeField, Min(0)] private int leftBlockingElevation;
    [SerializeField, Min(0)] private int rightBlockingElevation;
    [SerializeField, Min(0)] private int upperLeftBlockingElevation;
    [SerializeField, Min(0)] private int upperRightBlockingElevation;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color tint = new Color(0f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float nearDepthOpacity = 0f;
    [SerializeField, Range(0f, 1f)] private float farDepthOpacity = 1f;
    [SerializeField, Range(0.5f, 4f)] private float depthFalloffExponent = 1f;
    [SerializeField, Range(0f, 1f)] private float elevationOpacityPerLevel = 0.04f;
    [SerializeField, Range(0f, 1f)] private float adjacentBlockerOpacityPerLevel = 0.08f;
    [SerializeField, Range(0f, 1f)] private float diagonalBlockerOpacityPerLevel = 0.04f;
    [SerializeField, Range(0f, 1f)] private float frontExposureReductionPerLevel = 0.08f;
    [SerializeField, Range(0f, 1f)] private float maxOpacity = 1f;
    [SerializeField, Range(2, 16)] private int opacityBandCount = 10;

    public void Initialize(
        float sourceNormalizedDepth,
        int sourceElevation,
        int sourceLeftBlockingElevation,
        int sourceRightBlockingElevation,
        int sourceUpperLeftBlockingElevation,
        int sourceUpperRightBlockingElevation,
        SpriteRenderer sourceRenderer,
        ProceduralIsometricMapGenerator.FakeShadowSettings settings)
    {
        normalizedDepth = Mathf.Clamp01(sourceNormalizedDepth);
        elevation = Mathf.Max(0, sourceElevation);
        leftBlockingElevation = Mathf.Max(0, sourceLeftBlockingElevation);
        rightBlockingElevation = Mathf.Max(0, sourceRightBlockingElevation);
        upperLeftBlockingElevation = Mathf.Max(0, sourceUpperLeftBlockingElevation);
        upperRightBlockingElevation = Mathf.Max(0, sourceUpperRightBlockingElevation);
        spriteRenderer = sourceRenderer;

        if (settings == null)
        {
            tint = new Color(0f, 0f, 0f, 1f);
            nearDepthOpacity = 0f;
            farDepthOpacity = 1f;
            depthFalloffExponent = 1f;
            elevationOpacityPerLevel = 0.04f;
            adjacentBlockerOpacityPerLevel = 0.08f;
            diagonalBlockerOpacityPerLevel = 0.04f;
            frontExposureReductionPerLevel = 0.08f;
            maxOpacity = 1f;
            opacityBandCount = 10;
            return;
        }

        tint = settings.tint;
        nearDepthOpacity = Mathf.Clamp01(settings.nearDepthOpacity);
        farDepthOpacity = Mathf.Max(nearDepthOpacity, Mathf.Clamp01(settings.farDepthOpacity));
        depthFalloffExponent = Mathf.Clamp(settings.depthFalloffExponent, 0.5f, 4f);
        elevationOpacityPerLevel = Mathf.Clamp01(settings.elevationOpacityPerLevel);
        adjacentBlockerOpacityPerLevel = Mathf.Clamp01(settings.adjacentBlockerOpacityPerLevel);
        diagonalBlockerOpacityPerLevel = Mathf.Clamp01(settings.diagonalBlockerOpacityPerLevel);
        frontExposureReductionPerLevel = Mathf.Clamp01(settings.frontExposureReductionPerLevel);
        maxOpacity = Mathf.Clamp01(settings.maxOpacity);
        opacityBandCount = Mathf.Clamp(settings.opacityBandCount, 2, 16);
    }

    public void ApplyVisibilityContext(int visibleElevation, int focusElevation)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (elevation > visibleElevation || maxOpacity <= 0f || tint.a <= 0f)
        {
            spriteRenderer.color = new Color(tint.r, tint.g, tint.b, 0f);
            spriteRenderer.enabled = false;
            return;
        }

        int referenceElevation = Mathf.Max(visibleElevation, focusElevation);
        int visibleBlockerCap = Mathf.Max(visibleElevation, focusElevation);
        float depthOpacity = Mathf.Lerp(
            nearDepthOpacity,
            farDepthOpacity,
            Mathf.Pow(normalizedDepth, depthFalloffExponent));
        float shadowScore =
            depthOpacity +
            ComputeElevationContribution(referenceElevation) +
            ComputeNeighborContribution(leftBlockingElevation, visibleBlockerCap, adjacentBlockerOpacityPerLevel) +
            ComputeNeighborContribution(rightBlockingElevation, visibleBlockerCap, adjacentBlockerOpacityPerLevel) +
            ComputeNeighborContribution(upperLeftBlockingElevation, visibleBlockerCap, diagonalBlockerOpacityPerLevel) +
            ComputeNeighborContribution(upperRightBlockingElevation, visibleBlockerCap, diagonalBlockerOpacityPerLevel) -
            ComputeFrontExposureContribution(leftBlockingElevation) -
            ComputeFrontExposureContribution(rightBlockingElevation);

        float clampedScore = Mathf.Clamp01(shadowScore);
        float quantizedOpacity = QuantizeOpacity(clampedScore) * Mathf.Clamp01(maxOpacity);
        float finalAlpha = quantizedOpacity * tint.a;
        spriteRenderer.color = new Color(tint.r, tint.g, tint.b, finalAlpha);
        spriteRenderer.enabled = finalAlpha > 0.001f;
    }

    private float ComputeElevationContribution(int referenceElevation)
    {
        int depthLevels = Mathf.Max(0, referenceElevation - elevation);
        return depthLevels * elevationOpacityPerLevel;
    }

    private float ComputeNeighborContribution(int blockingElevation, int visibleBlockerCap, float opacityPerLevel)
    {
        int effectiveBlockingElevation = Mathf.Min(blockingElevation, visibleBlockerCap);
        int blockerLevels = Mathf.Max(0, effectiveBlockingElevation - elevation);
        return blockerLevels * opacityPerLevel;
    }

    private float ComputeFrontExposureContribution(int frontNeighborElevation)
    {
        int exposedLevels = Mathf.Max(0, elevation - frontNeighborElevation);
        return exposedLevels * frontExposureReductionPerLevel;
    }

    private float QuantizeOpacity(float opacity)
    {
        if (opacity <= 0f || opacityBandCount <= 1)
        {
            return opacity;
        }

        float scaledBand = opacity * (opacityBandCount - 1);
        float quantizedBand = Mathf.Round(scaledBand);
        return quantizedBand / (opacityBandCount - 1);
    }
}
