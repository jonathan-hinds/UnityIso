using UnityEngine;

public interface ITacticsSelectionHudTarget
{
    TacticsSelectionHudData BuildSelectionHudData();
}

public readonly struct TacticsSelectionHudData
{
    public TacticsSelectionHudData(
        string displayName,
        string ownerDisplayName,
        int level,
        TacticsSelectionHudResourceData health,
        TacticsSelectionHudResourceData mana,
        TacticsSelectionHudResourceData stamina,
        TacticsSelectionHudResourceData experience,
        TacticsSelectionHudCounterData remainingActions,
        TacticsSelectionHudCounterData remainingMovement,
        Color accentColor)
    {
        DisplayName = displayName;
        OwnerDisplayName = string.IsNullOrWhiteSpace(ownerDisplayName) ? string.Empty : ownerDisplayName.Trim();
        Level = Mathf.Max(1, level);
        Health = health;
        Mana = mana;
        Stamina = stamina;
        Experience = experience;
        RemainingActions = remainingActions;
        RemainingMovement = remainingMovement;
        AccentColor = accentColor;
    }

    public string DisplayName { get; }
    public string OwnerDisplayName { get; }
    public int Level { get; }
    public TacticsSelectionHudResourceData Health { get; }
    public TacticsSelectionHudResourceData Mana { get; }
    public TacticsSelectionHudResourceData Stamina { get; }
    public TacticsSelectionHudResourceData Experience { get; }
    public TacticsSelectionHudCounterData RemainingActions { get; }
    public TacticsSelectionHudCounterData RemainingMovement { get; }
    public Color AccentColor { get; }

    public TacticsSelectionHudData WithOwnerDisplayName(string ownerDisplayName)
    {
        return new TacticsSelectionHudData(
            DisplayName,
            ownerDisplayName,
            Level,
            Health,
            Mana,
            Stamina,
            Experience,
            RemainingActions,
            RemainingMovement,
            AccentColor);
    }
}

public readonly struct TacticsSelectionHudResourceData
{
    public TacticsSelectionHudResourceData(string label, int currentValue, int maxValue, Color fillColor, bool isVisible = true)
    {
        Label = label;
        CurrentValue = Mathf.Max(0, currentValue);
        MaxValue = Mathf.Max(0, maxValue);
        FillColor = fillColor;
        IsVisible = isVisible;
    }

    public string Label { get; }
    public int CurrentValue { get; }
    public int MaxValue { get; }
    public Color FillColor { get; }
    public bool IsVisible { get; }
    public float FillNormalized => MaxValue <= 0 ? 0f : Mathf.Clamp01((float)CurrentValue / MaxValue);
    public string ValueText => $"{CurrentValue}/{MaxValue}";
}

public readonly struct TacticsSelectionHudCounterData
{
    public TacticsSelectionHudCounterData(string label, int currentValue, int maxValue, bool isVisible = true)
    {
        Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
        MaxValue = Mathf.Max(0, maxValue);
        CurrentValue = Mathf.Clamp(currentValue, 0, MaxValue);
        IsVisible = isVisible;
    }

    public string Label { get; }
    public int CurrentValue { get; }
    public int MaxValue { get; }
    public bool IsVisible { get; }
    public string ValueText => $"{CurrentValue}/{MaxValue}";
}
