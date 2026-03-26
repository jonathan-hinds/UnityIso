using System;
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
        TacticsStatusEffectCategory category,
        bool appliesAtTurnStart,
        bool blocksActions)
    {
        StatusEffectType = statusEffectType;
        DisplayName = displayName;
        Category = category;
        AppliesAtTurnStart = appliesAtTurnStart;
        BlocksActions = blocksActions;
    }

    public TacticsStatusEffectType StatusEffectType { get; }
    public string DisplayName { get; }
    public TacticsStatusEffectCategory Category { get; }
    public bool AppliesAtTurnStart { get; }
    public bool BlocksActions { get; }
    public bool IsBuff => Category == TacticsStatusEffectCategory.Buff;
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
    public static TacticsStatusEffectDescriptor GetDescriptor(TacticsStatusEffectType statusEffectType)
    {
        return statusEffectType switch
        {
            TacticsStatusEffectType.Cleanse => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Cleanse,
                "Cleanse",
                TacticsStatusEffectCategory.Buff,
                appliesAtTurnStart: true,
                blocksActions: false),
            TacticsStatusEffectType.Stun => new TacticsStatusEffectDescriptor(
                TacticsStatusEffectType.Stun,
                "Stun",
                TacticsStatusEffectCategory.Debuff,
                appliesAtTurnStart: true,
                blocksActions: true),
            _ => new TacticsStatusEffectDescriptor(
                statusEffectType,
                statusEffectType.ToString(),
                TacticsStatusEffectCategory.Buff,
                appliesAtTurnStart: false,
                blocksActions: false)
        };
    }
}
