using UnityEngine;

public interface ITacticsTileBlocker
{
    Vector2Int GridPosition { get; }
    bool BlocksTile { get; }
    int CurrentSortingOrder { get; }
}
