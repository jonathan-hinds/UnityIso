using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum TacticsItemKind
{
    Consumable = 0,
    Equipment = 1
}

public enum TacticsEquipmentSlot
{
    Weapon = 0,
    Helmet = 1,
    Chest = 2,
    Pants = 3,
    Boots = 4
}

public enum TacticsWeaponType
{
    Sword = 0,
    Dagger = 1,
    Staff = 2,
    Wand = 3,
    Mace = 4
}

public enum TacticsInventoryActionKind
{
    None = 0,
    UseConsumable = 1,
    Equip = 2,
    Unequip = 3
}

public abstract class TacticsItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId = "item";
    [SerializeField] private string displayName = "Item";
    [SerializeField, TextArea] private string description = string.Empty;
    [SerializeField] private Sprite thumbnail;

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    public string Description => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
    public Sprite Thumbnail => thumbnail;
    public abstract TacticsItemKind ItemKind { get; }

    protected virtual void OnValidate()
    {
        itemId = string.IsNullOrWhiteSpace(itemId)
            ? name.ToLowerInvariant().Replace(' ', '_')
            : itemId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
    }
}

public abstract class TacticsEquipmentItemDefinition : TacticsItemDefinition
{
    [Header("Equipment")]
    [SerializeField] private TacticsEquipmentSlot slot = TacticsEquipmentSlot.Helmet;
    [SerializeField] private TacticsPrimaryStatBonuses primaryStatBonuses;
    [SerializeField] private TacticsDerivedStatBonuses derivedStatBonuses;

    public override TacticsItemKind ItemKind => TacticsItemKind.Equipment;
    public TacticsEquipmentSlot Slot => slot;
    public TacticsPrimaryStatBonuses PrimaryStatBonuses => primaryStatBonuses;
    public TacticsDerivedStatBonuses DerivedStatBonuses => derivedStatBonuses;
}

[Serializable]
public struct TacticsPrimaryStatBonuses
{
    public int stamina;
    public int strength;
    public int agility;
    public int wisdom;
    public int intelligence;

    public void Apply(ref TacticsCharacterStats stats)
    {
        stats.primaryStats.stamina = Mathf.Max(1, stats.primaryStats.stamina + stamina);
        stats.primaryStats.strength = Mathf.Max(1, stats.primaryStats.strength + strength);
        stats.primaryStats.agility = Mathf.Max(1, stats.primaryStats.agility + agility);
        stats.primaryStats.wisdom = Mathf.Max(1, stats.primaryStats.wisdom + wisdom);
        stats.primaryStats.intelligence = Mathf.Max(1, stats.primaryStats.intelligence + intelligence);
    }

    public bool HasAnyValue =>
        stamina != 0 ||
        strength != 0 ||
        agility != 0 ||
        wisdom != 0 ||
        intelligence != 0;
}

[Serializable]
public struct TacticsDerivedStatBonuses
{
    public int maxHitPoints;
    public int maxStamina;
    public int maxMana;
    public int moveRange;
    public int jumpHeight;
    public int meleeDamageMin;
    public int meleeDamageMax;
    public int magicDamageMin;
    public int magicDamageMax;
    [Range(-1f, 1f)] public float meleeCriticalHitChanceBonus;
    [Range(-1f, 1f)] public float magicCriticalHitChanceBonus;

    public void ApplyToBaseStats(ref TacticsCharacterStats stats)
    {
        stats.mobilityStats.moveRange = Mathf.Max(0, stats.mobilityStats.moveRange + moveRange);
        stats.mobilityStats.jumpHeight = Mathf.Max(0, stats.mobilityStats.jumpHeight + jumpHeight);
    }

