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

        if (TryGetClosestTarget(out TacticsCharacterController target, out List<Vector2Int> pathToTarget))
        {
            if (!TryUsePrimaryAbility(target) &&
                TryGetMovementDestination(pathToTarget, out Vector2Int destination) &&
                character.TryMoveTo(destination))
            {
                while (character != null && character.IsMoving)
                {
                    yield return null;
                }

                target = FindClosestPlayerTarget();
                TryUsePrimaryAbility(target);
            }
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

    private bool TryGetClosestTarget(out TacticsCharacterController closestTarget, out List<Vector2Int> shortestPath)
    {
        closestTarget = null;
        shortestPath = null;

        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController candidate = characters[i];
            if (candidate == null || !candidate.IsPlayerControlled || !candidate.isActiveAndEnabled || !candidate.IsAlive)
            {
                continue;
            }

            if (!character.TryGetPathTo(candidate.GridPosition, out List<Vector2Int> path, enforceMoveRange: false))
            {
                continue;
            }

            if (shortestPath == null || path.Count < shortestPath.Count)
            {
                closestTarget = candidate;
                shortestPath = path;
            }
        }

        return closestTarget != null;
    }

    private TacticsCharacterController FindClosestPlayerTarget()
    {
        return TryGetClosestTarget(out TacticsCharacterController closestTarget, out _) ? closestTarget : null;
    }

    private bool TryGetMovementDestination(IReadOnlyList<Vector2Int> pathToTarget, out Vector2Int destination)
    {
        destination = default;

        if (pathToTarget == null || pathToTarget.Count <= 2)
        {
            return false;
        }

        int destinationIndex = Mathf.Min(character.MoveRange, pathToTarget.Count - 2);
        if (destinationIndex <= 0)
        {
            return false;
        }

        destination = pathToTarget[destinationIndex];
        return destination != character.GridPosition;
    }

    private bool TryUsePrimaryAbility(TacticsCharacterController target)
    {
        if (character == null || combatSystem == null || target == null)
        {
            return false;
        }

        TacticsAbilityDefinition primaryAbility = character.GetPrimaryActionAbility();
        if (primaryAbility == null)
        {
            return false;
        }

        return combatSystem.TryUseAbility(character, primaryAbility, target.GridPosition);
    }
}
