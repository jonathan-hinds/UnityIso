using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

[Serializable]
public sealed class TacticsCloudProfileMetaData
{
    public int schemaVersion = 1;
    public string playerId = string.Empty;
    public string username = string.Empty;
    public string lastUpdatedUtc = string.Empty;
}

public sealed class TacticsCloudSavePlayerProfile
{
    private const string ProfileMetaKey = "profile_meta";
    private const string CurrencyKey = "currency_wallet";
    private const string PartySelectionKey = "party_selection";
    private const string CharacterProgressionKey = "character_progression";

    private readonly object sync = new object();
    private readonly string playerId;
    private readonly string preferredUsername;
    private readonly Dictionary<string, object> pendingSaves = new Dictionary<string, object>(StringComparer.Ordinal);
    private Task saveChain = Task.CompletedTask;

    private bool isInitialized;
    private TacticsCloudProfileMetaData metaData = new TacticsCloudProfileMetaData();
    private TacticsPlayerCurrencySaveData currency = new TacticsPlayerCurrencySaveData();
    private TacticsPartySelectionSaveData partySelection = new TacticsPartySelectionSaveData();
    private TacticsCharacterProgressionCollectionSaveData characterProgression = new TacticsCharacterProgressionCollectionSaveData();

    public TacticsCloudSavePlayerProfile(string playerId, string preferredUsername)
    {
        this.playerId = string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim();
        this.preferredUsername = string.IsNullOrWhiteSpace(preferredUsername) ? string.Empty : preferredUsername.Trim();
    }

    public bool IsInitialized => isInitialized;
    public string PlayerId => metaData != null && !string.IsNullOrWhiteSpace(metaData.playerId) ? metaData.playerId : playerId;
    public string Username => metaData != null && !string.IsNullOrWhiteSpace(metaData.username) ? metaData.username : preferredUsername;

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal)
        {
            ProfileMetaKey,
            CurrencyKey,
            PartySelectionKey,
            CharacterProgressionKey
        };

        Dictionary<string, Item> items = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);
        metaData = ReadValue(items, ProfileMetaKey, new TacticsCloudProfileMetaData
        {
            playerId = playerId,
            username = preferredUsername,
            lastUpdatedUtc = DateTime.UtcNow.ToString("O")
        });
        currency = ReadValue(items, CurrencyKey, new TacticsPlayerCurrencySaveData());
        partySelection = ReadValue(items, PartySelectionKey, new TacticsPartySelectionSaveData());
        characterProgression = ReadValue(items, CharacterProgressionKey, new TacticsCharacterProgressionCollectionSaveData());

        metaData.playerId = string.IsNullOrWhiteSpace(metaData.playerId) ? playerId : metaData.playerId.Trim();
        metaData.username = string.IsNullOrWhiteSpace(metaData.username) ? preferredUsername : metaData.username.Trim();
        metaData.lastUpdatedUtc = DateTime.UtcNow.ToString("O");
        currency.gold = Mathf.Max(0, currency.gold);
        partySelection.characterIds ??= new List<string>();
        characterProgression.characters ??= new List<TacticsCharacterProgressionSaveData>();
        isInitialized = true;

        QueueSave(ProfileMetaKey, metaData);
        await FlushAsync();
    }

    public bool TryLoadCurrency(out TacticsPlayerCurrencySaveData saveData)
    {
        saveData = currency != null
            ? new TacticsPlayerCurrencySaveData { gold = Mathf.Max(0, currency.gold) }
            : null;
        return isInitialized && saveData != null;
    }

    public bool TryLoadPartySelection(out TacticsPartySelectionSaveData saveData)
    {
        saveData = null;
        if (!isInitialized || partySelection == null)
        {
            return false;
        }

        saveData = new TacticsPartySelectionSaveData
        {
            characterIds = partySelection.characterIds != null
                ? new List<string>(partySelection.characterIds)
                : new List<string>()
        };
        return true;
    }

    public bool TryLoadCharacterProgression(out TacticsCharacterProgressionCollectionSaveData saveData)
    {
        saveData = null;
        if (!isInitialized || characterProgression == null)
        {
            return false;
        }

        saveData = new TacticsCharacterProgressionCollectionSaveData
        {
            characters = characterProgression.characters != null
                ? new List<TacticsCharacterProgressionSaveData>(characterProgression.characters)
                : new List<TacticsCharacterProgressionSaveData>()
        };
        return true;
    }

    public void SaveCurrency(TacticsPlayerCurrencySaveData saveData)
    {
        currency = saveData ?? new TacticsPlayerCurrencySaveData();
        currency.gold = Mathf.Max(0, currency.gold);
        TouchMeta();
        QueueSave(CurrencyKey, currency);
        QueueSave(ProfileMetaKey, metaData);
    }

    public void SavePartySelection(TacticsPartySelectionSaveData saveData)
    {
        partySelection = saveData ?? new TacticsPartySelectionSaveData();
        partySelection.characterIds ??= new List<string>();
        TouchMeta();
        QueueSave(PartySelectionKey, partySelection);
        QueueSave(ProfileMetaKey, metaData);
    }

    public void SaveCharacterProgression(TacticsCharacterProgressionCollectionSaveData saveData)
    {
        characterProgression = saveData ?? new TacticsCharacterProgressionCollectionSaveData();
        characterProgression.characters ??= new List<TacticsCharacterProgressionSaveData>();
        TouchMeta();
        QueueSave(CharacterProgressionKey, characterProgression);
        QueueSave(ProfileMetaKey, metaData);
    }

    public Task FlushAsync()
    {
        lock (sync)
        {
            return saveChain;
        }
    }

    private void TouchMeta()
    {
        metaData ??= new TacticsCloudProfileMetaData();
        metaData.playerId = PlayerId;
        metaData.username = Username;
        metaData.lastUpdatedUtc = DateTime.UtcNow.ToString("O");
    }

    private void QueueSave(string key, object value)
    {
        if (!isInitialized)
        {
            return;
        }

        lock (sync)
        {
            pendingSaves[key] = value;
            saveChain = AwaitQueuedSaveAsync(saveChain);
        }
    }

    private async Task AwaitQueuedSaveAsync(Task previousSave)
    {
        if (previousSave != null)
        {
            await previousSave;
        }

        await FlushPendingAsync();
    }

    private async Task FlushPendingAsync()
    {
        Dictionary<string, object> batch;
        lock (sync)
        {
            if (pendingSaves.Count == 0)
            {
                return;
            }

            batch = new Dictionary<string, object>(pendingSaves, StringComparer.Ordinal);
            pendingSaves.Clear();
        }

        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(batch);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Cloud save sync failed for player '{PlayerId}': {exception.Message}");
            lock (sync)
            {
                foreach (KeyValuePair<string, object> pair in batch)
                {
                    pendingSaves[pair.Key] = pair.Value;
                }
            }
        }
    }

    private static T ReadValue<T>(Dictionary<string, Item> items, string key, T fallback)
    {
        if (items != null && items.TryGetValue(key, out Item item))
        {
            try
            {
                return item.Value.GetAs<T>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Cloud save payload '{key}' could not be read and will fall back to defaults: {exception.Message}");
            }
        }

        return fallback;
    }
}