    public void ApplyToDerivedStats(ref TacticsCharacterDerivedStats stats)
    {
        stats.maxHitPoints = Mathf.Max(1, stats.maxHitPoints + maxHitPoints);
        stats.maxStamina = Mathf.Max(0, stats.maxStamina + maxStamina);
        stats.maxMana = Mathf.Max(0, stats.maxMana + maxMana);
        stats.baseMeleeDamageMin = Mathf.Max(0, stats.baseMeleeDamageMin + meleeDamageMin);
        stats.baseMeleeDamageMax = Mathf.Max(stats.baseMeleeDamageMin, stats.baseMeleeDamageMax + meleeDamageMax);
        stats.baseMagicDamageMin = Mathf.Max(0, stats.baseMagicDamageMin + magicDamageMin);
        stats.baseMagicDamageMax = Mathf.Max(stats.baseMagicDamageMin, stats.baseMagicDamageMax + magicDamageMax);
        stats.meleeCriticalHitChance = Mathf.Clamp01(stats.meleeCriticalHitChance + meleeCriticalHitChanceBonus);
        stats.magicCriticalHitChance = Mathf.Clamp01(stats.magicCriticalHitChance + magicCriticalHitChanceBonus);
    }

    public bool HasAnyValue =>
        maxHitPoints != 0 ||
        maxStamina != 0 ||
        maxMana != 0 ||
        moveRange != 0 ||
        jumpHeight != 0 ||
        meleeDamageMin != 0 ||
        meleeDamageMax != 0 ||
        magicDamageMin != 0 ||
        magicDamageMax != 0 ||
        Mathf.Abs(meleeCriticalHitChanceBonus) > Mathf.Epsilon ||
        Mathf.Abs(magicCriticalHitChanceBonus) > Mathf.Epsilon;
}

[Serializable]
public sealed class TacticsInventoryItemSaveData
{
    public string instanceId;
    public string itemId;
    public int quantity = 1;

    public TacticsInventoryItemSaveData Clone()
    {
        return new TacticsInventoryItemSaveData
        {
            instanceId = string.IsNullOrWhiteSpace(instanceId) ? string.Empty : instanceId.Trim(),
            itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim(),
            quantity = Mathf.Max(1, quantity)
        };
    }
}

[Serializable]
public sealed class TacticsEquippedItemSaveData
{
    public TacticsEquipmentSlot slot;
    public string instanceId;
    public string itemId;
    public int quantity = 1;

    public TacticsEquippedItemSaveData Clone()
    {
        return new TacticsEquippedItemSaveData
        {
            slot = slot,
            instanceId = string.IsNullOrWhiteSpace(instanceId) ? string.Empty : instanceId.Trim(),
            itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim(),
            quantity = Mathf.Max(1, quantity)
        };
    }
}

[Serializable]
public struct TacticsCharacterInventorySnapshot
{
    public string characterId;
    public List<TacticsInventoryItemSaveData> items;
    public List<TacticsEquippedItemSaveData> equippedItems;

    public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();

    public TacticsCharacterInventorySnapshot WithCharacterId(string value)
    {
        TacticsCharacterInventorySnapshot updated = this;
        updated.characterId = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return updated;
    }

    public TacticsCharacterInventorySnapshot Sanitize()
    {
        TacticsCharacterInventorySnapshot sanitized = this;
        sanitized.characterId = CharacterId;
        sanitized.items ??= new List<TacticsInventoryItemSaveData>();
        sanitized.equippedItems ??= new List<TacticsEquippedItemSaveData>();

        HashSet<string> seenInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = sanitized.items.Count - 1; i >= 0; i--)
        {
            TacticsInventoryItemSaveData item = sanitized.items[i];
            if (item == null)
            {
                sanitized.items.RemoveAt(i);
                continue;
            }

            item.instanceId = string.IsNullOrWhiteSpace(item.instanceId) ? string.Empty : item.instanceId.Trim();
            item.itemId = string.IsNullOrWhiteSpace(item.itemId) ? string.Empty : item.itemId.Trim();
            item.quantity = Mathf.Max(1, item.quantity);
            if (string.IsNullOrWhiteSpace(item.instanceId) ||
                string.IsNullOrWhiteSpace(item.itemId) ||
                !seenInstanceIds.Add(item.instanceId))
            {
                sanitized.items.RemoveAt(i);
            }
        }

