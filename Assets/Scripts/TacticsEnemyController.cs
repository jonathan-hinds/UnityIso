using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public interface ITacticsAutomatedTurnController
{
    void BeginAutomatedTurn();
    void CancelAutomatedTurn();
}

[DisallowMultipleComponent]
[RequireComponent(typeof(TacticsCharacterController))]
public sealed class TacticsEnemyController : MonoBehaviour, ITacticsAutomatedTurnController
{
    private static readonly ProfilerMarker ExecuteTurnRoutineMarker = new("TacticsEnemyController.ExecuteTurnRoutine");
    private static readonly ProfilerMarker BuildStrategicContextMarker = new("TacticsEnemyController.BuildStrategicContext");
    private static readonly ProfilerMarker ResolveBleedResponseMarker = new("TacticsEnemyController.ResolveBleedResponse");
    private static readonly ProfilerMarker PriorityPlanMarker = new("TacticsEnemyController.TryBuildPriorityTargetAbilityPlan");
    private static readonly ProfilerMarker BestAbilityPlanMarker = new("TacticsEnemyController.TryBuildBestAbilityPlan");
    private static readonly ProfilerMarker BestMovementPlanMarker = new("TacticsEnemyController.TryBuildBestMovementPlan");
    private static readonly ProfilerMarker ImmediateAbilityPlanMarker = new("TacticsEnemyController.TryUseBestAbilityFromCurrentPosition");
    private static readonly ProfilerMarker EvaluateAbilityPlansForTileMarker = new("TacticsEnemyController.EvaluateAbilityPlansForTile");
    private static readonly ProfilerMarker BestOffenseOpportunityMarker = new("TacticsEnemyController.GetBestOffensiveAbilityOpportunityScore");

    private enum EnemyBleedResponseMode
    {
        Ignore = 0,
        FightThrough = 1,
        Reposition = 2,
        SeekSupport = 3
    }

    [SerializeField] private TacticsCharacterController character;
    [SerializeField] private TacticsTurnManager turnManager;
    [SerializeField] private TacticsCombatSystem combatSystem;
    [SerializeField] private TacticsCoopSessionCoordinator coopSessionCoordinator;
    [SerializeField] private TacticsCharacterRegistry characterRegistry;
    [SerializeField, Min(0f)] private float thinkDelay = 0.2f;
    [SerializeField, Min(0f)] private float endTurnDelay = 0.15f;

    [Header("Bleed AI")]
    [SerializeField, Min(0f)] private float bleedIgnoreWeight = 0.6f;
    [SerializeField, Min(0f)] private float bleedFightThroughWeight = 1.35f;
    [SerializeField, Min(0f)] private float bleedRepositionWeight = 0.85f;
    [SerializeField, Min(0f)] private float bleedSeekSupportWeight = 0.9f;
    [SerializeField, Range(0f, 1f)] private float bleedLowHealthSeekThreshold = 0.4f;
    [SerializeField, Min(0f)] private float bleedLowHealthSeekBonusWeight = 1.4f;
    [SerializeField, Min(0f)] private float bleedMovementPenaltyPerDamage = 0.1f;
    [SerializeField, Min(0f)] private float bleedActionPenaltyPerDamage = 0.32f;
    [SerializeField, Min(0f)] private float bleedSeekSupportMovementBonus = 4f;
    [SerializeField, Range(0f, 1f)] private float tauntEndangeredHealthThreshold = 0.55f;
    [SerializeField, Range(0f, 1f)] private float tauntCriticalHealthThreshold = 0.35f;

    private Coroutine turnRoutine;
    private readonly List<TacticsCharacterController> reusableCandidateTargets = new();
    private readonly List<TacticsCharacterController> reusableAnchorTargets = new();
    private readonly List<TacticsCharacterController> reusableHostileTargets = new();
    private readonly List<TacticsCharacterController> reusableAlliedTargets = new();
    private readonly List<TacticsCharacterController> reusableAlliedTargetsIncludingSelf = new();
    private readonly List<Vector2Int> reusableThrowDestinationTiles = new();
    private readonly Dictionary<TacticsCharacterController, List<Vector2Int>> cachedPathsByTarget = new();
    private readonly Dictionary<Vector2Int, float> cachedOffenseScoreWithMovementByTile = new();
    private readonly Dictionary<Vector2Int, float> cachedOffenseScoreWithoutMovementByTile = new();
    private string priorityTargetRuntimeCharacterId = string.Empty;
    private TacticsEnemyStrategicContext strategicContext;
    private EnemyBleedResponseMode currentBleedResponseMode = EnemyBleedResponseMode.FightThrough;
    private bool turnTargetCacheInitialized;

