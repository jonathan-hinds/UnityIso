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
    [SerializeField, Min(0f)] private float thinkDelay = 0.2f;
    [SerializeField, Min(0f)] private float endTurnDelay = 0.15f;

    private Coroutine turnRoutine;

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
        if (TryBuildBestAbilityPlan(out EnemyAbilityPlan abilityPlan))
        {
            if (abilityPlan.RequiresMovement && character.TryMoveTo(abilityPlan.MoveDestination))
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
                attemptedAction = combatSystem.TryUseAbility(character, abilityPlan.Ability, abilityPlan.TargetTile);
            }
        }
        else if (TryBuildBestMovementPlan(out EnemyMovementPlan movementPlan))
        {
            if (character.TryMoveTo(movementPlan.Destination))
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
            character.TryEndTurn();
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
        TacticsCharacterController[] targets = GetPlayerTargets();
        if (targets.Length == 0)
        {
            return false;
        }

        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            TacticsCharacterController target = targets[targetIndex];
            if (!TryGetPathToTarget(target, out List<Vector2Int> pathToTarget))
            {
                continue;
            }

            EvaluateAbilityPlansForTile(
                abilities,
                target,
                character.GridPosition,
                character.GridPosition,
                requiresMovement: false,
                ref foundPlan,
                ref bestPlan);

            int furthestReachableIndex = Mathf.Min(character.MoveRange, pathToTarget.Count - 2);
            for (int i = 1; i <= furthestReachableIndex; i++)
            {
                Vector2Int candidateTile = pathToTarget[i];
                EvaluateAbilityPlansForTile(
                    abilities,
                    target,
                    candidateTile,
                    candidateTile,
                    requiresMovement: true,
                    ref foundPlan,
                    ref bestPlan);
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
        TacticsCharacterController[] targets = GetPlayerTargets();
        if (targets.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            TacticsCharacterController target = targets[i];
            if (!TryGetPathToTarget(target, out List<Vector2Int> pathToTarget))
            {
                continue;
            }

            int furthestReachableIndex = Mathf.Min(character.MoveRange, pathToTarget.Count - 2);
            for (int pathIndex = 1; pathIndex <= furthestReachableIndex; pathIndex++)
            {
                Vector2Int candidateTile = pathToTarget[pathIndex];

                float damageOpportunityScore = GetBestDamageOpportunityScore(candidateTile);
                float distanceToTarget = GetTileDistance(candidateTile, target.GridPosition);
                float score = damageOpportunityScore - (distanceToTarget * 0.25f) - (pathIndex * 0.05f);
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

        return combatSystem != null &&
               immediatePlan.Target != null &&
               immediatePlan.Target.isActiveAndEnabled &&
               immediatePlan.Target.IsAlive &&
               combatSystem.TryUseAbility(character, immediatePlan.Ability, immediatePlan.TargetTile);
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
        TacticsCharacterController[] targets = GetPlayerTargets();
        for (int i = 0; i < targets.Length; i++)
        {
            EvaluateAbilityPlansForTile(
                abilities,
                targets[i],
                character.GridPosition,
                character.GridPosition,
                requiresMovement: false,
                ref foundPlan,
                ref bestPlan);
        }

        return foundPlan;
    }

    private void EvaluateAbilityPlansForTile(
        IReadOnlyList<TacticsAbilityDefinition> abilities,
        TacticsCharacterController primaryTarget,
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

            float score = ScoreAbilityPlan(ability, primaryTarget, sourceTile, requiresMovement, affectedTargets.Count);
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

    private float ScoreAbilityPlan(
        TacticsAbilityDefinition ability,
        TacticsCharacterController target,
        Vector2Int sourceTile,
        bool requiresMovement,
        int affectedTargetCount)
    {
        float distanceToTarget = GetTileDistance(sourceTile, target.GridPosition);
        float preferredDistance = GetPreferredCombatDistance(ability);
        float averageDamage = GetAverageAbilityDamage(ability);
        float splashBonus = Mathf.Max(0, affectedTargetCount - 1) * 8f;
        float rangeBias = ability.UsesAbilityRange ? ability.Range * 0.35f : 0f;
        float movementPenalty = requiresMovement ? 0.75f : 0f;
        float distancePenalty = Mathf.Abs(distanceToTarget - preferredDistance) * 1.5f;

        return averageDamage + splashBonus + rangeBias - movementPenalty - distancePenalty;
    }

    private float GetAverageAbilityDamage(TacticsAbilityDefinition ability)
    {
        if (ability == null || character == null)
        {
            return 0f;
        }

        float baseDamage = ability.DamageType == TacticsAbilityDamageType.Magic
            ? (character.BaseMagicDamageMin + character.BaseMagicDamageMax) * 0.5f
            : (character.BaseMeleeDamageMin + character.BaseMeleeDamageMax) * 0.5f;

        float bonusDamage = 0f;
        IReadOnlyList<TacticsAbilityEffectDefinitionData> effects = ability.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            TacticsAbilityEffectDefinitionData effect = effects[i];
            if (effect.EffectKind != TacticsAbilityEffectKind.DealDamage)
            {
                continue;
            }

            TacticsDealDamageEffectData damage = effect.DealDamage;
            float effectBase = damage.DamageFormula == TacticsDamageFormula.FlatValue ? damage.FlatAmount : baseDamage;
            float scalingBonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(character, damage.Scaling);
            bonusDamage += Mathf.Max(0f, (effectBase + scalingBonus) * damage.BonusMultiplier);
        }

        return bonusDamage;
    }

    private float GetBestDamageOpportunityScore(Vector2Int sourceTile)
    {
        if (character == null || combatSystem == null)
        {
            return 0f;
        }

        float bestScore = 0f;
        bool foundOpportunity = false;
        IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
        TacticsCharacterController[] targets = GetPlayerTargets();
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            TacticsCharacterController target = targets[targetIndex];
            for (int abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
            {
                TacticsAbilityDefinition ability = abilities[abilityIndex];
                if (ability == null || !character.HasResourcesForAbility(ability))
                {
                    continue;
                }

                if (!combatSystem.CanTargetTileFromTile(character, sourceTile, ability, target.GridPosition))
                {
                    continue;
                }

                IReadOnlyList<TacticsCharacterController> affectedTargets = combatSystem.GetPreviewTargetsFromTile(
                    character,
                    sourceTile,
                    ability,
                    target.GridPosition);
                if (affectedTargets.Count == 0)
                {
                    continue;
                }

                float score = ScoreAbilityPlan(ability, target, sourceTile, requiresMovement: false, affectedTargetCount: affectedTargets.Count);
                if (!foundOpportunity || score > bestScore)
                {
                    foundOpportunity = true;
                    bestScore = score;
                }
            }
        }

        return foundOpportunity ? bestScore : 0f;
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

    private TacticsCharacterController[] GetPlayerTargets()
    {
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        List<TacticsCharacterController> targets = new();
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController candidate = characters[i];
            if (candidate == null || !candidate.IsPlayerControlled || !candidate.isActiveAndEnabled || !candidate.IsAlive)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets.ToArray();
    }

    private static int GetTileDistance(Vector2Int source, Vector2Int target)
    {
        return Mathf.Abs(source.x - target.x) + Mathf.Abs(source.y - target.y);
    }
}
