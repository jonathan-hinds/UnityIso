using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsFloatingCombatText : MonoBehaviour
{
    private static readonly Vector3[] OutlineDirections =
    {
        Vector3.left,
        Vector3.right,
        Vector3.up,
        Vector3.down
    };

    [SerializeField, Min(0.1f)] private float lifetime = 1.05f;
    [SerializeField, Min(0.01f)] private float burstDuration = 0.18f;
    [SerializeField, Min(0f)] private float burstRiseDistance = 0.42f;
    [SerializeField, Min(0f)] private float driftRiseDistance = 0.38f;
    [SerializeField, Min(0f)] private float swayDistance = 0.08f;
    [SerializeField, Range(0f, 1f)] private float fadeStartNormalized = 0.28f;
    [SerializeField, Min(0f)] private float baseCharacterSize = 0.045f;
    [SerializeField, Min(0f)] private float outlineThickness = 0.018f;
    [SerializeField] private Color fillColor = Color.white;
    [SerializeField] private Color outlineColor = Color.black;

    private readonly TextMesh[] textMeshes = new TextMesh[5];
    private readonly MeshRenderer[] textRenderers = new MeshRenderer[5];

    private Vector3 originPosition;
    private Vector3 swayDirection;
    private float elapsed;
    private float swayPhase;
    private Camera mainCamera;

    public static TacticsFloatingCombatText Create(
        Transform parent,
        Vector3 worldPosition,
        string text,
        int sortingLayerId,
        int sortingOrder)
    {
        GameObject textObject = new GameObject($"Damage Text {text}");
        if (parent != null)
        {
            textObject.transform.SetParent(parent, false);
        }

        TacticsFloatingCombatText floatingText = textObject.AddComponent<TacticsFloatingCombatText>();
        floatingText.Initialize(worldPosition, text, sortingLayerId, sortingOrder);
        return floatingText;
    }

    private void Initialize(Vector3 worldPosition, string text, int sortingLayerId, int sortingOrder)
    {
        originPosition = worldPosition;
        transform.position = worldPosition;

        float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
        swayDirection = new Vector3(horizontalDirection, 0f, 0f);
        swayPhase = Random.Range(0f, Mathf.PI);

        Font font = LoadBuiltinFont();
        CreateTextMesh(text, font, fillColor, sortingLayerId, sortingOrder, 0, Vector3.zero);

        for (int i = 0; i < OutlineDirections.Length; i++)
        {
            CreateTextMesh(
                text,
                font,
                outlineColor,
                sortingLayerId,
                sortingOrder - 1,
                i + 1,
                OutlineDirections[i] * outlineThickness);
        }

        ApplyAlpha(1f);
        transform.localScale = Vector3.one * 0.68f;
        AlignToCamera();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float normalized = Mathf.Clamp01(elapsed / lifetime);

        float burstT = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, burstDuration));
        float driftT = Mathf.Clamp01((elapsed - burstDuration) / Mathf.Max(0.0001f, lifetime - burstDuration));

        float rise = EaseOutCubic(burstT) * burstRiseDistance;
        rise += EaseOutQuad(driftT) * driftRiseDistance;

        float sway = Mathf.Sin((normalized * 5.5f) + swayPhase) * swayDistance * (1f - normalized);
        transform.position = originPosition + (swayDirection * sway) + (Vector3.up * rise);

        float scale = normalized < 0.18f
            ? Mathf.Lerp(0.68f, 1.1f, EaseOutBack(normalized / 0.18f))
            : Mathf.Lerp(1.1f, 0.96f, driftT);
        transform.localScale = Vector3.one * scale;

        float alpha = normalized < fadeStartNormalized
            ? 1f
            : 1f - Mathf.Clamp01((normalized - fadeStartNormalized) / Mathf.Max(0.0001f, 1f - fadeStartNormalized));
        ApplyAlpha(alpha);

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        AlignToCamera();
    }

    private void CreateTextMesh(
        string text,
        Font font,
        Color color,
        int sortingLayerId,
        int sortingOrder,
        int slotIndex,
        Vector3 localOffset)
    {
        GameObject meshObject = slotIndex == 0 ? gameObject : new GameObject($"Outline {slotIndex}");
        if (slotIndex != 0)
        {
            meshObject.transform.SetParent(transform, false);
        }

        meshObject.transform.localPosition = localOffset;

        TextMesh textMesh = meshObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.font = font;
        textMesh.fontSize = 64;
        textMesh.characterSize = baseCharacterSize;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;
        textMesh.richText = false;

        MeshRenderer renderer = meshObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font != null ? font.material : renderer.sharedMaterial;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;

        textMeshes[slotIndex] = textMesh;
        textRenderers[slotIndex] = renderer;
    }

    private static Font LoadBuiltinFont()
    {
        Font font = null;

        try
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (System.ArgumentException)
        {
            // Fall through to the older built-in name for compatibility with older Unity versions.
        }

        if (font == null)
        {
            try
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (System.ArgumentException)
            {
                // If neither built-in font is available, Unity will surface missing glyphs rather than breaking combat.
            }
        }

        return font;
    }

    private void ApplyAlpha(float alpha)
    {
        for (int i = 0; i < textMeshes.Length; i++)
        {
            if (textMeshes[i] == null)
            {
                continue;
            }

            Color color = i == 0 ? fillColor : outlineColor;
            color.a = alpha;
            textMeshes[i].color = color;
        }
    }

    private void AlignToCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        transform.rotation = mainCamera.transform.rotation;
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - Mathf.Clamp01(t);
        return 1f - (inverse * inverse * inverse);
    }

    private static float EaseOutQuad(float t)
    {
        float clamped = Mathf.Clamp01(t);
        return 1f - ((1f - clamped) * (1f - clamped));
    }

    private static float EaseOutBack(float t)
    {
        float clamped = Mathf.Clamp01(t);
        const float overshoot = 1.70158f;
        float adjusted = clamped - 1f;
        return 1f + ((overshoot + 1f) * adjusted * adjusted * adjusted) + (overshoot * adjusted * adjusted);
    }
}
