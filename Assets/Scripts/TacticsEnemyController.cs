using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITacticsAutomatedTurnController
{
    void BeginAutomatedTurn();
    void CancelAutomatedTurn();
}

[DisallowMultipleComponent]
[RequireComponent(typeof(TacticsCharacterController))]
public sealed class TacticsEnemyController : MonoBehaviour, ITacticsAutomatedTurnController
{
    [SerializeField] private TacticsCharacterController character;
    [SerializeField] private TacticsTurnManager turnManager;
    [SerializeField] private TacticsCombatSystem combatSystem;
    [SerializeField] private TacticsCoopSessionCoordinator coopSessionCoordinator;
    [SerializeField] private TacticsCharacterRegistry characterRegistry;
    [SerializeField, Min(0f)] private float thinkDelay = 0.2f;
    [SerializeField, Min(0f)] private float endTurnDelay = 0.15f;

    private Coroutine turnRoutine;
    private readonly List<TacticsCharacterController> reusableCandidateTargets = new();
    private readonly List<TacticsCharacterController> reusableAnchorTargets = new();
    private readonly List<TacticsCharacterController> reusableHostileTargets = new();
    private readonly List<TacticsCharacterController> reusableAlliedTargets = new();
    private readonly List<TacticsCharacterController> reusableAlliedTargetsIncludingSelf = new();
    private string priorityTargetRuntimeCharacterId = string.Empty;

    private readonly struct EnemyAbilityPlan
    {
        public EnemyAbilityPlan(
            TacticsAbilityDefinition ability,
            TacticsCharacterController target,
            Vector2Int sourceTile,
            Vector2Int targetTile,
            Vector2Int moveDestination,
            bool requiresMovement,
            float score)
        {
            Ability = ability;
            Target = target;
            SourceTile = sourceTile;
            TargetTile = targetTile;
            MoveDestination = moveDestination;
            RequiresMovement = requiresMovement;
            Score = score;
        }

        public TacticsAbilityDefinition Ability { get; }
        public TacticsCharacterController Target { get; }
        public Vector2Int SourceTile { get; }
        public Vector2Int TargetTile { get; }
        public Vector2Int MoveDestination { get; }
        public bool RequiresMovement { get; }
        public float Score { get; }
    }

    private readonly struct EnemyMovementPlan
    {
        public EnemyMovementPlan(Vector2Int destination, float score)
        {
            Destination = destination;
            Score = score;
        }

        public Vector2Int Destination { get; }
        public float Score { get; }
    }

    private void Awake()
    {
        if (character == null)
        {
            character = GetComponent<TacticsCharacterController>();
        }

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        if (combatSystem == null)
        {
            combatSystem = FindFirstObjectByType<TacticsCombatSystem>();
        }

        if (coopSessionCoordinator == null)
        {
            coopSessionCoordinator = FindFirstObjectByType<TacticsCoopSessionCoordinator>();
        }

        if (characterRegistry == null)
        {
            characterRegistry = FindFirstObjectByType<TacticsCharacterRegistry>();
        }
    }

    private void OnEnable()
    {
        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        if (combatSystem == null)
        {
            combatSystem = FindFirstObjectByType<TacticsCombatSystem>();
        }

        if (coopSessionCoordinator == null)
        {
            coopSessionCoordinator = FindFirstObjectByType<TacticsCoopSessionCoordinator>();
        }

        if (characterRegistry == null)
        {
            characterRegistry = FindFirstObjectByType<TacticsCharacterRegistry>();
        }
    }

