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
    public bool RequiresTargetSelection => rangeType != TacticsAbilityRangeType.SurroundingAoE;
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
    RestoreHitPoints = 1
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

    public TacticsAbilityEffectKind EffectKind => effectKind;
    public TacticsDealDamageEffectData DealDamage => dealDamage;
    public TacticsRestoreHitPointsEffectData RestoreHitPoints => restoreHitPoints;

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

    public void Sanitize()
    {
        dealDamage.Sanitize();
        restoreHitPoints.Sanitize();
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
