using UnityEngine;

public static class TacticsTileBlockerUtility
{
    public static bool IsBlockingTile(Vector2Int tile)
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ITacticsTileBlocker blocker &&
                blocker.BlocksTile &&
                blocker.GridPosition == tile)
            {
                return true;
            }
        }

        return false;
    }
}