    private void OnDisable()
    {
        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
            turnRoutine = null;
        }
    }

    public void AssignTurnManager(TacticsTurnManager manager)
    {
        turnManager = manager;
    }

    public void BeginAutomatedTurn()
    {
        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
        }

        if (coopSessionCoordinator != null && !coopSessionCoordinator.CanRunAutomatedTurns)
        {
            turnRoutine = null;
            return;
        }

        if (character == null || character.IsPlayerControlled || turnManager == null)
        {
            turnRoutine = null;
            return;
        }

        if (!ReferenceEquals(turnManager.ActiveParticipant, character) || !character.IsTurnActive)
        {
            turnRoutine = null;
            return;
        }

        turnRoutine = StartCoroutine(ExecuteTurnRoutine());
    }

    public void CancelAutomatedTurn()
    {
        if (turnRoutine == null)
        {
            return;
        }

        StopCoroutine(turnRoutine);
        turnRoutine = null;
    }

    public void SetPriorityTarget(TacticsCharacterController target)
    {
        priorityTargetRuntimeCharacterId = target != null ? target.RuntimeCharacterId : string.Empty;
    }

    private IEnumerator ExecuteTurnRoutine()
    {
        if (thinkDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(thinkDelay);
        }

        if (character == null || turnManager == null || !ReferenceEquals(turnManager.ActiveParticipant, character))
        {
            turnRoutine = null;
            yield break;
        }

        bool attemptedAction = false;
        if (TryBuildPriorityTargetAbilityPlan(out EnemyAbilityPlan priorityPlan))
        {
            attemptedAction = TryUseAbility(priorityPlan.Ability, priorityPlan.TargetTile);
            priorityTargetRuntimeCharacterId = string.Empty;
        }
        else if (TryBuildBestAbilityPlan(out EnemyAbilityPlan abilityPlan))
        {
            if (abilityPlan.RequiresMovement && TryMove(abilityPlan.MoveDestination))
            {
                while (character != null && character.IsMoving)
                {
                    yield return null;
                }
            }

            if (character != null &&
                combatSystem != null &&
                abilityPlan.Target != null &&
                abilityPlan.Target.isActiveAndEnabled &&
                abilityPlan.Target.IsAlive)
            {
                attemptedAction = TryUseAbility(abilityPlan.Ability, abilityPlan.TargetTile);
            }
        }
        else if (TryBuildBestMovementPlan(out EnemyMovementPlan movementPlan))
        {
            if (TryMove(movementPlan.Destination))
            {
                while (character != null && character.IsMoving)
                {
                    yield return null;
                }
            }
        }

        if (!attemptedAction)
        {
            TryUseBestAbilityFromCurrentPosition();
        }

        priorityTargetRuntimeCharacterId = string.Empty;

        while (combatSystem != null && combatSystem.State == TacticsCombatState.ResolvingAbility)
        {
            yield return null;
        }

        if (endTurnDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(endTurnDelay);
        }

        if (character != null && turnManager != null && ReferenceEquals(turnManager.ActiveParticipant, character))
        {
            TryEndTurn();
        }

        turnRoutine = null;
    }

    private bool TryBuildBestAbilityPlan(out EnemyAbilityPlan bestPlan)
    {
        bestPlan = default;

        if (character == null || combatSystem == null)
        {
            return false;
        }

        IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
        if (abilities == null || abilities.Count == 0)
        {
            return false;
        }

        bool foundPlan = false;
        List<TacticsCharacterController> anchorTargets = BuildMovementAnchorTargets();
        if (anchorTargets.Count == 0)
        {
            return false;
        }

        EvaluateAbilityPlansForTile(
            abilities,
            character.GridPosition,
            character.GridPosition,
            requiresMovement: false,
            ref foundPlan,
            ref bestPlan);

        for (int targetIndex = 0; targetIndex < anchorTargets.Count; targetIndex++)
        {
            TacticsCharacterController target = anchorTargets[targetIndex];
            if (!TryGetPathToTarget(target, out List<Vector2Int> pathToTarget))
            {
                continue;
            }

            int furthestReachableIndex = Mathf.Min(character.MoveRange, pathToTarget.Count - 2);
            for (int i = 1; i <= furthestReachableIndex; i++)
            {
                Vector2Int candidateTile = pathToTarget[i];
                EvaluateAbilityPlansForTile(
                    abilities,
                    candidateTile,
                    candidateTile,
                    requiresMovement: true,
                    ref foundPlan,
                    ref bestPlan);
            }
        }

        return foundPlan;
    }

    private bool TryBuildPriorityTargetAbilityPlan(out EnemyAbilityPlan bestPlan)
    {
        bestPlan = default;
        TacticsCharacterController priorityTarget = FindPriorityTarget();
        if (priorityTarget == null || character == null || combatSystem == null)
        {
            return false;
        }

        IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
        if (abilities == null || abilities.Count == 0)
        {
            return false;
        }

        bool foundPlan = false;
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null || !character.HasResourcesForAbility(ability))
            {
                continue;
            }

            if (!combatSystem.CanTargetTileFromTile(character, character.GridPosition, ability, priorityTarget.GridPosition))
            {
                continue;
            }

            IReadOnlyList<TacticsCharacterController> affectedTargets = combatSystem.GetPreviewTargetsFromTile(
                character,
                character.GridPosition,
                ability,
                priorityTarget.GridPosition);
            bool affectsPriorityTarget = false;
            for (int targetIndex = 0; targetIndex < affectedTargets.Count; targetIndex++)
            {
                if (ReferenceEquals(affectedTargets[targetIndex], priorityTarget))
                {
                    affectsPriorityTarget = true;
                    break;
                }
            }

            if (affectedTargets.Count == 0 || !affectsPriorityTarget)
            {
                continue;
            }

            float score = ScoreAbilityPlan(ability, priorityTarget, character.GridPosition, requiresMovement: false, affectedTargets);
            score += 1000f;
            if (!foundPlan || score > bestPlan.Score)
            {
                foundPlan = true;
                bestPlan = new EnemyAbilityPlan(
                    ability,
                    priorityTarget,
                    character.GridPosition,
                    priorityTarget.GridPosition,
                    character.GridPosition,
                    requiresMovement: false,
                    score);
            }
        }

        return foundPlan;
    }

    private bool TryBuildBestMovementPlan(out EnemyMovementPlan bestPlan)
    {
        bestPlan = default;

        if (character == null || combatSystem == null)
        {
            return false;
        }

        bool foundPlan = false;
        List<TacticsCharacterController> anchorTargets = BuildMovementAnchorTargets();
        if (anchorTargets.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < anchorTargets.Count; i++)
        {
            TacticsCharacterController target = anchorTargets[i];
            if (!TryGetPathToTarget(target, out List<Vector2Int> pathToTarget))
            {
                continue;
            }

            int furthestReachableIndex = Mathf.Min(character.MoveRange, pathToTarget.Count - 2);
            for (int pathIndex = 1; pathIndex <= furthestReachableIndex; pathIndex++)
            {
                Vector2Int candidateTile = pathToTarget[pathIndex];
                float score = ScoreMovementTile(candidateTile, target, pathIndex);
                if (!foundPlan || score > bestPlan.Score)
                {
                    foundPlan = true;
                    bestPlan = new EnemyMovementPlan(candidateTile, score);
                }
            }
        }

        return foundPlan;
    }

    private bool TryUseBestAbilityFromCurrentPosition()
    {
        if (!TryBuildBestImmediateAbilityPlan(out EnemyAbilityPlan immediatePlan))
        {
            return false;
        }

        return immediatePlan.Target != null &&
               immediatePlan.Target.isActiveAndEnabled &&
               immediatePlan.Target.IsAlive &&
               TryUseAbility(immediatePlan.Ability, immediatePlan.TargetTile);
    }

    private TacticsCharacterController FindPriorityTarget()
    {
        if (string.IsNullOrWhiteSpace(priorityTargetRuntimeCharacterId))
        {
            return null;
        }

        return characterRegistry != null &&
               characterRegistry.TryGetCharacterByRuntimeId(priorityTargetRuntimeCharacterId, out TacticsCharacterController target)
            ? target
            : null;
    }

    private bool TryBuildBestImmediateAbilityPlan(out EnemyAbilityPlan bestPlan)
    {
        bestPlan = default;

        if (character == null || combatSystem == null)
        {
            return false;
        }

        IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
        if (abilities == null || abilities.Count == 0)
        {
            return false;
        }

        bool foundPlan = false;
        EvaluateAbilityPlansForTile(
            abilities,
            character.GridPosition,
            character.GridPosition,
            requiresMovement: false,
            ref foundPlan,
            ref bestPlan);

        return foundPlan;
    }

    private void EvaluateAbilityPlansForTile(
        IReadOnlyList<TacticsAbilityDefinition> abilities,
        Vector2Int sourceTile,
        Vector2Int moveDestination,
        bool requiresMovement,
        ref bool foundPlan,
        ref EnemyAbilityPlan bestPlan)
    {
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null || !character.HasResourcesForAbility(ability))
            {
                continue;
            }

            List<TacticsCharacterController> candidateTargets = GetCandidateTargetsForAbility(ability);
            for (int targetIndex = 0; targetIndex < candidateTargets.Count; targetIndex++)
            {
                TacticsCharacterController primaryTarget = candidateTargets[targetIndex];
                if (primaryTarget == null || !primaryTarget.isActiveAndEnabled || !primaryTarget.IsAlive)
                {
                    continue;
                }

                if (!combatSystem.CanTargetTileFromTile(character, sourceTile, ability, primaryTarget.GridPosition))
                {
                    continue;
                }

                IReadOnlyList<TacticsCharacterController> affectedTargets = combatSystem.GetPreviewTargetsFromTile(
                    character,
                    sourceTile,
                    ability,
                    primaryTarget.GridPosition);

                if (affectedTargets.Count == 0)
                {
                    continue;
                }

                float score = ScoreAbilityPlan(ability, primaryTarget, sourceTile, requiresMovement, affectedTargets);
                if (!foundPlan || score > bestPlan.Score)
                {
                    foundPlan = true;
                    bestPlan = new EnemyAbilityPlan(
                        ability,
                        primaryTarget,
                        sourceTile,
                        primaryTarget.GridPosition,
                        moveDestination,
                        requiresMovement,
                        score);
                }
            }
        }
    }

    private float ScoreAbilityPlan(
        TacticsAbilityDefinition ability,
        TacticsCharacterController target,
        Vector2Int sourceTile,
        bool requiresMovement,
        IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        float tacticalValue = EvaluateAbilityTacticalValue(ability, affectedTargets);
        float distanceToTarget = GetTileDistance(sourceTile, target.GridPosition);
        float preferredDistance = GetPreferredCombatDistance(ability);
        float rangeBias = ability.UsesAbilityRange ? ability.Range * 0.35f : 0f;
        float movementPenalty = requiresMovement ? 0.75f : 0f;
        float distancePenalty = Mathf.Abs(distanceToTarget - preferredDistance) * 1.5f;
        float selfPreservationBias = character.CurrentHitPoints <= Mathf.CeilToInt(character.MaxHitPoints * 0.35f) &&
                                     IsHealingAbility(ability)
            ? 10f
            : 0f;
        float resourcePenalty = ability.HasResourceCost
            ? (ability.CostAmount / Mathf.Max(1f, character.GetMaxResource(ability.CostResourceType))) * 3f
            : 0f;

        return tacticalValue + rangeBias + selfPreservationBias - movementPenalty - distancePenalty - resourcePenalty;
    }

    private float EvaluateAbilityTacticalValue(TacticsAbilityDefinition ability, IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        if (ability == null || character == null || affectedTargets == null || affectedTargets.Count == 0)
        {
            return 0f;
        }

        float totalValue = 0f;
        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            switch (effect.EffectKind)
            {
                case TacticsAbilityEffectKind.DealDamage:
                    totalValue += ScoreDamageEffect(ability, effect.DealDamage, affectedTargets);
                    break;

                case TacticsAbilityEffectKind.RestoreHitPoints:
                    totalValue += ScoreHealingEffect(effect.RestoreHitPoints, affectedTargets);
                    break;
            }
        }

        return totalValue;
    }

    private float ScoreDamageEffect(
        TacticsAbilityDefinition ability,
        TacticsDealDamageEffectData damage,
        IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        float averageDamage = GetAverageDamageAmount(ability, damage);
        if (averageDamage <= 0f)
        {
            return 0f;
        }

        float totalValue = 0f;
        for (int i = 0; i < affectedTargets.Count; i++)
        {
            TacticsCharacterController target = affectedTargets[i];
            if (target == null || !target.IsAlive)
            {
                continue;
            }

            float effectiveDamage = Mathf.Min(target.CurrentHitPoints, averageDamage);
            float missingHealthPressure = (1f - GetHealthRatio(target)) * 10f;
            float lethalBonus = averageDamage >= target.CurrentHitPoints
                ? 18f + (target.CurrentHitPoints * 0.35f)
                : 0f;
            totalValue += effectiveDamage + missingHealthPressure + lethalBonus;
        }

        return totalValue;
    }

    private float ScoreHealingEffect(
        TacticsRestoreHitPointsEffectData restoreHitPoints,
        IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        float averageHealing = GetAverageHealingAmount(restoreHitPoints);
        if (averageHealing <= 0f)
        {
            return 0f;
        }

        float totalValue = 0f;
        for (int i = 0; i < affectedTargets.Count; i++)
        {
            TacticsCharacterController target = affectedTargets[i];
            if (target == null || !target.IsAlive)
            {
                continue;
            }

            int missingHitPoints = target.MaxHitPoints - target.CurrentHitPoints;
            if (missingHitPoints <= 0)
            {
                continue;
            }

            float effectiveHealing = Mathf.Min(missingHitPoints, averageHealing);
            float healthUrgency = (1f - GetHealthRatio(target)) * 14f;
            float emergencyBonus = GetHealthRatio(target) <= 0.35f ? 16f : 0f;
            float threatBonus = GetNearbyThreatCount(target) * 3.5f;
            float selfPreservationBonus = ReferenceEquals(target, character) ? 6f : 0f;
            totalValue += effectiveHealing + healthUrgency + emergencyBonus + threatBonus + selfPreservationBonus;
        }

        return totalValue;
    }

    private float GetAverageDamageAmount(TacticsAbilityDefinition ability, TacticsDealDamageEffectData damage)
    {
        if (ability == null || character == null)
        {
            return 0f;
        }

        float baseDamage = ability.DamageType == TacticsAbilityDamageType.Magic
            ? (character.BaseMagicDamageMin + character.BaseMagicDamageMax) * 0.5f
            : (character.BaseMeleeDamageMin + character.BaseMeleeDamageMax) * 0.5f;
        float effectBase = damage.DamageFormula == TacticsDamageFormula.FlatValue ? damage.FlatAmount : baseDamage;
        float scalingBonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(character, damage.Scaling);
        return Mathf.Max(0f, (effectBase + scalingBonus) * damage.BonusMultiplier);
    }

    private float GetAverageHealingAmount(TacticsRestoreHitPointsEffectData restoreHitPoints)
    {
        if (character == null)
        {
            return 0f;
        }

        float effectBase = restoreHitPoints.HealingFormula == TacticsDamageFormula.AttackerBaseDamage
            ? (character.BaseMagicDamageMin + character.BaseMagicDamageMax) * 0.5f
            : restoreHitPoints.FlatAmount;
        float scalingBonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(character, restoreHitPoints.Scaling);
        return Mathf.Max(0f, (effectBase + scalingBonus) * restoreHitPoints.BonusMultiplier);
    }

    private float GetBestAbilityOpportunityScore(Vector2Int sourceTile)
    {
        if (character == null || combatSystem == null)
        {
            return 0f;
        }

        EnemyAbilityPlan bestPlan = default;
        bool foundPlan = false;
        EvaluateAbilityPlansForTile(
            character.Abilities,
            sourceTile,
            sourceTile,
            requiresMovement: false,
            ref foundPlan,
            ref bestPlan);
        return foundPlan ? bestPlan.Score : 0f;
    }

    private float ScoreMovementTile(Vector2Int sourceTile, TacticsCharacterController anchorTarget, int pathIndex)
    {
        float immediateOpportunityScore = GetBestAbilityOpportunityScore(sourceTile);
        float anchorDistance = anchorTarget != null ? GetTileDistance(sourceTile, anchorTarget.GridPosition) : 0f;
        float hostilePressure = ScorePressurePosition(sourceTile);
        float supportPressure = ScoreSupportPosition(sourceTile);
        return immediateOpportunityScore +
               hostilePressure +
               supportPressure -
               (anchorDistance * 0.2f) -
               (pathIndex * 0.05f);
    }

    private float ScorePressurePosition(Vector2Int sourceTile)
    {
        List<TacticsCharacterController> hostileTargets = GetHostileTargets();
        float bestScore = 0f;
        for (int i = 0; i < hostileTargets.Count; i++)
        {
            TacticsCharacterController hostile = hostileTargets[i];
            float distance = GetTileDistance(sourceTile, hostile.GridPosition);
            bestScore = Mathf.Max(bestScore, (1f - GetHealthRatio(hostile)) * 4f - (distance * 0.25f));
        }

        return bestScore;
    }

    private float ScoreSupportPosition(Vector2Int sourceTile)
    {
        if (!HasHealingAbility())
        {
            return 0f;
        }

        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: true);
        float bestScore = 0f;
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            int missingHitPoints = ally.MaxHitPoints - ally.CurrentHitPoints;
            if (missingHitPoints <= 0)
            {
                continue;
            }

            float distance = GetTileDistance(sourceTile, ally.GridPosition);
            float urgency = (1f - GetHealthRatio(ally)) * 8f;
            bestScore = Mathf.Max(bestScore, urgency + (missingHitPoints * 0.15f) - (distance * 0.2f));
        }

        return bestScore;
    }

    private float GetPreferredCombatDistance(IReadOnlyList<TacticsAbilityDefinition> abilities)
    {
        if (abilities == null || abilities.Count == 0)
        {
            return 1f;
        }

        float preferredDistance = 1f;
        for (int i = 0; i < abilities.Count; i++)
        {
            preferredDistance = Mathf.Max(preferredDistance, GetPreferredCombatDistance(abilities[i]));
        }

        return preferredDistance;
    }

    private static float GetPreferredCombatDistance(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return 1f;
        }

        if (!ability.UsesAbilityRange)
        {
            return 1f;
        }

        return Mathf.Max(2f, ability.Range - 1);
    }

    private bool TryGetPathToTarget(TacticsCharacterController target, out List<Vector2Int> path)
    {
        path = null;

        if (character == null || target == null || !target.isActiveAndEnabled || !target.IsAlive)
        {
            return false;
        }

        return character.TryGetPathTo(target.GridPosition, out path, enforceMoveRange: false);
    }

    private List<TacticsCharacterController> BuildMovementAnchorTargets()
    {
        reusableAnchorTargets.Clear();
        AddTargetsIfMissing(reusableAnchorTargets, GetHostileTargets());
        AddTargetsIfMissing(reusableAnchorTargets, GetAlliedTargets(includeSelf: false));
        if (HasHealingAbility())
        {
            AddTargetsIfMissing(reusableAnchorTargets, GetAlliedTargets(includeSelf: true));
        }

        return reusableAnchorTargets;
    }

    private List<TacticsCharacterController> GetCandidateTargetsForAbility(TacticsAbilityDefinition ability)
    {
        reusableCandidateTargets.Clear();
        if (ability == null || character == null)
        {
            return reusableCandidateTargets;
        }

        switch (ability.TargetRule)
        {
            case TacticsAbilityTargetRule.HostileUnit:
                AddTargetsIfMissing(reusableCandidateTargets, GetHostileTargets());
                break;

            case TacticsAbilityTargetRule.AlliedUnit:
                AddTargetsIfMissing(reusableCandidateTargets, GetAlliedTargets(includeSelf: false));
                break;

            case TacticsAbilityTargetRule.AlliedUnitOrSelf:
                AddTargetsIfMissing(reusableCandidateTargets, GetAlliedTargets(includeSelf: true));
                break;

            case TacticsAbilityTargetRule.Self:
                reusableCandidateTargets.Add(character);
                break;
        }

        return reusableCandidateTargets;
    }

    private List<TacticsCharacterController> GetHostileTargets()
    {
        reusableHostileTargets.Clear();
        if (characterRegistry == null)
        {
            return reusableHostileTargets;
        }

        characterRegistry.GetHostileCharacters(character, reusableHostileTargets);
        return reusableHostileTargets;
    }

    private List<TacticsCharacterController> GetAlliedTargets(bool includeSelf)
    {
        List<TacticsCharacterController> results = includeSelf
            ? reusableAlliedTargetsIncludingSelf
            : reusableAlliedTargets;
        results.Clear();

        if (characterRegistry == null)
        {
            return results;
        }

        characterRegistry.GetAlliedCharacters(character, includeSelf, results);
        return results;
    }

    private bool HasHealingAbility()
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = character != null ? character.Abilities : null;
        if (abilities == null)
        {
            return false;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            if (IsHealingAbility(abilities[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHealingAbility(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return false;
        }

        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].EffectKind == TacticsAbilityEffectKind.RestoreHitPoints)
            {
                return true;
            }
        }

        return false;
    }

    private int GetNearbyThreatCount(TacticsCharacterController target)
    {
        if (target == null || character == null)
        {
            return 0;
        }

        int threatCount = 0;
        List<TacticsCharacterController> hostileTargets = GetHostileTargets();
        for (int i = 0; i < hostileTargets.Count; i++)
        {
            TacticsCharacterController candidate = hostileTargets[i];
            if (GetTileDistance(candidate.GridPosition, target.GridPosition) <= Mathf.Max(1, candidate.MoveRange + 1))
            {
                threatCount++;
            }
        }

        return threatCount;
    }

    private static float GetHealthRatio(TacticsCharacterController target)
    {
        if (target == null || target.MaxHitPoints <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(target.CurrentHitPoints / (float)target.MaxHitPoints);
    }

    private static void AddTargetsIfMissing(List<TacticsCharacterController> destination, List<TacticsCharacterController> source)
    {
        if (destination == null || source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            TacticsCharacterController candidate = source[i];
            if (candidate != null && !destination.Contains(candidate))
            {
                destination.Add(candidate);
            }
        }
    }

    private static int GetTileDistance(Vector2Int source, Vector2Int target)
    {
        return Mathf.Abs(source.x - target.x) + Mathf.Abs(source.y - target.y);
    }

    private bool TryMove(Vector2Int destination)
    {
        return coopSessionCoordinator != null
            ? coopSessionCoordinator.RequestMove(character, destination)
            : character != null && character.TryMoveTo(destination);
    }

    private bool TryUseAbility(TacticsAbilityDefinition ability, Vector2Int targetTile)
    {
        if (character == null || ability == null)
        {
            return false;
        }

        if (coopSessionCoordinator != null)
        {
            return coopSessionCoordinator.RequestUseAbility(character, ability, targetTile);
        }

        return combatSystem != null && combatSystem.TryUseAbility(character, ability, targetTile);
    }

    private bool TryEndTurn()
    {
        if (character == null)
        {
            return false;
        }

        return coopSessionCoordinator != null
            ? coopSessionCoordinator.RequestEndTurn(character)
            : character.TryEndTurn();
    }
}