    private readonly struct EnemyAbilityPlan
    {
        public EnemyAbilityPlan(
            TacticsAbilityDefinition ability,
            TacticsCharacterController target,
            Vector2Int sourceTile,
            Vector2Int targetTile,
            Vector2Int? throwDestination,
            Vector2Int moveDestination,
            TacticsAbilityCostPayment costPayment,
            bool requiresMovement,
            float score)
        {
            Ability = ability;
            Target = target;
            SourceTile = sourceTile;
            TargetTile = targetTile;
            ThrowDestination = throwDestination;
            MoveDestination = moveDestination;
            CostPayment = costPayment;
            RequiresMovement = requiresMovement;
            Score = score;
        }

        public TacticsAbilityDefinition Ability { get; }
        public TacticsCharacterController Target { get; }
        public Vector2Int SourceTile { get; }
        public Vector2Int TargetTile { get; }
        public Vector2Int? ThrowDestination { get; }
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

        ClearTurnEvaluationCaches();
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
        ClearTurnEvaluationCaches();
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

        PrepareTurnEvaluationCaches();
        using (BuildStrategicContextMarker.Auto())
        {
            strategicContext = BuildStrategicContext();
        }

        using (ResolveBleedResponseMarker.Auto())
        {
            currentBleedResponseMode = ResolveBleedResponseMode();
        }

        bool attemptedAction = false;
        if (TryBuildPriorityTargetAbilityPlan(out EnemyAbilityPlan priorityPlan))
        {
            attemptedAction = TryUseAbility(priorityPlan.Ability, priorityPlan.TargetTile, priorityPlan.ThrowDestination);
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
                attemptedAction = TryUseAbility(abilityPlan.Ability, abilityPlan.TargetTile, abilityPlan.ThrowDestination);
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
            using (ImmediateAbilityPlanMarker.Auto())
            {
                TryUseBestAbilityFromCurrentPosition();
            }
        }

        ClearTurnEvaluationCaches();
        priorityTargetRuntimeCharacterId = string.Empty;
        strategicContext = null;
        currentBleedResponseMode = EnemyBleedResponseMode.FightThrough;

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

            if (ability.AppliesThrowing)
            {
                IReadOnlyList<Vector2Int> throwDestinations = combatSystem.GetValidThrowDestinationsFromTile(
                    character,
                    character.GridPosition,
                    priorityTarget,
                    ability,
                    reusableThrowDestinationTiles);
                for (int throwIndex = 0; throwIndex < throwDestinations.Count; throwIndex++)
                {
                    Vector2Int throwDestination = throwDestinations[throwIndex];
                    if (!TryScoreAbilityPlan(
                            ability,
                            priorityTarget,
                            character.GridPosition,
                            payment,
                            requiresMovement: false,
                            affectedTargets,
                            throwDestination,
                            out float planScore))
                    {
                        continue;
                    }

                    planScore += 1000f;
                    if (!foundPlan || planScore > bestPlan.Score)
                    {
                        foundPlan = true;
                        bestPlan = new EnemyAbilityPlan(
                            ability,
                            priorityTarget,
                            character.GridPosition,
                            priorityTarget.GridPosition,
                            throwDestination,
                            character.GridPosition,
                            payment,
                            requiresMovement: false,
                            planScore);
                    }
                }

                continue;
            }

            if (!TryScoreAbilityPlan(
                    ability,
                    priorityTarget,
                    character.GridPosition,
                    payment,
                    requiresMovement: false,
                    affectedTargets,
                    null,
                    out float score))
            {
                continue;
            }

            score += 1000f;
            if (!foundPlan || score > bestPlan.Score)
            {
                foundPlan = true;
                bestPlan = new EnemyAbilityPlan(
                    ability,
                    priorityTarget,
                    character.GridPosition,
                    priorityTarget.GridPosition,
                    null,
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
               TryUseAbility(immediatePlan.Ability, immediatePlan.TargetTile, immediatePlan.ThrowDestination);
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
        using (EvaluateAbilityPlansForTileMarker.Auto())
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

                IReadOnlyList<TacticsCharacterController> candidateTargets = combatSystem.GetPrimaryTargetCandidatesFromTile(
                    character,
                    sourceTile,
                    ability,
                    reusableCandidateTargets);
                for (int targetIndex = 0; targetIndex < candidateTargets.Count; targetIndex++)
                {
                    TacticsCharacterController primaryTarget = candidateTargets[targetIndex];
                    if (primaryTarget == null || !primaryTarget.isActiveAndEnabled || !primaryTarget.IsAlive)
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

                    if (ability.AppliesThrowing)
                    {
                        IReadOnlyList<Vector2Int> throwDestinations = combatSystem.GetValidThrowDestinationsFromTile(
                            character,
                            sourceTile,
                            primaryTarget,
                            ability,
                            reusableThrowDestinationTiles);
                        for (int throwIndex = 0; throwIndex < throwDestinations.Count; throwIndex++)
                        {
                            Vector2Int throwDestination = throwDestinations[throwIndex];
                            if (!TryScoreAbilityPlan(
                                    ability,
                                    primaryTarget,
                                    sourceTile,
                                    payment,
                                    requiresMovement,
                                    affectedTargets,
                                    throwDestination,
                                    out float planScore))
                            {
                                continue;
                            }

                            if (!foundPlan || planScore > bestPlan.Score)
                            {
                                foundPlan = true;
                                bestPlan = new EnemyAbilityPlan(
                                    ability,
                                    primaryTarget,
                                    sourceTile,
                                    primaryTarget.GridPosition,
                                    throwDestination,
                                    moveDestination,
                                    payment,
                                    requiresMovement,
                                    planScore);
                            }
                        }

                        continue;
                    }

                    if (!TryScoreAbilityPlan(
                            ability,
                            primaryTarget,
                            sourceTile,
                            payment,
                            requiresMovement,
                            affectedTargets,
                            null,
                            out float score))
                    {
                        continue;
                    }

                    if (!foundPlan || score > bestPlan.Score)
                    {
                        foundPlan = true;
                        bestPlan = new EnemyAbilityPlan(
                            ability,
                            primaryTarget,
                            sourceTile,
                            primaryTarget.GridPosition,
                            null,
                            moveDestination,
                            payment,
                            requiresMovement,
                            score);
                    }
                }
            }
        }
    }

    private bool TryScoreAbilityPlan(
        TacticsAbilityDefinition ability,
        TacticsCharacterController target,
        Vector2Int sourceTile,
        TacticsAbilityCostPayment costPayment,
        bool requiresMovement,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        Vector2Int? throwDestination,
        out float score)
    {
        score = 0f;
        if (ability == null || target == null || affectedTargets == null || affectedTargets.Count == 0)
        {
            return false;
        }

        bool movementAvailable = !requiresMovement && character != null && character.HasMovementAvailableForAbilityCost;
        bool isSupportAbility = IsSupportAbility(ability);
        float bestAlternativeOffenseScore = isSupportAbility
            ? GetCachedBestOffensiveAbilityOpportunityScore(sourceTile, movementAvailable)
            : 0f;
        float tacticalValue = EvaluateAbilityTacticalValue(ability, sourceTile, affectedTargets, target, throwDestination, bestAlternativeOffenseScore);
        if (isSupportAbility && tacticalValue <= 0f)
        {
            return false;
        }

        score = ScoreAbilityPlan(
            ability,
            target,
            sourceTile,
            costPayment,
            requiresMovement,
            affectedTargets,
            tacticalValue,
            bestAlternativeOffenseScore,
            includeSupportCommitmentAdjustment: true);
        return score > 0f;
    }

    private float ScoreAbilityPlan(
        TacticsAbilityDefinition ability,
        TacticsCharacterController target,
        Vector2Int sourceTile,
        TacticsAbilityCostPayment costPayment,
        bool requiresMovement,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        float tacticalValue,
        float bestAlternativeOffenseScore,
        bool includeSupportCommitmentAdjustment)
    {
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
        float selfBleedActionPenalty = GetTriggeredSelfStatusDamage(TacticsStatusEffectTrigger.ActionPerformed) *
                                       bleedActionPenaltyPerDamage *
                                       GetSelfStatusDamagePenaltyScale() *
                                       GetBleedActionPenaltyScale();
        float supportCommitmentAdjustment = includeSupportCommitmentAdjustment
            ? TacticsEnemyUtilityAbilityScorer.ScoreSupportCommitment(
                character,
                ability,
                costPayment,
                affectedTargets,
                bestAlternativeOffenseScore,
                GetNearbyThreatCount(character))
            : 0f;

        return tacticalValue + coordinationBonus + rangeBias + selfPreservationBias + teamIntentBonus + supportCommitmentAdjustment - movementPenalty - distancePenalty - costPenalty - selfBleedActionPenalty;
    }

