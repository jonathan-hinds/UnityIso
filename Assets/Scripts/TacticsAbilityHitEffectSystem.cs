using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsAbilityHitEffectSystem : MonoBehaviour
{
    private const string SystemObjectName = "Tactics Ability Hit Effect System";

    private static TacticsAbilityHitEffectSystem instance;

    public static TacticsAbilityHitEffectSystem Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TacticsAbilityHitEffectSystem>();
            }

            if (instance == null)
            {
                GameObject systemObject = new GameObject(SystemObjectName);
                instance = systemObject.AddComponent<TacticsAbilityHitEffectSystem>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void Show(TacticsAbilityHitEffectDefinition definition, TacticsCharacterController target)
    {
        if (!definition.IsConfigured || target == null)
        {
            return;
        }

        Instance.Spawn(definition, target);
    }

    private void Spawn(TacticsAbilityHitEffectDefinition definition, TacticsCharacterController target)
    {
        GameObject effectObject = new GameObject($"Hit Effect - {target.DisplayName}");
        effectObject.transform.SetParent(transform, false);

        TacticsAbilityHitEffectInstance effectInstance = effectObject.AddComponent<TacticsAbilityHitEffectInstance>();
        effectInstance.Initialize(target, definition);
    }
}

[DisallowMultipleComponent]
public sealed class TacticsAbilityHitEffectInstance : MonoBehaviour
{
    private TacticsCharacterController target;
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float framesPerSecond;
    private float duration;
    private float scale;
    private Vector2 worldOffset;
    private Color tint;
    private int sortingOrderOffset;
    private float elapsedTime;
    private Vector3 lastKnownWorldPosition;
    private int lastKnownSortingLayerId;
    private int lastKnownSortingOrder;

    public void Initialize(TacticsCharacterController boundTarget, TacticsAbilityHitEffectDefinition definition)
    {
        target = boundTarget;
        frames = new Sprite[definition.FrameCount];
        for (int i = 0; i < definition.FrameCount; i++)
        {
            frames[i] = definition.Frames[i];
        }

        framesPerSecond = definition.FramesPerSecond;
        duration = definition.Duration;
        scale = definition.Scale;
        worldOffset = definition.WorldOffset;
        tint = definition.Tint;
        sortingOrderOffset = definition.SortingOrderOffset;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.color = tint;
        transform.localScale = Vector3.one * scale;
        UpdateAnchorFromTarget();
        RefreshVisual();
    }

    private void LateUpdate()
    {
        if (frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= duration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateAnchorFromTarget();
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0)
        {
            return;
        }

        int frameIndex = Mathf.Clamp(Mathf.FloorToInt(elapsedTime * framesPerSecond), 0, frames.Length - 1);
        spriteRenderer.sprite = frames[frameIndex];
        spriteRenderer.color = tint;
        spriteRenderer.sortingLayerID = lastKnownSortingLayerId;
        spriteRenderer.sortingOrder = lastKnownSortingOrder;
        transform.position = lastKnownWorldPosition;
    }

    private void UpdateAnchorFromTarget()
    {
        if (target == null || !target || !target.IsPresentationVisible)
        {
            return;
        }

        Vector3 anchorPosition = target.GetHitEffectAnchorPosition();
        lastKnownWorldPosition = anchorPosition + new Vector3(worldOffset.x, worldOffset.y, 0f);
        lastKnownSortingLayerId = target.GetCombatTextSortingLayerId();
        lastKnownSortingOrder = target.GetCombatTextSortingOrder() + sortingOrderOffset;
    }
}
