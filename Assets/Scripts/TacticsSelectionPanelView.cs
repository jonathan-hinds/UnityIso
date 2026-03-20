using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsSelectionPanelView : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.56f);
    [SerializeField] private Color panelEdgeColor = new Color(0.88f, 0.84f, 0.72f, 0.7f);
    [SerializeField] private Color panelShadowColor = new Color(0f, 0f, 0f, 0.18f);
    [SerializeField] private Color primaryTextColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color barBackgroundColor = new Color(0f, 0f, 0f, 0.32f);
    [SerializeField] private Color barFrameColor = new Color(1f, 1f, 1f, 0.08f);

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
        panelRect.sizeDelta = new Vector2(332f, 120f);

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        Outline panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = panelEdgeColor;
        panelOutline.effectDistance = new Vector2(1f, -1f);

        Shadow panelShadow = panelRoot.AddComponent<Shadow>();
        panelShadow.effectColor = panelShadowColor;
        panelShadow.effectDistance = new Vector2(0f, -4f);

        VerticalLayoutGroup panelLayout = panelRoot.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(14, 14, 10, 12);
        panelLayout.spacing = 8f;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlHeight = false;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;

        nameText = CreateText("Name", panelRoot.transform, 22, FontStyle.Bold, primaryTextColor);
        nameText.alignment = TextAnchor.MiddleCenter;
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.preferredHeight = 28f;

        healthBar = CreateSelectionBar("HealthBar", panelRoot.transform);
        staminaBar = CreateSelectionBar("StaminaBar", panelRoot.transform);
        manaBar = CreateSelectionBar("ManaBar", panelRoot.transform);
    }

    private void ApplyBar(SelectionBarWidgets widgets, TacticsSelectionHudResourceData resourceData)
    {
        widgets.Label.text = resourceData.Label.ToLowerInvariant();
        widgets.Value.text = $"{resourceData.CurrentValue}/{resourceData.MaxValue}";
        widgets.Fill.color = resourceData.FillColor;
        widgets.Fill.fillAmount = resourceData.FillNormalized;
    }

    private SelectionBarWidgets CreateSelectionBar(string objectName, Transform parent)
    {
        GameObject rowRoot = CreateUiObject(objectName, parent);
        LayoutElement rowLayout = rowRoot.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 20f;

        HorizontalLayoutGroup rowGroup = rowRoot.AddComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 6f;
        rowGroup.childAlignment = TextAnchor.MiddleLeft;
        rowGroup.childControlHeight = true;
        rowGroup.childControlWidth = false;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = true;

        Text labelText = CreateText("Label", rowRoot.transform, 16, FontStyle.Bold, secondaryTextColor);
        labelText.alignment = TextAnchor.MiddleLeft;
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 30f;

        GameObject meterRoot = CreateUiObject("Meter", rowRoot.transform);
        LayoutElement meterLayout = meterRoot.AddComponent<LayoutElement>();
        meterLayout.flexibleWidth = 1f;
        meterLayout.preferredHeight = 16f;

        Image meterBackground = meterRoot.AddComponent<Image>();
        meterBackground.color = barBackgroundColor;

        Outline meterOutline = meterRoot.AddComponent<Outline>();
        meterOutline.effectColor = barFrameColor;
        meterOutline.effectDistance = new Vector2(1f, -1f);

        GameObject fillObject = CreateUiObject("Fill", meterRoot.transform);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        Text valueText = CreateText("Value", rowRoot.transform, 10, FontStyle.Normal, primaryTextColor);
        valueText.alignment = TextAnchor.MiddleRight;
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 70f;

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

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = CreateUiObject(objectName, parent);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
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
