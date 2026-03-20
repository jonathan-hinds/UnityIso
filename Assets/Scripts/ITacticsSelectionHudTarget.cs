using UnityEngine;

public interface ITacticsSelectionHudTarget
{
    TacticsSelectionHudData BuildSelectionHudData();
}

public readonly struct TacticsSelectionHudData
{
    public TacticsSelectionHudData(
        string displayName,
        TacticsSelectionHudResourceData health,
        TacticsSelectionHudResourceData mana,
        TacticsSelectionHudResourceData stamina,
        Color accentColor)
    {
        DisplayName = displayName;
        Health = health;
        Mana = mana;
        Stamina = stamina;
        AccentColor = accentColor;
    }

    public string DisplayName { get; }
    public TacticsSelectionHudResourceData Health { get; }
    public TacticsSelectionHudResourceData Mana { get; }
    public TacticsSelectionHudResourceData Stamina { get; }
    public Color AccentColor { get; }
}

public readonly struct TacticsSelectionHudResourceData
{
    public TacticsSelectionHudResourceData(string label, int currentValue, int maxValue, Color fillColor)
    {
        Label = label;
        CurrentValue = Mathf.Max(0, currentValue);
        MaxValue = Mathf.Max(0, maxValue);
        FillColor = fillColor;
    }

    public string Label { get; }
    public int CurrentValue { get; }
    public int MaxValue { get; }
    public Color FillColor { get; }
    public float FillNormalized => MaxValue <= 0 ? 0f : Mathf.Clamp01((float)CurrentValue / MaxValue);
}
