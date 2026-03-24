using UnityEngine;

public interface ITacticsCombatTextAnchor
{
    Vector3 GetCombatTextSpawnPosition(float verticalPadding = 0.18f);
    int GetCombatTextSortingLayerId();
    int GetCombatTextSortingOrder();
}
