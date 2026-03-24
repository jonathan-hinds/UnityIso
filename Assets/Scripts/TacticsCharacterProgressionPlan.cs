using System;
using UnityEngine;

public sealed class TacticsCharacterProgressionPlan
{
    private TacticsCharacterProgressionSnapshot committedSnapshot;
    private TacticsCharacterProgressionSnapshot workingSnapshot;

    public TacticsCharacterProgressionPlan(TacticsCharacterProgressionSnapshot snapshot)
    {
        committedSnapshot = snapshot.Sanitize();
        workingSnapshot = committedSnapshot;
    }

    public TacticsCharacterProgressionSnapshot CommittedSnapshot => committedSnapshot;
    public TacticsCharacterProgressionSnapshot WorkingSnapshot => workingSnapshot;
    public bool HasPendingChanges => !AreEquivalent(committedSnapshot, workingSnapshot);

    public void SyncCommittedSnapshot(TacticsCharacterProgressionSnapshot snapshot, bool preservePendingChanges)
    {
        TacticsCharacterProgressionSnapshot sanitized = snapshot.Sanitize();
        bool hadPendingChanges = HasPendingChanges;
        committedSnapshot = sanitized;
        if (!preservePendingChanges || !hadPendingChanges)
        {
            workingSnapshot = sanitized;
        }
    }

    public void Reset()
    {
        workingSnapshot = committedSnapshot;
    }

    public bool CanIncrease(TacticsAbilityScalingStat stat)
    {
        return workingSnapshot.UnspentAttributePoints > 0 && IsSupportedStat(stat);
    }

    public bool CanDecrease(TacticsAbilityScalingStat stat)
    {
        return GetPendingAllocation(stat) > 0;
    }

    public bool TryIncrease(TacticsAbilityScalingStat stat)
    {
        if (!workingSnapshot.TryAllocatePoint(stat, out TacticsCharacterProgressionSnapshot updated))
        {
            return false;
        }

        workingSnapshot = updated;
        return true;
    }

    public bool TryDecrease(TacticsAbilityScalingStat stat)
    {
        if (!CanDecrease(stat))
        {
            return false;
        }

        TacticsCharacterProgressionSnapshot updated = workingSnapshot.Sanitize();
        switch (stat)
        {
            case TacticsAbilityScalingStat.Stamina:
                updated.allocatedPrimaryStats.stamina = Mathf.Max(0, updated.allocatedPrimaryStats.stamina - 1);
                break;
            case TacticsAbilityScalingStat.Strength:
                updated.allocatedPrimaryStats.strength = Mathf.Max(0, updated.allocatedPrimaryStats.strength - 1);
                break;
            case TacticsAbilityScalingStat.Agility:
                updated.allocatedPrimaryStats.agility = Mathf.Max(0, updated.allocatedPrimaryStats.agility - 1);
                break;
            case TacticsAbilityScalingStat.Wisdom:
                updated.allocatedPrimaryStats.wisdom = Mathf.Max(0, updated.allocatedPrimaryStats.wisdom - 1);
                break;
            case TacticsAbilityScalingStat.Intelligence:
                updated.allocatedPrimaryStats.intelligence = Mathf.Max(0, updated.allocatedPrimaryStats.intelligence - 1);
                break;
            default:
                return false;
        }

        updated.unspentAttributePoints++;
        workingSnapshot = updated.Sanitize();
        return true;
    }

    public void MarkCommitted()
    {
        committedSnapshot = workingSnapshot.Sanitize();
        workingSnapshot = committedSnapshot;
    }

    public int GetCommittedAllocation(TacticsAbilityScalingStat stat)
    {
        return committedSnapshot.GetAllocatedValue(stat);
    }

    public int GetWorkingAllocation(TacticsAbilityScalingStat stat)
    {
        return workingSnapshot.GetAllocatedValue(stat);
    }

    public int GetPendingAllocation(TacticsAbilityScalingStat stat)
    {
        return Mathf.Max(0, GetWorkingAllocation(stat) - GetCommittedAllocation(stat));
    }

    private static bool IsSupportedStat(TacticsAbilityScalingStat stat)
    {
        return stat is TacticsAbilityScalingStat.Stamina
            or TacticsAbilityScalingStat.Strength
            or TacticsAbilityScalingStat.Agility
            or TacticsAbilityScalingStat.Wisdom
            or TacticsAbilityScalingStat.Intelligence;
    }

    private static bool AreEquivalent(TacticsCharacterProgressionSnapshot left, TacticsCharacterProgressionSnapshot right)
    {
        TacticsCharacterProgressionSnapshot lhs = left.Sanitize();
        TacticsCharacterProgressionSnapshot rhs = right.Sanitize();
        return string.Equals(lhs.CharacterId, rhs.CharacterId, StringComparison.OrdinalIgnoreCase) &&
               lhs.Level == rhs.Level &&
               lhs.CurrentExperience == rhs.CurrentExperience &&
               lhs.UnspentAttributePoints == rhs.UnspentAttributePoints &&
               lhs.allocatedPrimaryStats.stamina == rhs.allocatedPrimaryStats.stamina &&
               lhs.allocatedPrimaryStats.strength == rhs.allocatedPrimaryStats.strength &&
               lhs.allocatedPrimaryStats.agility == rhs.allocatedPrimaryStats.agility &&
               lhs.allocatedPrimaryStats.wisdom == rhs.allocatedPrimaryStats.wisdom &&
               lhs.allocatedPrimaryStats.intelligence == rhs.allocatedPrimaryStats.intelligence;
    }
}
