using System;
using UnityEngine;

public enum TacticsCharacterStatType
{
    Strength = 0,
    Agility = 1,
    Stamina = 2,
    Wisdom = 3,
    Intelligence = 4,
    MoveRange = 5,
    JumpHeight = 6
}

[Serializable]
public struct TacticsStatusEffectStatModifierData
{
    [SerializeField] private TacticsCharacterStatType statType;

    public TacticsCharacterStatType StatType => statType;

    public static TacticsStatusEffectStatModifierData Default()
    {
        return new TacticsStatusEffectStatModifierData
        {
            statType = TacticsCharacterStatType.Strength
        };
    }

    public static TacticsStatusEffectStatModifierData Create(TacticsCharacterStatType statType)
    {
        return new TacticsStatusEffectStatModifierData
        {
            statType = statType
        };
    }
}

public static class TacticsCharacterStatModifierUtility
{
    public static void ApplyModifier(
        ref TacticsCharacterStats stats,
        TacticsStatusEffectStatModifierData modifier,
        int amount)
    {
        if (amount == 0)
        {
            return;
        }

        switch (modifier.StatType)
        {
            case TacticsCharacterStatType.Strength:
                stats.primaryStats.strength = Mathf.Max(1, stats.primaryStats.strength + amount);
                break;

            case TacticsCharacterStatType.Agility:
                stats.primaryStats.agility = Mathf.Max(1, stats.primaryStats.agility + amount);
                break;

            case TacticsCharacterStatType.Stamina:
                stats.primaryStats.stamina = Mathf.Max(1, stats.primaryStats.stamina + amount);
                break;

            case TacticsCharacterStatType.Wisdom:
                stats.primaryStats.wisdom = Mathf.Max(1, stats.primaryStats.wisdom + amount);
                break;

            case TacticsCharacterStatType.Intelligence:
                stats.primaryStats.intelligence = Mathf.Max(1, stats.primaryStats.intelligence + amount);
                break;

            case TacticsCharacterStatType.MoveRange:
                stats.mobilityStats.moveRange = Mathf.Max(0, stats.mobilityStats.moveRange + amount);
                break;

            case TacticsCharacterStatType.JumpHeight:
                stats.mobilityStats.jumpHeight = Mathf.Max(0, stats.mobilityStats.jumpHeight + amount);
                break;
        }
    }

    public static string GetStatLabel(TacticsCharacterStatType statType)
    {
        return statType switch
        {
            TacticsCharacterStatType.Strength => "STR",
            TacticsCharacterStatType.Agility => "AGI",
            TacticsCharacterStatType.Stamina => "STA",
            TacticsCharacterStatType.Wisdom => "WIS",
            TacticsCharacterStatType.Intelligence => "INT",
            TacticsCharacterStatType.MoveRange => "MOVE",
            TacticsCharacterStatType.JumpHeight => "JUMP",
            _ => statType.ToString().ToUpperInvariant()
        };
    }

    public static float GetStrategicWeight(TacticsCharacterStatType statType, TacticsCharacterController target)
    {
        float baseThreat = target == null
            ? 0f
            : Mathf.Max(
                (target.BaseMeleeDamageMin + target.BaseMeleeDamageMax) * 0.5f,
                (target.BaseMagicDamageMin + target.BaseMagicDamageMax) * 0.5f);

        return statType switch
        {
            TacticsCharacterStatType.Strength => 2.4f + (baseThreat * 0.06f),
            TacticsCharacterStatType.Intelligence => 2.4f + (baseThreat * 0.06f),
            TacticsCharacterStatType.Agility => 2.1f + (baseThreat * 0.04f),
            TacticsCharacterStatType.Stamina => 2f + (target != null ? target.MaxHitPoints * 0.04f : 0f),
            TacticsCharacterStatType.Wisdom => 1.9f + (target != null ? target.MaxMana * 0.05f : 0f),
            TacticsCharacterStatType.MoveRange => 2.8f + (target != null ? target.MoveRange * 0.8f : 0f),
            TacticsCharacterStatType.JumpHeight => 1.3f + (target != null ? target.JumpHeight * 0.75f : 0f),
            _ => 1f
        };
    }
}
