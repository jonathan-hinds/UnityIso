using System.Collections.Generic;
using UnityEngine;

public static class TacticsEnemyUtilityAbilityScorer
{
    private const float NoBenefitSupportPenalty = 32f;
    private const float MovementConversionWastePenalty = 18f;
    private const float NearbyThreatBonusPerHostile = 2.5f;
    private const float MaxUrgentTargetHealthRatio = 0.7f;

    public static float ScoreSupportCommitment(
        TacticsCharacterController actor,
        TacticsAbilityDefinition ability,
        TacticsAbilityCostPayment costPayment,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        float bestAlternativeOffenseScore,
        int nearbyHostileCount)
    {
        if (actor == null || ability == null || affectedTargets == null || affectedTargets.Count == 0 || !IsSupportAbility(ability))
        {
            return 0f;
        }

        float adjustment = -ScoreGeneralSupportOpportunityCost(
            actor,
            affectedTargets,
            bestAlternativeOffenseScore,
            nearbyHostileCount);
        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            if (effect.EffectKind == TacticsAbilityEffectKind.RestoreResource)
            {
                adjustment += ScoreResourceRestoreCommitment(
                    actor,
                    ability,
                    costPayment,
                    effect.RestoreResource,
                    affectedTargets,
                    bestAlternativeOffenseScore,
                    nearbyHostileCount);
            }
        }

