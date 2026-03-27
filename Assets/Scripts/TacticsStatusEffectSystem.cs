using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum TacticsStatusEffectType
{
    Cleanse = 0,
    Stun = 1,
    Taunt = 2,
    Bleed = 3,
    Poison = 4,
    Fire = 5,
    StatBuff = 6,
    StatDebuff = 7
}

public enum TacticsStatusEffectCategory
{
    Buff = 0,
    Debuff = 1
}

public enum TacticsStatusEffectTrigger
{
    TurnStart = 0,
    TileMoved = 1,
    ActionPerformed = 2
}

public enum TacticsStatusEffectStackingMode
{
    RefreshHighestPotency = 0,
    AddPotency = 1
}

public readonly struct TacticsStatusEffectDescriptor
{
    public TacticsStatusEffectDescriptor(
        TacticsStatusEffectType statusEffectType,
        string displayName,
        string shortLabel,
        TacticsStatusEffectCategory category,
        bool appliesAtTurnStart,
        bool blocksActions,
        TacticsStatusEffectStackingMode stackingMode,
        Color accentColor,
        Color backgroundColor)
    {
        StatusEffectType = statusEffectType;
        DisplayName = displayName;
        ShortLabel = shortLabel;
        Category = category;
        AppliesAtTurnStart = appliesAtTurnStart;
        BlocksActions = blocksActions;
        StackingMode = stackingMode;
        AccentColor = accentColor;
        BackgroundColor = backgroundColor;
    }

    public TacticsStatusEffectType StatusEffectType { get; }
    public string DisplayName { get; }
    public string ShortLabel { get; }
    public TacticsStatusEffectCategory Category { get; }
    public bool AppliesAtTurnStart { get; }
    public bool BlocksActions { get; }
    public TacticsStatusEffectStackingMode StackingMode { get; }
    public bool IsBuff => Category == TacticsStatusEffectCategory.Buff;
    public Color AccentColor { get; }
    public Color BackgroundColor { get; }
}

[Serializable]
public struct TacticsStatusEffectInstance
{
    [SerializeField] private TacticsStatusEffectType statusEffectType;
    [SerializeField, Min(0)] private int remainingTurns;
    [SerializeField] private int potency;
    [SerializeField] private string effectKey;
    [SerializeField] private string displayNameOverride;
    [SerializeField] private string shortLabelOverride;
    [SerializeField] private TacticsStatusEffectStatModifierData statModifier;

    public TacticsStatusEffectInstance(TacticsStatusEffectType statusEffectType, int remainingTurns, int potency)
    {
        this.statusEffectType = statusEffectType;
        this.remainingTurns = Mathf.Max(0, remainingTurns);
        this.potency = potency;
        effectKey = string.Empty;
        displayNameOverride = string.Empty;
        shortLabelOverride = string.Empty;
        statModifier = TacticsStatusEffectStatModifierData.Default();
    }

    public TacticsStatusEffectInstance(TacticsApplyStatusEffectData statusEffectData, int remainingTurns, int potency)
    {
        statusEffectType = statusEffectData.StatusEffectType;
        this.remainingTurns = Mathf.Max(0, remainingTurns);
        this.potency = potency;
        effectKey = TacticsStatusEffectLibrary.BuildEffectKey(statusEffectData);
        displayNameOverride = statusEffectData.CustomDisplayName;
        shortLabelOverride = statusEffectData.CustomShortLabel;
        statModifier = statusEffectData.StatModifier;
    }

    public TacticsStatusEffectType StatusEffectType => statusEffectType;
    public int RemainingTurns => Mathf.Max(0, remainingTurns);
    public int Potency => statusEffectType is TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff
        ? potency
        : Mathf.Max(0, potency);
    public string EffectKey => string.IsNullOrWhiteSpace(effectKey) ? statusEffectType.ToString() : effectKey;
    public string DisplayNameOverride => string.IsNullOrWhiteSpace(displayNameOverride) ? string.Empty : displayNameOverride.Trim();
    public string ShortLabelOverride => string.IsNullOrWhiteSpace(shortLabelOverride) ? string.Empty : shortLabelOverride.Trim().ToUpperInvariant();
    public TacticsStatusEffectStatModifierData StatModifier => statModifier;
    public bool IsExpired => RemainingTurns <= 0;