public sealed class TacticsCloudSaveCurrencyStore : ITacticsPlayerCurrencyStore
{
    private readonly TacticsCloudSavePlayerProfile profile;

    public TacticsCloudSaveCurrencyStore(TacticsCloudSavePlayerProfile profile)
    {
        this.profile = profile;
    }

    public bool TryLoad(out TacticsPlayerCurrencySaveData saveData)
    {
        if (profile == null)
        {
            saveData = null;
            return false;
        }

        return profile.TryLoadCurrency(out saveData);
    }

    public void Save(TacticsPlayerCurrencySaveData saveData)
    {
        profile?.SaveCurrency(saveData);
    }
}

public sealed class TacticsCloudSavePartySelectionStore : ITacticsPartySelectionStore
{
    private readonly TacticsCloudSavePlayerProfile profile;

    public TacticsCloudSavePartySelectionStore(TacticsCloudSavePlayerProfile profile)
    {
        this.profile = profile;
    }

    public bool TryLoad(out TacticsPartySelectionSaveData saveData)
    {
        if (profile == null)
        {
            saveData = null;
            return false;
        }

        return profile.TryLoadPartySelection(out saveData);
    }

    public void Save(TacticsPartySelectionSaveData saveData)
    {
        profile?.SavePartySelection(saveData);
    }
}

public sealed class TacticsCloudSaveCharacterProgressionStore : ITacticsCharacterProgressionStore
{
    private readonly TacticsCloudSavePlayerProfile profile;

    public TacticsCloudSaveCharacterProgressionStore(TacticsCloudSavePlayerProfile profile)
    {
        this.profile = profile;
    }

    public bool TryLoad(out TacticsCharacterProgressionCollectionSaveData saveData)
    {
        if (profile == null)
        {
            saveData = null;
            return false;
        }

        return profile.TryLoadCharacterProgression(out saveData);
    }

    public void Save(TacticsCharacterProgressionCollectionSaveData saveData)
    {
        profile?.SaveCharacterProgression(saveData);
    }
}