        return adjustment;
    }

    private static float ScoreGeneralSupportOpportunityCost(
        TacticsCharacterController actor,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        float bestAlternativeOffenseScore,
        int nearbyHostileCount)
    {
        if (actor == null || affectedTargets == null || affectedTargets.Count == 0 || bestAlternativeOffenseScore <= 0f)
        {
            return 0f;
        }

        int urgentTargets = 0;
        int pressuredTargets = 0;
        for (int i = 0; i < affectedTargets.Count; i++)
        {
            TacticsCharacterController target = affectedTargets[i];
            if (target == null || !target.IsAlive || target.Team != actor.Team)
            {
                continue;
            }

            float healthRatio = target.MaxHitPoints > 0
                ? Mathf.Clamp01(target.CurrentHitPoints / (float)target.MaxHitPoints)
                : 0f;
            if (healthRatio <= MaxUrgentTargetHealthRatio)
            {
                urgentTargets++;
            }

            if (ReferenceEquals(target, actor) ? nearbyHostileCount > 0 : healthRatio <= MaxUrgentTargetHealthRatio)
            {
                pressuredTargets++;
            }
        }

        float urgency = Mathf.Clamp01((urgentTargets * 0.3f) + (pressuredTargets * 0.25f) + (nearbyHostileCount * 0.12f));
        return bestAlternativeOffenseScore * Mathf.Lerp(0.85f, 0.25f, urgency);
    }

    private static float ScoreResourceRestoreCommitment(
        TacticsCharacterController actor,
        TacticsAbilityDefinition ability,
        TacticsAbilityCostPayment costPayment,
        TacticsRestoreResourceEffectData restoreResource,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        float bestAlternativeOffenseScore,
        int nearbyHostileCount)
    {
        if (restoreResource.ResourceType == TacticsAbilityResourceType.None)
        {
            return 0f;
        }

        float averageRestoreAmount = TacticsAbilityEffectMath.EvaluateRestoreResourceAmount(actor, restoreResource, useAverageRoll: true);
        if (averageRestoreAmount <= 0f)
        {
            return -NoBenefitSupportPenalty;
        }

        float adjustment = 0f;
        for (int i = 0; i < affectedTargets.Count; i++)
        {
            TacticsCharacterController target = affectedTargets[i];
            if (target == null || !target.IsAlive)
            {
                continue;
            }

            int missingResource = target.GetMissingResource(restoreResource.ResourceType);
            if (missingResource <= 0)
            {
                adjustment -= ReferenceEquals(target, actor) ? NoBenefitSupportPenalty : NoBenefitSupportPenalty * 0.35f;
                continue;
            }

            float effectiveRestore = Mathf.Min(missingResource, averageRestoreAmount);
            float restoreUtilization = effectiveRestore / Mathf.Max(1f, averageRestoreAmount);
            float unlockValue = GetUnlockedAbilityValue(target, restoreResource.ResourceType, effectiveRestore);
            bool isSelfTarget = ReferenceEquals(target, actor);

            if (isSelfTarget)
            {
                adjustment += unlockValue * 0.25f;

                if (costPayment.UsesMovement)
                {
                    float actionOpportunityPenalty = bestAlternativeOffenseScore * Mathf.Lerp(0.7f, 0.2f, Mathf.Clamp01(unlockValue / 20f));
                    float wastePenalty = restoreUtilization < 0.35f
                        ? Mathf.Lerp(MovementConversionWastePenalty, 0f, restoreUtilization / 0.35f)
                        : 0f;
                    float nearbyThreatBonus = nearbyHostileCount > 0 ? nearbyHostileCount * NearbyThreatBonusPerHostile : 0f;
                    adjustment += nearbyThreatBonus - actionOpportunityPenalty - wastePenalty;
                }
            }
        }

        return adjustment;
    }

    private static float GetUnlockedAbilityValue(
        TacticsCharacterController target,
        TacticsAbilityResourceType resourceType,
        float restoredAmount)
    {
        if (target == null || resourceType == TacticsAbilityResourceType.None || restoredAmount <= 0f)
        {
            return 0f;
        }

        int beforeAmount = target.GetCurrentResource(resourceType);
        int afterAmount = beforeAmount + Mathf.RoundToInt(restoredAmount);
        float bestValue = 0f;

        IReadOnlyList<TacticsAbilityDefinition> abilities = target.Abilities;
        if (abilities == null)
        {
            return 0f;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null ||
                ability.CostResourceType != resourceType ||
                ability.CostAmount <= beforeAmount ||
                ability.CostAmount > afterAmount)
            {
                continue;
            }

            bestValue = Mathf.Max(bestValue, GetAbilityStrategicValue(target, ability));
        }

        return bestValue;
    }

    private static float GetAbilityStrategicValue(TacticsCharacterController unit, TacticsAbilityDefinition ability)
    {
        if (unit == null || ability == null)
        {
            return 0f;
        }

        float value = 0f;
        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            switch (effect.EffectKind)
            {
                case TacticsAbilityEffectKind.DealDamage:
                    value += TacticsAbilityEffectMath.EvaluateDamageAmount(unit, ability, effect.DealDamage, useAverageRoll: true);
                    break;

                case TacticsAbilityEffectKind.RestoreHitPoints:
                    value += TacticsAbilityEffectMath.EvaluateRestoreHitPointsAmount(unit, effect.RestoreHitPoints, useAverageRoll: true) * 0.85f;
                    break;

                case TacticsAbilityEffectKind.RestoreResource:
                    value += TacticsAbilityEffectMath.EvaluateRestoreResourceAmount(unit, effect.RestoreResource, useAverageRoll: true) * 0.75f;
                    break;
            }
        }

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            TacticsApplyStatusEffectData statusEffect = statusEffects[i];
            TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffect.StatusEffectType);
            float potency = TacticsAbilityEffectMath.EvaluateStatusPotency(unit, unit, ability, statusEffect, useAverageRoll: true);
            value += statusEffect.StatusEffectType switch
            {
                TacticsStatusEffectType.Cleanse => (potency * statusEffect.DurationTurns * 0.8f) + 6f,
                TacticsStatusEffectType.Stun => 16f * statusEffect.DurationTurns,
                TacticsStatusEffectType.Taunt => (10f * statusEffect.DurationTurns) + (GetUnitBaseThreat(unit) * 0.85f),
                TacticsStatusEffectType.Bleed or TacticsStatusEffectType.Poison or TacticsStatusEffectType.Fire => TacticsStatusEffectLibrary.EvaluateStrategicValue(
                    statusEffect.StatusEffectType,
                    potency,
                    statusEffect.DurationTurns,
                    unit,
                    GetUnitBaseThreat(unit) * 5f),
                TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff => TacticsStatusEffectLibrary.EvaluateStrategicValue(
                    statusEffect,
                    potency,
                    statusEffect.DurationTurns,
                    unit,
                    GetUnitBaseThreat(unit) * 5f),
                _ => descriptor.IsBuff ? potency * 0.75f : potency
            };
        }

        if (ability.UsesAreaOfEffect)
        {
            value += ability.AreaOfEffectSize * 0.5f;
        }

        value += GetForcedMovementStrategicValue(ability);

        return value;
    }

    private static bool IsSupportAbility(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return false;
        }

        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].EffectKind is TacticsAbilityEffectKind.RestoreHitPoints or TacticsAbilityEffectKind.RestoreResource)
            {
                return true;
            }
        }

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (TacticsStatusEffectLibrary.GetDescriptor(statusEffects[i].StatusEffectType).IsBuff)
            {
                return true;
            }
        }

        return false;
    }

    private static float GetForcedMovementStrategicValue(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return 0f;
        }

        float value = ability.AppliesKnockback
            ? ability.Knockback.DistanceInTiles * 4.5f
            : 0f;
        if (ability.AppliesThrowing)
        {
            value += 7.5f;
        }

        return value;
    }

    private static float GetUnitBaseThreat(TacticsCharacterController unit)
    {
        if (unit == null)
        {
            return 0f;
        }

        float meleeAverage = (unit.BaseMeleeDamageMin + unit.BaseMeleeDamageMax) * 0.5f;
        float magicAverage = (unit.BaseMagicDamageMin + unit.BaseMagicDamageMax) * 0.5f;
        return Mathf.Max(meleeAverage, magicAverage) * 0.2f;
    }
}
