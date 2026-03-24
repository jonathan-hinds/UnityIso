using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsAbilityTooltipView : MonoBehaviour
{
    private const float EdgePadding = 12f;
    private static readonly Vector2 PointerOffset = new(26f, -20f);

    [Header("Theme")]
    [SerializeField] private Color panelColor = new(0.07f, 0.08f, 0.1f, 0.72f);
    [SerializeField] private Color innerPanelColor = new(1f, 1f, 1f, 0.035f);
    [SerializeField] private Color borderColor = new(0.86f, 0.81f, 0.68f, 1f);
    [SerializeField] private Color innerBorderColor = new(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.26f);
    [SerializeField] private Color titleColor = new(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color metaColor = new(0.76f, 0.69f, 0.5f, 1f);
    [SerializeField] private Color bodyColor = new(0.91f, 0.91f, 0.92f, 1f);
    [SerializeField] private Color footerColor = new(0.72f, 0.74f, 0.79f, 1f);
    [SerializeField] private Color dividerColor = new(0.76f, 0.69f, 0.5f, 0.72f);
    [SerializeField] private Color accentBarColor = new(0.76f, 0.69f, 0.5f, 0.92f);

    private Canvas parentCanvas;
    private RectTransform panelRect;
    private RectTransform chromeRect;
    private Text titleText;
    private Text metaText;
    private Text bodyText;
    private Text footerText;
    private Font sharedFont;

    private void Awake()
    {
        EnsureBuilt();
        Hide();
    }

    public void Show(TacticsAbilityTooltipContent content, Vector2 screenPosition, Canvas canvas)
    {
        if (!content.IsValid)
        {
            Hide();
            return;
        }

        EnsureBuilt();
        AssignCanvas(canvas);

        titleText.text = content.Title;
        metaText.text = content.Meta;
        bodyText.text = content.Body;
        footerText.text = content.Footer;
        footerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.Footer));

        panelRect.gameObject.SetActive(true);
        panelRect.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        Canvas.ForceUpdateCanvases();
        UpdatePosition(screenPosition);
    }

    public void UpdatePosition(Vector2 screenPosition)
    {
        if (panelRect == null || !panelRect.gameObject.activeSelf)
        {
            return;
        }

        EnsureBuilt();
        Rect screenBounds = parentCanvas != null
            ? parentCanvas.pixelRect
            : new Rect(0f, 0f, Screen.width, Screen.height);

        if (screenBounds.width <= 0f || screenBounds.height <= 0f)
        {
            return;
        }

        Vector2 preferredSize = LayoutUtility.GetPreferredSize(panelRect, 0) > 0f || LayoutUtility.GetPreferredSize(panelRect, 1) > 0f
            ? new Vector2(LayoutUtility.GetPreferredSize(panelRect, 0), LayoutUtility.GetPreferredSize(panelRect, 1))
            : panelRect.rect.size;
        if (preferredSize.x <= 0f || preferredSize.y <= 0f)
        {
            preferredSize = new Vector2(420f, 180f);
        }

        float minX = screenBounds.xMin + EdgePadding;
        float maxX = screenBounds.xMax - preferredSize.x - EdgePadding;
        float minY = screenBounds.yMin + preferredSize.y + EdgePadding;
        float maxY = screenBounds.yMax - EdgePadding;

        Vector2 clampedScreenPosition = new(
            Mathf.Clamp(screenPosition.x + PointerOffset.x, minX, maxX),
            Mathf.Clamp(screenPosition.y + PointerOffset.y, minY, maxY));

        panelRect.position = clampedScreenPosition;
    }

    public void Hide()
    {
        EnsureBuilt();
        panelRect.gameObject.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (panelRect != null)
        {
            return;
        }

        sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (sharedFont == null)
        {
            sharedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        GameObject panelObject = CreateUiObject("AbilityTooltipPanel", transform);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(420f, 0f);

        LayoutElement layoutElement = panelObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 420f;
        layoutElement.minWidth = 420f;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = panelObject.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(0f, -6f);

        VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 16);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = panelObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject chromeObject = CreateUiObject("Chrome", panelObject.transform);
        chromeRect = chromeObject.GetComponent<RectTransform>();
        chromeRect.SetAsFirstSibling();
        chromeRect.anchorMin = Vector2.zero;
        chromeRect.anchorMax = Vector2.one;
        chromeRect.offsetMin = new Vector2(7f, 7f);
        chromeRect.offsetMax = new Vector2(-7f, -7f);

        Image chromeImage = chromeObject.AddComponent<Image>();
        chromeImage.color = innerPanelColor;
        chromeImage.raycastTarget = false;

        Outline chromeOutline = chromeObject.AddComponent<Outline>();
        chromeOutline.effectColor = innerBorderColor;
        chromeOutline.effectDistance = new Vector2(1f, -1f);

        GameObject accentBarObject = CreateUiObject("AccentBar", panelObject.transform);
        LayoutElement accentBarLayout = accentBarObject.AddComponent<LayoutElement>();
        accentBarLayout.preferredHeight = 3f;
        accentBarLayout.minHeight = 3f;

        Image accentBarImage = accentBarObject.AddComponent<Image>();
        accentBarImage.color = accentBarColor;
        accentBarImage.raycastTarget = false;

        titleText = CreateText("Title", panelObject.transform, 20, FontStyle.Bold, titleColor, TextAnchor.UpperLeft);
        metaText = CreateText("Meta", panelObject.transform, 14, FontStyle.Bold, metaColor, TextAnchor.UpperLeft);

        GameObject dividerObject = CreateUiObject("Divider", panelObject.transform);
        LayoutElement dividerLayout = dividerObject.AddComponent<LayoutElement>();
        dividerLayout.preferredHeight = 2f;
        dividerLayout.minHeight = 2f;
        Image dividerImage = dividerObject.AddComponent<Image>();
        dividerImage.color = dividerColor;
        dividerImage.raycastTarget = false;

        bodyText = CreateText("Body", panelObject.transform, 15, FontStyle.Normal, bodyColor, TextAnchor.UpperLeft);
        footerText = CreateText("Footer", panelObject.transform, 13, FontStyle.Italic, footerColor, TextAnchor.UpperLeft);
    }

    private void AssignCanvas(Canvas canvas)
    {
        if (canvas != null)
        {
            parentCanvas = canvas;
        }
        else if (parentCanvas == null)
        {
            parentCanvas = GetComponent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
        }

    }

    private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = sharedFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}

public readonly struct TacticsAbilityTooltipContent
{
    public TacticsAbilityTooltipContent(string title, string meta, string body, string footer = "")
    {
        Title = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
        Meta = string.IsNullOrWhiteSpace(meta) ? string.Empty : meta.Trim();
        Body = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();
        Footer = string.IsNullOrWhiteSpace(footer) ? string.Empty : footer.Trim();
    }

    public string Title { get; }
    public string Meta { get; }
    public string Body { get; }
    public string Footer { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Body);
}
