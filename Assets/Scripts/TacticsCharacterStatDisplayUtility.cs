using System.Collections.Generic;
using UnityEngine;

public enum TacticsDerivedStatDisplayType
{
    Move,
    Jump,
    Hit,
    Dodge,
    Block,
    MaxHitPoints,
    MaxStamina,
    MaxMana,
    MeleeDamage,
    MagicDamage,
    MeleeCriticalHitChance,
    MagicCriticalHitChance
}

public readonly struct TacticsDerivedStatDisplayDefinition
{
    public TacticsDerivedStatDisplayDefinition(TacticsDerivedStatDisplayType statType, string label)
    {
        StatType = statType;
        Label = label;
    }

    public TacticsDerivedStatDisplayType StatType { get; }
    public string Label { get; }
}

public static class TacticsCharacterStatDisplayUtility
{
    private static readonly TacticsDerivedStatDisplayDefinition[] InGamePrimaryDerivedStats =
    {
        new(TacticsDerivedStatDisplayType.Move, "MOVE"),
        new(TacticsDerivedStatDisplayType.Jump, "JUMP")
    };

    private static readonly TacticsDerivedStatDisplayDefinition[] InGameSecondaryDerivedStats =
    {
        new(TacticsDerivedStatDisplayType.MaxHitPoints, "MAX HP"),
        new(TacticsDerivedStatDisplayType.MaxStamina, "MAX SP"),
        new(TacticsDerivedStatDisplayType.MaxMana, "MAX MP"),
        new(TacticsDerivedStatDisplayType.MeleeDamage, "MELEE"),
        new(TacticsDerivedStatDisplayType.MagicDamage, "MAGIC"),
        new(TacticsDerivedStatDisplayType.MeleeCriticalHitChance, "MELEE CRIT"),
        new(TacticsDerivedStatDisplayType.MagicCriticalHitChance, "MAGIC CRIT"),
        new(TacticsDerivedStatDisplayType.Hit, "HIT"),
        new(TacticsDerivedStatDisplayType.Dodge, "DODGE"),
        new(TacticsDerivedStatDisplayType.Block, "BLOCK")
    };

    private static readonly TacticsDerivedStatDisplayDefinition[] InspectorDerivedStats =
    {
        new(TacticsDerivedStatDisplayType.Move, "MOVE"),
        new(TacticsDerivedStatDisplayType.Jump, "JUMP"),
        new(TacticsDerivedStatDisplayType.MaxHitPoints, "MAX HP"),
        new(TacticsDerivedStatDisplayType.MaxStamina, "MAX SP"),
        new(TacticsDerivedStatDisplayType.MaxMana, "MAX MP"),
        new(TacticsDerivedStatDisplayType.MeleeDamage, "MELEE"),
        new(TacticsDerivedStatDisplayType.MagicDamage, "MAGIC"),
        new(TacticsDerivedStatDisplayType.MeleeCriticalHitChance, "MELEE CRIT"),
        new(TacticsDerivedStatDisplayType.MagicCriticalHitChance, "MAGIC CRIT"),
        new(TacticsDerivedStatDisplayType.Hit, "HIT"),
        new(TacticsDerivedStatDisplayType.Dodge, "DODGE"),
        new(TacticsDerivedStatDisplayType.Block, "BLOCK")
    };

    public static IReadOnlyList<TacticsDerivedStatDisplayDefinition> InGamePrimaryDerivedStatDefinitions => InGamePrimaryDerivedStats;
    public static IReadOnlyList<TacticsDerivedStatDisplayDefinition> InGameSecondaryDerivedStatDefinitions => InGameSecondaryDerivedStats;
    public static IReadOnlyList<TacticsDerivedStatDisplayDefinition> InspectorDerivedStatDefinitions => InspectorDerivedStats;

    public static string FormatResourceValue(float rawValue)
    {
        return Mathf.Max(0, Mathf.RoundToInt(rawValue)).ToString();
    }

    public static string FormatDamageValue(float rawValue)
    {
        return $"{Mathf.Max(0f, rawValue):0.##}";
    }

    public static string FormatChance(float fractionalValue)
    {
        return $"{TacticsCharacterDerivedStatFormula.FractionToPercent(fractionalValue):0.##}%";
    }

    public static string FormatDerivedValue(
        TacticsDerivedStatDisplayType statType,
        TacticsCharacterStats effectiveStats,
        TacticsCharacterDerivedStats derivedStats)
    {
        return statType switch
        {
            TacticsDerivedStatDisplayType.Move => effectiveStats.MoveRange.ToString(),
            TacticsDerivedStatDisplayType.Jump => effectiveStats.JumpHeight.ToString(),
            TacticsDerivedStatDisplayType.Hit => FormatChance(derivedStats.hitChance),
            TacticsDerivedStatDisplayType.Dodge => FormatChance(derivedStats.dodgeChance),
            TacticsDerivedStatDisplayType.Block => FormatChance(derivedStats.blockChance),
            TacticsDerivedStatDisplayType.MaxHitPoints => FormatResourceValue(derivedStats.maxHitPointsValue),
            TacticsDerivedStatDisplayType.MaxStamina => FormatResourceValue(derivedStats.maxStaminaValue),
            TacticsDerivedStatDisplayType.MaxMana => FormatResourceValue(derivedStats.maxManaValue),
            TacticsDerivedStatDisplayType.MeleeDamage => FormatDamageValue(derivedStats.baseMeleeDamage),
            TacticsDerivedStatDisplayType.MagicDamage => FormatDamageValue(derivedStats.baseMagicDamage),
            TacticsDerivedStatDisplayType.MeleeCriticalHitChance => FormatChance(derivedStats.meleeCriticalHitChance),
            TacticsDerivedStatDisplayType.MagicCriticalHitChance => FormatChance(derivedStats.magicCriticalHitChance),
            _ => string.Empty
        };
    }

