using System.Collections.Generic;
using UnityEngine;

public sealed class TacticsCharacterData
{
    private readonly IReadOnlyList<TacticsAbilityDefinition> startingAbilities;

    public TacticsCharacterData(
        string characterId,
        string displayName,
        TacticsUnitTeam team,
        string spriteSheetResourcePath,
        float walkFramesPerSecond,
        Color baseColor,
        Color selectedColor,
        float moveSpeed,
        float jumpDuration,
        float jumpArcHeight,
        int maxStepUp,
        int maxStepDown,
        Vector2 tileAnchorOffset,
        Vector2Int preferredSpawnTile,
        TacticsCharacterStats baseStats,
        IReadOnlyList<TacticsAbilityDefinition> startingAbilities,
        int experienceToNextLevel,
        int minExperienceReward,
        int maxExperienceReward)
    {
        CharacterId = string.IsNullOrWhiteSpace(characterId) ? "character" : characterId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? CharacterId : displayName.Trim();
        Team = team;
        SpriteSheetResourcePath = spriteSheetResourcePath;
        WalkFramesPerSecond = Mathf.Max(0.01f, walkFramesPerSecond);
        BaseColor = baseColor;
        SelectedColor = selectedColor;
        MoveSpeed = Mathf.Max(0.1f, moveSpeed);
        JumpDuration = Mathf.Max(0.01f, jumpDuration);
        JumpArcHeight = Mathf.Max(0f, jumpArcHeight);
        MaxStepUp = Mathf.Max(0, maxStepUp);
        MaxStepDown = Mathf.Max(0, maxStepDown);
        TileAnchorOffset = tileAnchorOffset;
        PreferredSpawnTile = preferredSpawnTile;
        BaseStats = baseStats;
        this.startingAbilities = startingAbilities ?? System.Array.Empty<TacticsAbilityDefinition>();
        ExperienceToNextLevel = Mathf.Max(0, experienceToNextLevel);
        MinExperienceReward = Mathf.Max(0, minExperienceReward);
        MaxExperienceReward = Mathf.Max(MinExperienceReward, maxExperienceReward);
    }

    public string CharacterId { get; }
    public string DisplayName { get; }
    public TacticsUnitTeam Team { get; }
    public string SpriteSheetResourcePath { get; }
    public float WalkFramesPerSecond { get; }
    public Color BaseColor { get; }
    public Color SelectedColor { get; }
    public float MoveSpeed { get; }
    public float JumpDuration { get; }
    public float JumpArcHeight { get; }
    public int MaxStepUp { get; }
    public int MaxStepDown { get; }
    public Vector2 TileAnchorOffset { get; }
    public Vector2Int PreferredSpawnTile { get; }
    public TacticsCharacterStats BaseStats { get; }
    public IReadOnlyList<TacticsAbilityDefinition> StartingAbilities => startingAbilities;
    public int ExperienceToNextLevel { get; }
    public int MinExperienceReward { get; }
    public int MaxExperienceReward { get; }

    public bool TryGetOrderedSprites(out IReadOnlyList<Sprite> sprites)
    {
        return TacticsCharacterDefinition.TryLoadOrderedSprites(SpriteSheetResourcePath, out sprites);
    }

    public int RollExperienceReward()
    {
        if (MaxExperienceReward <= 0)
        {
            return 0;
        }

        return Random.Range(MinExperienceReward, MaxExperienceReward + 1);
    }

    public static TacticsCharacterData FromDefinition(TacticsCharacterDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        return new TacticsCharacterData(
            definition.CharacterId,
            definition.DisplayName,
            definition.Team,
            definition.SpriteSheetResourcePath,
            definition.WalkFramesPerSecond,
            definition.BaseColor,
            definition.SelectedColor,
            definition.MoveSpeed,
            definition.JumpDuration,
            definition.JumpArcHeight,
            definition.MaxStepUp,
            definition.MaxStepDown,
            definition.TileAnchorOffset,
            definition.PreferredSpawnTile,
            definition.BaseStats,
            definition.StartingAbilities,
            definition.ExperienceToNextLevel,
            definition.ExperienceRewardMin,
            definition.ExperienceRewardMax);
    }

    public static TacticsCharacterData FromEnemyTableEntry(TacticsEnemyTableEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        return new TacticsCharacterData(
            entry.EnemyId,
            entry.DisplayName,
            TacticsUnitTeam.Enemy,
            entry.SpriteSheetResourcePath,
            entry.WalkFramesPerSecond,
            entry.BaseColor,
            entry.SelectedColor,
            entry.MoveSpeed,
            entry.JumpDuration,
            entry.JumpArcHeight,
            entry.MaxStepUp,
            entry.MaxStepDown,
            entry.TileAnchorOffset,
            Vector2Int.zero,
            entry.BaseStats,
            entry.StartingAbilities,
            0,
            entry.MinExperienceReward,
            entry.MaxExperienceReward);
    }
}
