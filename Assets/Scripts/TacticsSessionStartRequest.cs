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
    public List<TacticsCoopCharacterLoadout> hostPartyMembers = new();
    public List<TacticsCoopCharacterLoadout> clientPartyMembers = new();
    public TacticsMatchGenerationSettings matchSettings;
}

[Serializable]
public sealed class TacticsCoopCharacterLoadout
{
    public string characterId;
    public TacticsCharacterProgressionSnapshot progression;
}
