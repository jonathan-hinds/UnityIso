using System.Collections.Generic;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "TacticsCharacterRoster", menuName = "Tactics/Characters/Character Roster")]
public sealed class TacticsCharacterRoster : ScriptableObject
{
    [SerializeField] private List<TacticsCharacterDefinition> playableCharacters = new();

    public IReadOnlyList<TacticsCharacterDefinition> PlayableCharacters => playableCharacters;

    public Dictionary<string, TacticsCharacterDefinition> BuildLookupById()
    {
        Dictionary<string, TacticsCharacterDefinition> lookup = new Dictionary<string, TacticsCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < playableCharacters.Count; i++)
        {
            TacticsCharacterDefinition definition = playableCharacters[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.CharacterId))
            {
                continue;
            }

            lookup[definition.CharacterId] = definition;
        }

        return lookup;
    }

    public HashSet<string> BuildCharacterIdSet()
    {
        return new HashSet<string>(BuildLookupById().Keys, StringComparer.OrdinalIgnoreCase);
    }
}
