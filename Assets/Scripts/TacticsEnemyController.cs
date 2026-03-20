using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TacticsCharacterController))]
public sealed class TacticsEnemyController : MonoBehaviour
{
    [SerializeField] private TacticsCharacterController character;
    [SerializeField] private TacticsTurnManager turnManager;
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
    }

    private void OnEnable()
    {
        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.ActiveParticipantChanged += HandleActiveParticipantChanged;
        }
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
        }

        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
            turnRoutine = null;
        }
    }

    public void AssignTurnManager(TacticsTurnManager manager)
    {
        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
        }

        turnManager = manager;

        if (turnManager != null && isActiveAndEnabled)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.ActiveParticipantChanged += HandleActiveParticipantChanged;
        }
    }

    private void HandleActiveParticipantChanged(ITacticsTurnParticipant participant)
    {
        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
            turnRoutine = null;
        }

        if (!ReferenceEquals(participant, character) || character == null || character.IsPlayerControlled)
        {
            return;
        }

        turnRoutine = StartCoroutine(ExecuteTurnRoutine());
    }

    private IEnumerator ExecuteTurnRoutine()
    {
        while (turnManager != null && turnManager.IsTransitioningTurns)
        {
            yield return null;
        }

        if (thinkDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(thinkDelay);
        }

        if (character == null || turnManager == null || !ReferenceEquals(turnManager.ActiveParticipant, character))
        {
            turnRoutine = null;
            yield break;
        }

        if (TryGetBestMovementDestination(out Vector2Int destination))
        {
            character.TryMoveTo(destination);

            while (character != null && character.IsMoving)
            {
                yield return null;
            }
        }

        if (endTurnDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(endTurnDelay);
        }

        if (character != null && turnManager != null && ReferenceEquals(turnManager.ActiveParticipant, character))
        {
            turnManager.TryEndActiveTurn();
        }

        turnRoutine = null;
    }

    private bool TryGetBestMovementDestination(out Vector2Int destination)
    {
        destination = default;

        List<Vector2Int> shortestPath = null;
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController candidate = characters[i];
            if (candidate == null || !candidate.IsPlayerControlled || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            if (!character.TryGetPathTo(candidate.GridPosition, out List<Vector2Int> path, enforceMoveRange: false))
            {
                continue;
            }

            if (shortestPath == null || path.Count < shortestPath.Count)
            {
                shortestPath = path;
            }
        }

        if (shortestPath == null || shortestPath.Count <= 2)
        {
            return false;
        }

        int destinationIndex = Mathf.Min(character.MoveRange, shortestPath.Count - 2);
        if (destinationIndex <= 0)
        {
            return false;
        }

        destination = shortestPath[destinationIndex];
        return destination != character.GridPosition;
    }
}
