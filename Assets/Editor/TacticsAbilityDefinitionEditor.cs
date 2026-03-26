using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(TacticsAbilityDefinition))]
public sealed class TacticsAbilityDefinitionEditor : Editor
{
    private SerializedProperty abilityIdProperty;
    private SerializedProperty displayNameProperty;
    private SerializedProperty descriptionProperty;
    private SerializedProperty rangeTypeProperty;
    private SerializedProperty rangeProperty;
    private SerializedProperty areaOfEffectSizeProperty;
    private SerializedProperty targetRuleProperty;
    private SerializedProperty damageTypeProperty;
    private SerializedProperty projectilePrefabProperty;
    private SerializedProperty hitEffectProperty;
    private SerializedProperty effectsProperty;
    private SerializedProperty statusEffectsProperty;
    private SerializedProperty costResourceTypeProperty;
    private SerializedProperty costAmountProperty;
    private SerializedProperty allowMovementAsAlternateCostProperty;

    private void OnEnable()
    {
        abilityIdProperty = serializedObject.FindProperty("abilityId");
        displayNameProperty = serializedObject.FindProperty("displayName");
        descriptionProperty = serializedObject.FindProperty("description");
        rangeTypeProperty = serializedObject.FindProperty("rangeType");
        rangeProperty = serializedObject.FindProperty("range");
        areaOfEffectSizeProperty = serializedObject.FindProperty("areaOfEffectSize");
        targetRuleProperty = serializedObject.FindProperty("targetRule");
        damageTypeProperty = serializedObject.FindProperty("damageType");
        projectilePrefabProperty = serializedObject.FindProperty("projectilePrefab");
        hitEffectProperty = serializedObject.FindProperty("hitEffect");
        effectsProperty = serializedObject.FindProperty("effects");
        statusEffectsProperty = serializedObject.FindProperty("statusEffects");
        costResourceTypeProperty = serializedObject.FindProperty("costResourceType");
        costAmountProperty = serializedObject.FindProperty("costAmount");
        allowMovementAsAlternateCostProperty = serializedObject.FindProperty("allowMovementAsAlternateCost");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(abilityIdProperty);
        EditorGUILayout.PropertyField(displayNameProperty);
        EditorGUILayout.PropertyField(descriptionProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Targeting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rangeTypeProperty);

        TacticsAbilityRangeType rangeType = (TacticsAbilityRangeType)rangeTypeProperty.enumValueIndex;
        if (UsesAbilityRange(rangeType))
        {
            EditorGUILayout.PropertyField(rangeProperty);
        }

        if (UsesAreaOfEffect(rangeType))
        {
            EditorGUILayout.PropertyField(areaOfEffectSizeProperty, new GUIContent("AoE Size", "Width of the square area in tiles. Odd values keep the target tile centered."));
        }

        EditorGUILayout.PropertyField(targetRuleProperty);
        EditorGUILayout.PropertyField(damageTypeProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(projectilePrefabProperty, new GUIContent("Projectile Prefab", "Optional prefab spawned for ranged-style ability presentation. Leave empty for instant effects."));
        DrawHitEffectPresentationInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ability Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(effectsProperty, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Status Applications", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(statusEffectsProperty, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cost", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(costResourceTypeProperty);
        TacticsAbilityResourceType costType = (TacticsAbilityResourceType)costResourceTypeProperty.enumValueIndex;
        if (costType != TacticsAbilityResourceType.None)
        {
            if (costType == TacticsAbilityResourceType.Movement)
            {
                EditorGUILayout.HelpBox("Movement costs consume the unit's move for the turn instead of stamina or mana.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(costAmountProperty);
                EditorGUILayout.PropertyField(
                    allowMovementAsAlternateCostProperty,
                    new GUIContent("Allow Movement Alternative", "If the unit still has movement left, movement can be spent to cover this ability cost when the resource cost cannot be paid."));
            }
        }
        else
        {
            EditorGUILayout.PropertyField(costAmountProperty);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool UsesAbilityRange(TacticsAbilityRangeType rangeType)
    {
        return rangeType == TacticsAbilityRangeType.Ranged ||
               rangeType == TacticsAbilityRangeType.AbsoluteRanged ||
               rangeType == TacticsAbilityRangeType.RangedAoE ||
               rangeType == TacticsAbilityRangeType.AbsoluteAoE;
    }

    private static bool UsesAreaOfEffect(TacticsAbilityRangeType rangeType)
    {
        return rangeType == TacticsAbilityRangeType.SurroundingAoE ||
               rangeType == TacticsAbilityRangeType.RangedAoE ||
               rangeType == TacticsAbilityRangeType.AbsoluteAoE;
    }

    private void DrawHitEffectPresentationInspector()
    {
        if (hitEffectProperty == null)
        {
            return;
        }

        SerializedProperty sourceTextureProperty = hitEffectProperty.FindPropertyRelative("sourceTexture");
        SerializedProperty framesProperty = hitEffectProperty.FindPropertyRelative("frames");
        SerializedProperty framesPerSecondProperty = hitEffectProperty.FindPropertyRelative("framesPerSecond");
        SerializedProperty durationProperty = hitEffectProperty.FindPropertyRelative("duration");
        SerializedProperty scaleProperty = hitEffectProperty.FindPropertyRelative("scale");
        SerializedProperty worldOffsetProperty = hitEffectProperty.FindPropertyRelative("worldOffset");
        SerializedProperty tintProperty = hitEffectProperty.FindPropertyRelative("tint");
        SerializedProperty sortingOrderOffsetProperty = hitEffectProperty.FindPropertyRelative("sortingOrderOffset");

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(sourceTextureProperty, new GUIContent("Hit Effect Sprite Sheet", "Pick a sliced sprite sheet such as spell1 through spell6. All sprite frames from that texture will be used as the hit animation."));
        if (EditorGUI.EndChangeCheck())
        {
            SyncFramesFromTexture(sourceTextureProperty, framesProperty);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("Resolved Frames", framesProperty.arraySize);
        }

        if (framesProperty.arraySize > 0)
        {
            EditorGUILayout.PropertyField(framesPerSecondProperty);
            EditorGUILayout.PropertyField(durationProperty, new GUIContent("Duration", "How long the hit effect should remain visible. Leave at the default to match the animation length."));
            EditorGUILayout.PropertyField(scaleProperty);
            EditorGUILayout.PropertyField(worldOffsetProperty, new GUIContent("World Offset", "Additional offset applied over the target anchor in world units."));
            EditorGUILayout.PropertyField(tintProperty);
            EditorGUILayout.PropertyField(sortingOrderOffsetProperty);
        }
        else
        {
            EditorGUILayout.HelpBox("Assign one of the sliced spell sprite sheets to enable an animated hit effect for this ability.", MessageType.Info);
        }
    }

    private static void SyncFramesFromTexture(SerializedProperty sourceTextureProperty, SerializedProperty framesProperty)
    {
        framesProperty.ClearArray();

        Texture2D sourceTexture = sourceTextureProperty.objectReferenceValue as Texture2D;
        if (sourceTexture == null)
        {
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(sourceTexture);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((left, right) => EditorUtility.NaturalCompare(left.name, right.name));
        for (int i = 0; i < sprites.Count; i++)
        {
            framesProperty.InsertArrayElementAtIndex(i);
            framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }
}
