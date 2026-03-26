using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum TacticsStatusEffectType
{
    Cleanse = 0,
    Stun = 1
}

public enum TacticsStatusEffectCategory
{
    Buff = 0,
    Debuff = 1
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
        Color accentColor,
        Color backgroundColor)
    {
        StatusEffectType = statusEffectType;
        DisplayName = displayName;
        ShortLabel = shortLabel;
        Category = category;
        AppliesAtTurnStart = appliesAtTurnStart;
        BlocksActions = blocksActions;
        AccentColor = accentColor;
        BackgroundColor = backgroundColor;
    }

    public TacticsStatusEffectType StatusEffectType { get; }
    public string DisplayName { get; }
    public string ShortLabel { get; }
    public TacticsStatusEffectCategory Category { get; }
    public bool AppliesAtTurnStart { get; }
    public bool BlocksActions { get; }
    public bool IsBuff => Category == TacticsStatusEffectCategory.Buff;
    public Color AccentColor { get; }
    public Color BackgroundColor { get; }
}

[Serializable]
public struct TacticsStatusEffectInstance
{
    [SerializeField] private TacticsStatusEffectType statusEffectType;
    [SerializeField, Min(0)] private int remainingTurns;
    [SerializeField, Min(0)] private int potency;

    public TacticsStatusEffectInstance(TacticsStatusEffectType statusEffectType, int remainingTurns, int potency)
    {
        this.statusEffectType = statusEffectType;
        this.remainingTurns = Mathf.Max(0, remainingTurns);
        this.potency = Mathf.Max(0, potency);
    }

    public TacticsStatusEffectType StatusEffectType => statusEffectType;
    public int RemainingTurns => Mathf.Max(0, remainingTurns);
    public int Potency => Mathf.Max(0, potency);
    public bool IsExpired => RemainingTurns <= 0;

    public TacticsStatusEffectInstance WithRemainingTurns(int updatedRemainingTurns)
    {
        remainingTurns = Mathf.Max(0, updatedRemainingTurns);
        return this;
    }

    public TacticsStatusEffectInstance Refresh(int updatedRemainingTurns, int updatedPotency)
    {
        remainingTurns = Mathf.Max(0, updatedRemainingTurns);
        potency = Mathf.Max(0, updatedPotency);
        return this;
    }
}

public static class TacticsStatusEffectLibrary
{
    private const string IconResourcePath = "UI/StatusEffects";
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
                accentColor: new Color(0.44f, 0.88f, 0.62f, 1f),
                backgroundColor: new Color(0.12f, 0.34f, 0.2f, 0.92f)),
            TacticsStatusEffectType.Stun => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Stun,
                "Stun",
                "ST",
                TacticsStatusEffectCategory.Debuff,
                appliesAtTurnStart: true,
                blocksActions: true,
                accentColor: new Color(0.92f, 0.58f, 0.3f, 1f),
                backgroundColor: new Color(0.34f, 0.16f, 0.1f, 0.92f)),
            _ => new TacticsStatusEffectDescriptor(
                statusEffectType,
                statusEffectType.ToString(),
                statusEffectType.ToString().Length >= 2
                    ? statusEffectType.ToString().Substring(0, 2).ToUpperInvariant()
                    : statusEffectType.ToString().ToUpperInvariant(),
                TacticsStatusEffectCategory.Buff,
                appliesAtTurnStart: false,
                blocksActions: false,
                accentColor: new Color(0.72f, 0.8f, 0.94f, 1f),
                backgroundColor: new Color(0.14f, 0.18f, 0.25f, 0.92f))
        };
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
        TacticsStatusEffectDescriptor descriptor = GetDescriptor(statusEffect.StatusEffectType);
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

            default:
                builder.Append(descriptor.DisplayName);
                if (statusEffect.Potency > 0)
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
        TacticsStatusEffectDescriptor descriptor = GetDescriptor(statusEffect.StatusEffectType);
        string footer = statusEffect.RemainingTurns == 1
            ? "1 turn remaining"
            : $"{Mathf.Max(0, statusEffect.RemainingTurns)} turns remaining";
        return new TacticsAbilityTooltipContent(
            descriptor.DisplayName,
            string.Empty,
            BuildTooltipBody(statusEffect),
            footer);
    }
}
