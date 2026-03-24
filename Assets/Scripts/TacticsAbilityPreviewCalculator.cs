using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class TacticsAbilityPreviewCalculator
{
    private const float CriticalHitDamageMultiplier = 1.5f;

    public static TacticsAbilityTooltipContent BuildTooltipContent(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        string statusText = "")
    {
        if (ability == null)
        {
            return default;
        }

        string metaText = $"{BuildCostLabel(ability)}    {BuildRangeLabel(ability)}";
        string bodyText = BuildBodyText(source, ability);
        string footerText = string.IsNullOrWhiteSpace(statusText) || statusText == "Ready"
            ? string.Empty
            : statusText.Trim();

        return new TacticsAbilityTooltipContent(
            ability.DisplayName.ToUpperInvariant(),
            metaText,
            bodyText,
            footerText);
    }

    public static string BuildCostLabel(TacticsAbilityDefinition ability)
    {
        if (ability == null || !ability.HasResourceCost)
        {
            return "NO COST";
        }

        return ability.CostResourceType switch
        {
            TacticsAbilityResourceType.Stamina => $"COST ST {ability.CostAmount}",
            TacticsAbilityResourceType.Mana => $"COST MP {ability.CostAmount}",
            _ => "NO COST"
        };
    }

    public static string BuildRangeLabel(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return "RANGE ?";
        }

        return ability.RangeType switch
        {
            TacticsAbilityRangeType.Melee => "MELEE 1",
            TacticsAbilityRangeType.Ranged => $"RANGED {ability.Range}",
            TacticsAbilityRangeType.AbsoluteRanged => $"ABSOLUTE {ability.Range}",
            TacticsAbilityRangeType.SurroundingAoE => $"SURROUNDING {ability.AreaOfEffectSize}x{ability.AreaOfEffectSize}",
            TacticsAbilityRangeType.RangedAoE => $"RANGED AOE {ability.Range} / {ability.AreaOfEffectSize}x{ability.AreaOfEffectSize}",
            TacticsAbilityRangeType.AbsoluteAoE => $"ABSOLUTE AOE {ability.Range} / {ability.AreaOfEffectSize}x{ability.AreaOfEffectSize}",
            _ => $"RANGE {ability.Range}"
        };
    }

    private static string BuildBodyText(TacticsCharacterController source, TacticsAbilityDefinition ability)
    {
        StringBuilder builder = new();

        string description = string.IsNullOrWhiteSpace(ability.Description)
            ? "No description."
            : ability.Description.Trim();
        builder.Append(description);

        List<string> previewLines = BuildEffectPreviewLines(source, ability);
        if (previewLines.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
            for (int i = 0; i < previewLines.Count; i++)
            {
                builder.Append(previewLines[i]);
                if (i < previewLines.Count - 1)
                {
                    builder.AppendLine();
                }
            }
        }

        return builder.ToString();
    }

    private static List<string> BuildEffectPreviewLines(TacticsCharacterController source, TacticsAbilityDefinition ability)
    {
        List<string> lines = new();
        if (source == null || ability == null || ability.Effects == null)
        {
            return lines;
        }

        for (int i = 0; i < ability.Effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = ability.Effects[i];
            switch (effect.EffectKind)
            {
                case TacticsAbilityEffectKind.DealDamage:
                    lines.Add(BuildDamagePreviewLine(source, ability, effect.DealDamage));
                    break;

                case TacticsAbilityEffectKind.RestoreHitPoints:
                    lines.Add(BuildHealingPreviewLine(source, effect.RestoreHitPoints));
                    break;
            }
        }

        return lines;
    }

    private static string BuildDamagePreviewLine(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        TacticsDealDamageEffectData damage)
    {
        int bonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, damage.Scaling);
        (int baseMin, int baseMax) = GetBaseAmountRange(source, ability != null ? ability.DamageType : TacticsAbilityDamageType.Melee, damage.DamageFormula, damage.FlatAmount);
        (int amountMin, int amountMax) = ApplyBonusAndMultiplier(baseMin, baseMax, bonus, damage.BonusMultiplier);

        StringBuilder builder = new();
        builder.Append("Deals ");
        builder.Append(FormatRange(amountMin, amountMax));
        builder.Append(' ');
        builder.Append(GetDamageTypeLabel(ability != null ? ability.DamageType : TacticsAbilityDamageType.Melee));
        builder.Append(" damage");

        string scalingText = BuildScalingText(damage.Scaling);
        if (!string.IsNullOrWhiteSpace(scalingText))
        {
            builder.Append(" (");
            builder.Append(scalingText);
            builder.Append(')');
        }

        float critChance = ability != null && ability.DamageType == TacticsAbilityDamageType.Magic
            ? source.MagicCriticalHitChance
            : source.MeleeCriticalHitChance;
        if (critChance > 0f && amountMax > 0)
        {
            int critMin = Mathf.Max(1, Mathf.RoundToInt(amountMin * CriticalHitDamageMultiplier));
            int critMax = Mathf.Max(1, Mathf.RoundToInt(amountMax * CriticalHitDamageMultiplier));
            builder.Append(". Crit ");
            builder.Append(Mathf.RoundToInt(Mathf.Clamp01(critChance) * 100f));
            builder.Append("%: ");
            builder.Append(FormatRange(critMin, critMax));
        }

        return builder.ToString();
    }

    private static string BuildHealingPreviewLine(
        TacticsCharacterController source,
        TacticsRestoreHitPointsEffectData restoreHitPoints)
    {
        int bonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(source, restoreHitPoints.Scaling);
        (int baseMin, int baseMax) = GetBaseAmountRange(source, TacticsAbilityDamageType.Magic, restoreHitPoints.HealingFormula, restoreHitPoints.FlatAmount);
        (int amountMin, int amountMax) = ApplyBonusAndMultiplier(baseMin, baseMax, bonus, restoreHitPoints.BonusMultiplier);

        StringBuilder builder = new();
        builder.Append("Restores ");
        builder.Append(FormatRange(amountMin, amountMax));
        builder.Append(" HP");

        string scalingText = BuildScalingText(restoreHitPoints.Scaling);
        if (!string.IsNullOrWhiteSpace(scalingText))
        {
            builder.Append(" (");
            builder.Append(scalingText);
            builder.Append(')');
        }

        return builder.ToString();
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

    private static string BuildScalingText(IReadOnlyList<TacticsAbilityScalingDefinitionData> scalingDefinitions)
    {
        if (scalingDefinitions == null || scalingDefinitions.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new("Scales with ");
        for (int i = 0; i < scalingDefinitions.Count; i++)
        {
            TacticsAbilityScalingDefinitionData scaling = scalingDefinitions[i];
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GetScalingStatLabel(scaling.Stat));
            builder.Append(' ');
            builder.Append(scaling.Rank);
        }

        return builder.ToString();
    }

    private static string GetScalingStatLabel(TacticsAbilityScalingStat stat)
    {
        return stat switch
        {
            TacticsAbilityScalingStat.Strength => "STR",
            TacticsAbilityScalingStat.Agility => "AGI",
            TacticsAbilityScalingStat.Stamina => "STA",
            TacticsAbilityScalingStat.Wisdom => "WIS",
            TacticsAbilityScalingStat.Intelligence => "INT",
            _ => stat.ToString().ToUpperInvariant()
        };
    }

    private static string GetDamageTypeLabel(TacticsAbilityDamageType damageType)
    {
        return damageType == TacticsAbilityDamageType.Magic ? "magic" : "physical";
    }

    private static string FormatRange(int minAmount, int maxAmount)
    {
        return minAmount == maxAmount ? minAmount.ToString() : $"{minAmount}-{maxAmount}";
    }
}
