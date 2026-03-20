using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsSelectionPanelView : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);
    [SerializeField] private Color panelEdgeColor = new Color(0.88f, 0.84f, 0.72f, 1f);
    [SerializeField] private Color panelShadowColor = new Color(0f, 0f, 0f, 0.22f);
    [SerializeField] private Color primaryTextColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color accentColor = new Color(0.76f, 0.69f, 0.5f, 1f);
    [SerializeField] private Color barBackgroundColor = new Color(0.12f, 0.13f, 0.15f, 1f);
    [SerializeField] private Color barFrameColor = new Color(0.88f, 0.84f, 0.72f, 0.45f);

    private Canvas rootCanvas;
    private GameObject panelRoot;
    private Text nameText;
    private SelectionBarWidgets healthBar;
    private SelectionBarWidgets manaBar;
    private SelectionBarWidgets staminaBar;
    private Font sharedFont;

    private void Awake()
    {
        EnsureBuilt();
        Hide();
    }

    public void Show(ITacticsSelectionHudTarget selectionTarget)
    {
        EnsureBuilt();

        if (selectionTarget == null)
        {
            Hide();
            return;
        }

        Show(selectionTarget.BuildSelectionHudData());
    }

    public void Show(TacticsSelectionHudData hudData)
    {
        EnsureBuilt();
        panelRoot.SetActive(true);
        nameText.text = string.IsNullOrWhiteSpace(hudData.DisplayName)
            ? "UNIT"
            : hudData.DisplayName.ToUpperInvariant();

        ApplyBar(healthBar, hudData.Health);
        ApplyBar(staminaBar, hudData.Stamina);
        ApplyBar(manaBar, hudData.Mana);
    }

    public void Hide()
    {
        EnsureBuilt();
        panelRoot.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (panelRoot != null)
        {
            return;
        }

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 4995;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (sharedFont == null)
        {
            sharedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        panelRoot = CreateUiObject("SelectionPanel", transform);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(36f, 36f);
        panelRect.sizeDelta = new Vector2(576f, 232f);

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        Outline panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = panelEdgeColor;
        panelOutline.effectDistance = new Vector2(1f, -1f);

        Shadow panelShadow = panelRoot.AddComponent<Shadow>();
        panelShadow.effectColor = panelShadowColor;
        panelShadow.effectDistance = new Vector2(0f, -4f);

        GameObject divider = CreateUiObject("HeaderDivider", panelRoot.transform);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.offsetMin = new Vector2(28f, -72f);
        dividerRect.offsetMax = new Vector2(-28f, -68f);

        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = accentColor;

        nameText = CreateText("Name", panelRoot.transform, 40, FontStyle.Bold, primaryTextColor);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.offsetMin = new Vector2(28f, -24f);
        nameRect.offsetMax = new Vector2(-28f, 24f);
        nameText.alignment = TextAnchor.UpperCenter;

        healthBar = CreateSelectionBar("HealthBar", panelRoot.transform, 28f, 100f);
        staminaBar = CreateSelectionBar("StaminaBar", panelRoot.transform, 28f, 56f);
        manaBar = CreateSelectionBar("ManaBar", panelRoot.transform, 28f, 12f);
    }

    private void ApplyBar(SelectionBarWidgets widgets, TacticsSelectionHudResourceData resourceData)
    {
        widgets.Label.text = resourceData.Label.ToUpperInvariant();
        widgets.Value.text = $"{resourceData.CurrentValue}/{resourceData.MaxValue}";
        widgets.Fill.color = resourceData.FillColor;
        widgets.Fill.fillAmount = resourceData.FillNormalized;
    }

    private SelectionBarWidgets CreateSelectionBar(string objectName, Transform parent, float leftInset, float bottomInset)
    {
        GameObject rowRoot = CreateUiObject(objectName, parent);
        RectTransform rowRect = rowRoot.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.offsetMin = new Vector2(leftInset, bottomInset);
        rowRect.offsetMax = new Vector2(-28f, bottomInset + 36f);

        Text labelText = CreateText("Label", rowRoot.transform, 30, FontStyle.Bold, secondaryTextColor);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(68f, 0f);
        labelRect.anchoredPosition = Vector2.zero;
        labelText.alignment = TextAnchor.MiddleLeft;

        GameObject meterRoot = CreateUiObject("Meter", rowRoot.transform);
        RectTransform meterRect = meterRoot.GetComponent<RectTransform>();
        meterRect.anchorMin = new Vector2(0f, 0f);
        meterRect.anchorMax = new Vector2(1f, 1f);
        meterRect.offsetMin = new Vector2(76f, 2f);
        meterRect.offsetMax = new Vector2(-120f, -2f);

        Image meterBackground = meterRoot.AddComponent<Image>();
        meterBackground.color = barBackgroundColor;

        Outline meterOutline = meterRoot.AddComponent<Outline>();
        meterOutline.effectColor = barFrameColor;
        meterOutline.effectDistance = new Vector2(1f, -1f);

        GameObject fillObject = CreateUiObject("Fill", meterRoot.transform);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        Text valueText = CreateText("Value", rowRoot.transform, 20, FontStyle.Normal, primaryTextColor);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.sizeDelta = new Vector2(112f, 0f);
        valueRect.anchoredPosition = Vector2.zero;
        valueText.alignment = TextAnchor.MiddleRight;

        return new SelectionBarWidgets(labelText, valueText, fillImage);
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, Color textColor)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = sharedFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = string.Empty;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private readonly struct SelectionBarWidgets
    {
        public SelectionBarWidgets(Text label, Text value, Image fill)
        {
            Label = label;
            Value = value;
            Fill = fill;
        }

        public Text Label { get; }
        public Text Value { get; }
        public Image Fill { get; }
    }
}
