using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TacticsPartySelection
{
    public const int DefaultCapacity = 3;

    private readonly string[] characterIds;

    public TacticsPartySelection(IReadOnlyList<string> characterIds, int capacity = DefaultCapacity)
    {
        int resolvedCapacity = Mathf.Max(1, capacity);
        this.characterIds = new string[resolvedCapacity];

        if (characterIds == null)
        {
            return;
        }

        HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int writeIndex = 0;
        for (int i = 0; i < characterIds.Count && writeIndex < this.characterIds.Length; i++)
        {
            string candidate = NormalizeCharacterId(characterIds[i]);
            if (string.IsNullOrEmpty(candidate) || !seenIds.Add(candidate))
            {
                continue;
            }

            this.characterIds[writeIndex] = candidate;
            writeIndex++;
        }
    }

    public int Capacity => characterIds.Length;

    public bool IsFull
    {
        get
        {
            for (int i = 0; i < characterIds.Length; i++)
            {
                if (string.IsNullOrEmpty(characterIds[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public string GetCharacterId(int slotIndex)
    {
        return IsValidSlot(slotIndex) ? characterIds[slotIndex] : string.Empty;
    }

    public bool Contains(string characterId)
    {
        return IndexOf(characterId) >= 0;
    }

    public int IndexOf(string characterId)
    {
        string normalizedId = NormalizeCharacterId(characterId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return -1;
        }

        for (int i = 0; i < characterIds.Length; i++)
        {
            if (string.Equals(characterIds[i], normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public TacticsPartySelection AssignCharacter(int slotIndex, string characterId)
    {
        if (!IsValidSlot(slotIndex))
        {
            return this;
        }

        string normalizedId = NormalizeCharacterId(characterId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return ClearSlot(slotIndex);
        }

        string[] nextIds = CloneSlots();
        for (int i = 0; i < nextIds.Length; i++)
        {
            if (!string.Equals(nextIds[i], normalizedId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            nextIds[i] = string.Empty;
        }

        nextIds[slotIndex] = normalizedId;
        return new TacticsPartySelection(nextIds, Capacity);
    }

    public TacticsPartySelection ClearSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
        {
            return this;
        }

        string[] nextIds = CloneSlots();
        nextIds[slotIndex] = string.Empty;
        return new TacticsPartySelection(nextIds, Capacity);
    }

    public List<string> ToCharacterIdList()
    {
        List<string> ids = new List<string>(characterIds.Length);
        for (int i = 0; i < characterIds.Length; i++)
        {
            ids.Add(characterIds[i] ?? string.Empty);
        }

        return ids;
    }

    public IReadOnlyList<TacticsCharacterDefinition> ResolveDefinitions(TacticsCharacterRoster roster)
    {
        List<TacticsCharacterDefinition> definitions = new List<TacticsCharacterDefinition>(Capacity);
        if (roster == null)
        {
            return definitions;
        }

        Dictionary<string, TacticsCharacterDefinition> definitionsById = roster.BuildLookupById();
        for (int i = 0; i < characterIds.Length; i++)
        {
            string id = characterIds[i];
            if (string.IsNullOrEmpty(id) || !definitionsById.TryGetValue(id, out TacticsCharacterDefinition definition) || definition == null)
            {
                continue;
            }

            definitions.Add(definition);
        }

        return definitions;
    }

    public static TacticsPartySelection CreateDefault(TacticsCharacterRoster roster, int capacity = DefaultCapacity)
    {
        if (roster == null)
        {
            return new TacticsPartySelection(Array.Empty<string>(), capacity);
        }

        List<string> defaultIds = new List<string>(Mathf.Max(1, capacity));
        IReadOnlyList<TacticsCharacterDefinition> playableCharacters = roster.PlayableCharacters;
        for (int i = 0; i < playableCharacters.Count && defaultIds.Count < Mathf.Max(1, capacity); i++)
        {
            TacticsCharacterDefinition definition = playableCharacters[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.CharacterId))
            {
                continue;
            }

            defaultIds.Add(definition.CharacterId);
        }

        return new TacticsPartySelection(defaultIds, capacity);
    }

    public static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }

    private string[] CloneSlots()
    {
        string[] clone = new string[characterIds.Length];
        Array.Copy(characterIds, clone, characterIds.Length);
        return clone;
    }

    private bool IsValidSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < characterIds.Length;
    }
}
