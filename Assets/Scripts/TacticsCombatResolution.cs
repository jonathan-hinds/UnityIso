using UnityEngine;

public enum TacticsAttackOutcome
{
    Hit = 0,
    Miss = 1,
    Dodge = 2,
    Block = 3
}

public readonly struct TacticsAttackResolution
{
    public TacticsAttackResolution(TacticsAttackOutcome outcome)
    {
        Outcome = outcome;
    }

    public TacticsAttackOutcome Outcome { get; }
    public bool DidLand => Outcome == TacticsAttackOutcome.Hit;
    public bool IsAvoided => Outcome != TacticsAttackOutcome.Hit;
}

public static class TacticsCombatResolutionUtility
{
    public static TacticsAttackResolution Resolve(
        TacticsCharacterController source,
        TacticsCharacterController target,
        TacticsAbilityDefinition ability)
    {
        if (!RequiresAttackResolution(source, target, ability))
        {
            return new TacticsAttackResolution(TacticsAttackOutcome.Hit);
        }

        if (Random.value > Mathf.Clamp01(source.HitChance))
        {
            return new TacticsAttackResolution(TacticsAttackOutcome.Miss);
        }

        if (Random.value < Mathf.Clamp01(target.DodgeChance))
        {
            return new TacticsAttackResolution(TacticsAttackOutcome.Dodge);
        }

        if (CanBlock(ability) && Random.value < Mathf.Clamp01(target.BlockChance))
        {
            return new TacticsAttackResolution(TacticsAttackOutcome.Block);
        }

        return new TacticsAttackResolution(TacticsAttackOutcome.Hit);
    }

    public static bool RequiresAttackResolution(
        TacticsCharacterController source,
        TacticsCharacterController target,
        TacticsAbilityDefinition ability)
    {
        if (source == null || target == null || ability == null)
        {
            return false;
        }

        if (ability.TargetRule != TacticsAbilityTargetRule.HostileUnit)
        {
            return false;
        }

        return source.Team != target.Team;
    }

    public static bool CanBlock(TacticsAbilityDefinition ability)
    {
        return ability != null && ability.DamageType != TacticsAbilityDamageType.Magic;
    }
}
