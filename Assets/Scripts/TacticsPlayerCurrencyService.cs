using System;
using UnityEngine;

public interface ITacticsPlayerCurrencyStore
{
    bool TryLoad(out TacticsPlayerCurrencySaveData saveData);
    void Save(TacticsPlayerCurrencySaveData saveData);
}

[Serializable]
public sealed class TacticsPlayerCurrencySaveData
{
    public int gold;
}

public sealed class TacticsPlayerPrefsCurrencyStore : ITacticsPlayerCurrencyStore
{
    private const string SaveKey = "tactics.player.currency";

    public bool TryLoad(out TacticsPlayerCurrencySaveData saveData)
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

        saveData = JsonUtility.FromJson<TacticsPlayerCurrencySaveData>(json);
        return saveData != null;
    }

    public void Save(TacticsPlayerCurrencySaveData saveData)
    {
        string json = JsonUtility.ToJson(saveData ?? new TacticsPlayerCurrencySaveData());
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }
}

public sealed class TacticsPlayerCurrencyService
{
    private readonly ITacticsPlayerCurrencyStore store;
    private bool hasLoaded;
    private int cachedGold;

    public TacticsPlayerCurrencyService(ITacticsPlayerCurrencyStore store = null)
    {
        this.store = store ?? new TacticsPlayerPrefsCurrencyStore();
    }

    public event Action<int> GoldChanged;

    public int Gold
    {
        get
        {
            EnsureLoaded();
            return cachedGold;
        }
    }

    public int AddGold(int amount)
    {
        if (amount <= 0)
        {
            return Gold;
        }

        EnsureLoaded();
        cachedGold = Mathf.Max(0, cachedGold + amount);
        Persist();
        GoldChanged?.Invoke(cachedGold);
        return cachedGold;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        EnsureLoaded();
        if (cachedGold < amount)
        {
            return false;
        }

        cachedGold -= amount;
        Persist();
        GoldChanged?.Invoke(cachedGold);
        return true;
    }

    private void EnsureLoaded()
    {
        if (hasLoaded)
        {
            return;
        }

        hasLoaded = true;
        cachedGold = 0;

        if (!store.TryLoad(out TacticsPlayerCurrencySaveData saveData) || saveData == null)
        {
            return;
        }

        cachedGold = Mathf.Max(0, saveData.gold);
    }

    private void Persist()
    {
        store.Save(new TacticsPlayerCurrencySaveData
        {
            gold = Mathf.Max(0, cachedGold)
        });
    }
}
