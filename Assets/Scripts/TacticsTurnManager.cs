using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class TacticsTurnManager : MonoBehaviour
{
    [SerializeField] private TacticsTurnCameraDirector cameraDirector;
    [SerializeField, Min(0f)] private float nonPlayerAutoEndDelay = 0.75f;

    private readonly List<ITacticsTurnParticipant> participants = new();
    private Coroutine turnTransitionRoutine;
    private int activeTurnIndex = -1;

    public event Action<ITacticsTurnParticipant> ActiveParticipantChanged;
    public event Action TurnStateChanged;

    public ITacticsTurnParticipant ActiveParticipant { get; private set; }
    public TacticsCharacterController ActiveCharacter => ActiveParticipant as TacticsCharacterController;
    public bool IsTransitioningTurns => turnTransitionRoutine != null;
    public int ParticipantCount => participants.Count;
    public int RoundNumber { get; private set; }
    public int TurnNumber => activeTurnIndex >= 0 ? activeTurnIndex + 1 : 0;

    private void Awake()
    {
        if (cameraDirector == null)
        {
            cameraDirector = FindFirstObjectByType<TacticsTurnCameraDirector>();
        }
    }

    private void Start()
    {
        RefreshParticipantsAndStartBattle();
    }

    public void RegisterParticipant(ITacticsTurnParticipant participant)
    {
        if (participant == null || participants.Contains(participant))
        {
            return;
        }

        participants.Add(participant);
        participants.Sort(CompareParticipants);
        participant.TurnEnded += HandleParticipantTurnEnded;
        participant.TurnStateChanged += HandleParticipantTurnStateChanged;
        NotifyTurnStateChanged();
    }

    public void UnregisterParticipant(ITacticsTurnParticipant participant)
    {
        if (participant == null)
        {
            return;
        }

        participant.TurnEnded -= HandleParticipantTurnEnded;
        participant.TurnStateChanged -= HandleParticipantTurnStateChanged;

        int removedIndex = participants.IndexOf(participant);
        if (removedIndex < 0)
        {
            return;
        }

        participants.RemoveAt(removedIndex);

        if (removedIndex < activeTurnIndex)
        {
            activeTurnIndex--;
        }

        if (ReferenceEquals(ActiveParticipant, participant))
        {
            ActiveParticipant = null;
            activeTurnIndex = Mathf.Clamp(activeTurnIndex - 1, -1, participants.Count - 1);

            if (isActiveAndEnabled && participants.Count > 0)
            {
                StartTurnTransition();
            }
        }

        NotifyTurnStateChanged();
    }

    public void RefreshParticipantsAndStartBattle()
    {
        if (cameraDirector == null)
        {
            cameraDirector = FindFirstObjectByType<TacticsTurnCameraDirector>();
        }

        TacticsCharacterController[] discoveredCharacters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < discoveredCharacters.Length; i++)
        {
            RegisterParticipant(discoveredCharacters[i]);
        }

        if (ActiveParticipant == null && !IsTransitioningTurns && participants.Count > 0)
        {
            StartTurnTransition();
        }
    }

    public bool TryEndActiveTurn()
    {
        if (ActiveParticipant == null)
        {
            return false;
        }

        return ActiveParticipant.TryEndTurn();
    }

    private void HandleParticipantTurnEnded(ITacticsTurnParticipant participant)
    {
        if (!ReferenceEquals(ActiveParticipant, participant))
        {
            return;
        }

        StartTurnTransition();
    }

    private void HandleParticipantTurnStateChanged(ITacticsTurnParticipant participant)
    {
        if (ReferenceEquals(ActiveParticipant, participant))
        {
            NotifyTurnStateChanged();
        }
    }

    private void StartTurnTransition()
    {
        if (!isActiveAndEnabled || turnTransitionRoutine != null)
        {
            return;
        }

        turnTransitionRoutine = StartCoroutine(AdvanceTurnRoutine());
    }

    private IEnumerator AdvanceTurnRoutine()
    {
        CleanupParticipants();

        if (participants.Count == 0)
        {
            ActiveParticipant = null;
            activeTurnIndex = -1;
            turnTransitionRoutine = null;
            NotifyTurnStateChanged();
            yield break;
        }

        ITacticsTurnParticipant nextParticipant = GetNextEligibleParticipant();
        if (nextParticipant == null)
        {
            ActiveParticipant = null;
            activeTurnIndex = -1;
            turnTransitionRoutine = null;
            NotifyTurnStateChanged();
            yield break;
        }

        ActiveParticipant = nextParticipant;
        ActiveParticipant.BeginTurn();
        ActiveParticipantChanged?.Invoke(ActiveParticipant);
        NotifyTurnStateChanged();

        if (cameraDirector != null)
        {
            yield return cameraDirector.FocusOnWorldPoint(ActiveParticipant.TurnFocusPoint);
        }

        bool shouldAutoEnd = !ActiveParticipant.IsPlayerControlled && ActiveParticipant.CanEndTurn;
        ITacticsTurnParticipant participantToAutoEnd = ActiveParticipant;

        turnTransitionRoutine = null;
        NotifyTurnStateChanged();

        if (shouldAutoEnd)
        {
            StartCoroutine(AutoEndTurnRoutine(participantToAutoEnd));
        }
    }

    private ITacticsTurnParticipant GetNextEligibleParticipant()
    {
        int participantCount = participants.Count;
        for (int attempt = 0; attempt < participantCount; attempt++)
        {
            activeTurnIndex = (activeTurnIndex + 1 + participantCount) % participantCount;
            if (activeTurnIndex == 0)
            {
                RoundNumber = Mathf.Max(1, RoundNumber + 1);
            }

            ITacticsTurnParticipant candidate = participants[activeTurnIndex];
            if (candidate != null && candidate.IsTurnEligible)
            {
                return candidate;
            }
        }

        return null;
    }

    private void CleanupParticipants()
    {
        for (int i = participants.Count - 1; i >= 0; i--)
        {
            ITacticsTurnParticipant participant = participants[i];
            if (participant != null && participant.IsTurnEligible)
            {
                continue;
            }

            if (participant != null)
            {
                participant.TurnEnded -= HandleParticipantTurnEnded;
                participant.TurnStateChanged -= HandleParticipantTurnStateChanged;
            }

            participants.RemoveAt(i);
        }

        participants.Sort(CompareParticipants);

        if (activeTurnIndex >= participants.Count)
        {
            activeTurnIndex = participants.Count - 1;
        }
    }

    private void NotifyTurnStateChanged()
    {
        TurnStateChanged?.Invoke();
    }

    private IEnumerator AutoEndTurnRoutine(ITacticsTurnParticipant participant)
    {
        yield return new WaitForSecondsRealtime(nonPlayerAutoEndDelay);

        if (ReferenceEquals(ActiveParticipant, participant))
        {
            participant.TryEndTurn();
        }
    }

    private static int CompareParticipants(ITacticsTurnParticipant left, ITacticsTurnParticipant right)
    {
        MonoBehaviour leftBehaviour = left as MonoBehaviour;
        MonoBehaviour rightBehaviour = right as MonoBehaviour;

        if (leftBehaviour == null || rightBehaviour == null)
        {
            return 0;
        }

        return leftBehaviour.GetInstanceID().CompareTo(rightBehaviour.GetInstanceID());
    }
}
