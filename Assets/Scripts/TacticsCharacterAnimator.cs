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

    public SpriteRenderer TargetRenderer => targetRenderer;

    public void Initialize(SpriteRenderer spriteRenderer, Sprite[] sprites)
    {
        targetRenderer = spriteRenderer;
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

    private void AssignSprites(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length < 10)
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

        Vector2 spriteSize = sprite.bounds.size;
        targetRenderer.transform.localPosition = new Vector3(-(spriteSize.x * 0.5f), 0f, 0f);
    }
}
