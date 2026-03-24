using System;
using System.Collections.Generic;
using UnityEngine;

public interface ITacticsCharacterProgressionStore
{
    bool TryLoad(out TacticsCharacterProgressionCollectionSaveData saveData);
    void Save(TacticsCharacterProgressionCollectionSaveData saveData);
}

[Serializable]
public sealed class TacticsCharacterProgressionCollectionSaveData
{
    public List<TacticsCharacterProgressionSaveData> characters = new();
}

public sealed class TacticsPlayerPrefsCharacterProgressionStore : ITacticsCharacterProgressionStore
{
    private const string SaveKey = "tactics.character.progression";

    public bool TryLoad(out TacticsCharacterProgressionCollectionSaveData saveData)
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

        saveData = JsonUtility.FromJson<TacticsCharacterProgressionCollectionSaveData>(json);
        return saveData != null;
    }

    public void Save(TacticsCharacterProgressionCollectionSaveData saveData)
    {
        string json = JsonUtility.ToJson(saveData ?? new TacticsCharacterProgressionCollectionSaveData());
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }
}

public sealed class TacticsCharacterProgressionService
{
    private readonly ITacticsCharacterProgressionStore store;
    private readonly Dictionary<string, TacticsCharacterProgressionSnapshot> cachedProgressionByCharacterId =
        new(StringComparer.OrdinalIgnoreCase);
    private bool hasLoadedCache;

    public TacticsCharacterProgressionService(ITacticsCharacterProgressionStore store = null)
    {
        this.store = store ?? new TacticsPlayerPrefsCharacterProgressionStore();
    }

    public event Action<TacticsCharacterProgressionSnapshot> ProgressionSaved;

    public TacticsCharacterProgressionSnapshot GetProgression(string characterId)
    {
        string normalizedCharacterId = NormalizeCharacterId(characterId);
        if (string.IsNullOrEmpty(normalizedCharacterId))
        {
            return TacticsCharacterProgressionSnapshot.CreateDefault(string.Empty);
        }

        EnsureCacheLoaded();
        if (!cachedProgressionByCharacterId.TryGetValue(normalizedCharacterId, out TacticsCharacterProgressionSnapshot snapshot))
        {
            snapshot = TacticsCharacterProgressionSnapshot.CreateDefault(normalizedCharacterId);
            cachedProgressionByCharacterId[normalizedCharacterId] = snapshot;
        }

        return snapshot.Sanitize();
    }

    public TacticsCharacterProgressionSnapshot GetProgression(TacticsCharacterDefinition definition)
    {
        return definition == null
            ? TacticsCharacterProgressionSnapshot.CreateDefault(string.Empty)
            : GetProgression(definition.CharacterId);
    }

    public void SaveProgression(TacticsCharacterProgressionSnapshot snapshot)
    {
        TacticsCharacterProgressionSnapshot sanitized = snapshot.Sanitize();
        if (string.IsNullOrEmpty(sanitized.CharacterId))
        {
            return;
        }

        EnsureCacheLoaded();
        cachedProgressionByCharacterId[sanitized.CharacterId] = sanitized;
        PersistCache();
        ProgressionSaved?.Invoke(sanitized);
    }

    public IReadOnlyList<TacticsCharacterProgressionSnapshot> GetProgressions(IEnumerable<string> characterIds)
    {
        List<TacticsCharacterProgressionSnapshot> snapshots = new();
        if (characterIds == null)
        {
            return snapshots;
        }

        foreach (string characterId in characterIds)
        {
            string normalizedCharacterId = NormalizeCharacterId(characterId);
            if (string.IsNullOrEmpty(normalizedCharacterId))
            {
                continue;
            }

            snapshots.Add(GetProgression(normalizedCharacterId));
        }

        return snapshots;
    }

    private void EnsureCacheLoaded()
    {
        if (hasLoadedCache)
        {
            return;
        }

        hasLoadedCache = true;
        cachedProgressionByCharacterId.Clear();

        if (!store.TryLoad(out TacticsCharacterProgressionCollectionSaveData saveData) ||
            saveData?.characters == null)
        {
            return;
        }

        for (int i = 0; i < saveData.characters.Count; i++)
        {
            TacticsCharacterProgressionSaveData entry = saveData.characters[i];
            if (entry == null)
            {
                continue;
            }

            TacticsCharacterProgressionSnapshot snapshot = entry.ToSnapshot();
            if (string.IsNullOrEmpty(snapshot.CharacterId))
            {
                continue;
            }

            cachedProgressionByCharacterId[snapshot.CharacterId] = snapshot;
        }
    }

    private void PersistCache()
    {
        TacticsCharacterProgressionCollectionSaveData payload = new TacticsCharacterProgressionCollectionSaveData();
        foreach (KeyValuePair<string, TacticsCharacterProgressionSnapshot> pair in cachedProgressionByCharacterId)
        {
            payload.characters.Add(TacticsCharacterProgressionSaveData.FromSnapshot(pair.Value));
        }

        store.Save(payload);
    }

    private static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }
}
