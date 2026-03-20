using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsCharacterAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float walkFramesPerSecond = 7f;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.65f, 1f);

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

    public SpriteRenderer TargetRenderer => targetRenderer;

    public void Initialize(SpriteRenderer spriteRenderer, TacticsCharacterDefinition definition)
    {
        targetRenderer = spriteRenderer;
        characterDefinition = definition;

        if (characterDefinition != null)
        {
            walkFramesPerSecond = characterDefinition.WalkFramesPerSecond;
            baseColor = characterDefinition.BaseColor;
            selectedColor = characterDefinition.SelectedColor;
        }

        if (characterDefinition == null || !characterDefinition.TryGetOrderedSprites(out IReadOnlyList<Sprite> sprites))
        {
            Debug.LogWarning($"TacticsCharacterAnimator could not resolve sprites for '{name}'.");
            return;
        }

        AssignSprites(sprites);
        SetSelected(false);
        SetIdle(currentDirection);
    }

    public void SetSelected(bool isSelected)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.color = isSelected ? selectedColor : baseColor;
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
        targetRenderer.transform.localPosition = new Vector3(
            -(anchorSize.x * 0.5f) + trimOffsetUnits.x,
            trimOffsetUnits.y,
            0f);
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
