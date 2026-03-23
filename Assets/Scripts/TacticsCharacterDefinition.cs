using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "TacticsCharacterDefinition", menuName = "Tactics/Characters/Character Definition")]
public sealed class TacticsCharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string characterId = "character";
    [SerializeField] private string displayName = "Character";
    [SerializeField] private TacticsUnitTeam team = TacticsUnitTeam.Player;

    [Header("Visuals")]
    [SerializeField] private string spriteSheetResourcePath = "Characters/sprite-sheet_export_8x4_48x64";
    [SerializeField, Min(0.01f)] private float walkFramesPerSecond = 7f;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.65f, 1f);

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.75f;
    [SerializeField, Min(0.01f)] private float jumpDuration = 0.25f;
    [SerializeField, Min(0f)] private float jumpArcHeight = 0.22f;
    [SerializeField, Min(0)] private int maxStepUp = 1;
    [SerializeField, Min(0)] private int maxStepDown = 1;
    [SerializeField] private Vector2 tileAnchorOffset = new Vector2(0.18f, 0.125f);

    [Header("Spawn")]
    [SerializeField] private Vector2Int preferredSpawnTile = Vector2Int.zero;

    [Header("Base Stats")]
    [SerializeField] private TacticsCharacterStats baseStats = TacticsCharacterStats.Default();

    [Header("Abilities")]
    [SerializeField] private List<TacticsAbilityDefinition> startingAbilities = new();

    [Header("Progression")]
    [SerializeField, Min(1)] private int experienceToNextLevel = 100;

    [Header("Rewards")]
    [SerializeField, Min(0)] private int experienceRewardMin;
    [SerializeField, Min(0)] private int experienceRewardMax;

    public string CharacterId => characterId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public TacticsUnitTeam Team => team;
    public float WalkFramesPerSecond => walkFramesPerSecond;
    public Color BaseColor => baseColor;
    public Color SelectedColor => selectedColor;
    public float MoveSpeed => moveSpeed;
    public float JumpDuration => jumpDuration;
    public float JumpArcHeight => jumpArcHeight;
    public int MaxStepUp => maxStepUp;
    public int MaxStepDown => maxStepDown;
    public Vector2 TileAnchorOffset => tileAnchorOffset;
    public Vector2Int PreferredSpawnTile => preferredSpawnTile;
    public TacticsCharacterStats BaseStats => baseStats;
    public TacticsCharacterDerivedStats DerivedStats => baseStats.CalculateDerivedStats();
    public IReadOnlyList<TacticsAbilityDefinition> StartingAbilities => startingAbilities;
    public string SpriteSheetResourcePath => spriteSheetResourcePath;
    public int ExperienceToNextLevel => Mathf.Max(1, experienceToNextLevel);
    public int ExperienceRewardMin => Mathf.Max(0, experienceRewardMin);
    public int ExperienceRewardMax => Mathf.Max(ExperienceRewardMin, experienceRewardMax);

    public bool TryGetOrderedSprites(out IReadOnlyList<Sprite> sprites)
    {
        return TryLoadOrderedSprites(spriteSheetResourcePath, out sprites);
    }

    public TacticsCharacterData BuildRuntimeData()
    {
        return TacticsCharacterData.FromDefinition(this);
    }

    public static bool TryLoadOrderedSprites(string path, out IReadOnlyList<Sprite> sprites)
    {
        sprites = Array.Empty<Sprite>();

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        Sprite[] loadedSprites = LoadSprites(path);
        if (loadedSprites.Length < 10)
        {
            return false;
        }

        sprites = loadedSprites;
        return true;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        jumpDuration = Mathf.Max(0.01f, jumpDuration);
        jumpArcHeight = Mathf.Max(0f, jumpArcHeight);
        maxStepUp = Mathf.Max(0, maxStepUp);
        maxStepDown = Mathf.Max(0, maxStepDown);
        walkFramesPerSecond = Mathf.Max(0.01f, walkFramesPerSecond);
        characterId = string.IsNullOrWhiteSpace(characterId) ? name.ToLowerInvariant().Replace(' ', '_') : characterId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        experienceToNextLevel = Mathf.Max(1, experienceToNextLevel);
        experienceRewardMin = Mathf.Max(0, experienceRewardMin);
        experienceRewardMax = Mathf.Max(experienceRewardMin, experienceRewardMax);
        startingAbilities ??= new List<TacticsAbilityDefinition>();
    }

    private static Sprite[] LoadSprites(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<Sprite>();
        }

        Sprite[] resourceSprites = Resources.LoadAll<Sprite>(path)
            .OrderBy(ParseSpriteIndex)
            .ToArray();

        if (resourceSprites.Length >= 10)
        {
            return resourceSprites;
        }

#if UNITY_EDITOR
        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return resourceSprites;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(ParseSpriteIndex)
            .ToArray();
#else
        return resourceSprites;
#endif
    }

    private static int ParseSpriteIndex(Sprite sprite)
    {
        if (sprite == null)
        {
            return int.MaxValue;
        }

        string[] parts = sprite.name.Split('_');
        if (parts.Length == 0)
        {
            return int.MaxValue;
        }

        return int.TryParse(parts[parts.Length - 1], out int index) ? index : int.MaxValue;
    }
}

