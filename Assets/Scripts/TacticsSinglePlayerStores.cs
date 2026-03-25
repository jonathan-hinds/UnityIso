using System.Collections.Generic;
using UnityEngine;

public static class TacticsLegacySaveCleanup
{
    private const string CleanupFlagKey = "tactics.save.cleanup.v2";
    private static readonly string[] LegacySaveKeys =
    {
        "tactics.character.progression",
        "tactics.player.currency",
        "tactics.party.selection"
    };

    public static void CleanupLegacyPlayerPrefs()
    {
        if (PlayerPrefs.GetInt(CleanupFlagKey, 0) == 1)
        {
            return;
        }

        for (int i = 0; i < LegacySaveKeys.Length; i++)
        {
            PlayerPrefs.DeleteKey(LegacySaveKeys[i]);
        }

        PlayerPrefs.SetInt(CleanupFlagKey, 1);
        PlayerPrefs.Save();
    }
}

public sealed class TacticsSinglePlayerCharacterProgressionStore : ITacticsCharacterProgressionStore
{
    private const string SaveKey = "tactics.singleplayer.character.progression.v2";

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

public sealed class TacticsSinglePlayerCurrencyStore : ITacticsPlayerCurrencyStore
{
    private const string SaveKey = "tactics.singleplayer.player.currency.v2";

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

public sealed class TacticsSinglePlayerPartySelectionStore : ITacticsPartySelectionStore
{
    private const string SaveKey = "tactics.singleplayer.party.selection.v2";

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
        TacticsPartySelectionSaveData payload = saveData ?? new TacticsPartySelectionSaveData
        {
            characterIds = new List<string>()
        };
        string json = JsonUtility.ToJson(payload);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }
}
