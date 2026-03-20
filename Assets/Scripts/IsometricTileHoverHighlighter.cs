using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class IsometricTileHoverHighlighter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0f)] private float outlinePadding = 0.03f;
    [SerializeField, Min(0.01f)] private float baseLineWidth = 0.05f;
    [SerializeField, Min(0f)] private float pulseAmount = 0.015f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2.5f;
    [SerializeField, Min(0f)] private float dashScrollSpeed = 1.25f;
    [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.3f, 0.95f);
    [SerializeField] private int sortingOrder = 30000;
    [SerializeField] private string overlayShaderName = "Custom/AlwaysOnTopLine";

    private LineRenderer lineRenderer;
    private Material lineMaterial;
    private Texture2D dashTexture;
    private IsometricTileHoverInfo currentTile;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        lineRenderer = gameObject.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        ConfigureLineRenderer();
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        UpdateHoveredTile();
        AnimateHighlight();
    }

    private void ConfigureLineRenderer()
    {
        dashTexture = CreateDashTexture();

        Shader shader = Shader.Find(overlayShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        lineMaterial = new Material(shader);
        lineMaterial.mainTexture = dashTexture;
        if (lineMaterial.HasProperty("_Color"))
        {
            lineMaterial.SetColor("_Color", Color.white);
        }

        lineRenderer.loop = false;
        lineRenderer.useWorldSpace = true;
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = highlightColor;
        lineRenderer.endColor = highlightColor;
        lineRenderer.widthMultiplier = baseLineWidth;
        lineRenderer.positionCount = 5;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.enabled = false;
    }

    private void UpdateHoveredTile()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            currentTile = null;
            lineRenderer.enabled = false;
            return;
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);

        Collider2D[] hits = Physics2D.OverlapPointAll(point);
        IsometricTileHoverInfo hoveredTile = null;
        int bestSortingOrder = int.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            IsometricTileHoverInfo candidate = hits[i].GetComponent<IsometricTileHoverInfo>();
            if (candidate == null)
            {
                continue;
            }

            SpriteRenderer spriteRenderer = hits[i].GetComponent<SpriteRenderer>();
            int candidateSortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : int.MinValue;
            if (candidateSortingOrder <= bestSortingOrder)
            {
                continue;
            }

            bestSortingOrder = candidateSortingOrder;
            hoveredTile = candidate;
        }

        if (hoveredTile == null)
        {
            currentTile = null;
            lineRenderer.enabled = false;
            return;
        }

        if (currentTile != hoveredTile || !lineRenderer.enabled)
        {
            currentTile = hoveredTile;
            Vector3[] corners = currentTile.GetWorldCorners(outlinePadding);
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].z = currentTile.transform.position.z;
            }

            lineRenderer.SetPositions(corners);
            lineRenderer.enabled = true;
        }
    }

    private void AnimateHighlight()
    {
        if (!lineRenderer.enabled || lineMaterial == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float alpha = Mathf.Lerp(0.65f, 1f, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
        Color animatedColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, alpha);

        lineRenderer.widthMultiplier = baseLineWidth * pulse;
        lineRenderer.startColor = animatedColor;
        lineRenderer.endColor = animatedColor;

        Vector2 offset = lineMaterial.mainTextureOffset;
        offset.x = -Time.time * dashScrollSpeed;
        lineMaterial.mainTextureOffset = offset;
    }

    private Texture2D CreateDashTexture()
    {
        const int textureWidth = 64;
        Texture2D texture = new Texture2D(textureWidth, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            name = "TileHoverDashTexture"
        };

        for (int x = 0; x < textureWidth; x++)
        {
            bool isDash = (x / 4) % 2 == 0;
            texture.SetPixel(x, 0, isDash ? Color.white : Color.clear);
        }

        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            Destroy(lineMaterial);
            Destroy(dashTexture);
        }
        else
        {
            DestroyImmediate(lineMaterial);
            DestroyImmediate(dashTexture);
        }
    }
}
