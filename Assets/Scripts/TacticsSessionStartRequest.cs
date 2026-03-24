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
    public TacticsSessionStartRequest(TacticsSessionStartMode mode, string address)
    {
        Mode = mode;
        Address = string.IsNullOrWhiteSpace(address) ? string.Empty : address.Trim();
    }

    public TacticsSessionStartMode Mode { get; }
    public string Address { get; }
    public bool IsOnlineCoop => Mode is TacticsSessionStartMode.HostCoop or TacticsSessionStartMode.JoinCoop;
}

[Serializable]
public sealed class TacticsCoopBattleSetup
{
    public List<string> hostPartyCharacterIds = new();
    public List<string> clientPartyCharacterIds = new();
}
