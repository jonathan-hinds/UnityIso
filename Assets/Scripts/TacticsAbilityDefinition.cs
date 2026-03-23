using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "TacticsAbility", menuName = "Tactics/Combat/Ability")]
public sealed class TacticsAbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string abilityId = "attack";
    [SerializeField] private string displayName = "Attack";
    [SerializeField, TextArea] private string description = "A basic melee strike.";

    [Header("Targeting")]
    [SerializeField] private TacticsAbilityRangeType rangeType = TacticsAbilityRangeType.Melee;
    [SerializeField, Min(1)] private int range = 1;
    [SerializeField] private TacticsAbilityTargetRule targetRule = TacticsAbilityTargetRule.HostileUnit;
    [SerializeField] private TacticsAbilityDamageType damageType = TacticsAbilityDamageType.Melee;

    [Header("Effects")]
    [SerializeField] private List<TacticsAbilityEffectDefinitionData> effects = new()
    {
        TacticsAbilityEffectDefinitionData.CreateDealDamage()
    };

    public string AbilityId => abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public string Description => description;
    public TacticsAbilityRangeType RangeType => rangeType;
    public int Range => rangeType == TacticsAbilityRangeType.Melee ? 1 : Mathf.Max(1, range);
    public TacticsAbilityTargetRule TargetRule => targetRule;
    public TacticsAbilityDamageType DamageType => damageType;
    public IReadOnlyList<TacticsAbilityEffectDefinitionData> Effects => effects;

    public static TacticsAbilityDefinition CreateFallbackAttack()
    {
        TacticsAbilityDefinition ability = CreateInstance<TacticsAbilityDefinition>();
        ability.hideFlags = HideFlags.HideAndDontSave;
        ability.abilityId = "attack";
        ability.displayName = "Attack";
        ability.description = "A basic melee strike.";
        ability.rangeType = TacticsAbilityRangeType.Melee;
        ability.range = 1;
        ability.targetRule = TacticsAbilityTargetRule.HostileUnit;
        ability.damageType = TacticsAbilityDamageType.Melee;
        ability.effects = new List<TacticsAbilityEffectDefinitionData>
        {
            TacticsAbilityEffectDefinitionData.CreateDealDamage(
                TacticsDealDamageEffectData.CreateScaledDamage(
                    TacticsDamageFormula.AttackerBaseDamage,
                    TacticsAbilityScalingDefinitionData.Create(
                        TacticsAbilityScalingStat.Strength,
                        TacticsAbilityScalingRank.A)))
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

        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            effect.Sanitize();
            effects[i] = effect;
        }
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

public enum TacticsAbilityRangeType
{
    Melee = 0,
    Ranged = 1,
    AbsoluteRanged = 2
}

public enum TacticsAbilityEffectKind
{
    DealDamage = 0
}

public enum TacticsAbilityDamageType
{
    Melee = 0,
    Magic = 1
}

public enum TacticsDamageFormula
{
    AttackerBaseDamage = 0,
    FlatValue = 1
}

public enum TacticsAbilityScalingStat
{
    Strength = 0,
    Agility = 1,
    Stamina = 2,
    Wisdom = 3,
    Intelligence = 4
}

public enum TacticsAbilityScalingRank
{
    E = 0,
    D = 1,
    C = 2,
    B = 3,
    A = 4,
    S = 5
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

    public static TacticsAbilityEffectDefinitionData CreateDealDamage(TacticsDealDamageEffectData damageEffect)
    {
        return new TacticsAbilityEffectDefinitionData
        {
            effectKind = TacticsAbilityEffectKind.DealDamage,
            dealDamage = damageEffect
        };
    }

    public void Sanitize()
    {
        dealDamage.Sanitize();
    }
}

[Serializable]
public struct TacticsDealDamageEffectData
{
    [SerializeField] private TacticsDamageFormula damageFormula;
    [SerializeField, Min(0)] private int flatAmount;
    [SerializeField, Min(0.01f)]
    [FormerlySerializedAs("bonusAmount")]
    private float bonusMultiplier;
    [SerializeField] private List<TacticsAbilityScalingDefinitionData> scaling;

    public TacticsDamageFormula DamageFormula => damageFormula;
    public int FlatAmount => Mathf.Max(0, flatAmount);
    public float BonusMultiplier => bonusMultiplier <= 0f ? 1f : bonusMultiplier;
    public IReadOnlyList<TacticsAbilityScalingDefinitionData> Scaling => scaling;

    public static TacticsDealDamageEffectData Default()
    {
        return new TacticsDealDamageEffectData
        {
            damageFormula = TacticsDamageFormula.AttackerBaseDamage,
            flatAmount = 0,
            bonusMultiplier = 1f,
            scaling = new List<TacticsAbilityScalingDefinitionData>()
        };
    }

    public void Sanitize()
    {
        flatAmount = Mathf.Max(0, flatAmount);
        bonusMultiplier = bonusMultiplier <= 0f ? 1f : bonusMultiplier;
        scaling ??= new List<TacticsAbilityScalingDefinitionData>();
    }

    public static TacticsDealDamageEffectData CreateScaledDamage(
        TacticsDamageFormula formula,
        params TacticsAbilityScalingDefinitionData[] scalingDefinitions)
    {
        return new TacticsDealDamageEffectData
        {
            damageFormula = formula,
            flatAmount = 0,
            bonusMultiplier = 1f,
            scaling = scalingDefinitions != null
                ? new List<TacticsAbilityScalingDefinitionData>(scalingDefinitions)
                : new List<TacticsAbilityScalingDefinitionData>()
        };
    }
}

[Serializable]
public struct TacticsAbilityScalingDefinitionData
{
    [SerializeField] private TacticsAbilityScalingStat stat;
    [SerializeField] private TacticsAbilityScalingRank rank;

    public TacticsAbilityScalingStat Stat => stat;
    public TacticsAbilityScalingRank Rank => rank;

    public static TacticsAbilityScalingDefinitionData Create(
        TacticsAbilityScalingStat stat,
        TacticsAbilityScalingRank rank)
    {
        return new TacticsAbilityScalingDefinitionData
        {
            stat = stat,
            rank = rank
        };
    }
}

public static class TacticsAbilityScalingCalculator
{
    public static int EvaluateDamageBonus(
        TacticsCharacterController source,
        IReadOnlyList<TacticsAbilityScalingDefinitionData> scalingDefinitions)
    {
        if (source == null || scalingDefinitions == null || scalingDefinitions.Count == 0)
        {
            return 0;
        }

        float totalBonus = 0f;
        for (int i = 0; i < scalingDefinitions.Count; i++)
        {
            TacticsAbilityScalingDefinitionData scaling = scalingDefinitions[i];
            int statValue = source.GetPrimaryStat(scaling.Stat);
            totalBonus += statValue * GetRankMultiplier(scaling.Rank);
        }

        return Mathf.Max(0, Mathf.RoundToInt(totalBonus));
    }

    private static float GetRankMultiplier(TacticsAbilityScalingRank rank)
    {
        return rank switch
        {
            TacticsAbilityScalingRank.S => 1.4f,
            TacticsAbilityScalingRank.A => 1.1f,
            TacticsAbilityScalingRank.B => 0.85f,
            TacticsAbilityScalingRank.C => 0.6f,
            TacticsAbilityScalingRank.D => 0.35f,
            TacticsAbilityScalingRank.E => 0.15f,
            _ => 0f
        };
    }
}
