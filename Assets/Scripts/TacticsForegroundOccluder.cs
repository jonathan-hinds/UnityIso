using UnityEngine;

[DisallowMultipleComponent]
public class TacticsForegroundOccluder : MonoBehaviour
{
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int Elevation { get; private set; }
    public int SortBias { get; private set; }

    public bool IsTopSurface => SortBias == 2;
    public bool IsSideFace => SortBias == 0 || SortBias == 1;

    public void Initialize(int gridX, int gridY, int elevation, int sortBias)
    {
        GridX = gridX;
        GridY = gridY;
        Elevation = elevation;
        SortBias = sortBias;
    }
}
