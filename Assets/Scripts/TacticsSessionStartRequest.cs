using System;
using System.Collections.Generic;

public enum TacticsSessionStartMode
{
    SinglePlayer = 0,
    HostCoop = 1,
    JoinCoop = 2
}

public readonly struct TacticsSessionStartRequest
{
    public TacticsSessionStartRequest(TacticsSessionStartMode mode, string address, TacticsMatchGenerationSettings matchSettings = null)
    {
        Mode = mode;
        Address = string.IsNullOrWhiteSpace(address) ? string.Empty : address.Trim();
        MatchSettings = matchSettings?.Clone();
    }

    public TacticsSessionStartMode Mode { get; }
    public string Address { get; }
    public TacticsMatchGenerationSettings MatchSettings { get; }
    public bool IsOnlineCoop => Mode is TacticsSessionStartMode.HostCoop or TacticsSessionStartMode.JoinCoop;
}

[Serializable]
public sealed class TacticsCoopBattleSetup
{
    public List<TacticsCoopBattlePlayer> players = new();
    public TacticsMatchGenerationSettings matchSettings;
    public int turnOrderSeed;

    public TacticsCoopBattleSetup Clone()
    {
        TacticsCoopBattleSetup clone = new TacticsCoopBattleSetup
        {
            matchSettings = matchSettings?.Clone(),
            turnOrderSeed = turnOrderSeed
        };

        for (int i = 0; i < players.Count; i++)
        {
            TacticsCoopBattlePlayer player = players[i];
            if (player != null)
            {
                clone.players.Add(player.Clone());
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class TacticsCoopBattlePlayer
{
    public ulong clientId;
    public string username;
    public bool isHost;
    public List<TacticsCoopCharacterLoadout> partyMembers = new();

    public TacticsCoopBattlePlayer Clone()
    {
        TacticsCoopBattlePlayer clone = new TacticsCoopBattlePlayer
        {
            clientId = clientId,
            username = username,
            isHost = isHost
        };

        for (int i = 0; i < partyMembers.Count; i++)
        {
            TacticsCoopCharacterLoadout member = partyMembers[i];
            if (member != null)
            {
                clone.partyMembers.Add(member.Clone());
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class TacticsCoopLobbyState
{
    public ulong hostClientId;
    public int maxPlayers;
    public int minPlayersToStart;
    public bool isMatchStarting;
    public string relayJoinCode;
    public TacticsMatchGenerationSettings matchSettings;
    public List<TacticsCoopLobbyPlayerState> players = new();

    public TacticsCoopLobbyState Clone()
    {
        TacticsCoopLobbyState clone = new TacticsCoopLobbyState
        {
            hostClientId = hostClientId,
            maxPlayers = maxPlayers,
            minPlayersToStart = minPlayersToStart,
            isMatchStarting = isMatchStarting,
            relayJoinCode = relayJoinCode,
            matchSettings = matchSettings?.Clone()
        };

        for (int i = 0; i < players.Count; i++)
        {
            TacticsCoopLobbyPlayerState player = players[i];
            if (player != null)
            {
                clone.players.Add(player.Clone());
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class TacticsCoopLobbyPlayerState
{
    public ulong clientId;
    public string username;
    public bool isHost;
    public bool isReady;
    public List<TacticsCoopCharacterLoadout> partyMembers = new();

    public TacticsCoopLobbyPlayerState Clone()
    {
        TacticsCoopLobbyPlayerState clone = new TacticsCoopLobbyPlayerState
        {
            clientId = clientId,
            username = username,
            isHost = isHost,
            isReady = isReady
        };

        for (int i = 0; i < partyMembers.Count; i++)
        {
            TacticsCoopCharacterLoadout member = partyMembers[i];
            if (member != null)
            {
                clone.partyMembers.Add(member.Clone());
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class TacticsCoopCharacterLoadout
{
    public string characterId;
    public TacticsCharacterProgressionSnapshot progression;
    public TacticsCharacterInventorySnapshot inventory;

    public TacticsCoopCharacterLoadout Clone()
    {
        return new TacticsCoopCharacterLoadout
        {
            characterId = characterId,
            progression = progression.Sanitize(),
            inventory = inventory.Sanitize()
        };
    }
}
