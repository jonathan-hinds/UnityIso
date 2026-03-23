using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TacticsEnemyTable", menuName = "Tactics/Enemies/Enemy Table")]
public sealed class TacticsEnemyTable : ScriptableObject
{
    [SerializeField] private List<TacticsEnemyTableEntry> enemies = new();

    private Dictionary<string, TacticsEnemyTableEntry> entriesById;

    public IReadOnlyList<TacticsEnemyTableEntry> Enemies => enemies;

    public bool TryGetCharacterData(string enemyId, out TacticsCharacterData characterData)
    {
        characterData = null;
        if (!TryGetEntry(enemyId, out TacticsEnemyTableEntry entry))
        {
            return false;
        }

        characterData = entry.CreateCharacterData();
        return characterData != null;
    }

    public bool TryGetEntry(string enemyId, out TacticsEnemyTableEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            return false;
        }

        EnsureLookup();
        return entriesById.TryGetValue(enemyId.Trim(), out entry) && entry != null;
    }

    private void OnValidate()
    {
        enemies ??= new List<TacticsEnemyTableEntry>();
        for (int i = 0; i < enemies.Count; i++)
        {
            enemies[i]?.Sanitize();
        }

        entriesById = null;
    }

    private void EnsureLookup()
    {
        if (entriesById != null)
        {
            return;
        }

        entriesById = new Dictionary<string, TacticsEnemyTableEntry>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < enemies.Count; i++)
        {
            TacticsEnemyTableEntry entry = enemies[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyId))
            {
                continue;
            }

            entry.Sanitize();
            entriesById[entry.EnemyId] = entry;
        }
    }
}

[Serializable]
public sealed class TacticsEnemyTableEntry
{
    [SerializeField] private string enemyId = "enemy";
    [SerializeField] private string displayName = "Enemy";
    [SerializeField] private string spriteSheetResourcePath = "Characters/guy3";
    [SerializeField, Min(0.01f)] private float walkFramesPerSecond = 5f;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.58f, 0.58f, 1f);
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.2f;
    [SerializeField, Min(0.01f)] private float jumpDuration = 0.5f;
    [SerializeField, Min(0f)] private float jumpArcHeight = 0.5f;
    [SerializeField, Min(0)] private int maxStepUp = 1;
    [SerializeField, Min(0)] private int maxStepDown = 1;
    [SerializeField] private Vector2 tileAnchorOffset = new Vector2(0.18f, 0.17f);
    [SerializeField] private TacticsCharacterStats baseStats = TacticsCharacterStats.Default();
    [SerializeField] private List<TacticsAbilityDefinition> startingAbilities = new();
    [SerializeField, Min(0)] private int minExperienceReward = 8;
    [SerializeField, Min(0)] private int maxExperienceReward = 12;

    public string EnemyId => string.IsNullOrWhiteSpace(enemyId) ? "enemy" : enemyId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EnemyId : displayName.Trim();
    public string SpriteSheetResourcePath => spriteSheetResourcePath;
    public float WalkFramesPerSecond => Mathf.Max(0.01f, walkFramesPerSecond);
    public Color BaseColor => baseColor;
    public Color SelectedColor => selectedColor;
    public float MoveSpeed => Mathf.Max(0.1f, moveSpeed);
    public float JumpDuration => Mathf.Max(0.01f, jumpDuration);
    public float JumpArcHeight => Mathf.Max(0f, jumpArcHeight);
    public int MaxStepUp => Mathf.Max(0, maxStepUp);
    public int MaxStepDown => Mathf.Max(0, maxStepDown);
    public Vector2 TileAnchorOffset => tileAnchorOffset;
    public TacticsCharacterStats BaseStats => baseStats;
    public IReadOnlyList<TacticsAbilityDefinition> StartingAbilities => startingAbilities;
    public int MinExperienceReward => Mathf.Max(0, minExperienceReward);
    public int MaxExperienceReward => Mathf.Max(MinExperienceReward, maxExperienceReward);

    public TacticsCharacterData CreateCharacterData()
    {
        return TacticsCharacterData.FromEnemyTableEntry(this);
    }

    public void Sanitize()
    {
        enemyId = string.IsNullOrWhiteSpace(enemyId) ? "enemy" : enemyId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? enemyId : displayName.Trim();
        walkFramesPerSecond = Mathf.Max(0.01f, walkFramesPerSecond);
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        jumpDuration = Mathf.Max(0.01f, jumpDuration);
        jumpArcHeight = Mathf.Max(0f, jumpArcHeight);
        maxStepUp = Mathf.Max(0, maxStepUp);
        maxStepDown = Mathf.Max(0, maxStepDown);
        minExperienceReward = Mathf.Max(0, minExperienceReward);
        maxExperienceReward = Mathf.Max(minExperienceReward, maxExperienceReward);
        startingAbilities ??= new List<TacticsAbilityDefinition>();
    }
}
