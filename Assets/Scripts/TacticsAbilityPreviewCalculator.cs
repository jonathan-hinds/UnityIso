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
            if (ability != null && ability.HasMovementCost)
            {
                return "COST MOVE";
            }

            return "NO COST";
        }

        string baseCost = ability.CostResourceType switch
        {
            TacticsAbilityResourceType.Stamina => $"COST ST {ability.CostAmount}",
            TacticsAbilityResourceType.Mana => $"COST MP {ability.CostAmount}",
            _ => "NO COST"
        };

        if (ability.AllowsMovementAsAlternateCost)
        {
            return $"{baseCost} OR MOVE";
        }

        return baseCost;
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
        if (source == null || ability == null)
        {
            return lines;
        }

        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            switch (effect.EffectKind)
            {
                case TacticsAbilityEffectKind.DealDamage:
                    lines.Add(BuildDamagePreviewLine(source, ability, effect.DealDamage));
                    break;

                case TacticsAbilityEffectKind.RestoreHitPoints:
                    lines.Add(BuildHealingPreviewLine(source, effect.RestoreHitPoints));
                    break;

                case TacticsAbilityEffectKind.RestoreResource:
                    lines.Add(BuildResourceRestorePreviewLine(source, effect.RestoreResource));
                    break;
            }
        }

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            lines.Add(BuildStatusEffectPreviewLine(source, ability, statusEffects[i]));
        }

        return lines;
    }

    private static string BuildDamagePreviewLine(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        TacticsDealDamageEffectData damage)
    {
        (int amountMin, int amountMax) = TacticsAbilityEffectMath.GetDamageAmountRange(source, ability, damage);

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
        (int amountMin, int amountMax) = TacticsAbilityEffectMath.GetRestoreHitPointsAmountRange(source, restoreHitPoints);

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

    private static string BuildResourceRestorePreviewLine(
        TacticsCharacterController source,
        TacticsRestoreResourceEffectData restoreResource)
    {
        (int amountMin, int amountMax) = TacticsAbilityEffectMath.GetRestoreResourceAmountRange(source, restoreResource);

        StringBuilder builder = new();
        builder.Append("Restores ");
        builder.Append(FormatRange(amountMin, amountMax));
        builder.Append(' ');
        builder.Append(GetResourceLabel(restoreResource.ResourceType));

        string scalingText = BuildScalingText(restoreResource.Scaling);
        if (!string.IsNullOrWhiteSpace(scalingText))
        {
            builder.Append(" (");
            builder.Append(scalingText);
            builder.Append(')');
        }

        return builder.ToString();
    }

    private static string BuildStatusEffectPreviewLine(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability,
        TacticsApplyStatusEffectData statusEffect)
    {
        TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffect);
        StringBuilder builder = new();
        builder.Append("Applies ");
        builder.Append(descriptor.DisplayName);
        builder.Append(" for ");
        builder.Append(statusEffect.DurationTurns);
        builder.Append(statusEffect.DurationTurns == 1 ? " turn" : " turns");

        switch (statusEffect.StatusEffectType)
        {
            case TacticsStatusEffectType.Cleanse:
                (int amountMin, int amountMax) = TacticsAbilityEffectMath.GetStatusPotencyRange(source, ability, statusEffect);
                builder.Append(". Heals ");
                builder.Append(FormatRange(amountMin, amountMax));
                builder.Append(" HP at turn start");

                string scalingText = BuildScalingText(statusEffect.Scaling);
                if (!string.IsNullOrWhiteSpace(scalingText))
                {
                    builder.Append(" (");
                    builder.Append(scalingText);
                    builder.Append(')');
                }
                break;

            case TacticsStatusEffectType.Stun:
                builder.Append(". Target cannot act");
                break;

            case TacticsStatusEffectType.Taunt:
                builder.Append(". Hostile single-target abilities must target the affected unit while it remains a valid target");
                break;

            case TacticsStatusEffectType.Bleed:
                (int bleedMin, int bleedMax) = TacticsAbilityEffectMath.GetStatusPotencyRange(source, ability, statusEffect);
                builder.Append(". Deals ");
                builder.Append(FormatRange(bleedMin, bleedMax));
                builder.Append(" damage after each tile moved and after each action performed");

                string bleedScalingText = BuildScalingText(statusEffect.Scaling);
                if (!string.IsNullOrWhiteSpace(bleedScalingText))
                {
                    builder.Append(" (");
                    builder.Append(bleedScalingText);
                    builder.Append(')');
                }
                break;

            case TacticsStatusEffectType.Poison:
                (int poisonMin, int poisonMax) = TacticsAbilityEffectMath.GetStatusPotencyRange(source, ability, statusEffect);
                builder.Append(". Deals ");
                builder.Append(TacticsStatusEffectLibrary.GetPoisonPercentDisplayValue(statusEffect.PotencyMultiplier).ToString("0.##"));
                builder.Append("% max HP plus ");
                builder.Append(FormatRange(poisonMin, poisonMax));
                builder.Append(" damage at turn start");

                string poisonScalingText = BuildScalingText(statusEffect.Scaling);
                if (!string.IsNullOrWhiteSpace(poisonScalingText))
                {
                    builder.Append(" (");
                    builder.Append(poisonScalingText);
                    builder.Append(')');
                }
                break;

            case TacticsStatusEffectType.Fire:
                (int fireMin, int fireMax) = TacticsAbilityEffectMath.GetStatusPotencyRange(source, ability, statusEffect);
                builder.Append(". Deals ");
                builder.Append(FormatRange(fireMin, fireMax));
                builder.Append(" fire damage at turn start and stacks with other fire effects");

                string fireScalingText = BuildScalingText(statusEffect.Scaling);
                if (!string.IsNullOrWhiteSpace(fireScalingText))
                {
                    builder.Append(" (");
                    builder.Append(fireScalingText);
                    builder.Append(')');
                }
                break;

            case TacticsStatusEffectType.StatBuff:
            case TacticsStatusEffectType.StatDebuff:
                (int statMin, int statMax) = TacticsAbilityEffectMath.GetStatusPotencyRange(source, ability, statusEffect);
                builder.Append(". Modifies ");
                builder.Append(TacticsCharacterStatModifierUtility.GetStatLabel(statusEffect.StatModifier.StatType));
                builder.Append(" by ");
                builder.Append(FormatSignedRange(statMin, statMax));
                builder.Append(" while active");

                string statScalingText = BuildScalingText(statusEffect.Scaling);
                if (!string.IsNullOrWhiteSpace(statScalingText))
                {
                    builder.Append(" (");
                    builder.Append(statScalingText);
                    builder.Append(')');
                }
                break;
        }

        return builder.ToString();
    }

    private static string GetResourceLabel(TacticsAbilityResourceType resourceType)
    {
        return resourceType switch
        {
            TacticsAbilityResourceType.Mana => "MP",
            TacticsAbilityResourceType.Stamina => "ST",
            _ => "resource"
        };
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

    private static string FormatSignedRange(int minAmount, int maxAmount)
    {
        if (minAmount == maxAmount)
        {
            return FormatSignedValue(minAmount);
        }

        return $"{FormatSignedValue(minAmount)} to {FormatSignedValue(maxAmount)}";
    }

    private static string FormatSignedValue(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }
}
