using System;
using System.Collections.Generic;
using UnityEngine;

public interface ITacticsPartySelectionStore
{
    bool TryLoad(out TacticsPartySelectionSaveData saveData);
    void Save(TacticsPartySelectionSaveData saveData);
}

[Serializable]
public sealed class TacticsPartySelectionSaveData
{
    public List<string> characterIds = new();
}

public sealed class TacticsPlayerPrefsPartySelectionStore : ITacticsPartySelectionStore
{
    private const string SaveKey = "tactics.party.selection";

    public bool TryLoad(out TacticsPartySelectionSaveData saveData)
    {
        saveData = null;
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return false;
        }

        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        saveData = JsonUtility.FromJson<TacticsPartySelectionSaveData>(json);
        return saveData != null;
    }

    public void Save(TacticsPartySelectionSaveData saveData)
    {
        TacticsPartySelectionSaveData payload = saveData ?? new TacticsPartySelectionSaveData();
        string json = JsonUtility.ToJson(payload);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }
}

public sealed class TacticsPartySelectionService
{
    private const string CharacterRosterResourcePath = "Tactics/CharacterRoster";

    private readonly ITacticsPartySelectionStore store;
    private readonly int partyCapacity;
    private TacticsCharacterRoster cachedRoster;

    public TacticsPartySelectionService(ITacticsPartySelectionStore store = null, int partyCapacity = TacticsPartySelection.DefaultCapacity)
    {
        this.store = store ?? new TacticsPlayerPrefsPartySelectionStore();
        this.partyCapacity = Mathf.Max(1, partyCapacity);
    }

    public TacticsCharacterRoster LoadRoster()
    {
        cachedRoster ??= Resources.Load<TacticsCharacterRoster>(CharacterRosterResourcePath);
        return cachedRoster;
    }

    public IReadOnlyList<TacticsCharacterDefinition> LoadAvailableCharacters()
    {
        return LoadRoster()?.PlayableCharacters ?? Array.Empty<TacticsCharacterDefinition>();
    }

    public TacticsPartySelection LoadSelection()
    {
        TacticsCharacterRoster roster = LoadRoster();
        TacticsPartySelection defaultSelection = TacticsPartySelection.CreateDefault(roster, partyCapacity);

        if (!store.TryLoad(out TacticsPartySelectionSaveData saveData) ||
            saveData == null ||
            saveData.characterIds == null)
        {
            return defaultSelection;
        }

        TacticsPartySelection loadedSelection = new TacticsPartySelection(saveData.characterIds, partyCapacity);
        return SanitizeSelection(loadedSelection, roster, defaultSelection);
    }

    public void SaveSelection(TacticsPartySelection selection)
    {
        TacticsPartySelection sanitizedSelection = SanitizeSelection(
            selection ?? TacticsPartySelection.CreateDefault(LoadRoster(), partyCapacity),
            LoadRoster(),
            TacticsPartySelection.CreateDefault(LoadRoster(), partyCapacity));
        store.Save(new TacticsPartySelectionSaveData
        {
            characterIds = sanitizedSelection.ToCharacterIdList()
        });
    }

    public IReadOnlyList<TacticsCharacterDefinition> ResolveSelectedParty()
    {
        return LoadSelection().ResolveDefinitions(LoadRoster());
    }

    private TacticsPartySelection SanitizeSelection(
        TacticsPartySelection selection,
        TacticsCharacterRoster roster,
        TacticsPartySelection fallbackSelection)
    {
        if (selection == null)
        {
            return fallbackSelection;
        }

        if (roster == null)
        {
            return selection;
        }

        HashSet<string> validIds = roster.BuildCharacterIdSet();
        List<string> sanitizedIds = new List<string>(partyCapacity);
        for (int i = 0; i < selection.Capacity; i++)
        {
            string characterId = selection.GetCharacterId(i);
            if (string.IsNullOrEmpty(characterId) || !validIds.Contains(characterId))
            {
                sanitizedIds.Add(string.Empty);
                continue;
            }

            sanitizedIds.Add(characterId);
        }

        TacticsPartySelection sanitized = new TacticsPartySelection(sanitizedIds, partyCapacity);
        return sanitized.ResolveDefinitions(roster).Count > 0 ? sanitized : fallbackSelection;
    }
}
