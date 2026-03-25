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
    [SerializeField, Min(1)] private int areaOfEffectSize = 1;
    [SerializeField] private TacticsAbilityTargetRule targetRule = TacticsAbilityTargetRule.HostileUnit;
    [SerializeField] private TacticsAbilityDamageType damageType = TacticsAbilityDamageType.Melee;

    [Header("Presentation")]
    [SerializeField] private TacticsAbilityProjectile projectilePrefab;

    [Header("Effects")]
    [SerializeField] private List<TacticsAbilityEffectDefinitionData> effects = new()
    {
        TacticsAbilityEffectDefinitionData.CreateDealDamage()
    };

    [Header("Cost")]
    [SerializeField] private TacticsAbilityResourceType costResourceType = TacticsAbilityResourceType.None;
    [SerializeField, Min(0)] private int costAmount;

    public string AbilityId => abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public string Description => description;
    public TacticsAbilityRangeType RangeType => rangeType;
    public int Range => rangeType == TacticsAbilityRangeType.Melee ? 1 : UsesAbilityRange ? Mathf.Max(1, range) : 0;
    public int AreaOfEffectSize => UsesAreaOfEffect ? Mathf.Max(1, areaOfEffectSize) : 1;
    public int AreaOfEffectRadius => Mathf.Max(0, AreaOfEffectSize / 2);
    public bool UsesAbilityRange => rangeType == TacticsAbilityRangeType.Ranged || rangeType == TacticsAbilityRangeType.AbsoluteRanged ||
                                    rangeType == TacticsAbilityRangeType.RangedAoE || rangeType == TacticsAbilityRangeType.AbsoluteAoE;
    public bool UsesAreaOfEffect => rangeType == TacticsAbilityRangeType.SurroundingAoE ||
                                    rangeType == TacticsAbilityRangeType.RangedAoE ||
                                    rangeType == TacticsAbilityRangeType.AbsoluteAoE;
    public bool RequiresTargetSelection => true;
    public TacticsAbilityTargetRule TargetRule => targetRule;
    public TacticsAbilityDamageType DamageType => damageType;
    public TacticsAbilityProjectile ProjectilePrefab => projectilePrefab;
    public bool UsesProjectilePresentation => projectilePrefab != null;
    public IReadOnlyList<TacticsAbilityEffectDefinitionData> Effects => effects;
    public TacticsAbilityResourceType CostResourceType => costAmount > 0 ? costResourceType : TacticsAbilityResourceType.None;
    public int CostAmount => Mathf.Max(0, costAmount);
    public bool HasResourceCost => CostResourceType != TacticsAbilityResourceType.None && CostAmount > 0;

    public static TacticsAbilityDefinition CreateFallbackAttack()
    {
        TacticsAbilityDefinition ability = CreateInstance<TacticsAbilityDefinition>();
        ability.hideFlags = HideFlags.HideAndDontSave;
        ability.abilityId = "attack";
        ability.displayName = "Attack";
        ability.description = "A basic melee strike.";
        ability.rangeType = TacticsAbilityRangeType.Melee;
        ability.range = 1;
        ability.areaOfEffectSize = 1;
        ability.targetRule = TacticsAbilityTargetRule.HostileUnit;
        ability.damageType = TacticsAbilityDamageType.Melee;
        ability.costResourceType = TacticsAbilityResourceType.None;
        ability.costAmount = 0;
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
        areaOfEffectSize = Mathf.Max(1, areaOfEffectSize);
        if (areaOfEffectSize % 2 == 0)
        {
            areaOfEffectSize += 1;
        }

        costAmount = Mathf.Max(0, costAmount);
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
    HostileUnit = 0,
    AlliedUnit = 1,
    AlliedUnitOrSelf = 2,
    Self = 3
}

public enum TacticsAbilityRangeType
{
    Melee = 0,
    Ranged = 1,
    AbsoluteRanged = 2,
    SurroundingAoE = 3,
    RangedAoE = 4,
    AbsoluteAoE = 5
}

