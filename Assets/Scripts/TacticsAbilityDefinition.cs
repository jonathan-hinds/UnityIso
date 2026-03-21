using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TacticsAbility", menuName = "Tactics/Combat/Ability")]
public sealed class TacticsAbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string abilityId = "attack";
    [SerializeField] private string displayName = "Attack";
    [SerializeField, TextArea] private string description = "A basic melee strike.";

    [Header("Targeting")]
    [SerializeField, Min(1)] private int range = 1;
    [SerializeField] private TacticsAbilityTargetRule targetRule = TacticsAbilityTargetRule.HostileUnit;

    [Header("Effects")]
    [SerializeField] private List<TacticsAbilityEffectDefinitionData> effects = new()
    {
        TacticsAbilityEffectDefinitionData.CreateDealDamage()
    };

    public string AbilityId => abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public string Description => description;
    public int Range => Mathf.Max(1, range);
    public TacticsAbilityTargetRule TargetRule => targetRule;
    public IReadOnlyList<TacticsAbilityEffectDefinitionData> Effects => effects;

    public static TacticsAbilityDefinition CreateFallbackAttack()
    {
        TacticsAbilityDefinition ability = CreateInstance<TacticsAbilityDefinition>();
        ability.hideFlags = HideFlags.HideAndDontSave;
        ability.abilityId = "attack";
        ability.displayName = "Attack";
        ability.description = "A basic melee strike.";
        ability.range = 1;
        ability.targetRule = TacticsAbilityTargetRule.HostileUnit;
        ability.effects = new List<TacticsAbilityEffectDefinitionData>
        {
            TacticsAbilityEffectDefinitionData.CreateDealDamage()
        };
        return ability;
    }

    private void OnValidate()
    {
        abilityId = string.IsNullOrWhiteSpace(abilityId)
            ? name.ToLowerInvariant().Replace(' ', '_')
            : abilityId.Trim();

        displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        range = Mathf.Max(1, range);
        effects ??= new List<TacticsAbilityEffectDefinitionData>();
    }
}

public static class TacticsAbilityCatalogResources
{
    public const string ResourcePath = "Tactics/Combat/AbilityCatalog";

    private static TacticsAbilityCatalog cachedCatalog;
    private static TacticsAbilityDefinition fallbackAttackAbility;

    public static TacticsAbilityCatalog LoadCatalog()
    {
        if (cachedCatalog == null)
        {
            cachedCatalog = Resources.Load<TacticsAbilityCatalog>(ResourcePath);
            if (cachedCatalog == null)
            {
                cachedCatalog = CreateFallbackCatalog();
            }
        }

        return cachedCatalog;
    }

    private static TacticsAbilityCatalog CreateFallbackCatalog()
    {
        fallbackAttackAbility ??= TacticsAbilityDefinition.CreateFallbackAttack();
        return TacticsAbilityCatalog.CreateFallback(fallbackAttackAbility);
    }
}

public enum TacticsAbilityTargetRule
{
    HostileUnit = 0
}

public enum TacticsAbilityEffectKind
{
    DealDamage = 0
}

public enum TacticsDamageFormula
{
    AttackerBaseDamage = 0,
    FlatValue = 1
}

[Serializable]
public struct TacticsAbilityEffectDefinitionData
{
    [SerializeField] private TacticsAbilityEffectKind effectKind;
    [SerializeField] private TacticsDealDamageEffectData dealDamage;

    public TacticsAbilityEffectKind EffectKind => effectKind;
    public TacticsDealDamageEffectData DealDamage => dealDamage;

    public static TacticsAbilityEffectDefinitionData CreateDealDamage()
    {
        return new TacticsAbilityEffectDefinitionData
        {
            effectKind = TacticsAbilityEffectKind.DealDamage,
            dealDamage = TacticsDealDamageEffectData.Default()
        };
    }
}

[Serializable]
public struct TacticsDealDamageEffectData
{
    [SerializeField] private TacticsDamageFormula damageFormula;
    [SerializeField, Min(0)] private int flatAmount;
    [SerializeField] private int bonusAmount;

    public TacticsDamageFormula DamageFormula => damageFormula;
    public int FlatAmount => Mathf.Max(0, flatAmount);
    public int BonusAmount => bonusAmount;

    public static TacticsDealDamageEffectData Default()
    {
        return new TacticsDealDamageEffectData
        {
            damageFormula = TacticsDamageFormula.AttackerBaseDamage,
            flatAmount = 0,
            bonusAmount = 0
        };
    }
}
