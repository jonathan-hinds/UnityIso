using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsOverheadHealthBar : MonoBehaviour
{
    private const float BarWidth = 0.58f;
    private const float BarHeight = 0.055f;
    private const float BorderPadding = 0.016f;
    private const float VerticalPadding = 0.06f;
    private const int SortingOrderOffset = 18;

    private static Sprite sharedSprite;

    private TacticsCharacterController target;
    private Transform barRoot;
    private SpriteRenderer borderRenderer;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private bool isVisible;

    public static TacticsOverheadHealthBar ShowFor(TacticsCharacterController character)
    {
        if (character == null)
        {
            return null;
        }

        TacticsOverheadHealthBar bar = character.GetComponent<TacticsOverheadHealthBar>();
        if (bar == null)
        {
            bar = character.gameObject.AddComponent<TacticsOverheadHealthBar>();
        }

        bar.Bind(character);
        bar.SetVisible(true);
        bar.RefreshVisuals();
        return bar;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(this);
            return;
        }

        if (barRoot == null)
        {
            EnsureVisualTree();
        }

        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (barRoot != null)
        {
            barRoot.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (barRoot != null)
        {
            Destroy(barRoot.gameObject);
        }
    }

    private void Bind(TacticsCharacterController character)
    {
        target = character;
        EnsureVisualTree();
    }

    private void EnsureVisualTree()
    {
        if (barRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("Overhead Health Bar");
        rootObject.transform.SetParent(transform, false);
        barRoot = rootObject.transform;

        borderRenderer = CreatePart(
            "Border",
            barRoot,
            new Vector2(BarWidth + BorderPadding, BarHeight + BorderPadding),
            new Color(0.08f, 0.02f, 0.02f, 0.95f),
            0);

        backgroundRenderer = CreatePart(
            "Background",
            barRoot,
            new Vector2(BarWidth, BarHeight),
            new Color(0.16f, 0.03f, 0.03f, 0.88f),
            1);

        fillRenderer = CreatePart(
            "Fill",
            barRoot,
            new Vector2(BarWidth, BarHeight),
            new Color(0.9f, 0.17f, 0.2f, 0.98f),
            2);

        SetVisible(isVisible);
    }

    private SpriteRenderer CreatePart(
        string objectName,
        Transform parent,
        Vector2 size,
        Color color,
        int sortingOrderOffset)
    {
        GameObject partObject = new GameObject(objectName);
        partObject.transform.SetParent(parent, false);

        SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSharedSprite();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
        renderer.maskInteraction = SpriteMaskInteraction.None;
        renderer.sortingLayerID = 0;
        renderer.sortingOrder = sortingOrderOffset;
        return renderer;
    }

    private void RefreshVisuals()
    {
        if (target == null || barRoot == null)
        {
            return;
        }

        if (!isVisible || !target.IsAlive)
        {
            barRoot.gameObject.SetActive(false);
            return;
        }

        barRoot.gameObject.SetActive(true);
        barRoot.position = target.GetCombatTextSpawnPosition(VerticalPadding);

        int sortingLayerId = target.GetCombatTextSortingLayerId();
        int baseSortingOrder = target.GetCombatTextSortingOrder() + SortingOrderOffset;
        borderRenderer.sortingLayerID = sortingLayerId;
        backgroundRenderer.sortingLayerID = sortingLayerId;
        fillRenderer.sortingLayerID = sortingLayerId;
        borderRenderer.sortingOrder = baseSortingOrder;
        backgroundRenderer.sortingOrder = baseSortingOrder + 1;
        fillRenderer.sortingOrder = baseSortingOrder + 2;

        float healthRatio = target.MaxHitPoints > 0
            ? Mathf.Clamp01((float)target.CurrentHitPoints / target.MaxHitPoints)
            : 0f;
        float fillWidth = Mathf.Max(0f, BarWidth * healthRatio);
        fillRenderer.size = new Vector2(fillWidth, BarHeight);
        fillRenderer.transform.localPosition = new Vector3((-BarWidth + fillWidth) * 0.5f, 0f, 0f);
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (barRoot != null)
        {
            barRoot.gameObject.SetActive(visible);
        }
    }

    private static Sprite GetSharedSprite()
    {
        if (sharedSprite == null)
        {
            sharedSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                Vector4.zero);
        }

        return sharedSprite;
    }
}
