using UnityEditor;
using UnityEngine;

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
    private SerializedProperty effectsProperty;
    private SerializedProperty costResourceTypeProperty;
    private SerializedProperty costAmountProperty;

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
        effectsProperty = serializedObject.FindProperty("effects");
        costResourceTypeProperty = serializedObject.FindProperty("costResourceType");
        costAmountProperty = serializedObject.FindProperty("costAmount");
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(effectsProperty, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cost", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(costResourceTypeProperty);
        EditorGUILayout.PropertyField(costAmountProperty);

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
}
