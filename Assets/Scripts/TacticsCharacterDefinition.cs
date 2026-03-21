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

    public bool TryGetOrderedSprites(out IReadOnlyList<Sprite> sprites)
    {
        sprites = Array.Empty<Sprite>();

        if (string.IsNullOrWhiteSpace(spriteSheetResourcePath))
        {
            return false;
        }

        Sprite[] loadedSprites = LoadSprites(spriteSheetResourcePath);

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
    public TacticsDerivedStatModifiers derivedStatModifiers;
    public TacticsMobilityStats mobilityStats;

    public int MoveRange => Mathf.Max(0, mobilityStats.moveRange);
    public int JumpHeight => Mathf.Max(0, mobilityStats.jumpHeight);

    public TacticsCharacterDerivedStats CalculateDerivedStats()
    {
        int maxHitPoints = 40 + (primaryStats.stamina * 8) + (primaryStats.strength * 2) + derivedStatModifiers.bonusMaxHitPoints;
        int maxStamina = 15 + (primaryStats.stamina * 6) + (primaryStats.agility * 2) + derivedStatModifiers.bonusMaxStamina;
        int maxMana = 10 + (primaryStats.intelligence * 6) + (primaryStats.wisdom * 4) + derivedStatModifiers.bonusMaxMana;

        int calculatedBaseDamageMin = 1 + (primaryStats.strength * 2) + Mathf.FloorToInt(primaryStats.agility * 0.5f) + derivedStatModifiers.bonusBaseDamageMin;
        int clampedBaseDamageMin = Mathf.Max(0, calculatedBaseDamageMin);
        int calculatedBaseDamageMax = calculatedBaseDamageMin + Mathf.Max(1, primaryStats.strength + Mathf.CeilToInt(primaryStats.agility * 0.5f)) + derivedStatModifiers.bonusBaseDamageMax;

        return new TacticsCharacterDerivedStats
        {
            maxHitPoints = Mathf.Max(1, maxHitPoints),
            maxStamina = Mathf.Max(0, maxStamina),
            maxMana = Mathf.Max(0, maxMana),
            baseDamageMin = clampedBaseDamageMin,
            baseDamageMax = Mathf.Max(clampedBaseDamageMin, calculatedBaseDamageMax)
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
            derivedStatModifiers = TacticsDerivedStatModifiers.Default(),
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
public struct TacticsDerivedStatModifiers
{
    [FormerlySerializedAs("maxHitPoints")]
    public int bonusMaxHitPoints;
    [FormerlySerializedAs("maxManaPoints")]
    public int bonusMaxMana;
    public int bonusMaxStamina;
    [FormerlySerializedAs("physicalAttack")]
    public int bonusBaseDamageMin;
    public int bonusBaseDamageMax;

    public static TacticsDerivedStatModifiers Default()
    {
        return new TacticsDerivedStatModifiers
        {
            bonusMaxHitPoints = 0,
            bonusMaxMana = 0,
            bonusMaxStamina = 0,
            bonusBaseDamageMin = 0,
            bonusBaseDamageMax = 0
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
    [Min(0)] public int baseDamageMin;
    [Min(0)] public int baseDamageMax;
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
