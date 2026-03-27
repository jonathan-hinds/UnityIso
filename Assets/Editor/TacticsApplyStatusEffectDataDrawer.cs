using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TacticsApplyStatusEffectData))]
public sealed class TacticsApplyStatusEffectDataDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect contentRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(contentRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        SerializedProperty statusEffectType = property.FindPropertyRelative("statusEffectType");
        SerializedProperty durationTurns = property.FindPropertyRelative("durationTurns");
        SerializedProperty potencyFormula = property.FindPropertyRelative("potencyFormula");
        SerializedProperty flatPotency = property.FindPropertyRelative("flatPotency");
        SerializedProperty potencyMultiplier = property.FindPropertyRelative("potencyMultiplier");
        SerializedProperty scaling = property.FindPropertyRelative("scaling");
        SerializedProperty customDisplayName = property.FindPropertyRelative("customDisplayName");
        SerializedProperty customShortLabel = property.FindPropertyRelative("customShortLabel");
        SerializedProperty statModifier = property.FindPropertyRelative("statModifier");

        TacticsStatusEffectType effectType = (TacticsStatusEffectType)statusEffectType.enumValueIndex;
        bool isStatModifier = effectType is TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff;

        contentRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        DrawProperty(ref contentRect, statusEffectType);
        DrawProperty(ref contentRect, durationTurns);
        DrawProperty(ref contentRect, potencyFormula);
        DrawProperty(
            ref contentRect,
            flatPotency,
            new GUIContent(
                isStatModifier ? "Modifier Amount" : "Flat Potency",
                isStatModifier
                    ? "Base stat change before scaling. Negative values intentionally invert the chosen buff or debuff."
                    : "Base potency before scaling is applied."));
        DrawProperty(ref contentRect, potencyMultiplier);

        if (isStatModifier)
        {
            DrawProperty(ref contentRect, customDisplayName, new GUIContent("Display Name", "Optional name shown in tooltips and combat text."));
            DrawProperty(ref contentRect, customShortLabel, new GUIContent("Short Label", "Optional tray label. Leave empty to use the modified stat abbreviation."));
            DrawProperty(ref contentRect, statModifier, new GUIContent("Modified Stat"));
        }

        DrawProperty(ref contentRect, scaling, new GUIContent("Scaling"));
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        SerializedProperty statusEffectType = property.FindPropertyRelative("statusEffectType");
        TacticsStatusEffectType effectType = (TacticsStatusEffectType)statusEffectType.enumValueIndex;
        bool isStatModifier = effectType is TacticsStatusEffectType.StatBuff or TacticsStatusEffectType.StatDebuff;

        height += VerticalSpacing + EditorGUI.GetPropertyHeight(statusEffectType);
        height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("durationTurns"));
        height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("potencyFormula"));
        height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("flatPotency"));
        height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("potencyMultiplier"));

        if (isStatModifier)
        {
            height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("customDisplayName"));
            height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("customShortLabel"));
            height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("statModifier"));
        }

        height += VerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("scaling"), true);
        return height;
    }

    private static void DrawProperty(ref Rect position, SerializedProperty property, GUIContent label = null)
    {
        float propertyHeight = EditorGUI.GetPropertyHeight(property, true);
        Rect propertyRect = new Rect(position.x, position.y, position.width, propertyHeight);
        EditorGUI.PropertyField(propertyRect, property, label ?? new GUIContent(property.displayName), true);
        position.y += propertyHeight + VerticalSpacing;
    }
}
