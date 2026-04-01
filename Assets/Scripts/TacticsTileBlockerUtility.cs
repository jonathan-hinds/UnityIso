using UnityEngine;

public static class TacticsTileBlockerUtility
{
    public static bool IsBlockingTile(Vector2Int tile)
    {
        return TacticsTileBlockerRegistry.IsBlockingTile(tile);
    }
}
