using UnityEngine;

[DisallowMultipleComponent]
public class IsometricTileHoverInfo : MonoBehaviour
{
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int Elevation { get; private set; }
    public float TileWidth { get; private set; }
    public float TileHeight { get; private set; }

    public void Initialize(int gridX, int gridY, int elevation, float tileWidth, float tileHeight)
    {
        GridX = gridX;
        GridY = gridY;
        Elevation = elevation;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
    }

    public Vector3[] GetWorldCorners(float padding = 0f)
    {
        float halfWidth = (TileWidth * 0.5f) + padding;
        float halfHeight = (TileHeight * 0.5f) + padding;
        Vector3 center = transform.position;

        return new[]
        {
            center + new Vector3(0f, halfHeight, 0f),
            center + new Vector3(halfWidth, 0f, 0f),
            center + new Vector3(0f, -halfHeight, 0f),
            center + new Vector3(-halfWidth, 0f, 0f),
            center + new Vector3(0f, halfHeight, 0f)
        };
    }
}