public enum TacticsAbilityEffectKind
{
    DealDamage = 0,
    RestoreHitPoints = 1,
    RestoreResource = 2
}

public enum TacticsAbilityDamageType
{
    Melee = 0,
    Magic = 1
}

public enum TacticsAbilityResourceType
{
    None = 0,
    Stamina = 1,
    Mana = 2
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
    [SerializeField] private TacticsRestoreHitPointsEffectData restoreHitPoints;
    [SerializeField] private TacticsRestoreResourceEffectData restoreResource;

    public TacticsAbilityEffectKind EffectKind => effectKind;
    public TacticsDealDamageEffectData DealDamage => dealDamage;
    public TacticsRestoreHitPointsEffectData RestoreHitPoints => restoreHitPoints;
    public TacticsRestoreResourceEffectData RestoreResource => restoreResource;

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

    public static TacticsAbilityEffectDefinitionData CreateRestoreHitPoints()
    {
        return new TacticsAbilityEffectDefinitionData
        {
            effectKind = TacticsAbilityEffectKind.RestoreHitPoints,
            restoreHitPoints = TacticsRestoreHitPointsEffectData.Default()
        };
    }

    public static TacticsAbilityEffectDefinitionData CreateRestoreHitPoints(TacticsRestoreHitPointsEffectData restoreEffect)
    {
        return new TacticsAbilityEffectDefinitionData
        {
            effectKind = TacticsAbilityEffectKind.RestoreHitPoints,
            restoreHitPoints = restoreEffect
        };
    }

    public static TacticsAbilityEffectDefinitionData CreateRestoreResource()
    {
        return new TacticsAbilityEffectDefinitionData
        {
            effectKind = TacticsAbilityEffectKind.RestoreResource,
            restoreResource = TacticsRestoreResourceEffectData.Default()
        };
    }

    public static TacticsAbilityEffectDefinitionData CreateRestoreResource(TacticsRestoreResourceEffectData restoreEffect)
    {
        return new TacticsAbilityEffectDefinitionData
        {
            effectKind = TacticsAbilityEffectKind.RestoreResource,
            restoreResource = restoreEffect
        };
    }

