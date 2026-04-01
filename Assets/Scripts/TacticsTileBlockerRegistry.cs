using System.Collections.Generic;
using UnityEngine;

public static class TacticsTileBlockerRegistry
{
    private struct TileBlockerEntry
    {
        public Vector2Int Tile;
        public bool BlocksTile;
    }

    private static readonly Dictionary<ITacticsTileBlocker, TileBlockerEntry> blockerStates = new();
    private static readonly Dictionary<Vector2Int, int> blockerCountsByTile = new();

    public static void Register(ITacticsTileBlocker blocker)
    {
        if (blocker == null)
        {
            return;
        }

        RemoveFromTile(blocker);

        TileBlockerEntry entry = new TileBlockerEntry
        {
            Tile = blocker.GridPosition,
            BlocksTile = blocker.BlocksTile
        };

        blockerStates[blocker] = entry;
        if (entry.BlocksTile)
        {
            AddToTile(entry.Tile);
        }
    }

    public static void Refresh(ITacticsTileBlocker blocker)
    {
        Register(blocker);
    }

    public static void Unregister(ITacticsTileBlocker blocker)
    {
        if (blocker == null)
        {
            return;
        }

        RemoveFromTile(blocker);
        blockerStates.Remove(blocker);
    }

    public static bool IsBlockingTile(Vector2Int tile)
    {
        return blockerCountsByTile.TryGetValue(tile, out int count) && count > 0;
    }

    private static void AddToTile(Vector2Int tile)
    {
        blockerCountsByTile[tile] = blockerCountsByTile.TryGetValue(tile, out int count)
            ? count + 1
            : 1;
    }

    private static void RemoveFromTile(ITacticsTileBlocker blocker)
    {
        if (!blockerStates.TryGetValue(blocker, out TileBlockerEntry previous) || !previous.BlocksTile)
        {
            return;
        }

        if (!blockerCountsByTile.TryGetValue(previous.Tile, out int count))
        {
            return;
        }

        if (count <= 1)
        {
            blockerCountsByTile.Remove(previous.Tile);
            return;
        }

        blockerCountsByTile[previous.Tile] = count - 1;
    }
}
