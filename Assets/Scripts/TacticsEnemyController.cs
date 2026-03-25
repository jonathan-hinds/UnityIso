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
    private TacticsEnemyStrategicContext strategicContext;

    private readonly struct EnemyAbilityPlan
    {
        public EnemyAbilityPlan(
            TacticsAbilityDefinition ability,
            TacticsCharacterController target,
            Vector2Int sourceTile,
            Vector2Int targetTile,
            Vector2Int moveDestination,
            TacticsAbilityCostPayment costPayment,
            bool requiresMovement,
            float score)
        {
            Ability = ability;
            Target = target;
            SourceTile = sourceTile;
            TargetTile = targetTile;
            MoveDestination = moveDestination;
            CostPayment = costPayment;
            RequiresMovement = requiresMovement;
            Score = score;
        }

        public TacticsAbilityDefinition Ability { get; }
        public TacticsCharacterController Target { get; }
        public Vector2Int SourceTile { get; }
        public Vector2Int TargetTile { get; }
        public Vector2Int MoveDestination { get; }
        public TacticsAbilityCostPayment CostPayment { get; }
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

        strategicContext = BuildStrategicContext();

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
        strategicContext = null;

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
            if (ability == null ||
                !character.TryGetAbilityCostPayment(ability, movementAvailable: true, out TacticsAbilityCostPayment payment))
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

            float score = ScoreAbilityPlan(ability, priorityTarget, character.GridPosition, payment, requiresMovement: false, affectedTargets);
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
                    payment,
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
        bool movementAvailable = !requiresMovement && character != null && character.HasMovementAvailableForAbilityCost;

        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null ||
                !character.TryGetAbilityCostPayment(ability, movementAvailable, out TacticsAbilityCostPayment payment))
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
                    primaryTarget.GridPosition,
                    movementAvailable);

                if (affectedTargets.Count == 0)
                {
                    continue;
                }

                float score = ScoreAbilityPlan(ability, primaryTarget, sourceTile, payment, requiresMovement, affectedTargets);
                if (!foundPlan || score > bestPlan.Score)
                {
                    foundPlan = true;
                    bestPlan = new EnemyAbilityPlan(
                        ability,
                        primaryTarget,
                        sourceTile,
                        primaryTarget.GridPosition,
                        moveDestination,
                        payment,
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
        TacticsAbilityCostPayment costPayment,
        bool requiresMovement,
        IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        float tacticalValue = EvaluateAbilityTacticalValue(ability, affectedTargets);
        float distanceToTarget = GetTileDistance(sourceTile, target.GridPosition);
        float preferredDistance = GetPreferredCombatDistance(ability);
        float rangeBias = ability.UsesAbilityRange ? ability.Range * 0.35f : 0f;
        float movementPenalty = requiresMovement ? 0.75f : 0f;
        float distancePenalty = Mathf.Abs(distanceToTarget - preferredDistance) * 1.5f;
        float coordinationBonus = strategicContext != null
            ? strategicContext.ScoreFocusFire(sourceTile, target) + strategicContext.ScoreFormation(ability, sourceTile, target)
            : 0f;
        float selfPreservationBias = character.CurrentHitPoints <= Mathf.CeilToInt(character.MaxHitPoints * 0.35f) &&
                                     IsSupportAbility(ability)
            ? 10f
            : 0f;
        float costPenalty = GetCostPenalty(ability, costPayment, requiresMovement);
        float teamIntentBonus = strategicContext != null
            ? strategicContext.ScoreTeamPlan(sourceTile, target, ability, affectedTargets, costPayment)
            : 0f;

        return tacticalValue + coordinationBonus + rangeBias + selfPreservationBias + teamIntentBonus - movementPenalty - distancePenalty - costPenalty;
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

                case TacticsAbilityEffectKind.RestoreResource:
                    totalValue += ScoreResourceRestoreEffect(effect.RestoreResource, affectedTargets);
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
            float teamBonus = strategicContext != null
                ? strategicContext.ScoreHealingTarget(target, effectiveHealing, ReferenceEquals(target, character))
                : 0f;
            totalValue += effectiveHealing + healthUrgency + emergencyBonus + threatBonus + selfPreservationBonus + teamBonus;
        }

        return totalValue;
    }

    private float ScoreResourceRestoreEffect(
        TacticsRestoreResourceEffectData restoreResource,
        IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        float averageRestore = GetAverageRestoreResourceAmount(restoreResource);
        if (averageRestore <= 0f || restoreResource.ResourceType == TacticsAbilityResourceType.None)
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

            int missingResource = target.GetMissingResource(restoreResource.ResourceType);
            if (missingResource <= 0)
            {
                continue;
            }

            float effectiveRestore = Mathf.Min(missingResource, averageRestore);
            float urgency = GetResourceRatio(target, restoreResource.ResourceType) <= 0.35f ? 12f : 0f;
            float teamBonus = strategicContext != null
                ? strategicContext.ScoreResourceSupportTarget(target, restoreResource.ResourceType, effectiveRestore, ReferenceEquals(target, character))
                : 0f;
            totalValue += effectiveRestore + urgency + teamBonus;
        }

        return totalValue;
    }

    private float GetAverageDamageAmount(TacticsAbilityDefinition ability, TacticsDealDamageEffectData damage)
    {
        return TacticsAbilityEffectMath.EvaluateDamageAmount(character, ability, damage, useAverageRoll: true);
    }

    private float GetAverageHealingAmount(TacticsRestoreHitPointsEffectData restoreHitPoints)
    {
        return TacticsAbilityEffectMath.EvaluateRestoreHitPointsAmount(character, restoreHitPoints, useAverageRoll: true);
    }

    private float GetAverageRestoreResourceAmount(TacticsRestoreResourceEffectData restoreResource)
    {
        return TacticsAbilityEffectMath.EvaluateRestoreResourceAmount(character, restoreResource, useAverageRoll: true);
    }

    private float GetCostPenalty(
        TacticsAbilityDefinition ability,
        TacticsAbilityCostPayment costPayment,
        bool requiresMovement)
    {
        if (ability == null || !costPayment.HasCost || character == null)
        {
            return 0f;
        }

        if (costPayment.UsesMovement)
        {
            return requiresMovement ? 1000f : 1.25f;
        }

        int maxResource = Mathf.Max(1, character.GetMaxResource(costPayment.ResourceType));
        return (costPayment.Amount / (float)maxResource) * 3f;
    }

    private float GetBestAbilityOpportunityScore(Vector2Int sourceTile, bool movementAvailable)
    {
        if (character == null || combatSystem == null)
        {
            return 0f;
        }

        EnemyAbilityPlan bestPlan = default;
        bool foundPlan = false;
        IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null ||
                !character.TryGetAbilityCostPayment(ability, movementAvailable, out TacticsAbilityCostPayment payment))
            {
                continue;
            }

            List<TacticsCharacterController> candidateTargets = GetCandidateTargetsForAbility(ability);
            for (int targetIndex = 0; targetIndex < candidateTargets.Count; targetIndex++)
            {
                TacticsCharacterController primaryTarget = candidateTargets[targetIndex];
                if (primaryTarget == null ||
                    !primaryTarget.isActiveAndEnabled ||
                    !primaryTarget.IsAlive ||
                    !combatSystem.CanTargetTileFromTile(character, sourceTile, ability, primaryTarget.GridPosition))
                {
                    continue;
                }

                IReadOnlyList<TacticsCharacterController> affectedTargets = combatSystem.GetPreviewTargetsFromTile(
                    character,
                    sourceTile,
                    ability,
                    primaryTarget.GridPosition,
                    movementAvailable);
                if (affectedTargets.Count == 0)
                {
                    continue;
                }

                float score = ScoreAbilityPlan(ability, primaryTarget, sourceTile, payment, requiresMovement: !movementAvailable, affectedTargets);
                if (!foundPlan || score > bestPlan.Score)
                {
                    foundPlan = true;
                    bestPlan = new EnemyAbilityPlan(
                        ability,
                        primaryTarget,
                        sourceTile,
                        primaryTarget.GridPosition,
                        sourceTile,
                        payment,
                        requiresMovement: !movementAvailable,
                        score);
                }
            }
        }

        return foundPlan ? bestPlan.Score : 0f;
    }

    private float ScoreMovementTile(Vector2Int sourceTile, TacticsCharacterController anchorTarget, int pathIndex)
    {
        float immediateOpportunityScore = GetBestAbilityOpportunityScore(sourceTile, movementAvailable: false);
        float anchorDistance = anchorTarget != null ? GetTileDistance(sourceTile, anchorTarget.GridPosition) : 0f;
        float hostilePressure = ScorePressurePosition(sourceTile);
        float supportPressure = ScoreSupportPosition(sourceTile);
        float teamCoordination = strategicContext != null
            ? strategicContext.ScoreSupportPosition(sourceTile, HasHealingAbility(), HasResourceRestoreAbility()) +
              strategicContext.ScoreFormation(null, sourceTile, anchorTarget) +
              (anchorTarget != null ? strategicContext.ScoreFocusFire(sourceTile, anchorTarget) : 0f)
            : 0f;
        return immediateOpportunityScore +
               hostilePressure +
               supportPressure +
               teamCoordination -
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
        if (!HasHealingAbility() && !HasResourceRestoreAbility())
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

        if (HasResourceRestoreAbility())
        {
            for (int i = 0; i < allies.Count; i++)
            {
                TacticsCharacterController ally = allies[i];
                float staminaNeed = 0f;
                if (ally != null && ally.MaxStamina > 0)
                {
                    staminaNeed = (1f - GetResourceRatio(ally, TacticsAbilityResourceType.Stamina)) * 6f;
                }

                float manaNeed = 0f;
                if (ally != null && ally.MaxMana > 0)
                {
                    manaNeed = (1f - GetResourceRatio(ally, TacticsAbilityResourceType.Mana)) * 6f;
                }

                float bestNeed = Mathf.Max(staminaNeed, manaNeed);
                if (bestNeed <= 0f)
                {
                    continue;
                }

                float distance = GetTileDistance(sourceTile, ally.GridPosition);
                bestScore = Mathf.Max(bestScore, bestNeed - (distance * 0.2f));
            }
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
        if (HasHealingAbility() || HasResourceRestoreAbility())
        {
            AddTargetsIfMissing(reusableAnchorTargets, GetAlliedTargets(includeSelf: true));
        }

        if (strategicContext != null && strategicContext.FocusTarget != null && !reusableAnchorTargets.Contains(strategicContext.FocusTarget))
        {
            reusableAnchorTargets.Add(strategicContext.FocusTarget);
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

    private bool HasResourceRestoreAbility()
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = character != null ? character.Abilities : null;
        if (abilities == null)
        {
            return false;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            if (IsResourceRestoreAbility(abilities[i]))
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

    private static bool IsResourceRestoreAbility(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return false;
        }

        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].EffectKind == TacticsAbilityEffectKind.RestoreResource)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportAbility(TacticsAbilityDefinition ability)
    {
        return IsHealingAbility(ability) || IsResourceRestoreAbility(ability);
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

    private static float GetResourceRatio(TacticsCharacterController target, TacticsAbilityResourceType resourceType)
    {
        if (target == null)
        {
            return 0f;
        }

        int maxResource = target.GetMaxResource(resourceType);
        if (maxResource <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(target.GetCurrentResource(resourceType) / (float)maxResource);
    }

    private TacticsEnemyStrategicContext BuildStrategicContext()
    {
        return character != null && characterRegistry != null
            ? new TacticsEnemyStrategicContext(character, characterRegistry)
            : null;
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

public sealed class TacticsEnemyStrategicContext
{
    private readonly TacticsCharacterController actor;
    private readonly List<TacticsCharacterController> allies = new();
    private readonly List<TacticsCharacterController> hostiles = new();

    public TacticsEnemyStrategicContext(TacticsCharacterController actor, TacticsCharacterRegistry registry)
    {
        this.actor = actor;

        if (actor == null || registry == null)
        {
            return;
        }

        registry.GetAlliedCharacters(actor, includeSelf: true, allies);
        registry.GetHostileCharacters(actor, hostiles);
        FocusTarget = ResolveFocusTarget();
    }

    public IReadOnlyList<TacticsCharacterController> Allies => allies;
    public IReadOnlyList<TacticsCharacterController> Hostiles => hostiles;
    public TacticsCharacterController FocusTarget { get; }

    public float ScoreFocusFire(Vector2Int sourceTile, TacticsCharacterController target)
    {
        if (actor == null || target == null || target.Team == actor.Team)
        {
            return 0f;
        }

        float score = ReferenceEquals(target, FocusTarget) ? 10f : 0f;
        score += CountAlliesThreateningTarget(target) * 2.5f;
        score -= GetTileDistance(sourceTile, target.GridPosition) * 0.15f;
        return score;
    }

    public float ScoreFormation(TacticsAbilityDefinition ability, Vector2Int sourceTile, TacticsCharacterController primaryTarget)
    {
        if (actor == null)
        {
            return 0f;
        }

        float preferredDistance = GetPreferredCombatDistance(actor);
        bool actorIsBackliner = preferredDistance > 1.5f || IsSupportUnit(actor);
        float bestScore = 0f;

        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || ReferenceEquals(ally, actor))
            {
                continue;
            }

            float allyPreferredDistance = GetPreferredCombatDistance(ally);
            bool allyIsFrontliner = allyPreferredDistance <= 1.5f && !IsSupportUnit(ally);
            bool complementaryPair = actorIsBackliner ? allyIsFrontliner : (allyPreferredDistance > 1.5f || IsSupportUnit(ally));
            if (!complementaryPair)
            {
                continue;
            }

            int allyDistance = GetTileDistance(sourceTile, ally.GridPosition);
            float distanceScore = actorIsBackliner
                ? Mathf.Clamp(4f - Mathf.Abs(allyDistance - 3f), -2f, 4f)
                : Mathf.Clamp(3f - Mathf.Abs(allyDistance - 2f), -2f, 3f);
            float sharedPressureBonus = 0f;
            if (primaryTarget != null && primaryTarget.Team != actor.Team)
            {
                int allyTargetDistance = GetTileDistance(ally.GridPosition, primaryTarget.GridPosition);
                sharedPressureBonus = Mathf.Max(0f, 3f - (allyTargetDistance * 0.5f));
            }

            bestScore = Mathf.Max(bestScore, distanceScore + sharedPressureBonus);
        }

        return bestScore;
    }

    public float ScoreSupportPosition(Vector2Int sourceTile, bool canRestoreHitPoints, bool canRestoreResources)
    {
        if (!canRestoreHitPoints && !canRestoreResources)
        {
            return 0f;
        }

        float bestScore = 0f;
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null)
            {
                continue;
            }

            float needScore = 0f;
            if (canRestoreHitPoints)
            {
                needScore += (1f - GetHealthRatio(ally)) * 8f;
            }

            if (canRestoreResources)
            {
                needScore += Mathf.Max(
                    ScoreResourceNeed(ally, TacticsAbilityResourceType.Stamina, restoredAmount: 0f),
                    ScoreResourceNeed(ally, TacticsAbilityResourceType.Mana, restoredAmount: 0f));
            }

            if (needScore <= 0f)
            {
                continue;
            }

            float distance = GetTileDistance(sourceTile, ally.GridPosition);
            bestScore = Mathf.Max(bestScore, needScore - (distance * 0.35f));
        }

        return bestScore;
    }

    public float ScoreTeamPlan(
        Vector2Int sourceTile,
        TacticsCharacterController primaryTarget,
        TacticsAbilityDefinition ability,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        TacticsAbilityCostPayment costPayment)
    {
        if (actor == null)
        {
            return 0f;
        }

        float pairingScore = ScoreRolePairing(sourceTile, primaryTarget);
        float supportChainScore = ScoreSupportChain(sourceTile, ability, affectedTargets);
        float costSynergy = costPayment.UsesMovement && ability != null && ability.AllowsMovementAsAlternateCost
            ? 2.5f
            : 0f;
        return pairingScore + supportChainScore + costSynergy;
    }

    public float ScoreHealingTarget(TacticsCharacterController target, float effectiveHealing, bool isSelfTarget)
    {
        if (target == null || target.Team != actor.Team || effectiveHealing <= 0f)
        {
            return 0f;
        }

        float healthUrgency = (1f - GetHealthRatio(target)) * 14f;
        float offensiveValue = GetOffensivePotential(target) * 0.15f;
        float threatBonus = CountNearbyHostiles(target) * 2.25f;
        float selfBonus = isSelfTarget ? 5f : 2f;
        return effectiveHealing + healthUrgency + offensiveValue + threatBonus + selfBonus;
    }

    public float ScoreResourceSupportTarget(
        TacticsCharacterController target,
        TacticsAbilityResourceType resourceType,
        float restoredAmount,
        bool isSelfTarget)
    {
        if (target == null || target.Team != actor.Team || resourceType == TacticsAbilityResourceType.None)
        {
            return 0f;
        }

        return ScoreResourceNeed(target, resourceType, restoredAmount) + (isSelfTarget ? 3f : 1.5f);
    }

    private float ScoreRolePairing(Vector2Int sourceTile, TacticsCharacterController primaryTarget)
    {
        if (actor == null)
        {
            return 0f;
        }

        bool actorIsBackliner = GetPreferredCombatDistance(actor) > 1.5f || IsSupportUnit(actor);
        float bestScore = 0f;

        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || ReferenceEquals(ally, actor))
            {
                continue;
            }

            bool allyIsFrontliner = GetPreferredCombatDistance(ally) <= 1.5f && !IsSupportUnit(ally);
            bool complementaryPair = actorIsBackliner ? allyIsFrontliner : !allyIsFrontliner;
            if (!complementaryPair)
            {
                continue;
            }

            float allyDistance = GetTileDistance(sourceTile, ally.GridPosition);
            float spacingScore = actorIsBackliner
                ? Mathf.Clamp(4f - Mathf.Abs(allyDistance - 3f), -2f, 4f)
                : Mathf.Clamp(3f - Mathf.Abs(allyDistance - 2f), -2f, 3f);
            float targetPressure = 0f;
            if (primaryTarget != null && primaryTarget.Team != actor.Team)
            {
                targetPressure = Mathf.Max(0f, 4f - (GetTileDistance(ally.GridPosition, primaryTarget.GridPosition) * 0.5f));
            }

            bestScore = Mathf.Max(bestScore, spacingScore + targetPressure);
        }

        return bestScore;
    }

    private float ScoreSupportChain(
        Vector2Int sourceTile,
        TacticsAbilityDefinition ability,
        IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        if (ability == null || affectedTargets == null || affectedTargets.Count == 0)
        {
            return 0f;
        }

        float bestScore = 0f;
        for (int i = 0; i < affectedTargets.Count; i++)
        {
            TacticsCharacterController target = affectedTargets[i];
            if (target == null || target.Team != actor.Team)
            {
                continue;
            }

            float chainScore = GetOffensivePotential(target) * 0.08f;
            chainScore += CountNearbyHostiles(target) * 1.5f;
            chainScore -= GetTileDistance(sourceTile, target.GridPosition) * 0.1f;
            bestScore = Mathf.Max(bestScore, chainScore);
        }

        return bestScore;
    }

    private TacticsCharacterController ResolveFocusTarget()
    {
        TacticsCharacterController bestTarget = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hostiles.Count; i++)
        {
            TacticsCharacterController hostile = hostiles[i];
            if (hostile == null)
            {
                continue;
            }

            float score = (1f - GetHealthRatio(hostile)) * 18f;
            score += GetOffensivePotential(hostile) * 0.2f;
            score += CountAlliesThreateningTarget(hostile) * 2f;
            score -= GetTileDistance(actor.GridPosition, hostile.GridPosition) * 0.1f;
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = hostile;
            }
        }

        return bestTarget;
    }

    private float ScoreResourceNeed(TacticsCharacterController target, TacticsAbilityResourceType resourceType, float restoredAmount)
    {
        int maxResource = target.GetMaxResource(resourceType);
        if (maxResource <= 0)
        {
            return 0f;
        }

        int currentResource = target.GetCurrentResource(resourceType);
        int missingResource = target.GetMissingResource(resourceType);
        if (missingResource <= 0 && restoredAmount <= 0f)
        {
            return 0f;
        }

        float effectiveRestore = restoredAmount > 0f ? Mathf.Min(missingResource, restoredAmount) : missingResource;
        float spenderProfile = GetResourceSpendProfile(target, resourceType);
        float unlockBonus = GetUnlockedAbilityBonus(target, resourceType, currentResource, currentResource + Mathf.RoundToInt(restoredAmount));
        float pressureBonus = GetOffensivePotential(target) * 0.12f;
        float urgency = (missingResource / (float)maxResource) * 12f;
        return effectiveRestore * 0.4f + spenderProfile * 0.16f + unlockBonus + pressureBonus + urgency;
    }

    private float GetUnlockedAbilityBonus(
        TacticsCharacterController target,
        TacticsAbilityResourceType resourceType,
        int beforeAmount,
        int afterAmount)
    {
        float bestBonus = 0f;
        IReadOnlyList<TacticsAbilityDefinition> abilities = target != null ? target.Abilities : null;
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

            bestBonus = Mathf.Max(bestBonus, GetAbilityStrategicValue(target, ability) * 0.45f + 5f);
        }

        return bestBonus;
    }

    private float GetResourceSpendProfile(TacticsCharacterController target, TacticsAbilityResourceType resourceType)
    {
        float profile = 0f;
        IReadOnlyList<TacticsAbilityDefinition> abilities = target != null ? target.Abilities : null;
        if (abilities == null)
        {
            return 0f;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null || ability.CostResourceType != resourceType || ability.CostAmount <= 0)
            {
                continue;
            }

            profile += ability.CostAmount + (GetAbilityStrategicValue(target, ability) * 0.35f);
        }

        return profile;
    }

    private float GetAbilityStrategicValue(TacticsCharacterController unit, TacticsAbilityDefinition ability)
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

        if (ability.UsesAreaOfEffect)
        {
            value += ability.AreaOfEffectSize * 0.5f;
        }

        return value;
    }

    private float GetOffensivePotential(TacticsCharacterController unit)
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = unit != null ? unit.Abilities : null;
        if (abilities == null || abilities.Count == 0)
        {
            return 0f;
        }

        float bestValue = 0f;
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            float abilityValue = 0f;
            IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
            for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                TacticsAbilityEffectDefinitionData effect = effects[effectIndex];
                if (effect.EffectKind == TacticsAbilityEffectKind.DealDamage)
                {
                    abilityValue += TacticsAbilityEffectMath.EvaluateDamageAmount(unit, ability, effect.DealDamage, useAverageRoll: true);
                }
            }

            bestValue = Mathf.Max(bestValue, abilityValue);
        }

        return bestValue;
    }

    private int CountAlliesThreateningTarget(TacticsCharacterController target)
    {
        int count = 0;
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null)
            {
                continue;
            }

            int preferredDistance = Mathf.RoundToInt(GetPreferredCombatDistance(ally));
            if (GetTileDistance(ally.GridPosition, target.GridPosition) <= Mathf.Max(1, preferredDistance + ally.MoveRange))
            {
                count++;
            }
        }

        return count;
    }

    private int CountNearbyHostiles(TacticsCharacterController target)
    {
        int count = 0;
        for (int i = 0; i < hostiles.Count; i++)
        {
            TacticsCharacterController hostile = hostiles[i];
            if (hostile != null && GetTileDistance(hostile.GridPosition, target.GridPosition) <= hostile.MoveRange + 1)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSupportUnit(TacticsCharacterController unit)
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = unit != null ? unit.Abilities : null;
        if (abilities == null)
        {
            return false;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
            for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                if (effects[effectIndex].EffectKind is TacticsAbilityEffectKind.RestoreHitPoints or TacticsAbilityEffectKind.RestoreResource)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float GetPreferredCombatDistance(TacticsCharacterController unit)
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = unit != null ? unit.Abilities : null;
        if (abilities == null || abilities.Count == 0)
        {
            return 1f;
        }

        float preferredDistance = 1f;
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            preferredDistance = Mathf.Max(preferredDistance, ability.UsesAbilityRange ? Mathf.Max(2f, ability.Range - 1) : 1f);
        }

        return preferredDistance;
    }

    private static int GetTileDistance(Vector2Int source, Vector2Int target)
    {
        return Mathf.Abs(source.x - target.x) + Mathf.Abs(source.y - target.y);
    }

    private static float GetHealthRatio(TacticsCharacterController target)
    {
        if (target == null || target.MaxHitPoints <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(target.CurrentHitPoints / (float)target.MaxHitPoints);
    }
}
