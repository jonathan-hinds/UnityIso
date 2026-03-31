using System;
using System.Collections.Generic;
using UnityEngine;

public interface ITacticsCharacterInventoryStore
{
    bool TryLoad(out TacticsCharacterInventoryCollectionSaveData saveData);
    void Save(TacticsCharacterInventoryCollectionSaveData saveData);
}

[Serializable]
public sealed class TacticsCharacterInventoryCollectionSaveData
{
    public List<TacticsCharacterInventorySaveData> characters = new();
}

[Serializable]
public sealed class TacticsCharacterInventorySaveData
{
    public string characterId;
    public List<TacticsInventoryItemSaveData> items = new();
    public List<TacticsEquippedItemSaveData> equippedItems = new();

    public TacticsCharacterInventorySnapshot ToSnapshot()
    {
        return new TacticsCharacterInventorySnapshot
        {
            characterId = characterId,
            items = items != null ? new List<TacticsInventoryItemSaveData>(items.Count) : new List<TacticsInventoryItemSaveData>(),
            equippedItems = equippedItems != null ? new List<TacticsEquippedItemSaveData>(equippedItems.Count) : new List<TacticsEquippedItemSaveData>()
        }.CopyFrom(this).Sanitize();
    }

    public static TacticsCharacterInventorySaveData FromSnapshot(TacticsCharacterInventorySnapshot snapshot)
    {
        TacticsCharacterInventorySnapshot sanitized = snapshot.Sanitize();
        TacticsCharacterInventorySaveData saveData = new TacticsCharacterInventorySaveData
        {
            characterId = sanitized.CharacterId,
            items = new List<TacticsInventoryItemSaveData>(sanitized.items.Count),
            equippedItems = new List<TacticsEquippedItemSaveData>(sanitized.equippedItems.Count)
        };

        for (int i = 0; i < sanitized.items.Count; i++)
        {
            saveData.items.Add(sanitized.items[i]?.Clone());
        }

        for (int i = 0; i < sanitized.equippedItems.Count; i++)
        {
            saveData.equippedItems.Add(sanitized.equippedItems[i]?.Clone());
        }

        return saveData;
    }
}

public sealed class TacticsCharacterInventoryService
{
    private readonly ITacticsCharacterInventoryStore store;
    private readonly Dictionary<string, TacticsCharacterInventorySnapshot> cachedInventoryByCharacterId =
        new(StringComparer.OrdinalIgnoreCase);
    private bool hasLoadedCache;

    public TacticsCharacterInventoryService(ITacticsCharacterInventoryStore store = null)
    {
        this.store = store ?? new TacticsSinglePlayerCharacterInventoryStore();
    }

    public event Action<TacticsCharacterInventorySnapshot> InventorySaved;

    public TacticsCharacterInventorySnapshot GetInventory(string characterId)
    {
        string normalizedCharacterId = NormalizeCharacterId(characterId);
        if (string.IsNullOrEmpty(normalizedCharacterId))
        {
            return TacticsCharacterInventorySnapshot.CreateDefault(string.Empty);
        }

        EnsureCacheLoaded();
        if (!cachedInventoryByCharacterId.TryGetValue(normalizedCharacterId, out TacticsCharacterInventorySnapshot snapshot))
        {
            snapshot = TacticsCharacterInventorySnapshot.CreateDefault(normalizedCharacterId);
            cachedInventoryByCharacterId[normalizedCharacterId] = snapshot;
        }

        return snapshot.Sanitize();
    }

    public void SaveInventory(TacticsCharacterInventorySnapshot snapshot)
    {
        TacticsCharacterInventorySnapshot sanitized = snapshot.Sanitize();
        if (string.IsNullOrEmpty(sanitized.CharacterId))
        {
            return;
        }

        EnsureCacheLoaded();
        cachedInventoryByCharacterId[sanitized.CharacterId] = sanitized;
        PersistCache();
        InventorySaved?.Invoke(sanitized);
    }

    private void EnsureCacheLoaded()
    {
        if (hasLoadedCache)
        {
            return;
        }

        hasLoadedCache = true;
        cachedInventoryByCharacterId.Clear();

        if (!store.TryLoad(out TacticsCharacterInventoryCollectionSaveData saveData) ||
            saveData?.characters == null)
        {
            return;
        }

        for (int i = 0; i < saveData.characters.Count; i++)
        {
            TacticsCharacterInventorySaveData entry = saveData.characters[i];
            if (entry == null)
            {
                continue;
            }

            TacticsCharacterInventorySnapshot snapshot = entry.ToSnapshot();
            if (string.IsNullOrEmpty(snapshot.CharacterId))
            {
                continue;
            }

            cachedInventoryByCharacterId[snapshot.CharacterId] = snapshot;
        }
    }

    private void PersistCache()
    {
        TacticsCharacterInventoryCollectionSaveData payload = new TacticsCharacterInventoryCollectionSaveData();
        foreach (KeyValuePair<string, TacticsCharacterInventorySnapshot> pair in cachedInventoryByCharacterId)
        {
            payload.characters.Add(TacticsCharacterInventorySaveData.FromSnapshot(pair.Value));
        }

        store.Save(payload);
    }

    private static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }
}

internal static class TacticsCharacterInventorySnapshotExtensions
{
    public static TacticsCharacterInventorySnapshot CopyFrom(
        this TacticsCharacterInventorySnapshot snapshot,
        TacticsCharacterInventorySaveData saveData)
    {
        snapshot.characterId = saveData != null ? saveData.characterId : string.Empty;
        snapshot.items = new List<TacticsInventoryItemSaveData>();
        snapshot.equippedItems = new List<TacticsEquippedItemSaveData>();

        if (saveData?.items != null)
        {
            for (int i = 0; i < saveData.items.Count; i++)
            {
                snapshot.items.Add(saveData.items[i]?.Clone());
            }
        }

        if (saveData?.equippedItems != null)
        {
            for (int i = 0; i < saveData.equippedItems.Count; i++)
            {
                snapshot.equippedItems.Add(saveData.equippedItems[i]?.Clone());
            }
        }

        return snapshot;
    }
}
