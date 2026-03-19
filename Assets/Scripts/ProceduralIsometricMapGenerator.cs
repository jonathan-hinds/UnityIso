using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProceduralIsometricMapGenerator : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int length = 10;
    [SerializeField, Min(1)] private int width = 10;
    [SerializeField, Min(0)] private int minElevation = 0;
    [SerializeField, Min(0)] private int maxElevation = 4;

    [Header("Noise")]
    [SerializeField, Min(0.01f)] private float noiseScale = 5f;
    [SerializeField, Range(1, 6)] private int noiseOctaves = 3;
    [SerializeField, Range(0.1f, 1f)] private float persistence = 0.5f;
    [SerializeField, Min(1f)] private float lacunarity = 2f;
    [SerializeField, Range(0, 4)] private int smoothingPasses = 1;

    [Header("Tile Art")]
    [SerializeField] private Sprite topSprite;
    [SerializeField] private Sprite leftSideSprite;
    [SerializeField] private Sprite rightSideSprite;
    [SerializeField] private Color defaultTopColor = new Color(0.40f, 0.74f, 0.33f, 1f);
    [SerializeField] private Color defaultLeftSideColor = new Color(0.26f, 0.49f, 0.22f, 1f);
    [SerializeField] private Color defaultRightSideColor = new Color(0.20f, 0.38f, 0.17f, 1f);
    [SerializeField] private Color defaultOutlineColor = new Color(0.12f, 0.20f, 0.10f, 1f);

    [Header("Layout")]
    [SerializeField, Min(0.1f)] private float tileWidth = 1f;
    [SerializeField, Min(0.1f)] private float tileHeight = 0.5f;
    [SerializeField, Min(0.05f)] private float elevationStep = 0.5f;
    [SerializeField] private Vector3 mapOffset = Vector3.zero;

    [Header("Scene")]
    [SerializeField] private bool autoFrameCamera = true;
    [SerializeField, Min(0f)] private float cameraPadding = 1f;

    private const string GeneratedRootName = "Generated Isometric Map";
    private const int DefaultSpritePixels = 128;
    private const float DefaultPixelsPerUnit = 128f;

    private static Sprite cachedDefaultTopSprite;
    private static Sprite cachedDefaultLeftSideSprite;
    private static Sprite cachedDefaultRightSideSprite;

    private readonly List<SpriteRenderer> spawnedRenderers = new List<SpriteRenderer>();

    private void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        GenerateMap();
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ValidateSettings();
        EnsureDefaultSprites();
        ClearGeneratedMap();
        spawnedRenderers.Clear();

        int[,] generatedHeights = GenerateHeights();
        Transform generatedRoot = CreateGeneratedRoot();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                int height = generatedHeights[x, y];
                if (height <= 0)
                {
                    continue;
                }

                Vector3 topPosition = GridToWorld(x, y, height);
                CreateTilePart(
                    generatedRoot,
                    topSprite != null ? topSprite : cachedDefaultTopSprite,
                    topPosition,
                    $"Top_{x}_{y}_{height}",
                    x,
                    y,
                    height,
                    2);
                ConfigureTopTile(partName: $"Top_{x}_{y}_{height}", generatedRoot: generatedRoot, gridX: x, gridY: y, height: height);

                // Render the two player-facing faces for the tile column. In an isometric
                // heightfield, the visible walls come from the neighbors in front of the
                // current tile on screen, not the tiles behind it. Comparing against the
                // back neighbors creates contradictory depth cues and leads to the
                // impossible-staircase / Penrose-style artifact.
                int leftNeighborHeight = GetHeight(generatedHeights, x, y + 1);
                int rightNeighborHeight = GetHeight(generatedHeights, x + 1, y);
                for (int layer = leftNeighborHeight + 1; layer <= height; layer++)
                {
                    Vector3 sidePosition = GridToWorld(x, y, layer);
                    CreateTilePart(
                        generatedRoot,
                        leftSideSprite != null ? leftSideSprite : cachedDefaultLeftSideSprite,
                        sidePosition,
                        $"Left_{x}_{y}_{layer}",
                        x,
                        y,
                        layer,
                        0);
                }

                for (int layer = rightNeighborHeight + 1; layer <= height; layer++)
                {
                    Vector3 sidePosition = GridToWorld(x, y, layer);
                    CreateTilePart(
                        generatedRoot,
                        rightSideSprite != null ? rightSideSprite : cachedDefaultRightSideSprite,
                        sidePosition,
                        $"Right_{x}_{y}_{layer}",
                        x,
                        y,
                        layer,
                        1);
                }
            }
        }

        if (autoFrameCamera)
        {
            FrameMainCamera();
        }
    }

    private void ValidateSettings()
    {
        if (maxElevation < minElevation)
        {
            maxElevation = minElevation;
        }
    }

    private Transform CreateGeneratedRoot()
    {
        GameObject root = new GameObject(GeneratedRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        return root.transform;
    }

    private void ClearGeneratedMap()
    {
        Transform existingRoot = transform.Find(GeneratedRootName);
        if (existingRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existingRoot.gameObject);
        }
        else
        {
            DestroyImmediate(existingRoot.gameObject);
        }
    }

    private int[,] GenerateHeights()
    {
        int[,] heights = new int[width, length];
        float[,] samples = new float[width, length];
        System.Random random = new System.Random(seed);
        Vector2 seedOffset = new Vector2(random.Next(-100000, 100000), random.Next(-100000, 100000));

        float minSample = float.MaxValue;
        float maxSample = float.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float sample = 0f;
                float amplitudeSum = 0f;

                for (int octave = 0; octave < noiseOctaves; octave++)
                {
                    float sampleX = ((x + seedOffset.x) / noiseScale) * frequency;
                    float sampleY = ((y + seedOffset.y) / noiseScale) * frequency;
                    float perlin = Mathf.PerlinNoise(sampleX, sampleY);
                    sample += perlin * amplitude;
                    amplitudeSum += amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                if (amplitudeSum > 0f)
                {
                    sample /= amplitudeSum;
                }

                samples[x, y] = sample;
                minSample = Mathf.Min(minSample, sample);
                maxSample = Mathf.Max(maxSample, sample);
            }
        }

        for (int pass = 0; pass < smoothingPasses; pass++)
        {
            samples = SmoothSamples(samples);
        }

        if (Mathf.Approximately(maxSample, minSample))
        {
            maxSample = minSample + 0.0001f;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                float normalized = Mathf.InverseLerp(minSample, maxSample, samples[x, y]);
                heights[x, y] = Mathf.RoundToInt(Mathf.Lerp(minElevation, maxElevation, normalized));
            }
        }

        return heights;
    }

    private float[,] SmoothSamples(float[,] source)
    {
        float[,] smoothed = new float[width, length];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                float total = 0f;
                int count = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int sampleX = x + dx;
                        int sampleY = y + dy;
                        if (sampleX < 0 || sampleX >= width || sampleY < 0 || sampleY >= length)
                        {
                            continue;
                        }

                        total += source[sampleX, sampleY];
                        count++;
                    }
                }

                smoothed[x, y] = count > 0 ? total / count : source[x, y];
            }
        }

        return smoothed;
    }

    private int GetHeight(int[,] heights, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= length)
        {
            return 0;
        }

        return heights[x, y];
    }

    private Vector3 GridToWorld(int x, int y, int elevation)
    {
        float worldX = (x - y) * (tileWidth * 0.5f);
        float worldY = (x + y) * (tileHeight * 0.5f) + (elevation * elevationStep);
        return new Vector3(worldX, worldY, 0f) + mapOffset;
    }

    private void CreateTilePart(
        Transform parent,
        Sprite sprite,
        Vector3 position,
        string objectName,
        int gridX,
        int gridY,
        int layer,
        int sortBias)
    {
        GameObject part = new GameObject(objectName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = CalculateSortingOrder(gridX, gridY, layer, sortBias);
        spawnedRenderers.Add(renderer);
    }

    private void ConfigureTopTile(string partName, Transform generatedRoot, int gridX, int gridY, int height)
    {
        Transform topTransform = generatedRoot.Find(partName);
        if (topTransform == null)
        {
            return;
        }

        PolygonCollider2D collider = topTransform.gameObject.AddComponent<PolygonCollider2D>();
        collider.points = new[]
        {
            new Vector2(0f, tileHeight * 0.5f),
            new Vector2(tileWidth * 0.5f, 0f),
            new Vector2(0f, -tileHeight * 0.5f),
            new Vector2(-tileWidth * 0.5f, 0f)
        };

        IsometricTileHoverInfo hoverInfo = topTransform.gameObject.AddComponent<IsometricTileHoverInfo>();
        hoverInfo.Initialize(gridX, gridY, height, tileWidth, tileHeight);
    }

    private int CalculateSortingOrder(int gridX, int gridY, int layer, int sortBias)
    {
        // Sort by screen depth first (back to front), then stabilize ties so a full
        // column behaves like stacked blocks instead of interleaved paper cutouts.
        int maxDepth = width + length + maxElevation + 2;
        int tileDepth = gridX + gridY + layer;
        int backToFrontOrder = (maxDepth - tileDepth) * 100;
        int rowTieBreak = (length - gridY) * 10;
        int columnTieBreak = (width - gridX) * 2;
        return backToFrontOrder + rowTieBreak + columnTieBreak + sortBias;
    }

    private void FrameMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || spawnedRenderers.Count == 0)
        {
            return;
        }

        Bounds bounds = spawnedRenderers[0].bounds;
        for (int i = 1; i < spawnedRenderers.Count; i++)
        {
            bounds.Encapsulate(spawnedRenderers[i].bounds);
        }

        mainCamera.orthographic = true;
        Vector3 position = mainCamera.transform.position;
        position.x = bounds.center.x;
        position.y = bounds.center.y;
        mainCamera.transform.position = position;

        float verticalSize = bounds.extents.y + cameraPadding;
        float horizontalSize = (bounds.extents.x + cameraPadding) / Mathf.Max(0.01f, mainCamera.aspect);
        mainCamera.orthographicSize = Mathf.Max(verticalSize, horizontalSize, 1f);
    }

    private void EnsureDefaultSprites()
    {
        if (cachedDefaultTopSprite == null)
        {
            cachedDefaultTopSprite = CreateTopSprite();
        }

        if (cachedDefaultLeftSideSprite == null)
        {
            cachedDefaultLeftSideSprite = CreateLeftSideSprite();
        }

        if (cachedDefaultRightSideSprite == null)
        {
            cachedDefaultRightSideSprite = CreateRightSideSprite();
        }
    }

    private Sprite CreateTopSprite()
    {
        Texture2D texture = CreateTransparentTexture();
        Vector2[] diamond =
        {
            new Vector2(64f, 96f),
            new Vector2(127f, 64f),
            new Vector2(64f, 32f),
            new Vector2(0f, 64f)
        };

        FillPolygon(texture, diamond, defaultTopColor);
        return FinalizeSprite(texture, "Default_Isometric_Top");
    }

    private Sprite CreateLeftSideSprite()
    {
        Texture2D texture = CreateTransparentTexture(128, 192);
        Vector2[] leftFace =
        {
            new Vector2(0f, 96f),
            new Vector2(64f, 64f),
            new Vector2(64f, 0f),
            new Vector2(0f, 32f)
        };

        FillPolygon(texture, leftFace, defaultLeftSideColor);
        StrokePolygon(texture, leftFace, defaultOutlineColor);
        return FinalizeSprite(texture, "Default_Isometric_Left");
    }

    private Sprite CreateRightSideSprite()
    {
        Texture2D texture = CreateTransparentTexture(128, 192);
        Vector2[] rightFace =
        {
            new Vector2(64f, 64f),
            new Vector2(127f, 96f),
            new Vector2(127f, 32f),
            new Vector2(64f, 0f)
        };

        FillPolygon(texture, rightFace, defaultRightSideColor);
        StrokePolygon(texture, rightFace, defaultOutlineColor);
        return FinalizeSprite(texture, "Default_Isometric_Right");
    }

    private Texture2D CreateTransparentTexture()
    {
        return CreateTransparentTexture(DefaultSpritePixels, DefaultSpritePixels);
    }

    private Texture2D CreateTransparentTexture(int widthPixels, int heightPixels)
    {
        Texture2D texture = new Texture2D(widthPixels, heightPixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Generated_Isometric_Texture"
        };

        Color[] pixels = new Color[widthPixels * heightPixels];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        texture.SetPixels(pixels);
        return texture;
    }

    private Sprite FinalizeSprite(Texture2D texture, string spriteName)
    {
        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            DefaultPixelsPerUnit);
        sprite.name = spriteName;
        return sprite;
    }

    private void FillPolygon(Texture2D texture, IReadOnlyList<Vector2> points, Color fillColor)
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

    private void StrokePolygon(Texture2D texture, IReadOnlyList<Vector2> points, Color strokeColor)
    {
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Count];
            DrawLine(texture, start, end, strokeColor);
        }
    }

    private void DrawLine(Texture2D texture, Vector2 start, Vector2 end, Color color)
    {
        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);

        int deltaX = Mathf.Abs(x1 - x0);
        int deltaY = Mathf.Abs(y1 - y0);
        int stepX = x0 < x1 ? 1 : -1;
        int stepY = y0 < y1 ? 1 : -1;
        int error = deltaX - deltaY;

        while (true)
        {
            if (x0 >= 0 && x0 < texture.width && y0 >= 0 && y0 < texture.height)
            {
                texture.SetPixel(x0, y0, color);
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int doubledError = error * 2;
            if (doubledError > -deltaY)
            {
                error -= deltaY;
                x0 += stepX;
            }

            if (doubledError < deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    private bool IsPointInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
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

    private float GetMin(IReadOnlyList<Vector2> points, Func<Vector2, float> selector)
    {
        float value = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            value = Mathf.Min(value, selector(points[i]));
        }

        return value;
    }

    private float GetMax(IReadOnlyList<Vector2> points, Func<Vector2, float> selector)
    {
        float value = float.MinValue;
        for (int i = 0; i < points.Count; i++)
        {
            value = Mathf.Max(value, selector(points[i]));
        }

        return value;
    }
}
