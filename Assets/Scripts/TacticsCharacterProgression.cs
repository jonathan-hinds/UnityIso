using System;
using UnityEngine;

[Serializable]
public struct TacticsCharacterProgressionSnapshot
{
    public const int StartingLevel = 1;
    public const int AttributePointsPerLevel = 3;

    public string characterId;
    [Min(1)] public int level;
    [Min(0)] public int currentExperience;
    [Min(0)] public int unspentAttributePoints;
    public TacticsPrimaryStats allocatedPrimaryStats;

    public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    public int Level => Mathf.Max(StartingLevel, level);
    public int CurrentExperience => Mathf.Max(0, currentExperience);
    public int UnspentAttributePoints => Mathf.Max(0, unspentAttributePoints);

    public TacticsCharacterProgressionSnapshot WithCharacterId(string value)
    {
        TacticsCharacterProgressionSnapshot updated = this;
        updated.characterId = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return updated;
    }

    public TacticsCharacterProgressionSnapshot Sanitize()
    {
        TacticsCharacterProgressionSnapshot sanitized = this;
        sanitized.characterId = CharacterId;
        sanitized.level = Level;
        sanitized.currentExperience = CurrentExperience;
        sanitized.unspentAttributePoints = UnspentAttributePoints;
        sanitized.allocatedPrimaryStats = SanitizeAllocatedStats(allocatedPrimaryStats);
        return sanitized;
    }

    public TacticsCharacterStats ApplyTo(TacticsCharacterStats baseStats)
    {
        TacticsCharacterStats effectiveStats = baseStats;
        effectiveStats.primaryStats.stamina = Mathf.Max(1, baseStats.primaryStats.stamina + Mathf.Max(0, allocatedPrimaryStats.stamina));
        effectiveStats.primaryStats.strength = Mathf.Max(1, baseStats.primaryStats.strength + Mathf.Max(0, allocatedPrimaryStats.strength));
        effectiveStats.primaryStats.agility = Mathf.Max(1, baseStats.primaryStats.agility + Mathf.Max(0, allocatedPrimaryStats.agility));
        effectiveStats.primaryStats.wisdom = Mathf.Max(1, baseStats.primaryStats.wisdom + Mathf.Max(0, allocatedPrimaryStats.wisdom));
        effectiveStats.primaryStats.intelligence = Mathf.Max(1, baseStats.primaryStats.intelligence + Mathf.Max(0, allocatedPrimaryStats.intelligence));
        return effectiveStats;
    }

    public int GetAllocatedValue(TacticsAbilityScalingStat stat)
    {
        return stat switch
        {
            TacticsAbilityScalingStat.Stamina => Mathf.Max(0, allocatedPrimaryStats.stamina),
            TacticsAbilityScalingStat.Strength => Mathf.Max(0, allocatedPrimaryStats.strength),
            TacticsAbilityScalingStat.Agility => Mathf.Max(0, allocatedPrimaryStats.agility),
            TacticsAbilityScalingStat.Wisdom => Mathf.Max(0, allocatedPrimaryStats.wisdom),
            TacticsAbilityScalingStat.Intelligence => Mathf.Max(0, allocatedPrimaryStats.intelligence),
            _ => 0
        };
    }

    public bool TryAllocatePoint(TacticsAbilityScalingStat stat, out TacticsCharacterProgressionSnapshot updated)
    {
        updated = Sanitize();
        if (updated.UnspentAttributePoints <= 0)
        {
            return false;
        }

        switch (stat)
        {
            case TacticsAbilityScalingStat.Stamina:
                updated.allocatedPrimaryStats.stamina++;
                break;

            case TacticsAbilityScalingStat.Strength:
                updated.allocatedPrimaryStats.strength++;
                break;

            case TacticsAbilityScalingStat.Agility:
                updated.allocatedPrimaryStats.agility++;
                break;

            case TacticsAbilityScalingStat.Wisdom:
                updated.allocatedPrimaryStats.wisdom++;
                break;

            case TacticsAbilityScalingStat.Intelligence:
                updated.allocatedPrimaryStats.intelligence++;
                break;

            default:
                return false;
        }

        updated.unspentAttributePoints = Mathf.Max(0, updated.unspentAttributePoints - 1);
        updated = updated.Sanitize();
        return true;
    }

    public bool TryAwardExperience(int amount, int experienceToNextLevel, out TacticsCharacterProgressionSnapshot updated, out int levelsGained)
    {
        updated = Sanitize();
        levelsGained = 0;

        if (amount <= 0 || experienceToNextLevel <= 0)
        {
            return false;
        }

        updated.currentExperience += amount;
        int threshold = Mathf.Max(1, experienceToNextLevel);
        while (updated.currentExperience >= threshold)
        {
            updated.currentExperience -= threshold;
            updated.level++;
            updated.unspentAttributePoints += AttributePointsPerLevel;
            levelsGained++;
        }

        updated = updated.Sanitize();
        return levelsGained > 0 || amount > 0;
    }

    public static TacticsCharacterProgressionSnapshot CreateDefault(string characterId)
    {
        return new TacticsCharacterProgressionSnapshot
        {
            characterId = string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim(),
            level = StartingLevel,
            currentExperience = 0,
            unspentAttributePoints = 0,
            allocatedPrimaryStats = default
        };
    }

    private static TacticsPrimaryStats SanitizeAllocatedStats(TacticsPrimaryStats value)
    {
        value.stamina = Mathf.Max(0, value.stamina);
        value.strength = Mathf.Max(0, value.strength);
        value.agility = Mathf.Max(0, value.agility);
        value.wisdom = Mathf.Max(0, value.wisdom);
        value.intelligence = Mathf.Max(0, value.intelligence);
        return value;
    }
}

[Serializable]
public sealed class TacticsCharacterProgressionSaveData
{
    public string characterId;
    public int level;
    public int currentExperience;
    public int unspentAttributePoints;
    public TacticsPrimaryStats allocatedPrimaryStats;

    public TacticsCharacterProgressionSnapshot ToSnapshot()
    {
        return new TacticsCharacterProgressionSnapshot
        {
            characterId = characterId,
            level = level,
            currentExperience = currentExperience,
            unspentAttributePoints = unspentAttributePoints,
            allocatedPrimaryStats = allocatedPrimaryStats
        }.Sanitize();
    }

    public static TacticsCharacterProgressionSaveData FromSnapshot(TacticsCharacterProgressionSnapshot snapshot)
    {
        TacticsCharacterProgressionSnapshot sanitized = snapshot.Sanitize();
        return new TacticsCharacterProgressionSaveData
        {
            characterId = sanitized.CharacterId,
            level = sanitized.Level,
            currentExperience = sanitized.CurrentExperience,
            unspentAttributePoints = sanitized.UnspentAttributePoints,
            allocatedPrimaryStats = sanitized.allocatedPrimaryStats
        };
    }
}