    private float EvaluateAbilityTacticalValue(
        TacticsAbilityDefinition ability,
        Vector2Int sourceTile,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        TacticsCharacterController primaryTarget = null,
        Vector2Int? throwDestination = null,
        float bestAlternativeOffenseScore = 0f)
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

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            totalValue += ScoreStatusEffect(ability, sourceTile, statusEffects[i], affectedTargets, bestAlternativeOffenseScore);
        }

        totalValue += ScoreKnockbackEffect(ability, sourceTile, affectedTargets);
        totalValue += ScoreThrowEffect(ability, sourceTile, primaryTarget, throwDestination);

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

    private float ScoreStatusEffect(
        TacticsAbilityDefinition ability,
        Vector2Int sourceTile,
        TacticsApplyStatusEffectData statusEffect,
        IReadOnlyList<TacticsCharacterController> affectedTargets,
        float bestAlternativeOffenseScore)
    {
        TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffect.StatusEffectType);
        float totalValue = 0f;

        for (int i = 0; i < affectedTargets.Count; i++)
        {
            TacticsCharacterController target = affectedTargets[i];
            if (target == null || !target.IsAlive)
            {
                continue;
            }

            float averagePotency = GetAverageStatusPotency(ability, statusEffect, target);
            switch (statusEffect.StatusEffectType)
            {
                case TacticsStatusEffectType.Cleanse:
                {
                    if (target.Team != character.Team)
                    {
                        continue;
                    }

                    int missingHitPoints = target.MaxHitPoints - target.CurrentHitPoints;
                    float totalHealingWindow = averagePotency * statusEffect.DurationTurns;
                    float effectiveHealing = missingHitPoints > 0
                        ? Mathf.Min(missingHitPoints + averagePotency * Mathf.Max(0, statusEffect.DurationTurns - 1), totalHealingWindow)
                        : totalHealingWindow * 0.4f;
                    float pressureBonus = GetNearbyThreatCount(target) * 2.5f;
                    float urgencyBonus = (1f - GetHealthRatio(target)) * 12f;
                    float selfBonus = ReferenceEquals(target, character) ? 4f : 0f;
                    totalValue += effectiveHealing + pressureBonus + urgencyBonus + selfBonus;
                    break;
                }

                case TacticsStatusEffectType.Stun:
                {
                    if (target.Team == character.Team)
                    {
                        continue;
                    }

                    bool targetAlreadyStunned = target.HasStatusEffect(TacticsStatusEffectType.Stun);
                    float denialValue = 18f * statusEffect.DurationTurns;
                    float offensiveSuppression = GetTargetOffensivePotential(target) * 0.65f;
                    float focusFireBonus = CountAlliedPressureOnTarget(target) * 2.5f;
                    float refreshBonus = targetAlreadyStunned ? 2f : 6f;

                    if (targetAlreadyStunned && HasAlternativeAbilityChoice(ability))
                    {
                        denialValue *= 0.05f;
                        offensiveSuppression *= 0.05f;
                        focusFireBonus *= 0.2f;
                        refreshBonus = 0.5f;
                    }

                    totalValue += denialValue + offensiveSuppression + focusFireBonus + refreshBonus;
                    break;
                }

                case TacticsStatusEffectType.Taunt:
                {
                    if (target.Team != character.Team)
                    {
                        continue;
                    }

                    bool isSelfTarget = ReferenceEquals(target, character);
                    Vector2Int tauntTile = ReferenceEquals(target, character) ? sourceTile : target.GridPosition;
                    int immediateThreats = CountImmediateThreatsAtTile(tauntTile);
                    int protectedAllies = CountProtectedAlliesAtTile(target, tauntTile);
                    int endangeredProtectedAllies = CountProtectedAlliesByHealthThresholdAtTile(target, tauntTile, tauntEndangeredHealthThreshold);
                    int criticalProtectedAllies = CountProtectedAlliesByHealthThresholdAtTile(target, tauntTile, tauntCriticalHealthThreshold);
                    float allyProtectionValue = ScoreTauntProtectionWindow(target, tauntTile);
                    float laneControlValue = ScoreTauntLaneControl(tauntTile);
                    bool isFrontliner = GetPreferredCombatDistance(target) <= 1.5f && !IsSupportUnit(target);
                    TacticsEnemyTauntEvaluationContext tauntContext = new(
                        bestAlternativeOffenseScore,
                        immediateThreats,
                        protectedAllies,
                        endangeredProtectedAllies,
                        criticalProtectedAllies,
                        CountNearbyAlliedTaunters(target, tauntTile),
                        GetRemainingTauntTurns(target),
                        GetHighestNearbyAlliedTauntCoverageTurns(target, tauntTile),
                        allyProtectionValue,
                        laneControlValue,
                        GetHealthRatio(target),
                        GetTargetOffensivePotential(target),
                        GetAlliedDamagePressure(),
                        target.HasStatusEffect(TacticsStatusEffectType.Taunt),
                        isSelfTarget,
                        isFrontliner);
                    totalValue += TacticsEnemyTauntAbilityScorer.Score(tauntContext);
                    break;
                }

                case TacticsStatusEffectType.Bleed:
                {
                    if (target.Team == character.Team)
                    {
                        continue;
                    }

                    float offensivePotential = GetTargetOffensivePotential(target);
                    float bleedValue = TacticsStatusEffectLibrary.EvaluateStrategicValue(
                        statusEffect.StatusEffectType,
                        averagePotency,
                        statusEffect.DurationTurns,
                        target,
                        offensivePotential);
                    float focusFireBonus = CountAlliedPressureOnTarget(target) * 1.5f;
                    float applyBonus = target.HasStatusEffect(TacticsStatusEffectType.Bleed) ? 1.5f : 5f;
                    totalValue += bleedValue + focusFireBonus + applyBonus;
                    break;
                }

                case TacticsStatusEffectType.Poison:
                {
                    if (target.Team == character.Team)
                    {
                        continue;
                    }

                    float offensivePotential = GetTargetOffensivePotential(target);
                    float poisonValue = TacticsStatusEffectLibrary.EvaluateStrategicValue(
                        statusEffect.StatusEffectType,
                        averagePotency,
                        statusEffect.DurationTurns,
                        target,
                        offensivePotential);
                    float focusFireBonus = CountAlliedPressureOnTarget(target) * 1.25f;
                    float tankPressureBonus = Mathf.Clamp(target.MaxHitPoints * 0.04f, 1f, 8f);
                    float applyBonus = target.HasStatusEffect(TacticsStatusEffectType.Poison) ? 1.25f : 5.5f;
                    totalValue += poisonValue + focusFireBonus + tankPressureBonus + applyBonus;
                    break;
                }

                case TacticsStatusEffectType.Fire:
                {
                    if (target.Team == character.Team)
                    {
                        continue;
                    }

                    float offensivePotential = GetTargetOffensivePotential(target);
                    float fireValue = TacticsStatusEffectLibrary.EvaluateStrategicValue(
                        statusEffect.StatusEffectType,
                        averagePotency,
                        statusEffect.DurationTurns,
                        target,
                        offensivePotential);
                    float focusFireBonus = CountAlliedPressureOnTarget(target) * 1.4f;
                    float existingFirePotency = TacticsStatusEffectLibrary.GetActivePotency(target, TacticsStatusEffectType.Fire);
                    float stackBonus = existingFirePotency > 0f
                        ? Mathf.Clamp(existingFirePotency * 0.4f, 2f, 10f)
                        : 0f;
                    float applyBonus = existingFirePotency > 0f ? 6.75f : 4.75f;
                    totalValue += fireValue + focusFireBonus + stackBonus + applyBonus;
                    break;
                }

                case TacticsStatusEffectType.StatBuff:
                {
                    if (target.Team != character.Team)
                    {
                        continue;
                    }

                    float buffValue = TacticsStatusEffectLibrary.EvaluateStrategicValue(
                        statusEffect,
                        averagePotency,
                        statusEffect.DurationTurns,
                        target,
                        GetTargetOffensivePotential(target));
                    float selfBonus = ReferenceEquals(target, character) ? 3.5f : 1.5f;
                    float urgencyBonus = GetNearbyThreatCount(target) * 1.35f;
                    totalValue += buffValue + selfBonus + urgencyBonus;
                    break;
                }

                case TacticsStatusEffectType.StatDebuff:
                {
                    if (target.Team == character.Team)
                    {
                        continue;
                    }

                    float debuffValue = TacticsStatusEffectLibrary.EvaluateStrategicValue(
                        statusEffect,
                        averagePotency,
                        statusEffect.DurationTurns,
                        target,
                        GetTargetOffensivePotential(target));
                    float focusFireBonus = CountAlliedPressureOnTarget(target) * 1.2f;
                    float applyBonus = 4.25f;
                    totalValue += debuffValue + focusFireBonus + applyBonus;
                    break;
                }
            }
        }

        if (!descriptor.IsBuff && ability != null && ability.UsesAreaOfEffect)
        {
            totalValue += ability.AreaOfEffectSize * 1.5f;
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

    private float GetAverageStatusPotency(
        TacticsAbilityDefinition ability,
        TacticsApplyStatusEffectData statusEffect,
        TacticsCharacterController target = null)
    {
        return TacticsAbilityEffectMath.EvaluateStatusPotency(character, target, ability, statusEffect, useAverageRoll: true);
    }

    private float ScoreKnockbackEffect(
        TacticsAbilityDefinition ability,
        Vector2Int sourceTile,
        IReadOnlyList<TacticsCharacterController> affectedTargets)
    {
        if (ability == null ||
            !ability.AppliesKnockback ||
            combatSystem == null ||
            character == null ||
            affectedTargets == null)
        {
            return 0f;
        }

        float totalValue = 0f;
        for (int i = 0; i < affectedTargets.Count; i++)
        {
            TacticsCharacterController target = affectedTargets[i];
            if (target == null ||
                !target.IsAlive ||
                !combatSystem.TryGetKnockbackDestination(character, sourceTile, target, ability, out Vector2Int destination))
            {
                continue;
            }

            int movedTiles = GetTileDistance(target.GridPosition, destination);
            if (movedTiles <= 0)
            {
                continue;
            }

            float beforeDistance = GetTileDistance(sourceTile, target.GridPosition);
            float afterDistance = GetTileDistance(sourceTile, destination);
            float preferredDistance = GetPreferredCombatDistance(target);
            float preferenceDisruption = Mathf.Abs(afterDistance - preferredDistance) - Mathf.Abs(beforeDistance - preferredDistance);
            float spacingChange = afterDistance - beforeDistance;
            float offensiveSuppression = GetTargetOffensivePotential(target) * 0.06f * movedTiles;
            float teamBias = target.Team == character.Team ? -8f : 2f;
            totalValue += (movedTiles * 4f) +
                          (preferenceDisruption * 5f) +
                          (spacingChange * 2.5f) +
                          offensiveSuppression +
                          teamBias;
        }

        return totalValue;
    }

    private float ScoreThrowEffect(
        TacticsAbilityDefinition ability,
        Vector2Int sourceTile,
        TacticsCharacterController target,
        Vector2Int? throwDestination)
    {
        if (ability == null ||
            !ability.AppliesThrowing ||
            character == null ||
            target == null ||
            !target.IsAlive ||
            !throwDestination.HasValue)
        {
            return 0f;
        }

        Vector2Int destination = throwDestination.Value;
        int movedTiles = GetTileDistance(target.GridPosition, destination);
        if (movedTiles <= 0)
        {
            return 0f;
        }

        float beforeDistance = GetTileDistance(sourceTile, target.GridPosition);
        float afterDistance = GetTileDistance(sourceTile, destination);
        float preferredDistance = GetPreferredCombatDistance(target);
        float preferenceDisruption = Mathf.Abs(afterDistance - preferredDistance) - Mathf.Abs(beforeDistance - preferredDistance);
        float targetIsolation = strategicContext != null
            ? strategicContext.ScoreFocusFire(sourceTile, target) * 0.3f
            : 0f;
        float offensiveSuppression = GetTargetOffensivePotential(target) * 0.08f * movedTiles;
        float rangeControl = ability.UsesAbilityRange ? Mathf.Max(0f, ability.Range - afterDistance) * 1.25f : 0f;

        return (movedTiles * 5.5f) +
               (preferenceDisruption * 6f) +
               offensiveSuppression +
               targetIsolation +
               rangeControl;
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

            IReadOnlyList<TacticsCharacterController> candidateTargets = combatSystem.GetPrimaryTargetCandidatesFromTile(
                character,
                sourceTile,
                ability,
                reusableCandidateTargets);
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

                if (ability.AppliesThrowing)
                {
                    IReadOnlyList<Vector2Int> throwDestinations = combatSystem.GetValidThrowDestinationsFromTile(
                        character,
                        sourceTile,
                        primaryTarget,
                        ability,
                        reusableThrowDestinationTiles);
                    for (int throwIndex = 0; throwIndex < throwDestinations.Count; throwIndex++)
                    {
                        Vector2Int throwDestination = throwDestinations[throwIndex];
                        float throwTacticalValue = EvaluateAbilityTacticalValue(ability, sourceTile, affectedTargets, primaryTarget, throwDestination);
                        if (throwTacticalValue <= 0f)
                        {
                            continue;
                        }

                        float throwScore = ScoreAbilityPlan(
                            ability,
                            primaryTarget,
                            sourceTile,
                            payment,
                            requiresMovement: !movementAvailable,
                            affectedTargets,
                            throwTacticalValue,
                            bestAlternativeOffenseScore: 0f,
                            includeSupportCommitmentAdjustment: false);
                        if (!foundPlan || throwScore > bestPlan.Score)
                        {
                            foundPlan = true;
                            bestPlan = new EnemyAbilityPlan(
                                ability,
                                primaryTarget,
                                sourceTile,
                                primaryTarget.GridPosition,
                                throwDestination,
                                sourceTile,
                                payment,
                                requiresMovement: !movementAvailable,
                                throwScore);
                        }
                    }

                    continue;
                }

                float tacticalValue = EvaluateAbilityTacticalValue(ability, sourceTile, affectedTargets, primaryTarget);
                if (tacticalValue <= 0f)
                {
                    continue;
                }

                float score = ScoreAbilityPlan(
                    ability,
                    primaryTarget,
                    sourceTile,
                    payment,
                    requiresMovement: !movementAvailable,
                    affectedTargets,
                    tacticalValue,
                    bestAlternativeOffenseScore: 0f,
                    includeSupportCommitmentAdjustment: false);
                if (!foundPlan || score > bestPlan.Score)
                {
                    foundPlan = true;
                    bestPlan = new EnemyAbilityPlan(
                        ability,
                        primaryTarget,
                        sourceTile,
                        primaryTarget.GridPosition,
                        null,
                        sourceTile,
                        payment,
                        requiresMovement: !movementAvailable,
                        score);
                }
            }
        }

        return foundPlan ? bestPlan.Score : 0f;
    }

    private float GetBestOffensiveAbilityOpportunityScore(Vector2Int sourceTile, bool movementAvailable)
    {
        using (BestOffenseOpportunityMarker.Auto())
        {
            if (character == null || combatSystem == null)
            {
                return 0f;
            }

            float bestScore = 0f;
            IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
            if (abilities == null)
            {
                return 0f;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                TacticsAbilityDefinition ability = abilities[i];
                if (ability == null ||
                    IsSupportAbility(ability) ||
                    !character.TryGetAbilityCostPayment(ability, movementAvailable, out TacticsAbilityCostPayment payment))
                {
                    continue;
                }

                IReadOnlyList<TacticsCharacterController> candidateTargets = combatSystem.GetPrimaryTargetCandidatesFromTile(
                    character,
                    sourceTile,
                    ability,
                    reusableCandidateTargets);
                for (int targetIndex = 0; targetIndex < candidateTargets.Count; targetIndex++)
                {
                    TacticsCharacterController primaryTarget = candidateTargets[targetIndex];
                    if (primaryTarget == null ||
                        !primaryTarget.isActiveAndEnabled ||
                        !primaryTarget.IsAlive)
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

                    if (ability.AppliesThrowing)
                    {
                        IReadOnlyList<Vector2Int> throwDestinations = combatSystem.GetValidThrowDestinationsFromTile(
                            character,
                            sourceTile,
                            primaryTarget,
                            ability,
                            reusableThrowDestinationTiles);
                        for (int throwIndex = 0; throwIndex < throwDestinations.Count; throwIndex++)
                        {
                            Vector2Int throwDestination = throwDestinations[throwIndex];
                            float throwTacticalValue = EvaluateAbilityTacticalValue(ability, sourceTile, affectedTargets, primaryTarget, throwDestination);
                            if (throwTacticalValue <= 0f)
                            {
                                continue;
                            }

                            float throwScore = ScoreAbilityPlan(
                                ability,
                                primaryTarget,
                                sourceTile,
                                payment,
                                requiresMovement: !movementAvailable,
                                affectedTargets,
                                throwTacticalValue,
                                bestAlternativeOffenseScore: 0f,
                                includeSupportCommitmentAdjustment: false);
                            bestScore = Mathf.Max(bestScore, throwScore);
                        }

                        continue;
                    }

                    float tacticalValue = EvaluateAbilityTacticalValue(ability, sourceTile, affectedTargets, primaryTarget);
                    if (tacticalValue <= 0f)
                    {
                        continue;
                    }

                    float score = ScoreAbilityPlan(
                        ability,
                        primaryTarget,
                        sourceTile,
                        payment,
                        requiresMovement: !movementAvailable,
                        affectedTargets,
                        tacticalValue,
                        bestAlternativeOffenseScore: 0f,
                        includeSupportCommitmentAdjustment: false);
                    bestScore = Mathf.Max(bestScore, score);
                }
            }

            return bestScore;
        }
    }

    private float GetCachedBestOffensiveAbilityOpportunityScore(Vector2Int sourceTile, bool movementAvailable)
    {
        Dictionary<Vector2Int, float> cache = movementAvailable
            ? cachedOffenseScoreWithMovementByTile
            : cachedOffenseScoreWithoutMovementByTile;

        if (cache.TryGetValue(sourceTile, out float cachedScore))
        {
            return cachedScore;
        }

        float score = GetBestOffensiveAbilityOpportunityScore(sourceTile, movementAvailable);
        cache[sourceTile] = score;
        return score;
    }

    private float ScoreMovementTile(Vector2Int sourceTile, TacticsCharacterController anchorTarget, int pathIndex)
    {
        float immediateOpportunityScore = GetBestAbilityOpportunityScore(sourceTile, movementAvailable: false);
        float anchorDistance = anchorTarget != null ? GetTileDistance(sourceTile, anchorTarget.GridPosition) : 0f;
        float hostilePressure = ScorePressurePosition(sourceTile);
        float supportPressure = ScoreSupportPosition(sourceTile);
        float tauntResponseWeight = HasTauntAbility() ? GetTauntResponseWeight(sourceTile) : 0f;
        float tauntSetupScore = HasTauntAbility() ? ScoreTauntLaneControl(sourceTile) * tauntResponseWeight * 0.45f : 0f;
        float tauntProtectionScore = HasTauntAbility() ? ScoreTauntProtectionWindow(character, sourceTile) * tauntResponseWeight * 0.35f : 0f;
        float tauntCounterScore = ScoreCounterTauntPosition(sourceTile);
        float teamCoordination = strategicContext != null
            ? strategicContext.ScoreSupportPosition(sourceTile, HasHealingAbility(), HasResourceRestoreAbility()) +
              strategicContext.ScoreFormation(null, sourceTile, anchorTarget) +
              (anchorTarget != null ? strategicContext.ScoreFocusFire(sourceTile, anchorTarget) : 0f)
            : 0f;
        float selfBleedMovementPenalty = GetTriggeredSelfStatusDamage(TacticsStatusEffectTrigger.TileMoved) *
                                         bleedMovementPenaltyPerDamage *
                                         pathIndex *
                                         GetSelfStatusDamagePenaltyScale() *
                                         GetBleedMovementPenaltyScale();
        float bleedSupportBias = supportPressure * GetBleedSupportMovementBias();
        return immediateOpportunityScore +
               (hostilePressure * 1.35f) +
               (supportPressure * 0.75f) +
               bleedSupportBias +
               tauntSetupScore +
               tauntProtectionScore +
               tauntCounterScore +
               teamCoordination -
               (anchorDistance * 0.2f) -
               (pathIndex * 0.05f) -
               selfBleedMovementPenalty;
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

    private static float GetPreferredCombatDistance(IReadOnlyList<TacticsAbilityDefinition> abilities)
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

        if (cachedPathsByTarget.TryGetValue(target, out path))
        {
            return path != null && path.Count > 1;
        }

        bool foundPath = character.TryGetPathTo(target.GridPosition, out path, enforceMoveRange: false);
        cachedPathsByTarget[target] = foundPath ? path : null;
        return foundPath;
    }

    private List<TacticsCharacterController> BuildMovementAnchorTargets()
    {
        EnsureTurnTargetCache();
        reusableAnchorTargets.Clear();
        AddTargetsIfMissing(reusableAnchorTargets, GetTauntingHostileTargets());
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

    private List<TacticsCharacterController> GetTauntingHostileTargets()
    {
        EnsureTurnTargetCache();
        reusableCandidateTargets.Clear();
        List<TacticsCharacterController> hostiles = GetHostileTargets();
        for (int i = 0; i < hostiles.Count; i++)
        {
            TacticsCharacterController hostile = hostiles[i];
            if (hostile != null && hostile.IsTaunting)
            {
                reusableCandidateTargets.Add(hostile);
            }
        }

        return reusableCandidateTargets;
    }

    private List<TacticsCharacterController> GetHostileTargets()
    {
        EnsureTurnTargetCache();
        return reusableHostileTargets;
    }

    private List<TacticsCharacterController> GetAlliedTargets(bool includeSelf)
    {
        EnsureTurnTargetCache();
        return includeSelf
            ? reusableAlliedTargetsIncludingSelf
            : reusableAlliedTargets;
    }

    private void PrepareTurnEvaluationCaches()
    {
        ClearTurnEvaluationCaches();
        EnsureTurnTargetCache();
    }

    private void EnsureTurnTargetCache()
    {
        if (turnTargetCacheInitialized)
        {
            return;
        }

        reusableHostileTargets.Clear();
        reusableAlliedTargets.Clear();
        reusableAlliedTargetsIncludingSelf.Clear();

        if (characterRegistry != null)
        {
            characterRegistry.GetHostileCharacters(character, reusableHostileTargets);
            characterRegistry.GetAlliedCharacters(character, includeSelf: false, reusableAlliedTargets);
            characterRegistry.GetAlliedCharacters(character, includeSelf: true, reusableAlliedTargetsIncludingSelf);
        }

        turnTargetCacheInitialized = true;
    }

    private void ClearTurnEvaluationCaches()
    {
        cachedPathsByTarget.Clear();
        cachedOffenseScoreWithMovementByTile.Clear();
        cachedOffenseScoreWithoutMovementByTile.Clear();
        reusableHostileTargets.Clear();
        reusableAlliedTargets.Clear();
        reusableAlliedTargetsIncludingSelf.Clear();
        reusableAnchorTargets.Clear();
        reusableCandidateTargets.Clear();
        turnTargetCacheInitialized = false;
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

    private bool HasTauntAbility()
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = character != null ? character.Abilities : null;
        if (abilities == null)
        {
            return false;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            if (IsTauntAbility(abilities[i]))
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

    private static bool IsBeneficialStatusAbility(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return false;
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

    private static bool IsTauntAbility(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return false;
        }

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffects[i].StatusEffectType == TacticsStatusEffectType.Taunt)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAlternativeAbilityChoice(TacticsAbilityDefinition currentAbility)
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = character != null ? character.Abilities : null;
        if (abilities == null)
        {
            return false;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability != null && !ReferenceEquals(ability, currentAbility))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportAbility(TacticsAbilityDefinition ability)
    {
        return IsHealingAbility(ability) || IsResourceRestoreAbility(ability) || IsBeneficialStatusAbility(ability);
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
            if (IsSupportAbility(abilities[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static float GetPreferredCombatDistance(TacticsCharacterController unit)
    {
        IReadOnlyList<TacticsAbilityDefinition> abilities = unit != null ? unit.Abilities : null;
        return abilities == null || abilities.Count == 0
            ? 1f
            : GetPreferredCombatDistance(abilities);
    }

    private float GetTargetOffensivePotential(TacticsCharacterController unit)
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

            IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
            for (int effectIndex = 0; effectIndex < statusEffects.Count; effectIndex++)
            {
                TacticsApplyStatusEffectData effect = statusEffects[effectIndex];
                if (!TacticsStatusEffectLibrary.GetDescriptor(effect.StatusEffectType).IsBuff)
                {
                    abilityValue += 12f * effect.DurationTurns;
                }
            }

            bestValue = Mathf.Max(bestValue, abilityValue);
        }

        return bestValue;
    }

    private int CountAlliedPressureOnTarget(TacticsCharacterController target)
    {
        if (target == null)
        {
            return 0;
        }

        int count = 0;
        List<TacticsCharacterController> alliedTargets = GetAlliedTargets(includeSelf: true);
        for (int i = 0; i < alliedTargets.Count; i++)
        {
            TacticsCharacterController ally = alliedTargets[i];
            if (ally == null)
            {
                continue;
            }

            if (GetTileDistance(ally.GridPosition, target.GridPosition) <= Mathf.Max(1, ally.MoveRange + 1))
            {
                count++;
            }
        }

        return count;
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

    private float GetAlliedDamagePressure()
    {
        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: true);
        if (allies.Count == 0)
        {
            return 0f;
        }

        float totalPressure = 0f;
        int evaluatedAllies = 0;
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || !ally.IsAlive)
            {
                continue;
            }

            float missingHealthRatio = 1f - GetHealthRatio(ally);
            float threatMultiplier = GetNearbyThreatCount(ally) > 0 ? 1.35f : 0.8f;
            totalPressure += missingHealthRatio * threatMultiplier;
            evaluatedAllies++;
        }

        if (evaluatedAllies == 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(totalPressure / evaluatedAllies);
    }

    private float GetTauntResponseWeight(Vector2Int sourceTile)
    {
        float teamDamagePressure = GetAlliedDamagePressure();
        float tileThreatPressure = Mathf.Clamp01(CountImmediateThreatsAtTile(sourceTile) / 3f);
        return Mathf.Clamp01((teamDamagePressure * 0.7f) + (tileThreatPressure * 0.6f));
    }

    private float ScoreTauntProtectionWindow(TacticsCharacterController taunter, Vector2Int tauntTile)
    {
        if (taunter == null || taunter.Team != character.Team)
        {
            return 0f;
        }

        float protectionValue = 0f;
        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: true);
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || ReferenceEquals(ally, taunter))
            {
                continue;
            }

            if (GetTileDistance(ally.GridPosition, tauntTile) > 3)
            {
                continue;
            }

            int coverableThreats = CountThreatsCoverableFromTile(tauntTile, ally);
            if (coverableThreats <= 0)
            {
                continue;
            }

            float allyValue = ScoreProtectedAllyValue(ally);
            protectionValue += 4f + (coverableThreats * 3.5f) + allyValue;
        }

        return protectionValue;
    }

    private float ScoreTauntLaneControl(Vector2Int tauntTile)
    {
        return CountImmediateThreatsAtTile(tauntTile) * 1.5f;
    }

    private float ScoreCounterTauntPosition(Vector2Int sourceTile)
    {
        List<TacticsCharacterController> tauntingHostiles = GetTauntingHostileTargets();
        if (tauntingHostiles.Count == 0)
        {
            return 0f;
        }

        List<TacticsCharacterController> hostiles = GetHostileTargets();
        float bestScore = 0f;
        for (int i = 0; i < hostiles.Count; i++)
        {
            TacticsCharacterController target = hostiles[i];
            if (target == null || target.IsTaunting)
            {
                continue;
            }

            float targetValue = ScoreProtectedAllyValue(target);
            float targetDistance = GetTileDistance(sourceTile, target.GridPosition);
            float nearestTaunterDistance = float.MaxValue;
            for (int tauntIndex = 0; tauntIndex < tauntingHostiles.Count; tauntIndex++)
            {
                TacticsCharacterController taunter = tauntingHostiles[tauntIndex];
                nearestTaunterDistance = Mathf.Min(nearestTaunterDistance, GetTileDistance(sourceTile, taunter.GridPosition));
            }

            float flankAdvantage = nearestTaunterDistance - targetDistance;
            float score = targetValue - (targetDistance * 0.35f) + (flankAdvantage * 1.75f);
            bestScore = Mathf.Max(bestScore, score);
        }

        return Mathf.Max(0f, bestScore);
    }

    private int CountThreatsCoverableFromTile(Vector2Int tauntTile, TacticsCharacterController ally)
    {
        if (ally == null)
        {
            return 0;
        }

        int count = 0;
        List<TacticsCharacterController> hostiles = GetHostileTargets();
        for (int i = 0; i < hostiles.Count; i++)
        {
            TacticsCharacterController hostile = hostiles[i];
            if (hostile == null)
            {
                continue;
            }

            if (CanUnitPressureTile(hostile, ally.GridPosition) &&
                CanUnitPressureTile(hostile, tauntTile))
            {
                count++;
            }
        }

        return count;
    }

    private int CountImmediateThreatsAtTile(Vector2Int tile)
    {
        int count = 0;
        List<TacticsCharacterController> hostiles = GetHostileTargets();
        for (int i = 0; i < hostiles.Count; i++)
        {
            TacticsCharacterController hostile = hostiles[i];
            if (hostile != null && CanUnitPressureTile(hostile, tile))
            {
                count++;
            }
        }

        return count;
    }

    private int CountProtectedAlliesAtTile(TacticsCharacterController taunter, Vector2Int tauntTile)
    {
        int count = 0;
        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: true);
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || ReferenceEquals(ally, taunter))
            {
                continue;
            }

            if (GetTileDistance(ally.GridPosition, tauntTile) > 3)
            {
                continue;
            }

            if (CountThreatsCoverableFromTile(tauntTile, ally) > 0)
            {
                count++;
            }
        }

        return count;
    }

    private int CountProtectedAlliesByHealthThresholdAtTile(
        TacticsCharacterController taunter,
        Vector2Int tauntTile,
        float maxHealthRatio)
    {
        if (maxHealthRatio <= 0f)
        {
            return 0;
        }

        int count = 0;
        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: true);
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || ReferenceEquals(ally, taunter))
            {
                continue;
            }

            if (GetTileDistance(ally.GridPosition, tauntTile) > 3)
            {
                continue;
            }

            if (GetHealthRatio(ally) > maxHealthRatio)
            {
                continue;
            }

            if (CountThreatsCoverableFromTile(tauntTile, ally) > 0)
            {
                count++;
            }
        }

        return count;
    }

    private int CountNearbyAlliedTaunters(TacticsCharacterController taunter, Vector2Int tauntTile)
    {
        int count = 0;
        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: true);
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || ReferenceEquals(ally, taunter) || !ally.IsTaunting)
            {
                continue;
            }

            if (GetTileDistance(ally.GridPosition, tauntTile) <= 3)
            {
                count++;
            }
        }

        return count;
    }

    private int GetHighestNearbyAlliedTauntCoverageTurns(TacticsCharacterController taunter, Vector2Int tauntTile)
    {
        int bestRemainingTurns = 0;
        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: true);
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || ReferenceEquals(ally, taunter) || !ally.IsTaunting)
            {
                continue;
            }

            if (GetTileDistance(ally.GridPosition, tauntTile) > 3)
            {
                continue;
            }

            if (CountProtectedAlliesAtTile(ally, ally.GridPosition) <= 0)
            {
                continue;
            }

            bestRemainingTurns = Mathf.Max(bestRemainingTurns, GetRemainingTauntTurns(ally));
        }

        return bestRemainingTurns;
    }

    private float ScoreProtectedAllyValue(TacticsCharacterController ally)
    {
        if (ally == null)
        {
            return 0f;
        }

        float roleValue = IsSupportUnit(ally) ? 9f : (GetPreferredCombatDistance(ally) > 1.5f ? 6f : 2.5f);
        float offenseValue = GetTargetOffensivePotential(ally) * 0.16f;
        float fragilityValue = (1f - GetHealthRatio(ally)) * 8f;
        return roleValue + offenseValue + fragilityValue;
    }

    private static int GetRemainingTauntTurns(TacticsCharacterController unit)
    {
        return unit != null
            ? unit.GetStatusEffectRemainingTurns(TacticsStatusEffectType.Taunt)
            : 0;
    }

    private static bool CanUnitPressureTile(TacticsCharacterController unit, Vector2Int tile)
    {
        if (unit == null)
        {
            return false;
        }

        int reach = Mathf.Max(1, Mathf.RoundToInt(GetPreferredCombatDistance(unit)));
        return GetTileDistance(unit.GridPosition, tile) <= reach;
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
        return TryUseAbility(ability, targetTile, null);
    }

    private bool TryUseAbility(TacticsAbilityDefinition ability, Vector2Int targetTile, Vector2Int? throwDestination)
    {
        if (character == null || ability == null)
        {
            return false;
        }

        if (coopSessionCoordinator != null)
        {
            return coopSessionCoordinator.RequestUseAbility(character, ability, targetTile, throwDestination);
        }

        return combatSystem != null && combatSystem.TryUseAbility(character, ability, targetTile, throwDestination);
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

    private float GetTriggeredSelfStatusDamage(TacticsStatusEffectTrigger trigger)
    {
        if (character == null || character.ActiveStatusEffects == null)
        {
            return 0f;
        }

        float totalDamage = 0f;
        IReadOnlyList<TacticsStatusEffectInstance> activeEffects = character.ActiveStatusEffects;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            totalDamage += TacticsStatusEffectLibrary.GetTriggeredDamage(activeEffects[i], trigger);
        }

        return totalDamage;
    }

    private float GetSelfStatusDamagePenaltyScale()
    {
        if (character == null || character.MaxHitPoints <= 0)
        {
            return 1f;
        }

        return character.CurrentHitPoints <= Mathf.CeilToInt(character.MaxHitPoints * 0.35f)
            ? 1.6f
            : 1f;
    }

    private EnemyBleedResponseMode ResolveBleedResponseMode()
    {
        float movementDamage = GetTriggeredSelfStatusDamage(TacticsStatusEffectTrigger.TileMoved);
        float actionDamage = GetTriggeredSelfStatusDamage(TacticsStatusEffectTrigger.ActionPerformed);
        if (movementDamage <= 0f && actionDamage <= 0f)
        {
            return EnemyBleedResponseMode.FightThrough;
        }

        float healthRatio = GetHealthRatio(character);
        bool hasSupportiveAlly = HasSupportiveAlly();
        float ignoreWeight = Mathf.Max(0f, bleedIgnoreWeight + (healthRatio >= 0.75f ? 0.2f : 0f));
        float fightWeight = Mathf.Max(0.05f, bleedFightThroughWeight + (healthRatio >= 0.5f ? 0.25f : 0f));
        float repositionWeight = Mathf.Max(0f, bleedRepositionWeight + (healthRatio < 0.65f ? 0.2f : 0f));
        float seekSupportWeight = Mathf.Max(0f, hasSupportiveAlly ? bleedSeekSupportWeight : bleedSeekSupportWeight * 0.2f);
        if (hasSupportiveAlly && healthRatio <= bleedLowHealthSeekThreshold)
        {
            seekSupportWeight += bleedLowHealthSeekBonusWeight;
        }

        float totalWeight = ignoreWeight + fightWeight + repositionWeight + seekSupportWeight;
        if (totalWeight <= 0f)
        {
            return EnemyBleedResponseMode.FightThrough;
        }

        float roll = Random.value * totalWeight;
        if (roll < ignoreWeight)
        {
            return EnemyBleedResponseMode.Ignore;
        }

        roll -= ignoreWeight;
        if (roll < fightWeight)
        {
            return EnemyBleedResponseMode.FightThrough;
        }

        roll -= fightWeight;
        if (roll < repositionWeight)
        {
            return EnemyBleedResponseMode.Reposition;
        }

        return EnemyBleedResponseMode.SeekSupport;
    }

    private float GetBleedMovementPenaltyScale()
    {
        return currentBleedResponseMode switch
        {
            EnemyBleedResponseMode.Ignore => 0f,
            EnemyBleedResponseMode.FightThrough => 0.2f,
            EnemyBleedResponseMode.Reposition => 0.08f,
            EnemyBleedResponseMode.SeekSupport => 0.03f,
            _ => 0.2f
        };
    }

    private float GetBleedActionPenaltyScale()
    {
        return currentBleedResponseMode switch
        {
            EnemyBleedResponseMode.Ignore => 0f,
            EnemyBleedResponseMode.FightThrough => 0.18f,
            EnemyBleedResponseMode.Reposition => 0.3f,
            EnemyBleedResponseMode.SeekSupport => 0.24f,
            _ => 0.18f
        };
    }

    private float GetBleedSupportMovementBias()
    {
        return currentBleedResponseMode switch
        {
            EnemyBleedResponseMode.SeekSupport => bleedSeekSupportMovementBonus,
            EnemyBleedResponseMode.Reposition => bleedSeekSupportMovementBonus * 0.35f,
            _ => 0f
        };
    }

    private bool HasSupportiveAlly()
    {
        List<TacticsCharacterController> allies = GetAlliedTargets(includeSelf: false);
        for (int i = 0; i < allies.Count; i++)
        {
            TacticsCharacterController ally = allies[i];
            if (ally == null || !ally.IsAlive)
            {
                continue;
            }

            IReadOnlyList<TacticsAbilityDefinition> allyAbilities = ally.Abilities;
            for (int abilityIndex = 0; abilityIndex < allyAbilities.Count; abilityIndex++)
            {
                TacticsAbilityDefinition ability = allyAbilities[abilityIndex];
                if (ability != null && IsSupportAbility(ability))
                {
                    return true;
                }
            }
        }

        return false;
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
            score += hostile.IsTaunting ? 22f : 0f;
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

        IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            value += GetStatusStrategicValue(unit, ability, statusEffects[i]);
        }

        if (ability.UsesAreaOfEffect)
        {
            value += ability.AreaOfEffectSize * 0.5f;
        }

        value += GetKnockbackStrategicValue(ability);

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

            IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
            for (int effectIndex = 0; effectIndex < statusEffects.Count; effectIndex++)
            {
                TacticsApplyStatusEffectData effect = statusEffects[effectIndex];
                if (!TacticsStatusEffectLibrary.GetDescriptor(effect.StatusEffectType).IsBuff)
                {
                    abilityValue += GetStatusStrategicValue(unit, ability, effect);
                }
            }

            abilityValue += GetKnockbackStrategicValue(ability);

            bestValue = Mathf.Max(bestValue, abilityValue);
        }

        return bestValue;
    }

    private float GetStatusStrategicValue(
        TacticsCharacterController unit,
        TacticsAbilityDefinition ability,
        TacticsApplyStatusEffectData statusEffect)
    {
        TacticsStatusEffectDescriptor descriptor = TacticsStatusEffectLibrary.GetDescriptor(statusEffect.StatusEffectType);
        float potency = TacticsAbilityEffectMath.EvaluateStatusPotency(unit, unit, ability, statusEffect, useAverageRoll: true);
        return statusEffect.StatusEffectType switch
        {
            TacticsStatusEffectType.Cleanse => (potency * statusEffect.DurationTurns * 0.8f) + 6f,
            TacticsStatusEffectType.Stun => (16f * statusEffect.DurationTurns) + GetUnitBaseThreat(unit),
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

    private static float GetKnockbackStrategicValue(TacticsAbilityDefinition ability)
    {
        return ability != null && ability.AppliesKnockback
            ? ability.Knockback.DistanceInTiles * 4.5f
            : 0f;
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
                TacticsAbilityEffectDefinitionData effect = effects[effectIndex];
                if (effect.EffectKind is TacticsAbilityEffectKind.RestoreHitPoints or TacticsAbilityEffectKind.RestoreResource)
                {
                    return true;
                }
            }

            IReadOnlyList<TacticsApplyStatusEffectData> statusEffects = ability.StatusEffects;
            for (int effectIndex = 0; effectIndex < statusEffects.Count; effectIndex++)
            {
                if (TacticsStatusEffectLibrary.GetDescriptor(statusEffects[effectIndex].StatusEffectType).IsBuff)
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
