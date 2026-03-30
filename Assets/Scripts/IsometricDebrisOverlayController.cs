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
