using UnityEngine;

public readonly struct TacticsEnemyTauntEvaluationContext
{
    public TacticsEnemyTauntEvaluationContext(
        float bestAlternativeOffenseScore,
        int immediateThreats,
        int protectedAllies,
        int endangeredProtectedAllies,
        int criticalProtectedAllies,
        int nearbyAlliedTaunters,
        int targetTauntRemainingTurns,
        int nearbyTauntCoverageTurns,
        float allyProtectionValue,
        float laneControlValue,
        float targetDurabilityRatio,
        float targetOffensivePotential,
        float teamDamagePressure,
        bool targetAlreadyTaunting,
        bool isSelfTarget,
        bool isFrontliner)
    {
        BestAlternativeOffenseScore = Mathf.Max(0f, bestAlternativeOffenseScore);
        ImmediateThreats = Mathf.Max(0, immediateThreats);
        ProtectedAllies = Mathf.Max(0, protectedAllies);
        EndangeredProtectedAllies = Mathf.Max(0, endangeredProtectedAllies);
        CriticalProtectedAllies = Mathf.Max(0, criticalProtectedAllies);
        NearbyAlliedTaunters = Mathf.Max(0, nearbyAlliedTaunters);
        TargetTauntRemainingTurns = Mathf.Max(0, targetTauntRemainingTurns);
        NearbyTauntCoverageTurns = Mathf.Max(0, nearbyTauntCoverageTurns);
        AllyProtectionValue = Mathf.Max(0f, allyProtectionValue);
        LaneControlValue = Mathf.Max(0f, laneControlValue);
        TargetDurabilityRatio = Mathf.Clamp01(targetDurabilityRatio);
        TargetOffensivePotential = Mathf.Max(0f, targetOffensivePotential);
        TeamDamagePressure = Mathf.Clamp01(teamDamagePressure);
        TargetAlreadyTaunting = targetAlreadyTaunting;
        IsSelfTarget = isSelfTarget;
        IsFrontliner = isFrontliner;
    }

    public float BestAlternativeOffenseScore { get; }
    public int ImmediateThreats { get; }
    public int ProtectedAllies { get; }
    public int EndangeredProtectedAllies { get; }
    public int CriticalProtectedAllies { get; }
    public int NearbyAlliedTaunters { get; }
    public int TargetTauntRemainingTurns { get; }
    public int NearbyTauntCoverageTurns { get; }
    public float AllyProtectionValue { get; }
    public float LaneControlValue { get; }
    public float TargetDurabilityRatio { get; }
    public float TargetOffensivePotential { get; }
    public float TeamDamagePressure { get; }
    public bool TargetAlreadyTaunting { get; }
    public bool IsSelfTarget { get; }
    public bool IsFrontliner { get; }
}

public static class TacticsEnemyTauntAbilityScorer
{
    public static float Score(in TacticsEnemyTauntEvaluationContext context)
    {
        bool hasCriticalAllies = context.CriticalProtectedAllies > 0;
        bool hasEndangeredAllies = context.EndangeredProtectedAllies > 0 || hasCriticalAllies;
        bool hasExistingCoverage = context.TargetAlreadyTaunting || context.NearbyTauntCoverageTurns > 0 || context.NearbyAlliedTaunters > 0;

        if (context.ImmediateThreats <= 0)
        {
            return -26f;
        }

        if (context.ProtectedAllies <= 0)
        {
            return -18f - (context.BestAlternativeOffenseScore * 0.35f);
        }

        if (!hasEndangeredAllies)
        {
            float redundantCoveragePenalty =
                (context.TargetTauntRemainingTurns * 11f) +
                (context.NearbyTauntCoverageTurns * 8f) +
                (context.NearbyAlliedTaunters * 14f);
            return hasExistingCoverage
                ? -22f - redundantCoveragePenalty - (context.BestAlternativeOffenseScore * 0.9f)
                : -10f - (context.BestAlternativeOffenseScore * 0.45f);
        }

        float threatPressure = Mathf.Clamp01(context.ImmediateThreats / 3f);
        float protectionPressure = Mathf.Clamp01(context.ProtectedAllies / 2f);
        float endangeredPressure = Mathf.Clamp01(
            (context.EndangeredProtectedAllies * 0.4f) +
            (context.CriticalProtectedAllies * 0.75f));
        float responseUrgency = Mathf.Clamp01(
            (context.TeamDamagePressure * 0.65f) +
            (threatPressure * 0.45f) +
            (protectionPressure * 0.4f) +
            (endangeredPressure * 0.75f));
        float urgencyProtectionBonus = context.EndangeredProtectedAllies * 14f;
        urgencyProtectionBonus += context.CriticalProtectedAllies * 26f;
        urgencyProtectionBonus += hasCriticalAllies ? 10f : 0f;

        float score = 0f;
        score += context.ImmediateThreats * 4f;
        score += context.ProtectedAllies * 7f;
        score += urgencyProtectionBonus;
        score += context.AllyProtectionValue * 0.55f;
        score += context.LaneControlValue * 0.2f;
        score += responseUrgency * 24f;
        score += context.IsFrontliner ? 6f : 0f;
        score += context.TargetOffensivePotential * (context.IsFrontliner ? 0.08f : 0.03f);
        score += context.TargetDurabilityRatio >= 0.55f ? 6f : -10f;
        score += context.IsSelfTarget ? 2.5f : 0.5f;
        score -= context.TargetAlreadyTaunting
            ? 18f + (context.TargetTauntRemainingTurns * (hasCriticalAllies ? 3f : 8f))
            : 0f;
        score -= context.NearbyAlliedTaunters * (hasCriticalAllies ? 7f : 14f);
        score -= context.NearbyTauntCoverageTurns * (hasCriticalAllies ? 2.5f : 6f);
        score -= context.BestAlternativeOffenseScore * Mathf.Lerp(0.35f, 0.08f, responseUrgency);

        if (responseUrgency < 0.35f)
        {
            score -= 10f;
        }

        return score;
    }
}
