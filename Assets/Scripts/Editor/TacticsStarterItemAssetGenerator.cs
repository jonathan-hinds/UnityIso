using UnityEditor;
using UnityEngine;

public static class TacticsStarterItemAssetGenerator
{
    private const string ItemFolder = "Assets/Resources/Tactics/Items";

    [MenuItem("Tools/Tactics/Generate Starter Item Assets")]
    public static void GenerateStarterItems()
    {
        EnsureFolder("Assets/Resources/Tactics");
        EnsureFolder(ItemFolder);

        TacticsAbilityDefinition lesserHeal = Resources.Load<TacticsAbilityDefinition>("Tactics/Combat/LesserHeal");
        TacticsAbilityDefinition manaTap = Resources.Load<TacticsAbilityDefinition>("Tactics/Combat/ManaTap");
        TacticsAbilityDefinition secondWind = Resources.Load<TacticsAbilityDefinition>("Tactics/Combat/SecondWind");

        CreateConsumable("HealthPotion", "health_potion", "Health Potion", "Restores health using the standard healing ability.", lesserHeal);
        CreateConsumable("ManaPotion", "mana_potion", "Mana Potion", "Restores mana using the standard mana recovery ability.", manaTap);
        CreateConsumable("StaminaPotion", "stamina_potion", "Stamina Potion", "Restores stamina using the standard recovery ability.", secondWind);

        CreateArmor("IronHelm", "iron_helm", "Iron Helm", "A simple helm that sharpens frontline instincts.", TacticsEquipmentSlot.Helmet, agility: 0, strength: 0, meleeCrit: 0.05f);
        CreateArmor("LeatherTunic", "leather_tunic", "Leather Tunic", "Flexible chest armor for skirmishers.", TacticsEquipmentSlot.Chest, agility: 1, strength: 0, meleeCrit: 0f);
        CreateArmor("FieldTrousers", "field_trousers", "Field Trousers", "Travel pants that improve footing.", TacticsEquipmentSlot.Pants, agility: 1, strength: 0, meleeCrit: 0f);
        CreateArmor("ScoutBoots", "scout_boots", "Scout Boots", "Boots that add mobility and poise.", TacticsEquipmentSlot.Boots, agility: 1, strength: 0, meleeCrit: 0f, moveRange: 1);

        CreateWeapon("IronSword", "iron_sword", "Iron Sword", "Balanced melee weapon for strength builds.", TacticsWeaponType.Sword, TacticsAbilityDamageType.Melee, 2, 4, TacticsAbilityScalingStat.Strength, TacticsAbilityScalingRank.B);
        CreateWeapon("QuickDagger", "quick_dagger", "Quick Dagger", "Fast weapon with agile scaling.", TacticsWeaponType.Dagger, TacticsAbilityDamageType.Melee, 1, 3, TacticsAbilityScalingStat.Agility, TacticsAbilityScalingRank.B);
        CreateWeapon("OakStaff", "oak_staff", "Oak Staff", "Simple focus for channeling magic.", TacticsWeaponType.Staff, TacticsAbilityDamageType.Magic, 2, 5, TacticsAbilityScalingStat.Wisdom, TacticsAbilityScalingRank.B);
        CreateWeapon("ApprenticeWand", "apprentice_wand", "Apprentice Wand", "Focused casting wand for intellect scaling.", TacticsWeaponType.Wand, TacticsAbilityDamageType.Magic, 1, 4, TacticsAbilityScalingStat.Intelligence, TacticsAbilityScalingRank.B);
        CreateWeapon("WarMace", "war_mace", "War Mace", "Heavy melee weapon with strong strength scaling.", TacticsWeaponType.Mace, TacticsAbilityDamageType.Melee, 3, 5, TacticsAbilityScalingStat.Strength, TacticsAbilityScalingRank.A);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Generated starter item assets.");
    }

    private static void CreateConsumable(string assetName, string itemId, string displayName, string description, TacticsAbilityDefinition linkedAbility)
    {
        string path = $"{ItemFolder}/{assetName}.asset";
        TacticsConsumableItemDefinition asset = LoadOrCreateAsset<TacticsConsumableItemDefinition>(path);
        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("linkedAbility").objectReferenceValue = linkedAbility;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void CreateArmor(
        string assetName,
        string itemId,
        string displayName,
        string description,
        TacticsEquipmentSlot slot,
        int agility,
        int strength,
        float meleeCrit,
        int moveRange = 0)
    {
        string path = $"{ItemFolder}/{assetName}.asset";
        TacticsArmorItemDefinition asset = LoadOrCreateAsset<TacticsArmorItemDefinition>(path);
        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("slot").enumValueIndex = (int)slot;
        serialized.FindProperty("primaryStatBonuses").FindPropertyRelative("agility").intValue = agility;
        serialized.FindProperty("primaryStatBonuses").FindPropertyRelative("strength").intValue = strength;
        serialized.FindProperty("derivedStatBonuses").FindPropertyRelative("meleeCriticalHitChanceBonus").floatValue = meleeCrit;
        serialized.FindProperty("derivedStatBonuses").FindPropertyRelative("moveRange").intValue = moveRange;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void CreateWeapon(
        string assetName,
        string itemId,
        string displayName,
        string description,
        TacticsWeaponType weaponType,
        TacticsAbilityDamageType damageType,
        int minDamage,
        int maxDamage,
        TacticsAbilityScalingStat scalingStat,
        TacticsAbilityScalingRank scalingRank)
    {
        string path = $"{ItemFolder}/{assetName}.asset";
        TacticsWeaponItemDefinition asset = LoadOrCreateAsset<TacticsWeaponItemDefinition>(path);
        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("slot").enumValueIndex = (int)TacticsEquipmentSlot.Weapon;
        serialized.FindProperty("weaponType").enumValueIndex = (int)weaponType;
        serialized.FindProperty("damageType").enumValueIndex = (int)damageType;
        serialized.FindProperty("baseDamageMinBonus").intValue = minDamage;
        serialized.FindProperty("baseDamageMaxBonus").intValue = maxDamage;

        SerializedProperty scaling = serialized.FindProperty("damageScaling");
        scaling.ClearArray();
        scaling.InsertArrayElementAtIndex(0);
        SerializedProperty entry = scaling.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("stat").enumValueIndex = (int)scalingStat;
        entry.FindPropertyRelative("rank").enumValueIndex = (int)scalingRank;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
