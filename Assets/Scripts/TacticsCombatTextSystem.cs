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

    public static void ShowDamage(TacticsCharacterController target, int amount, bool isCriticalHit = false)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        Instance.SpawnDamageNumber(target, amount, isCriticalHit);
    }

    public static void ShowExperienceReward(TacticsCharacterController target, int amount)
    {
        if (target == null || amount <= 0)
        {
            return;
        }

        Instance.SpawnText(target, $"+EXP: {amount}", Color.white, Color.black);
    }

    private void SpawnDamageNumber(TacticsCharacterController target, int amount, bool isCriticalHit)
    {
        SpawnText(target, isCriticalHit ? $"{amount}!" : amount.ToString(), Color.white, Color.black);
    }

    private void SpawnText(TacticsCharacterController target, string text, Color fillColor, Color outlineColor)
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
            isExperienceText: text.StartsWith("+EXP: "));
    }
}
