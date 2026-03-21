using UnityEngine;

[DisallowMultipleComponent]
public class TacticsForegroundOccluder : MonoBehaviour
{
    private Vector2[] occlusionPolygon;

    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int Elevation { get; private set; }
    public int SortBias { get; private set; }

    public bool IsTopSurface => SortBias == 2;
    public bool IsSideFace => SortBias == 0 || SortBias == 1;
    public bool IsTopEdgeShadow => SortBias >= 3;

    public void Initialize(int gridX, int gridY, int elevation, int sortBias)
    {
        GridX = gridX;
        GridY = gridY;
        Elevation = elevation;
        SortBias = sortBias;
    }

    public void SetOcclusionPolygon(Vector2[] polygonPoints)
    {
        occlusionPolygon = polygonPoints;
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (occlusionPolygon == null || occlusionPolygon.Length < 3)
        {
            return false;
        }

        Vector2 localPoint = transform.InverseTransformPoint(worldPoint);
        return IsPointInsidePolygon(localPoint, occlusionPolygon);
    }

    private static bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int previousIndex = polygon.Length - 1;
        for (int currentIndex = 0; currentIndex < polygon.Length; currentIndex++)
        {
            Vector2 current = polygon[currentIndex];
            Vector2 previous = polygon[previousIndex];

            bool intersects = ((current.y > point.y) != (previous.y > point.y)) &&
                              (point.x < ((previous.x - current.x) * (point.y - current.y) / ((previous.y - current.y) + Mathf.Epsilon)) + current.x);
            if (intersects)
            {
                inside = !inside;
            }

            previousIndex = currentIndex;
        }

        return inside;
    }
}
