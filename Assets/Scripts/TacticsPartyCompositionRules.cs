using System;
using System.Collections.Generic;

public static class TacticsPartyCompositionRules
{
    public static int ResolveRequiredMemberCount(TacticsCharacterRoster roster, int capacity)
    {
        int availableCount = roster?.PlayableCharacters?.Count ?? 0;
        int resolvedCapacity = Math.Max(1, capacity);
        return Math.Min(resolvedCapacity, availableCount);
    }

    public static int CountAssignedMembers(TacticsPartySelection selection)
    {
        if (selection == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < selection.Capacity; i++)
        {
            if (!string.IsNullOrWhiteSpace(selection.GetCharacterId(i)))
            {
                count++;
            }
        }

        return count;
    }

    public static bool HasRequiredMembers(TacticsPartySelection selection, TacticsCharacterRoster roster, int capacity)
    {
        if (selection == null)
        {
            return false;
        }

        int requiredMemberCount = ResolveRequiredMemberCount(roster, capacity);
        return requiredMemberCount > 0 && CountValidSelectionMembers(selection, roster) >= requiredMemberCount;
    }

    public static int CountValidSelectionMembers(TacticsPartySelection selection, TacticsCharacterRoster roster)
    {
        if (selection == null)
        {
            return 0;
        }

        Dictionary<string, TacticsCharacterDefinition> definitionsById = roster?.BuildLookupById();
        if (definitionsById == null || definitionsById.Count == 0)
        {
            return CountAssignedMembers(selection);
        }

        int count = 0;
        HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < selection.Capacity; i++)
        {
            string characterId = TacticsPartySelection.NormalizeCharacterId(selection.GetCharacterId(i));
            if (string.IsNullOrEmpty(characterId) ||
                !seenIds.Add(characterId) ||
                !definitionsById.ContainsKey(characterId))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    public static bool HasRequiredMembers(IReadOnlyList<TacticsCoopCharacterLoadout> loadout, TacticsCharacterRoster roster, int capacity)
    {
        int requiredMemberCount = ResolveRequiredMemberCount(roster, capacity);
        return requiredMemberCount > 0 && CountValidLoadoutMembers(loadout, roster) >= requiredMemberCount;
    }

    public static int CountValidLoadoutMembers(IReadOnlyList<TacticsCoopCharacterLoadout> loadout, TacticsCharacterRoster roster)
    {
        return SanitizeLoadout(loadout, roster, int.MaxValue).Count;
    }

    public static List<TacticsCoopCharacterLoadout> SanitizeLoadout(
        IReadOnlyList<TacticsCoopCharacterLoadout> loadout,
        TacticsCharacterRoster roster,
        int capacity)
    {
        List<TacticsCoopCharacterLoadout> sanitized = new List<TacticsCoopCharacterLoadout>();
        if (loadout == null)
        {
            return sanitized;
        }

        int maxMembers = Math.Max(1, capacity);
        Dictionary<string, TacticsCharacterDefinition> definitionsById = roster?.BuildLookupById();
        HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < loadout.Count && sanitized.Count < maxMembers; i++)
        {
            TacticsCoopCharacterLoadout entry = loadout[i];
            string characterId = TacticsPartySelection.NormalizeCharacterId(entry?.characterId);
            if (string.IsNullOrEmpty(characterId) || !seenIds.Add(characterId))
            {
                continue;
            }

            if (definitionsById != null &&
                definitionsById.Count > 0 &&
                !definitionsById.ContainsKey(characterId))
            {
                continue;
            }

            sanitized.Add(new TacticsCoopCharacterLoadout
            {
                characterId = characterId,
                progression = entry?.progression != null
                    ? entry.progression.WithCharacterId(characterId).Sanitize()
                    : TacticsCharacterProgressionSnapshot.CreateDefault(characterId)
            });
        }

        return sanitized;
    }
}