[Serializable]
public struct TacticsCharacterStats
{
    public TacticsPrimaryStats primaryStats;
    public TacticsMobilityStats mobilityStats;

    public int MoveRange => Mathf.Max(0, mobilityStats.moveRange);
    public int JumpHeight => Mathf.Max(0, mobilityStats.jumpHeight);
    public int GetPrimaryStat(TacticsAbilityScalingStat stat) => primaryStats.GetValue(stat);

    public TacticsCharacterDerivedStats CalculateDerivedStats()
    {
        int maxHitPoints = 40 + (primaryStats.stamina * 10);
        int maxStamina = 15 + (primaryStats.agility * 5);
        int maxMana = 10 + (primaryStats.wisdom * 5);

        int calculatedMeleeDamageMin = 1 + (primaryStats.strength * 2);
        int clampedMeleeDamageMin = Mathf.Max(0, calculatedMeleeDamageMin);
        int calculatedMeleeDamageMax = calculatedMeleeDamageMin + Mathf.Max(1, primaryStats.strength);

        int calculatedMagicDamageMin = 1 + (primaryStats.intelligence * 2);
        int clampedMagicDamageMin = Mathf.Max(0, calculatedMagicDamageMin);
        int calculatedMagicDamageMax = calculatedMagicDamageMin + Mathf.Max(1, primaryStats.intelligence);

        float meleeCriticalHitChance = Mathf.Clamp01(0.05f + (primaryStats.agility * 0.015f));
        float magicCriticalHitChance = Mathf.Clamp01(0.05f + (primaryStats.wisdom * 0.015f));

        return new TacticsCharacterDerivedStats
        {
            maxHitPoints = Mathf.Max(1, maxHitPoints),
            maxStamina = Mathf.Max(0, maxStamina),
            maxMana = Mathf.Max(0, maxMana),
            baseMeleeDamageMin = clampedMeleeDamageMin,
            baseMeleeDamageMax = Mathf.Max(clampedMeleeDamageMin, calculatedMeleeDamageMax),
            baseMagicDamageMin = clampedMagicDamageMin,
            baseMagicDamageMax = Mathf.Max(clampedMagicDamageMin, calculatedMagicDamageMax),
            meleeCriticalHitChance = meleeCriticalHitChance,
            magicCriticalHitChance = magicCriticalHitChance
        };
    }

    public TacticsCharacterRuntimeResources CreateRuntimeResources()
    {
        return TacticsCharacterRuntimeResources.FromDerivedStats(CalculateDerivedStats());
    }

    public static TacticsCharacterStats Default()
    {
        return new TacticsCharacterStats
        {
            primaryStats = TacticsPrimaryStats.Default(),
            mobilityStats = TacticsMobilityStats.Default()
        };
    }
}

[Serializable]
public struct TacticsPrimaryStats
{
    [Min(1)] public int stamina;
    [Min(1)] public int strength;
    [Min(1)] public int agility;
    [Min(1)] public int wisdom;
    [FormerlySerializedAs("magicAttack")]
    [Min(1)] public int intelligence;

    public int GetValue(TacticsAbilityScalingStat stat)
    {
        return stat switch
        {
            TacticsAbilityScalingStat.Stamina => Mathf.Max(0, stamina),
            TacticsAbilityScalingStat.Strength => Mathf.Max(0, strength),
            TacticsAbilityScalingStat.Agility => Mathf.Max(0, agility),
            TacticsAbilityScalingStat.Wisdom => Mathf.Max(0, wisdom),
            TacticsAbilityScalingStat.Intelligence => Mathf.Max(0, intelligence),
            _ => 0
        };
    }

    public static TacticsPrimaryStats Default()
    {
        return new TacticsPrimaryStats
        {
            stamina = 5,
            strength = 5,
            agility = 5,
            wisdom = 5,
            intelligence = 5
        };
    }
}

[Serializable]
public struct TacticsMobilityStats
{
    [FormerlySerializedAs("moveRange")]
    [Min(1)] public int moveRange;
    [FormerlySerializedAs("jumpHeight")]
    [Min(0)] public int jumpHeight;

    public static TacticsMobilityStats Default()
    {
        return new TacticsMobilityStats
        {
            moveRange = 4,
            jumpHeight = 2
        };
    }
}

[Serializable]
public struct TacticsCharacterDerivedStats
{
    [Min(1)] public int maxHitPoints;
    [Min(0)] public int maxStamina;
    [Min(0)] public int maxMana;
    [Min(0)] public int baseMeleeDamageMin;
    [Min(0)] public int baseMeleeDamageMax;
    [Min(0)] public int baseMagicDamageMin;
    [Min(0)] public int baseMagicDamageMax;
    [Range(0f, 1f)] public float meleeCriticalHitChance;
    [Range(0f, 1f)] public float magicCriticalHitChance;
}

[Serializable]
public struct TacticsCharacterRuntimeResources
{
    [Min(0)] public int hitPoints;
    [Min(0)] public int stamina;
    [Min(0)] public int mana;

    public static TacticsCharacterRuntimeResources FromDerivedStats(TacticsCharacterDerivedStats derivedStats)
    {
        return new TacticsCharacterRuntimeResources
        {
            hitPoints = derivedStats.maxHitPoints,
            stamina = derivedStats.maxStamina,
            mana = derivedStats.maxMana
        };
    }
}
