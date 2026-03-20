using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "TacticsCharacterDefinition", menuName = "Tactics/Characters/Character Definition")]
public sealed class TacticsCharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string characterId = "character";
    [SerializeField] private string displayName = "Character";

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

    public string CharacterId => characterId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
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
    [Min(1)] public int maxHitPoints;
    [Min(0)] public int maxManaPoints;
    [Min(1)] public int speed;
    [Min(1)] public int physicalAttack;
    [Min(1)] public int magicAttack;
    [Min(1)] public int moveRange;
    [Min(0)] public int jumpHeight;
    [Range(0, 100)] public int brave;
    [Range(0, 100)] public int faith;

    public static TacticsCharacterStats Default()
    {
        return new TacticsCharacterStats
        {
            maxHitPoints = 70,
            maxManaPoints = 20,
            speed = 6,
            physicalAttack = 7,
            magicAttack = 5,
            moveRange = 4,
            jumpHeight = 2,
            brave = 70,
            faith = 60
        };
    }
}