    public static bool TryFormatDerivedDelta(
        TacticsDerivedStatDisplayType statType,
        TacticsCharacterStats currentStats,
        TacticsCharacterDerivedStats currentDerived,
        TacticsCharacterStats previewStats,
        TacticsCharacterDerivedStats previewDerived,
        out string formattedDelta)
    {
        formattedDelta = string.Empty;

        float delta = GetComparableValue(statType, previewStats, previewDerived) -
                      GetComparableValue(statType, currentStats, currentDerived);

        if (Mathf.Approximately(delta, 0f))
        {
            return true;
        }

        if (IsPercentStat(statType))
        {
            formattedDelta = delta > 0f ? $"+{delta:0.##}%" : $"{delta:0.##}%";
            return true;
        }

        if (IsWholeNumberStat(statType))
        {
            int roundedDelta = Mathf.RoundToInt(delta);
            if (roundedDelta == 0)
            {
                return true;
            }

            formattedDelta = roundedDelta > 0 ? $"+{roundedDelta}" : roundedDelta.ToString();
            return true;
        }

        formattedDelta = delta > 0f ? $"+{delta:0.##}" : $"{delta:0.##}";
        return true;
    }

    public static bool TryFormatNumericDelta(string currentValue, string previewValue, out string formattedDelta)
    {
        formattedDelta = string.Empty;
        if (!float.TryParse(currentValue, out float currentNumber) || !float.TryParse(previewValue, out float previewNumber))
        {
            return false;
        }

        float delta = previewNumber - currentNumber;
        if (Mathf.Approximately(delta, 0f))
        {
            return true;
        }

        formattedDelta = delta > 0f ? $"+{delta:0.##}" : $"{delta:0.##}";
        return true;
    }

    private static bool IsPercentStat(TacticsDerivedStatDisplayType statType)
    {
        return statType is TacticsDerivedStatDisplayType.Hit or
            TacticsDerivedStatDisplayType.Dodge or
            TacticsDerivedStatDisplayType.Block or
            TacticsDerivedStatDisplayType.MeleeCriticalHitChance or
            TacticsDerivedStatDisplayType.MagicCriticalHitChance;
    }

    private static bool IsWholeNumberStat(TacticsDerivedStatDisplayType statType)
    {
        return statType is TacticsDerivedStatDisplayType.Move or
            TacticsDerivedStatDisplayType.Jump or
            TacticsDerivedStatDisplayType.MaxHitPoints or
            TacticsDerivedStatDisplayType.MaxStamina or
            TacticsDerivedStatDisplayType.MaxMana;
    }

    private static float GetComparableValue(
        TacticsDerivedStatDisplayType statType,
        TacticsCharacterStats effectiveStats,
        TacticsCharacterDerivedStats derivedStats)
    {
        return statType switch
        {
            TacticsDerivedStatDisplayType.Move => effectiveStats.MoveRange,
            TacticsDerivedStatDisplayType.Jump => effectiveStats.JumpHeight,
            TacticsDerivedStatDisplayType.Hit => TacticsCharacterDerivedStatFormula.FractionToPercent(derivedStats.hitChance),
            TacticsDerivedStatDisplayType.Dodge => TacticsCharacterDerivedStatFormula.FractionToPercent(derivedStats.dodgeChance),
            TacticsDerivedStatDisplayType.Block => TacticsCharacterDerivedStatFormula.FractionToPercent(derivedStats.blockChance),
            TacticsDerivedStatDisplayType.MaxHitPoints => derivedStats.maxHitPointsValue,
            TacticsDerivedStatDisplayType.MaxStamina => derivedStats.maxStaminaValue,
            TacticsDerivedStatDisplayType.MaxMana => derivedStats.maxManaValue,
            TacticsDerivedStatDisplayType.MeleeDamage => derivedStats.baseMeleeDamage,
            TacticsDerivedStatDisplayType.MagicDamage => derivedStats.baseMagicDamage,
            TacticsDerivedStatDisplayType.MeleeCriticalHitChance => TacticsCharacterDerivedStatFormula.FractionToPercent(derivedStats.meleeCriticalHitChance),
            TacticsDerivedStatDisplayType.MagicCriticalHitChance => TacticsCharacterDerivedStatFormula.FractionToPercent(derivedStats.magicCriticalHitChance),
            _ => 0f
        };
    }
}