    public TacticsStatusEffectInstance WithRemainingTurns(int updatedRemainingTurns)
    {
        remainingTurns = Mathf.Max(0, updatedRemainingTurns);
        return this;
    }

    public TacticsStatusEffectInstance Refresh(int updatedRemainingTurns, int updatedPotency)
    {
        remainingTurns = Mathf.Max(0, updatedRemainingTurns);
        potency = updatedPotency;
        return this;
    }
}

public static class TacticsStatusEffectLibrary
{
    private const string IconResourcePath = "UI/StatusEffects";
    public const float PoisonMaxHitPointPercent = 0.03f;
    private static readonly Dictionary<TacticsStatusEffectType, Sprite> IconCache = new();

    public static TacticsStatusEffectDescriptor GetDescriptor(TacticsStatusEffectType statusEffectType)
    {
        return statusEffectType switch
        {
            TacticsStatusEffectType.Cleanse => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Cleanse,
                "Cleanse",
                "CL",
                TacticsStatusEffectCategory.Buff,
                appliesAtTurnStart: true,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.44f, 0.88f, 0.62f, 1f),
                backgroundColor: new Color(0.12f, 0.34f, 0.2f, 0.92f)),
            TacticsStatusEffectType.Stun => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Stun,
                "Stun",
                "ST",
                TacticsStatusEffectCategory.Debuff,
                appliesAtTurnStart: true,
                blocksActions: true,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.92f, 0.58f, 0.3f, 1f),
                backgroundColor: new Color(0.34f, 0.16f, 0.1f, 0.92f)),
            TacticsStatusEffectType.Taunt => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Taunt,
                "Taunt",
                "TA",
                TacticsStatusEffectCategory.Buff,
                appliesAtTurnStart: false,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.97f, 0.85f, 0.34f, 1f),
                backgroundColor: new Color(0.38f, 0.28f, 0.08f, 0.92f)),
            TacticsStatusEffectType.Bleed => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Bleed,
                "Bleed",
                "BL",
                TacticsStatusEffectCategory.Debuff,
                appliesAtTurnStart: false,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.94f, 0.29f, 0.29f, 1f),
                backgroundColor: new Color(0.34f, 0.08f, 0.08f, 0.92f)),
            TacticsStatusEffectType.Poison => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Poison,
                "Poison",
                "PS",
                TacticsStatusEffectCategory.Debuff,
                appliesAtTurnStart: true,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.54f, 0.88f, 0.26f, 1f),
                backgroundColor: new Color(0.16f, 0.3f, 0.08f, 0.92f)),
            TacticsStatusEffectType.Fire => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Fire,
                "Fire",
                "FI",
                TacticsStatusEffectCategory.Debuff,
                appliesAtTurnStart: true,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.AddPotency,
                accentColor: new Color(1f, 0.48f, 0.2f, 1f),
                backgroundColor: new Color(0.34f, 0.12f, 0.04f, 0.92f)),
            TacticsStatusEffectType.StatBuff => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.StatBuff,
                "Stat Buff",
                "SB",
                TacticsStatusEffectCategory.Buff,
                appliesAtTurnStart: false,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.42f, 0.86f, 0.95f, 1f),
                backgroundColor: new Color(0.08f, 0.23f, 0.31f, 0.92f)),
            TacticsStatusEffectType.StatDebuff => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.StatDebuff,
                "Stat Debuff",
                "SD",
                TacticsStatusEffectCategory.Debuff,
                appliesAtTurnStart: false,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.96f, 0.49f, 0.49f, 1f),
                backgroundColor: new Color(0.32f, 0.1f, 0.12f, 0.92f)),
            _ => new TacticsStatusEffectDescriptor(
                statusEffectType,
                statusEffectType.ToString(),
                statusEffectType.ToString().Length >= 2
                    ? statusEffectType.ToString().Substring(0, 2).ToUpperInvariant()
                    : statusEffectType.ToString().ToUpperInvariant(),
                TacticsStatusEffectCategory.Buff,
                appliesAtTurnStart: false,
                blocksActions: false,
                stackingMode: TacticsStatusEffectStackingMode.RefreshHighestPotency,
                accentColor: new Color(0.72f, 0.8f, 0.94f, 1f),
                backgroundColor: new Color(0.14f, 0.18f, 0.25f, 0.92f))
        };
    }

    public static TacticsStatusEffectDescriptor GetDescriptor(TacticsApplyStatusEffectData statusEffect)
    {
        TacticsStatusEffectDescriptor baseDescriptor = GetDescriptor(statusEffect.StatusEffectType);
        if (!statusEffect.UsesCustomDescriptor)
        {
            return baseDescriptor;
        }

        return new TacticsStatusEffectDescriptor(
            baseDescriptor.StatusEffectType,
            GetDisplayName(statusEffect),
            GetShortLabel(statusEffect),
            baseDescriptor.Category,
            baseDescriptor.AppliesAtTurnStart,
            baseDescriptor.BlocksActions,
            baseDescriptor.StackingMode,
            baseDescriptor.AccentColor,
            baseDescriptor.BackgroundColor);
    }

    public static TacticsStatusEffectDescriptor GetDescriptor(TacticsStatusEffectInstance statusEffect)
    {
        TacticsStatusEffectDescriptor baseDescriptor = GetDescriptor(statusEffect.StatusEffectType);
        if (statusEffect.StatusEffectType is not (TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff))
        {
            return baseDescriptor;
        }

        return new TacticsStatusEffectDescriptor(
            baseDescriptor.StatusEffectType,
            GetDisplayName(statusEffect),
            GetShortLabel(statusEffect),
            baseDescriptor.Category,
            baseDescriptor.AppliesAtTurnStart,
            baseDescriptor.BlocksActions,
            baseDescriptor.StackingMode,
            baseDescriptor.AccentColor,
            baseDescriptor.BackgroundColor);
    }

    public static Sprite GetIconSprite(TacticsStatusEffectType statusEffectType)
    {
        if (IconCache.TryGetValue(statusEffectType, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        Sprite sprite = Resources.Load<Sprite>($"{IconResourcePath}/{statusEffectType}");
        IconCache[statusEffectType] = sprite;
        return sprite;
    }

    public static string BuildTooltipBody(TacticsStatusEffectInstance statusEffect)
    {
        TacticsStatusEffectDescriptor descriptor = GetDescriptor(statusEffect);
        StringBuilder builder = new();

        switch (statusEffect.StatusEffectType)
        {
            case TacticsStatusEffectType.Cleanse:
                builder.Append("Heals ");
                builder.Append(Mathf.Max(0, statusEffect.Potency));
                builder.Append(" HP at the start of each turn.");
                break;

            case TacticsStatusEffectType.Stun:
                builder.Append("This unit cannot act during its turn.");
                break;

            case TacticsStatusEffectType.Taunt:
                builder.Append("Hostile single-target abilities must target this unit when it is a valid target.");
                break;

            case TacticsStatusEffectType.Bleed:
                builder.Append("Takes ");
                builder.Append(Mathf.Max(1, statusEffect.Potency));
                builder.Append(" damage after each tile moved and after each action performed.");
                break;

            case TacticsStatusEffectType.Poison:
                builder.Append("Takes ");
                builder.Append(Mathf.Max(1, statusEffect.Potency));
                builder.Append(" damage at the start of each turn.");
                break;

            case TacticsStatusEffectType.Fire:
                builder.Append("Takes ");
                builder.Append(Mathf.Max(1, statusEffect.Potency));
                builder.Append(" fire damage at the start of each turn. Additional fire stacks add to the burn damage.");
                break;

            case TacticsStatusEffectType.StatBuff:
            case TacticsStatusEffectType.StatDebuff:
                AppendStatModifierDescription(builder, descriptor.DisplayName, statusEffect.StatModifier, statusEffect.Potency);
                break;

            default:
                builder.Append(descriptor.DisplayName);
                if (statusEffect.Potency != 0)
                {
                    builder.Append(" potency: ");
                    builder.Append(statusEffect.Potency);
                    builder.Append('.');
                }
                else
                {
                    builder.Append(" is active.");
                }
                break;
        }

        return builder.ToString();
    }

    public static TacticsAbilityTooltipContent BuildTooltipContent(TacticsStatusEffectInstance statusEffect)
    {
        TacticsStatusEffectDescriptor descriptor = GetDescriptor(statusEffect);
        string footer = statusEffect.RemainingTurns == 1
            ? "1 turn remaining"
            : $"{Mathf.Max(0, statusEffect.RemainingTurns)} turns remaining";
        return new TacticsAbilityTooltipContent(
            descriptor.DisplayName,
            string.Empty,
            BuildTooltipBody(statusEffect),
            footer);
    }

    public static bool RespondsToTrigger(
        TacticsStatusEffectInstance statusEffect,
        TacticsStatusEffectTrigger trigger)
    {
        return statusEffect.StatusEffectType switch
        {
            TacticsStatusEffectType.Cleanse => trigger == TacticsStatusEffectTrigger.TurnStart,
            TacticsStatusEffectType.Stun => trigger == TacticsStatusEffectTrigger.TurnStart,
            TacticsStatusEffectType.Poison => trigger == TacticsStatusEffectTrigger.TurnStart,
            TacticsStatusEffectType.Fire => trigger == TacticsStatusEffectTrigger.TurnStart,
            TacticsStatusEffectType.Bleed => trigger is TacticsStatusEffectTrigger.TileMoved or TacticsStatusEffectTrigger.ActionPerformed,
            _ => false
        };
    }

    public static int GetTriggeredDamage(
        TacticsStatusEffectInstance statusEffect,
        TacticsStatusEffectTrigger trigger)
    {
        if (!RespondsToTrigger(statusEffect, trigger))
        {
            return 0;
        }

        return statusEffect.StatusEffectType switch
        {
            TacticsStatusEffectType.Bleed => Mathf.Max(0, statusEffect.Potency),
            TacticsStatusEffectType.Poison => Mathf.Max(0, statusEffect.Potency),
            TacticsStatusEffectType.Fire => Mathf.Max(0, statusEffect.Potency),
            _ => 0
        };
    }

    public static float EvaluateStrategicValue(
        TacticsStatusEffectType statusEffectType,
        float potency,
        int durationTurns,
        TacticsCharacterController target,
        float targetOffensivePotential = 0f)
    {
        return statusEffectType switch
        {
            TacticsStatusEffectType.Bleed => EvaluateBleedStrategicValue(potency, durationTurns, target, targetOffensivePotential),
            TacticsStatusEffectType.Poison => EvaluatePoisonStrategicValue(potency, durationTurns, target, targetOffensivePotential),
            TacticsStatusEffectType.Fire => EvaluateFireStrategicValue(potency, durationTurns, target, targetOffensivePotential),
            _ => Mathf.Max(0f, potency)
        };
    }

    public static float EvaluateStrategicValue(
        TacticsApplyStatusEffectData statusEffect,
        float potency,
        int durationTurns,
        TacticsCharacterController target,
        float targetOffensivePotential = 0f)
    {
        if (statusEffect.StatusEffectType is not (TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff))
        {
            return EvaluateStrategicValue(
                statusEffect.StatusEffectType,
                potency,
                durationTurns,
                target,
                targetOffensivePotential);
        }

        return EvaluateStatModifierStrategicValue(statusEffect.StatModifier, potency, durationTurns, target, targetOffensivePotential);
    }

    public static int NormalizePotency(TacticsStatusEffectType statusEffectType, int potency)
    {
        return statusEffectType switch
        {
            TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff => potency,
            TacticsStatusEffectType.Cleanse or TacticsStatusEffectType.Bleed or TacticsStatusEffectType.Poison or TacticsStatusEffectType.Fire => Mathf.Max(1, potency),
            _ => Mathf.Max(0, potency)
        };
    }

    public static int MergePotency(TacticsStatusEffectType statusEffectType, int existingPotency, int incomingPotency)
    {
        int normalizedExistingPotency = NormalizePotency(statusEffectType, existingPotency);
        int normalizedIncomingPotency = NormalizePotency(statusEffectType, incomingPotency);
        TacticsStatusEffectDescriptor descriptor = GetDescriptor(statusEffectType);
        if (statusEffectType is TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff)
        {
            return descriptor.StackingMode == TacticsStatusEffectStackingMode.AddPotency
                ? normalizedExistingPotency + normalizedIncomingPotency
                : Mathf.Abs(normalizedExistingPotency) >= Mathf.Abs(normalizedIncomingPotency)
                    ? normalizedExistingPotency
                    : normalizedIncomingPotency;
        }

        return descriptor.StackingMode == TacticsStatusEffectStackingMode.AddPotency
            ? Mathf.Max(1, normalizedExistingPotency + normalizedIncomingPotency)
            : Mathf.Max(normalizedExistingPotency, normalizedIncomingPotency);
    }

    public static int GetActivePotency(TacticsCharacterController target, TacticsStatusEffectType statusEffectType)
    {
        if (target == null || target.ActiveStatusEffects == null)
        {
            return 0;
        }

        IReadOnlyList<TacticsStatusEffectInstance> activeEffects = target.ActiveStatusEffects;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            TacticsStatusEffectInstance activeEffect = activeEffects[i];
            if (activeEffect.IsExpired || activeEffect.StatusEffectType != statusEffectType)
            {
                continue;
            }

            return Mathf.Max(0, activeEffect.Potency);
        }

        return 0;
    }

    public static string BuildEffectKey(TacticsApplyStatusEffectData statusEffect)
    {
        if (statusEffect.StatusEffectType is not (TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff))
        {
            return statusEffect.StatusEffectType.ToString();
        }

        string displayName = GetDisplayName(statusEffect);
        return $"{statusEffect.StatusEffectType}:{displayName}:{statusEffect.StatModifier.StatType}";
    }

    public static string GetDisplayName(TacticsApplyStatusEffectData statusEffect)
    {
        if (!string.IsNullOrWhiteSpace(statusEffect.CustomDisplayName))
        {
            return statusEffect.CustomDisplayName;
        }

        return statusEffect.StatusEffectType switch
        {
            TacticsStatusEffectType.StatBuff => $"{TacticsCharacterStatModifierUtility.GetStatLabel(statusEffect.StatModifier.StatType)} Buff",
            TacticsStatusEffectType.StatDebuff => $"{TacticsCharacterStatModifierUtility.GetStatLabel(statusEffect.StatModifier.StatType)} Debuff",
            _ => GetDescriptor(statusEffect.StatusEffectType).DisplayName
        };
    }

    public static string GetDisplayName(TacticsStatusEffectInstance statusEffect)
    {
        if (!string.IsNullOrWhiteSpace(statusEffect.DisplayNameOverride))
        {
            return statusEffect.DisplayNameOverride;
        }

        return statusEffect.StatusEffectType switch
        {
            TacticsStatusEffectType.StatBuff => $"{TacticsCharacterStatModifierUtility.GetStatLabel(statusEffect.StatModifier.StatType)} Buff",
            TacticsStatusEffectType.StatDebuff => $"{TacticsCharacterStatModifierUtility.GetStatLabel(statusEffect.StatModifier.StatType)} Debuff",
            _ => GetDescriptor(statusEffect.StatusEffectType).DisplayName
        };
    }

    public static string GetShortLabel(TacticsApplyStatusEffectData statusEffect)
    {
        if (!string.IsNullOrWhiteSpace(statusEffect.CustomShortLabel))
        {
            return statusEffect.CustomShortLabel;
        }

        return TacticsCharacterStatModifierUtility.GetStatLabel(statusEffect.StatModifier.StatType);
    }

    public static string GetShortLabel(TacticsStatusEffectInstance statusEffect)
    {
        if (!string.IsNullOrWhiteSpace(statusEffect.ShortLabelOverride))
        {
            return statusEffect.ShortLabelOverride;
        }

        return TacticsCharacterStatModifierUtility.GetStatLabel(statusEffect.StatModifier.StatType);
    }

    public static bool TryApplyPersistentStatModifier(ref TacticsCharacterStats stats, TacticsStatusEffectInstance statusEffect)
    {
        if (statusEffect.IsExpired ||
            statusEffect.StatusEffectType is not (TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff) ||
            statusEffect.Potency == 0)
        {
            return false;
        }

        TacticsCharacterStatModifierUtility.ApplyModifier(ref stats, statusEffect.StatModifier, statusEffect.Potency);
        return true;
    }

    private static float EvaluateBleedStrategicValue(
        float potency,
        int durationTurns,
        TacticsCharacterController target,
        float targetOffensivePotential)
    {
        if (potency <= 0f || durationTurns <= 0)
        {
            return 0f;
        }

        float expectedTriggers = EstimateBleedTriggerCount(target, durationTurns);
        float expectedDamage = potency * expectedTriggers;
        float effectiveDamage = target != null
            ? Mathf.Min(Mathf.Max(1f, target.CurrentHitPoints), expectedDamage)
            : expectedDamage;
        float healthPressure = target != null
            ? (1f - GetHealthRatio(target)) * 8f
            : 0f;
        float offensivePressure = targetOffensivePotential > 0f
            ? targetOffensivePotential * 0.3f
            : EstimateUnitThreat(target) * 0.5f;
        float mobilityPressure = target != null
            ? Mathf.Max(0f, target.MoveRange - 1) * durationTurns * 0.8f
            : 0f;
        float applyBias = target != null && target.HasStatusEffect(TacticsStatusEffectType.Bleed)
            ? expectedDamage * 0.2f
            : 6f;
        return effectiveDamage + healthPressure + offensivePressure + mobilityPressure + applyBias;
    }

    private static float EvaluatePoisonStrategicValue(
        float potency,
        int durationTurns,
        TacticsCharacterController target,
        float targetOffensivePotential)
    {
        if (potency <= 0f || durationTurns <= 0)
        {
            return 0f;
        }

        float expectedTriggers = Mathf.Max(1f, durationTurns);
        float expectedDamage = potency * expectedTriggers;
        float effectiveDamage = target != null
            ? Mathf.Min(Mathf.Max(1f, target.CurrentHitPoints), expectedDamage)
            : expectedDamage;
        float offensivePressure = targetOffensivePotential > 0f
            ? targetOffensivePotential * 0.28f
            : EstimateUnitThreat(target) * 0.45f;
        float durabilityPressure = target != null
            ? Mathf.Max(0f, target.MaxHitPoints * 0.05f)
            : 0f;
        float sustainPressure = target != null
            ? GetHealthRatio(target) * 7f
            : 0f;
        float applyBias = target != null && target.HasStatusEffect(TacticsStatusEffectType.Poison)
            ? expectedDamage * 0.15f
            : 5f;
        return effectiveDamage + offensivePressure + durabilityPressure + sustainPressure + applyBias;
    }

    private static float EvaluateFireStrategicValue(
        float potency,
        int durationTurns,
        TacticsCharacterController target,
        float targetOffensivePotential)
    {
        if (potency <= 0f || durationTurns <= 0)
        {
            return 0f;
        }

        float expectedTriggers = Mathf.Max(1f, durationTurns);
        float expectedDamage = potency * expectedTriggers;
        float effectiveDamage = target != null
            ? Mathf.Min(Mathf.Max(1f, target.CurrentHitPoints), expectedDamage)
            : expectedDamage;
        float offensivePressure = targetOffensivePotential > 0f
            ? targetOffensivePotential * 0.32f
            : EstimateUnitThreat(target) * 0.52f;
        float sustainPressure = target != null
            ? GetHealthRatio(target) * 5.5f
            : 0f;
        float stackPressure = target != null
            ? Mathf.Clamp(GetActivePotency(target, TacticsStatusEffectType.Fire) * 0.45f, 0f, 14f)
            : 0f;
        float applyBias = target != null && target.HasStatusEffect(TacticsStatusEffectType.Fire)
            ? 6.5f + stackPressure
            : 4.5f;
        return effectiveDamage + offensivePressure + sustainPressure + stackPressure + applyBias;
    }

    private static float EvaluateStatModifierStrategicValue(
        TacticsStatusEffectStatModifierData modifier,
        float potency,
        int durationTurns,
        TacticsCharacterController target,
        float targetOffensivePotential)
    {
        if (potency == 0f || durationTurns <= 0)
        {
            return 0f;
        }

        float signedPotency = potency;
        float absolutePotency = Mathf.Abs(signedPotency);
        float statWeight = TacticsCharacterStatModifierUtility.GetStrategicWeight(modifier.StatType, target);
        float threatPressure = targetOffensivePotential > 0f
            ? targetOffensivePotential * 0.14f
            : EstimateUnitThreat(target) * 0.7f;
        float durationWeight = Mathf.Lerp(0.9f, 1.65f, Mathf.Clamp01((durationTurns - 1) / 3f));
        float value = absolutePotency * statWeight * durationWeight;

        if (modifier.StatType == TacticsCharacterStatType.Stamina && target != null)
        {
            value += Mathf.Max(0f, target.MaxHitPoints * 0.08f);
        }

        if (modifier.StatType == TacticsCharacterStatType.MoveRange && target != null)
        {
            value += Mathf.Max(0f, target.MoveRange - 1) * 2.5f;
        }

        return value + threatPressure;
    }

    private static void AppendStatModifierDescription(
        StringBuilder builder,
        string displayName,
        TacticsStatusEffectStatModifierData modifier,
        int potency)
    {
        builder.Append(displayName);
        builder.Append(": ");
        builder.Append(potency >= 0 ? '+' : '-');
        builder.Append(Mathf.Abs(potency));
        builder.Append(' ');
        builder.Append(TacticsCharacterStatModifierUtility.GetStatLabel(modifier.StatType));
        builder.Append(". Derived combat stats update immediately while this effect is active.");
    }

    private static float EstimateBleedTriggerCount(TacticsCharacterController target, int durationTurns)
    {
        float actionTriggers = EstimateBleedActionTriggers(target, durationTurns);
        float movementTriggers = EstimateBleedMovementTriggersPerTurn(target) * Mathf.Max(1, durationTurns);
        return Mathf.Max(1f, actionTriggers + movementTriggers);
    }

    private static float EstimateBleedActionTriggers(TacticsCharacterController target, int durationTurns)
    {
        if (durationTurns <= 0)
        {
            return 0f;
        }

        if (target == null)
        {
            return durationTurns;
        }

        float actionTriggers = durationTurns;
        if (target.IsActionLockedThisTurn)
        {
            actionTriggers *= 0.25f;
        }
        else if (target.IsTurnActive && target.HasActedThisTurn)
        {
            actionTriggers = Mathf.Max(0.35f, durationTurns - 0.75f);
        }
        else if (target.IsTurnActive)
        {
            actionTriggers += 0.35f;
        }

        return Mathf.Max(0.35f, actionTriggers);
    }

    private static float EstimateBleedMovementTriggersPerTurn(TacticsCharacterController target)
    {
        if (target == null || target.MoveRange <= 0)
        {
            return 0f;
        }

        float expectedMovementTiles = Mathf.Clamp(target.MoveRange * 0.55f, 0.5f, 3.5f);
        float rangePressure = GetAbilityRangePressure(target);
        float mobilityBias = rangePressure <= 1.5f
            ? 1.15f
            : rangePressure <= 2.5f
                ? 0.85f
                : 0.55f;
        return expectedMovementTiles * mobilityBias;
    }

    private static float GetAbilityRangePressure(TacticsCharacterController target)
    {
        if (target == null || target.Abilities == null || target.Abilities.Count == 0)
        {
            return 1f;
        }

        float maxPreferredDistance = 1f;
        IReadOnlyList<TacticsAbilityDefinition> abilities = target.Abilities;
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            maxPreferredDistance = Mathf.Max(
                maxPreferredDistance,
                ability.UsesAbilityRange ? Mathf.Max(2f, ability.Range - 1) : 1f);
        }

        return maxPreferredDistance;
    }

    private static float EstimateUnitThreat(TacticsCharacterController target)
    {
        if (target == null)
        {
            return 0f;
        }

        float meleeAverage = (target.BaseMeleeDamageMin + target.BaseMeleeDamageMax) * 0.5f;
        float magicAverage = (target.BaseMagicDamageMin + target.BaseMagicDamageMax) * 0.5f;
        return Mathf.Max(meleeAverage, magicAverage) * 0.2f;
    }

    private static float GetHealthRatio(TacticsCharacterController target)
    {
        if (target == null || target.MaxHitPoints <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(target.CurrentHitPoints / (float)target.MaxHitPoints);
    }

    public static float GetPoisonPercentDisplayValue(float potencyMultiplier = 1f)
    {
        return PoisonMaxHitPointPercent * 100f * Mathf.Max(0.01f, potencyMultiplier);
    }

    public static float GetPoisonBaseDamage(int maxHitPoints)
    {
        if (maxHitPoints <= 0)
        {
            return 0f;
        }

        return Mathf.Ceil(maxHitPoints * PoisonMaxHitPointPercent);
    }
}