    public void Sanitize()
    {
        dealDamage.Sanitize();
        restoreHitPoints.Sanitize();
        restoreResource.Sanitize();
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
public struct TacticsRestoreHitPointsEffectData
{
    [SerializeField] private TacticsDamageFormula healingFormula;
    [SerializeField, Min(0)] private int flatAmount;
    [SerializeField, Min(0.01f)] private float bonusMultiplier;
    [SerializeField] private List<TacticsAbilityScalingDefinitionData> scaling;

    public TacticsDamageFormula HealingFormula => healingFormula;
    public int FlatAmount => Mathf.Max(0, flatAmount);
    public float BonusMultiplier => bonusMultiplier <= 0f ? 1f : bonusMultiplier;
    public IReadOnlyList<TacticsAbilityScalingDefinitionData> Scaling => scaling;

    public static TacticsRestoreHitPointsEffectData Default()
    {
        return new TacticsRestoreHitPointsEffectData
        {
            healingFormula = TacticsDamageFormula.FlatValue,
            flatAmount = 1,
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

    public static TacticsRestoreHitPointsEffectData CreateScaledHealing(
        TacticsDamageFormula formula,
        int flatAmount = 0,
        params TacticsAbilityScalingDefinitionData[] scalingDefinitions)
    {
        return new TacticsRestoreHitPointsEffectData
        {
            healingFormula = formula,
            flatAmount = Mathf.Max(0, flatAmount),
            bonusMultiplier = 1f,
            scaling = scalingDefinitions != null
                ? new List<TacticsAbilityScalingDefinitionData>(scalingDefinitions)
                : new List<TacticsAbilityScalingDefinitionData>()
        };
    }
}

[Serializable]
public struct TacticsRestoreResourceEffectData
{
    [SerializeField] private TacticsAbilityResourceType resourceType;
    [SerializeField] private TacticsDamageFormula restoreFormula;
    [SerializeField, Min(0)] private int flatAmount;
    [SerializeField, Min(0.01f)] private float bonusMultiplier;
    [SerializeField] private List<TacticsAbilityScalingDefinitionData> scaling;

    public TacticsAbilityResourceType ResourceType => resourceType;
    public TacticsDamageFormula RestoreFormula => restoreFormula;
    public int FlatAmount => Mathf.Max(0, flatAmount);
    public float BonusMultiplier => bonusMultiplier <= 0f ? 1f : bonusMultiplier;
    public IReadOnlyList<TacticsAbilityScalingDefinitionData> Scaling => scaling;

    public static TacticsRestoreResourceEffectData Default()
    {
        return new TacticsRestoreResourceEffectData
        {
            resourceType = TacticsAbilityResourceType.Stamina,
            restoreFormula = TacticsDamageFormula.FlatValue,
            flatAmount = 1,
            bonusMultiplier = 1f,
            scaling = new List<TacticsAbilityScalingDefinitionData>()
        };
    }

    public void Sanitize()
    {
        if (resourceType == TacticsAbilityResourceType.None)
        {
            resourceType = TacticsAbilityResourceType.Stamina;
        }

        flatAmount = Mathf.Max(0, flatAmount);
        bonusMultiplier = bonusMultiplier <= 0f ? 1f : bonusMultiplier;
        scaling ??= new List<TacticsAbilityScalingDefinitionData>();
    }

    public static TacticsRestoreResourceEffectData Create(
        TacticsAbilityResourceType resourceType,
        TacticsDamageFormula formula,
        int flatAmount = 0,
        params TacticsAbilityScalingDefinitionData[] scalingDefinitions)
    {
        return new TacticsRestoreResourceEffectData
        {
            resourceType = resourceType == TacticsAbilityResourceType.None
                ? TacticsAbilityResourceType.Stamina
                : resourceType,
            restoreFormula = formula,
            flatAmount = Mathf.Max(0, flatAmount),
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

public static class TacticsAbilityEffectMath
{
    public static int EvaluateDamageAmount(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        TacticsDealDamageEffectData damage,
        bool useAverageRoll)
    {
        if (source == null)
        {
            return 0;
        }

        float baseAmount = damage.DamageFormula switch
        {
            TacticsDamageFormula.FlatValue => damage.FlatAmount,
            _ => useAverageRoll
                ? GetAverageBaseDamage(source, ability != null ? ability.DamageType : TacticsAbilityDamageType.Melee)
                : source.RollBaseDamage(ability != null ? ability.DamageType : TacticsAbilityDamageType.Melee)
        };

        float scalingBonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, damage.Scaling);
        return Mathf.Max(0, Mathf.RoundToInt((baseAmount + scalingBonus) * damage.BonusMultiplier));
    }

    public static int EvaluateRestoreHitPointsAmount(
        TacticsCharacterController source,
        TacticsRestoreHitPointsEffectData restoreHitPoints,
        bool useAverageRoll)
    {
        if (source == null)
        {
            return 0;
        }

        float baseAmount = restoreHitPoints.HealingFormula switch
        {
            TacticsDamageFormula.AttackerBaseDamage => useAverageRoll
                ? GetAverageBaseDamage(source, TacticsAbilityDamageType.Magic)
                : source.RollBaseDamage(TacticsAbilityDamageType.Magic),
            _ => restoreHitPoints.FlatAmount
        };

        float scalingBonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, restoreHitPoints.Scaling);
        return Mathf.Max(0, Mathf.RoundToInt((baseAmount + scalingBonus) * restoreHitPoints.BonusMultiplier));
    }

    public static int EvaluateRestoreResourceAmount(
        TacticsCharacterController source,
        TacticsRestoreResourceEffectData restoreResource,
        bool useAverageRoll)
    {
        if (source == null || restoreResource.ResourceType == TacticsAbilityResourceType.None)
        {
            return 0;
        }

        float baseAmount = restoreResource.RestoreFormula switch
        {
            TacticsDamageFormula.AttackerBaseDamage => useAverageRoll
                ? GetAverageBaseDamage(source, ResolveScalingDamageType(restoreResource.ResourceType))
                : source.RollBaseDamage(ResolveScalingDamageType(restoreResource.ResourceType)),
            _ => restoreResource.FlatAmount
        };

        float scalingBonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, restoreResource.Scaling);
        return Mathf.Max(0, Mathf.RoundToInt((baseAmount + scalingBonus) * restoreResource.BonusMultiplier));
    }

    public static (int min, int max) GetDamageAmountRange(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        TacticsDealDamageEffectData damage)
    {
        int bonus = source != null
            ? TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, damage.Scaling)
            : 0;
        (int baseMin, int baseMax) = GetBaseAmountRange(
            source,
            ability != null ? ability.DamageType : TacticsAbilityDamageType.Melee,
            damage.DamageFormula,
            damage.FlatAmount);
        return ApplyBonusAndMultiplier(baseMin, baseMax, bonus, damage.BonusMultiplier);
    }

    public static (int min, int max) GetRestoreHitPointsAmountRange(
        TacticsCharacterController source,
        TacticsRestoreHitPointsEffectData restoreHitPoints)
    {
        int bonus = source != null
            ? TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, restoreHitPoints.Scaling)
            : 0;
        (int baseMin, int baseMax) = GetBaseAmountRange(
            source,
            TacticsAbilityDamageType.Magic,
            restoreHitPoints.HealingFormula,
            restoreHitPoints.FlatAmount);
        return ApplyBonusAndMultiplier(baseMin, baseMax, bonus, restoreHitPoints.BonusMultiplier);
    }

    public static (int min, int max) GetRestoreResourceAmountRange(
        TacticsCharacterController source,
        TacticsRestoreResourceEffectData restoreResource)
    {
        int bonus = source != null
            ? TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, restoreResource.Scaling)
            : 0;
        (int baseMin, int baseMax) = GetBaseAmountRange(
            source,
            ResolveScalingDamageType(restoreResource.ResourceType),
            restoreResource.RestoreFormula,
            restoreResource.FlatAmount);
        return ApplyBonusAndMultiplier(baseMin, baseMax, bonus, restoreResource.BonusMultiplier);
    }

    public static float GetAverageBaseDamage(TacticsCharacterController source, TacticsAbilityDamageType damageType)
    {
        if (source == null)
        {
            return 0f;
        }

        return damageType == TacticsAbilityDamageType.Magic
            ? (source.BaseMagicDamageMin + source.BaseMagicDamageMax) * 0.5f
            : (source.BaseMeleeDamageMin + source.BaseMeleeDamageMax) * 0.5f;
    }

    private static TacticsAbilityDamageType ResolveScalingDamageType(TacticsAbilityResourceType resourceType)
    {
        return resourceType == TacticsAbilityResourceType.Mana
            ? TacticsAbilityDamageType.Magic
            : TacticsAbilityDamageType.Melee;
    }

    private static (int min, int max) GetBaseAmountRange(
        TacticsCharacterController source,
        TacticsAbilityDamageType damageType,
        TacticsDamageFormula formula,
        int flatAmount)
    {
        if (formula == TacticsDamageFormula.FlatValue)
        {
            int value = Mathf.Max(0, flatAmount);
            return (value, value);
        }

        if (source == null)
        {
            return (0, 0);
        }

        return damageType == TacticsAbilityDamageType.Magic
            ? (Mathf.Max(0, source.BaseMagicDamageMin), Mathf.Max(source.BaseMagicDamageMin, source.BaseMagicDamageMax))
            : (Mathf.Max(0, source.BaseMeleeDamageMin), Mathf.Max(source.BaseMeleeDamageMin, source.BaseMeleeDamageMax));
    }

    private static (int min, int max) ApplyBonusAndMultiplier(int baseMin, int baseMax, int bonus, float multiplier)
    {
        int minAmount = Mathf.Max(0, Mathf.RoundToInt((baseMin + bonus) * multiplier));
        int maxAmount = Mathf.Max(0, Mathf.RoundToInt((baseMax + bonus) * multiplier));
        return (minAmount, Mathf.Max(minAmount, maxAmount));
    }
}