        HashSet<TacticsEquipmentSlot> seenSlots = new HashSet<TacticsEquipmentSlot>();
        HashSet<string> seenEquippedInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = sanitized.equippedItems.Count - 1; i >= 0; i--)
        {
            TacticsEquippedItemSaveData equipped = sanitized.equippedItems[i];
            if (equipped == null)
            {
                sanitized.equippedItems.RemoveAt(i);
                continue;
            }

            equipped.instanceId = string.IsNullOrWhiteSpace(equipped.instanceId) ? string.Empty : equipped.instanceId.Trim();
            equipped.itemId = string.IsNullOrWhiteSpace(equipped.itemId) ? string.Empty : equipped.itemId.Trim();
            equipped.quantity = Mathf.Max(1, equipped.quantity);
            if (string.IsNullOrWhiteSpace(equipped.instanceId) ||
                !seenSlots.Add(equipped.slot) ||
                !seenEquippedInstanceIds.Add(equipped.instanceId))
            {
                sanitized.equippedItems.RemoveAt(i);
            }
        }

        return sanitized;
    }

    public static TacticsCharacterInventorySnapshot CreateDefault(string characterId)
    {
        return new TacticsCharacterInventorySnapshot
        {
            characterId = string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim(),
            items = new List<TacticsInventoryItemSaveData>(),
            equippedItems = new List<TacticsEquippedItemSaveData>()
        };
    }
}

public readonly struct TacticsInventoryResolvedItem
{
    public TacticsInventoryResolvedItem(
        TacticsInventoryItemSaveData saveData,
        TacticsItemDefinition definition,
        bool isEquipped,
        int quantity)
    {
        SaveData = saveData;
        Definition = definition;
        IsEquipped = isEquipped;
        Quantity = Mathf.Max(1, quantity);
    }

    public TacticsInventoryItemSaveData SaveData { get; }
    public TacticsItemDefinition Definition { get; }
    public bool IsEquipped { get; }
    public int Quantity { get; }
    public string InstanceId => SaveData != null ? SaveData.instanceId : string.Empty;
}

public readonly struct TacticsEquipmentRuntimeSummary
{
    public TacticsEquipmentRuntimeSummary(
        TacticsEquipmentItemDefinition equipment,
        TacticsInventoryItemSaveData saveData)
    {
        Equipment = equipment;
        SaveData = saveData;
    }

    public TacticsEquipmentItemDefinition Equipment { get; }
    public TacticsInventoryItemSaveData SaveData { get; }
    public bool IsValid => Equipment != null && SaveData != null;
}

public static class TacticsItemCatalogResources
{
    private const string ItemResourcesPath = "Tactics/Items";
    private static readonly Dictionary<string, TacticsItemDefinition> ItemsById = new(StringComparer.OrdinalIgnoreCase);
    private static bool isLoaded;

    public static IReadOnlyCollection<TacticsItemDefinition> LoadAll()
    {
        EnsureLoaded();
        return ItemsById.Values;
    }

    public static bool TryGetItem(string itemId, out TacticsItemDefinition item)
    {
        EnsureLoaded();
        return ItemsById.TryGetValue(string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim(), out item);
    }

    public static void Invalidate()
    {
        isLoaded = false;
        ItemsById.Clear();
    }

    private static void EnsureLoaded()
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;
        ItemsById.Clear();
        TacticsItemDefinition[] loadedItems = Resources.LoadAll<TacticsItemDefinition>(ItemResourcesPath);
        for (int i = 0; i < loadedItems.Length; i++)
        {
            TacticsItemDefinition item = loadedItems[i];
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
            {
                continue;
            }

            ItemsById[item.ItemId] = item;
        }
    }
}

public static class TacticsItemTooltipUtility
{
    public static TacticsAbilityTooltipContent BuildTooltipContent(TacticsItemDefinition item)
    {
        if (item == null)
        {
            return default;
        }

        if (item is TacticsConsumableItemDefinition)
        {
            return new TacticsAbilityTooltipContent(
                item.DisplayName,
                "Consumable",
                item.Description,
                string.Empty,
                "Right click to use on self.");
        }

        if (item is TacticsWeaponItemDefinition weapon)
        {
            return new TacticsAbilityTooltipContent(
                item.DisplayName,
                $"{weapon.WeaponType}  |  {weapon.DamageType}",
                item.Description,
                BuildEquipmentSummary(weapon),
                "Right click to equip or unequip.");
        }

        if (item is TacticsEquipmentItemDefinition equipment)
        {
            return new TacticsAbilityTooltipContent(
                item.DisplayName,
                equipment.Slot.ToString(),
                item.Description,
                BuildEquipmentSummary(equipment),
                "Right click to equip or unequip.");
        }

        return new TacticsAbilityTooltipContent(item.DisplayName, string.Empty, item.Description);
    }

