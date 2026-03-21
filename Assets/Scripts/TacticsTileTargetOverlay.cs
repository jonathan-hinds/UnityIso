using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsTileTargetOverlay : MonoBehaviour
{
    [SerializeField] private Color attackTileColor = new Color(0.88f, 0.18f, 0.2f, 0.45f);
    [SerializeField] private int sortingOrder = 28000;

    private readonly List<SpriteRenderer> overlays = new();
    private readonly Dictionary<Vector2Int, IsometricTileHoverInfo> tileLookup = new();

    private Transform overlayRoot;
    private Sprite tileOverlaySprite;

    private void Awake()
    {
        EnsureResources();
        Hide();
    }

    public void ShowTiles(IReadOnlyList<Vector2Int> tiles)
    {
        EnsureResources();
        RebuildTileLookup();

        if (tiles == null || tiles.Count == 0)
        {
            Hide();
            return;
        }

        EnsureOverlayCount(tiles.Count);

        int shownCount = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (!tileLookup.TryGetValue(tiles[i], out IsometricTileHoverInfo tileInfo) || tileInfo == null)
            {
                continue;
            }

            SpriteRenderer overlay = overlays[shownCount];
            SpriteRenderer tileRenderer = tileInfo.GetComponent<SpriteRenderer>();
            overlay.gameObject.SetActive(true);
            overlay.color = attackTileColor;
            overlay.sortingOrder = sortingOrder;
            overlay.transform.position = tileInfo.transform.position + new Vector3(0f, 0f, 0.01f);
            overlay.transform.rotation = tileInfo.transform.rotation;

            if (tileRenderer != null && tileRenderer.sprite != null)
            {
                // Mirror the actual top tile art so the target area lands exactly on the tile footprint.
                overlay.sprite = tileRenderer.sprite;
                overlay.drawMode = SpriteDrawMode.Simple;
                overlay.size = tileRenderer.size;
                overlay.transform.localScale = tileInfo.transform.lossyScale;
                overlay.flipX = tileRenderer.flipX;
                overlay.flipY = tileRenderer.flipY;
                overlay.sortingLayerID = tileRenderer.sortingLayerID;
            }
            else
            {
                overlay.sprite = tileOverlaySprite;
                overlay.drawMode = SpriteDrawMode.Simple;
                overlay.flipX = false;
                overlay.flipY = false;
                overlay.transform.localScale = new Vector3(tileInfo.TileWidth, tileInfo.TileHeight, 1f);
            }

            shownCount++;
        }

        for (int i = shownCount; i < overlays.Count; i++)
        {
            overlays[i].gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        for (int i = 0; i < overlays.Count; i++)
        {
            if (overlays[i] != null)
            {
                overlays[i].gameObject.SetActive(false);
            }
        }
    }

    private void EnsureResources()
    {
        if (overlayRoot == null)
        {
            GameObject root = new GameObject("Tile Target Overlay Root");
            root.transform.SetParent(transform, false);
            overlayRoot = root.transform;
        }

        tileOverlaySprite ??= CreateOverlaySprite();
    }

    private void RebuildTileLookup()
    {
        tileLookup.Clear();

        IsometricTileHoverInfo[] tiles = FindObjectsByType<IsometricTileHoverInfo>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            IsometricTileHoverInfo tile = tiles[i];
            if (tile == null)
            {
                continue;
            }

            tileLookup[new Vector2Int(tile.GridX, tile.GridY)] = tile;
        }
    }

    private void EnsureOverlayCount(int requiredCount)
    {
        while (overlays.Count < requiredCount)
        {
            GameObject overlayObject = new GameObject($"Tile Overlay {overlays.Count + 1}");
            overlayObject.transform.SetParent(overlayRoot, false);

            SpriteRenderer renderer = overlayObject.AddComponent<SpriteRenderer>();
            renderer.sprite = tileOverlaySprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = attackTileColor;
            overlays.Add(renderer);
        }
    }

    private Sprite CreateOverlaySprite()
    {
        const int textureWidth = 128;
        const int textureHeight = 64;

        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "TargetTileOverlay"
        };

        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        texture.SetPixels(pixels);

        Vector2[] diamond =
        {
            new Vector2(textureWidth * 0.5f, textureHeight - 1f),
            new Vector2(textureWidth - 1f, textureHeight * 0.5f),
            new Vector2(textureWidth * 0.5f, 0f),
            new Vector2(0f, textureHeight * 0.5f)
        };

        FillPolygon(texture, diamond, attackTileColor);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            textureWidth);
    }

    private static void FillPolygon(Texture2D texture, IReadOnlyList<Vector2> points, Color fillColor)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(GetMin(points, point => point.x)), 0, texture.width - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(GetMax(points, point => point.x)), 0, texture.width - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(GetMin(points, point => point.y)), 0, texture.height - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(GetMax(points, point => point.y)), 0, texture.height - 1);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (IsPointInsidePolygon(new Vector2(x + 0.5f, y + 0.5f), points))
                {
                    texture.SetPixel(x, y, fillColor);
                }
            }
        }
    }

    private static bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool isInside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            bool crosses = ((a.y > point.y) != (b.y > point.y)) &&
                           (point.x < ((b.x - a.x) * (point.y - a.y) / ((b.y - a.y) + 0.0001f)) + a.x);
            if (crosses)
            {
                isInside = !isInside;
            }
        }

        return isInside;
    }

    private static float GetMin(IReadOnlyList<Vector2> points, System.Func<Vector2, float> selector)
    {
        float value = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            value = Mathf.Min(value, selector(points[i]));
        }

        return value;
    }

    private static float GetMax(IReadOnlyList<Vector2> points, System.Func<Vector2, float> selector)
    {
        float value = float.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            value = Mathf.Max(value, selector(points[i]));
        }

        return value;
    }
}
