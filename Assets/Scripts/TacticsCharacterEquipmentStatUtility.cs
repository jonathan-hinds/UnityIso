using System.Collections.Generic;
using UnityEngine;

public static class TacticsCharacterEquipmentStatUtility
{
    public static TacticsCharacterStats BuildEffectiveStats(
        TacticsCharacterData characterData,
        TacticsCharacterProgressionSnapshot progression,
        TacticsCharacterInventorySnapshot inventorySnapshot)
    {
        TacticsCharacterStats stats = progression.ApplyTo(characterData != null ? characterData.BaseStats : TacticsCharacterStats.Default());
        ApplyEquipmentBonusesToStats(ref stats, inventorySnapshot);
        return stats;
    }

    public static TacticsCharacterDerivedStats BuildDerivedStats(
        TacticsCharacterStats effectiveStats,
        TacticsCharacterInventorySnapshot inventorySnapshot)
    {
        TacticsCharacterDerivedStats derivedStats = effectiveStats.CalculateDerivedStats();
        ApplyEquipmentBonusesToDerivedStats(ref derivedStats, effectiveStats, inventorySnapshot);
        return derivedStats;
    }

    public static void ApplyEquipmentBonusesToStats(
        ref TacticsCharacterStats stats,
        TacticsCharacterInventorySnapshot inventorySnapshot)
    {
        List<TacticsInventoryItemSaveData> equippedItems = ResolveEquippedItems(inventorySnapshot);
        for (int i = 0; i < equippedItems.Count; i++)
        {
            if (!TryGetEquipmentDefinition(equippedItems[i], out TacticsEquipmentItemDefinition equipment))
            {
                continue;
            }

            TacticsPrimaryStatBonuses primaryBonuses = equipment.PrimaryStatBonuses;
            primaryBonuses.Apply(ref stats);
            equipment.DerivedStatBonuses.ApplyToBaseStats(ref stats);
        }
    }

    public static void ApplyEquipmentBonusesToDerivedStats(
        ref TacticsCharacterDerivedStats stats,
        TacticsCharacterStats effectiveStats,
        TacticsCharacterInventorySnapshot inventorySnapshot)
    {
        List<TacticsInventoryItemSaveData> equippedItems = ResolveEquippedItems(inventorySnapshot);
        for (int i = 0; i < equippedItems.Count; i++)
        {
            if (!TryGetEquipmentDefinition(equippedItems[i], out TacticsEquipmentItemDefinition equipment))
            {
                continue;
            }

            equipment.DerivedStatBonuses.ApplyToDerivedStats(ref stats);
            if (equipment is not TacticsWeaponItemDefinition weapon)
            {
                continue;
            }

            int scalingBonus = TacticsAbilityScalingCalculator.EvaluateDamageBonus(effectiveStats, weapon.DamageScaling);
            float displayedBonus = ((weapon.BaseDamageMinBonus + weapon.BaseDamageMaxBonus) * 0.5f) + scalingBonus;
            if (weapon.DamageType == TacticsAbilityDamageType.Magic)
            {
                stats.baseMagicDamage = Mathf.Max(0f, stats.baseMagicDamage + displayedBonus);
                stats.baseMagicDamageMin = Mathf.Max(0, stats.baseMagicDamageMin + weapon.BaseDamageMinBonus + scalingBonus);
                stats.baseMagicDamageMax = Mathf.Max(stats.baseMagicDamageMin, stats.baseMagicDamageMax + weapon.BaseDamageMaxBonus + scalingBonus);
            }
            else
            {
                stats.baseMeleeDamage = Mathf.Max(0f, stats.baseMeleeDamage + displayedBonus);
                stats.baseMeleeDamageMin = Mathf.Max(0, stats.baseMeleeDamageMin + weapon.BaseDamageMinBonus + scalingBonus);
                stats.baseMeleeDamageMax = Mathf.Max(stats.baseMeleeDamageMin, stats.baseMeleeDamageMax + weapon.BaseDamageMaxBonus + scalingBonus);
            }
        }
    }

    private static List<TacticsInventoryItemSaveData> ResolveEquippedItems(TacticsCharacterInventorySnapshot inventorySnapshot)
    {
        TacticsCharacterInventorySnapshot sanitized = inventorySnapshot.Sanitize();
        List<TacticsInventoryItemSaveData> equippedItems = new List<TacticsInventoryItemSaveData>(sanitized.equippedItems.Count);
        Dictionary<string, TacticsInventoryItemSaveData> itemsByInstanceId = new Dictionary<string, TacticsInventoryItemSaveData>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sanitized.items.Count; i++)
        {
            TacticsInventoryItemSaveData item = sanitized.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.instanceId))
            {
                continue;
            }

            itemsByInstanceId[item.instanceId] = item;
        }

        for (int i = 0; i < sanitized.equippedItems.Count; i++)
        {
            TacticsEquippedItemSaveData equipped = sanitized.equippedItems[i];
            if (equipped == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(equipped.itemId))
            {
                equippedItems.Add(new TacticsInventoryItemSaveData
                {
                    instanceId = equipped.instanceId,
                    itemId = equipped.itemId,
                    quantity = Mathf.Max(1, equipped.quantity)
                });
                continue;
            }

            if (itemsByInstanceId.TryGetValue(equipped.instanceId, out TacticsInventoryItemSaveData legacyItem) && legacyItem != null)
            {
                equippedItems.Add(legacyItem.Clone());
            }
        }

        return equippedItems;
    }

    private static bool TryGetEquipmentDefinition(
        TacticsInventoryItemSaveData item,
        out TacticsEquipmentItemDefinition equipment)
    {
        equipment = null;
        return item != null &&
               TacticsItemCatalogResources.TryGetItem(item.itemId, out TacticsItemDefinition definition) &&
               (equipment = definition as TacticsEquipmentItemDefinition) != null;
    }
}