    public static string BuildEquipmentSummary(TacticsEquipmentItemDefinition item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        AppendPrimaryBonuses(builder, item.PrimaryStatBonuses);
        AppendDerivedBonuses(builder, item.DerivedStatBonuses);

        if (item is TacticsWeaponItemDefinition weapon)
        {
            AppendLine(builder, $"{weapon.DamageType} damage +{weapon.BaseDamageMinBonus}-{weapon.BaseDamageMaxBonus}");
            if (weapon.DamageScaling != null)
            {
                for (int i = 0; i < weapon.DamageScaling.Count; i++)
                {
                    TacticsAbilityScalingDefinitionData scaling = weapon.DamageScaling[i];
                    AppendLine(builder, $"{scaling.Stat} scaling {scaling.Rank}");
                }
            }
        }

        return builder.ToString().Trim();
    }

    private static void AppendPrimaryBonuses(StringBuilder builder, TacticsPrimaryStatBonuses bonuses)
    {
        AppendSignedLine(builder, "Stamina", bonuses.stamina);
        AppendSignedLine(builder, "Strength", bonuses.strength);
        AppendSignedLine(builder, "Agility", bonuses.agility);
        AppendSignedLine(builder, "Wisdom", bonuses.wisdom);
        AppendSignedLine(builder, "Intellect", bonuses.intelligence);
    }

    private static void AppendDerivedBonuses(StringBuilder builder, TacticsDerivedStatBonuses bonuses)
    {
        AppendSignedLine(builder, "Max HP", bonuses.maxHitPoints);
        AppendSignedLine(builder, "Max ST", bonuses.maxStamina);
        AppendSignedLine(builder, "Max MP", bonuses.maxMana);
        AppendSignedLine(builder, "Move", bonuses.moveRange);
        AppendSignedLine(builder, "Jump", bonuses.jumpHeight);
        AppendSignedLine(builder, "Melee dmg min", bonuses.meleeDamageMin);
        AppendSignedLine(builder, "Melee dmg max", bonuses.meleeDamageMax);
        AppendSignedLine(builder, "Magic dmg min", bonuses.magicDamageMin);
        AppendSignedLine(builder, "Magic dmg max", bonuses.magicDamageMax);
        AppendSignedPercentLine(builder, "Melee crit", bonuses.meleeCriticalHitChanceBonus);
        AppendSignedPercentLine(builder, "Magic crit", bonuses.magicCriticalHitChanceBonus);
    }

    private static void AppendSignedLine(StringBuilder builder, string label, int value)
    {
        if (value == 0)
        {
            return;
        }

        AppendLine(builder, $"{label} {(value > 0 ? "+" : string.Empty)}{value}");
    }

    private static void AppendSignedPercentLine(StringBuilder builder, string label, float value)
    {
        if (Mathf.Abs(value) <= Mathf.Epsilon)
        {
            return;
        }

        float percent = value * 100f;
        AppendLine(builder, $"{label} {(percent > 0f ? "+" : string.Empty)}{percent:0.#}%");
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line.Trim());
    }
}

[Serializable]
public sealed class TacticsChestItemPoolEntry
{
    public TacticsItemDefinition itemDefinition;
    public string itemId;
    [Min(1)] public int weight = 1;

    public bool IsValid => !string.IsNullOrWhiteSpace(itemId) && weight > 0;

    public TacticsChestItemPoolEntry Clone()
    {
        return new TacticsChestItemPoolEntry
        {
            itemDefinition = itemDefinition,
            itemId = itemId,
            weight = weight
        };
    }

    public void Sanitize()
    {
        if (itemDefinition != null)
        {
            itemId = itemDefinition.ItemId;
        }

        itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        weight = Mathf.Max(1, weight);
    }
}

[Serializable]
public sealed class TacticsInventoryCommandMessage
{
    public string runtimeCharacterId;
    public TacticsInventoryActionKind actionKind;
    public string itemInstanceId;
    public TacticsEquipmentSlot slot;
    public string randomStateJson;
}
