using UnityEngine;

public enum IsometricMapElevationElementType
{
    TopFace = 0,
    SideFace = 1,
    Shadow = 2,
    SliceCap = 3
}

[DisallowMultipleComponent]
public sealed class IsometricMapElevationElement : MonoBehaviour
{
    [SerializeField, Min(0)] private int elevation;
    [SerializeField] private IsometricMapElevationElementType elementType;

    private SpriteRenderer spriteRenderer;
    private Collider2D interactionCollider;
    private IsometricTileHoverInfo hoverInfo;
    private Color baseColor = Color.white;

    public int Elevation => elevation;
    public IsometricMapElevationElementType ElementType => elementType;

    public void Initialize(int sourceElevation, IsometricMapElevationElementType sourceElementType, SpriteRenderer sourceRenderer)
    {
        elevation = sourceElevation;
        elementType = sourceElementType;
        spriteRenderer = sourceRenderer;

        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }
    }

    public void AttachInteraction(Collider2D sourceCollider, IsometricTileHoverInfo sourceHoverInfo)
    {
        interactionCollider = sourceCollider;
        hoverInfo = sourceHoverInfo;
    }

    public void SetPresentation(float alpha, bool isInteractable)
    {
        if (spriteRenderer != null)
        {
            Color color = baseColor;
            color.a *= Mathf.Clamp01(alpha);
            spriteRenderer.color = color;
            spriteRenderer.enabled = color.a > 0.001f;
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = isInteractable;
        }

        if (hoverInfo != null)
        {
            hoverInfo.SetInteractable(isInteractable);
        }
    }
}
