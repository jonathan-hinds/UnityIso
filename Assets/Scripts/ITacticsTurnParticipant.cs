using System;
using UnityEngine;

public interface ITacticsTurnParticipant
{
    string DisplayName { get; }
    TacticsUnitTeam Team { get; }
    bool IsPlayerControlled { get; }
    bool IsTurnEligible { get; }
    bool IsTurnActive { get; }
    bool CanEndTurn { get; }
    Vector3 TurnFocusPoint { get; }

    event Action<ITacticsTurnParticipant> TurnEnded;
    event Action<ITacticsTurnParticipant> TurnStateChanged;

    void BeginTurn();
    bool TryEndTurn();
}
