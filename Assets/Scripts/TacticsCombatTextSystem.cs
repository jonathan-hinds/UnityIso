using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsCombatTextSystem : MonoBehaviour
{
    private const string SystemObjectName = "Tactics Combat Text System";

    private static TacticsCombatTextSystem instance;

    [SerializeField, Min(0f)] private float verticalSpawnPadding = 0.16f;
    [SerializeField, Min(0)] private int sortingOrderOffset = 24;

    public static TacticsCombatTextSystem Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TacticsCombatTextSystem>();
            }

            if (instance == null)
            {
                GameObject systemObject = new GameObject(SystemObjectName);
                instance = systemObject.AddComponent<TacticsCombatTextSystem>();
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

    public static void ShowDamage(ITacticsCombatTextAnchor target, int amount, bool isCriticalHit = false)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        Instance.SpawnDamageNumber(target, amount, isCriticalHit);
    }

    public static void ShowHealing(ITacticsCombatTextAnchor target, int amount)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        Instance.SpawnHealingNumber(target, amount);
    }

    public static void ShowResourceRestore(ITacticsCombatTextAnchor target, TacticsAbilityResourceType resourceType, int amount)
    {
        if (target == null || amount <= 0 || resourceType == TacticsAbilityResourceType.None)
        {
            return;
        }

        Instance.SpawnResourceRestoreNumber(target, resourceType, amount);
    }

    public static void ShowExperienceReward(ITacticsCombatTextAnchor target, int amount)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        Instance.SpawnText(target, $"+EXP: {amount}", Color.white, Color.black);
    }

    public static void ShowGoldReward(ITacticsCombatTextAnchor target, int amount)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        Instance.SpawnText(target, $"+{amount}G", new Color(0.98f, 0.86f, 0.28f, 1f), new Color(0.32f, 0.21f, 0.02f, 1f), useExperienceMotion: true);
    }

    public static void ShowStatusEffectApplied(ITacticsCombatTextAnchor target, TacticsStatusEffectType statusEffectType)
    {
        if (target == null)
        {
            return;
        }

        Instance.SpawnStatusEffectAppliedText(target, statusEffectType);
    }

    public static void ShowStatusEffectApplied(ITacticsCombatTextAnchor target, TacticsApplyStatusEffectData statusEffect)
    {
        if (target == null)
        {
            return;
        }

        Instance.SpawnStatusEffectAppliedText(target, statusEffect);
    }

    private void SpawnDamageNumber(ITacticsCombatTextAnchor target, int amount, bool isCriticalHit)
    {
        SpawnText(target, isCriticalHit ? $"-{amount}!" : $"-{amount}", Color.white, Color.black);
    }

    private void SpawnHealingNumber(ITacticsCombatTextAnchor target, int amount)
    {
        SpawnText(target, $"+{amount}", new Color(0.54f, 0.9f, 0.5f, 1f), new Color(0.08f, 0.25f, 0.08f, 1f));
    }

    private void SpawnResourceRestoreNumber(ITacticsCombatTextAnchor target, TacticsAbilityResourceType resourceType, int amount)
    {
        string label = resourceType == TacticsAbilityResourceType.Mana ? "MP" : "ST";
        Color fillColor = resourceType == TacticsAbilityResourceType.Mana
            ? new Color(0.45f, 0.8f, 1f, 1f)
            : new Color(0.68f, 0.92f, 0.46f, 1f);
        Color outlineColor = resourceType == TacticsAbilityResourceType.Mana
            ? new Color(0.07f, 0.18f, 0.34f, 1f)
            : new Color(0.12f, 0.24f, 0.08f, 1f);
        SpawnText(target, $"+{amount} {label}", fillColor, outlineColor);
    }

    private void SpawnStatusEffectAppliedText(ITacticsCombatTextAnchor target, TacticsStatusEffectType statusEffectType)
    {
        TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffectType);
        Color fillColor = descriptor.IsBuff
            ? new Color(0.58f, 0.94f, 0.64f, 1f)
            : new Color(1f, 0.72f, 0.4f, 1f);
        Color outlineColor = descriptor.IsBuff
            ? new Color(0.08f, 0.28f, 0.11f, 1f)
            : new Color(0.36f, 0.12f, 0.04f, 1f);
        SpawnText(target, descriptor.DisplayName.ToUpperInvariant(), fillColor, outlineColor);
    }

    private void SpawnStatusEffectAppliedText(ITacticsCombatTextAnchor target, TacticsApplyStatusEffectData statusEffect)
    {
        TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffect);
        Color fillColor = descriptor.IsBuff
            ? new Color(0.58f, 0.94f, 0.64f, 1f)
            : new Color(1f, 0.72f, 0.4f, 1f);
        Color outlineColor = descriptor.IsBuff
            ? new Color(0.08f, 0.28f, 0.11f, 1f)
            : new Color(0.36f, 0.12f, 0.04f, 1f);
        SpawnText(target, descriptor.DisplayName.ToUpperInvariant(), fillColor, outlineColor);
    }

    private void SpawnText(
        ITacticsCombatTextAnchor target,
        string text,
        Color fillColor,
        Color outlineColor,
        bool useExperienceMotion = false)
    {
        Vector3 spawnPosition = target.GetCombatTextSpawnPosition(verticalSpawnPadding);
        int sortingLayerId = target.GetCombatTextSortingLayerId();
        int sortingOrder = target.GetCombatTextSortingOrder() + sortingOrderOffset;

        TacticsFloatingCombatText.Create(
            parent: transform,
            worldPosition: spawnPosition,
            text: text,
            sortingLayerId: sortingLayerId,
            sortingOrder: sortingOrder,
            fillColor: fillColor,
            outlineColor: outlineColor,
            isExperienceText: useExperienceMotion || text.StartsWith("+EXP: "));
    }
}
