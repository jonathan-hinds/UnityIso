using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsElevationSliderView : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.94f);
    [SerializeField] private Color panelEdgeColor = new Color(0.88f, 0.84f, 0.72f, 1f);
    [SerializeField] private Color panelShadowColor = new Color(0f, 0f, 0f, 0.22f);
    [SerializeField] private Color accentColor = new Color(0.76f, 0.69f, 0.5f, 1f);
    [SerializeField] private Color trackColor = new Color(0.13f, 0.14f, 0.16f, 1f);
    [SerializeField] private Color fillColor = new Color(0.46f, 0.4f, 0.27f, 1f);
    [SerializeField] private Color primaryTextColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.67f, 0.68f, 0.72f, 1f);

    private readonly List<GameObject> tickPool = new();

    private Canvas rootCanvas;
    private GameObject panelRoot;
    private Slider slider;
    private Text titleText;
    private Text valueText;
    private RectTransform tickContainer;
    private Font sharedFont;
    private IsometricMapLayerVisibilityController visibilityController;
    private bool suppressValueChanged;
    private bool isPanelVisible;

    private void Awake()
    {
        EnsureBuilt();
        isPanelVisible = false;
        Hide();
    }

    private void OnDestroy()
    {
        AssignVisibilityController(null);
    }

    public void AssignVisibilityController(IsometricMapLayerVisibilityController controller)
    {
        if (visibilityController != null)
        {
            visibilityController.VisibilityChanged -= HandleVisibilityChanged;
        }

        visibilityController = controller;

        if (visibilityController != null)
        {
            visibilityController.VisibilityChanged -= HandleVisibilityChanged;
            visibilityController.VisibilityChanged += HandleVisibilityChanged;
            HandleVisibilityChanged(visibilityController.VisibleElevation, visibilityController.MaximumElevation);
        }
        else
        {
            Hide();
        }
    }

    public bool IsPanelVisible => panelRoot != null && panelRoot.activeSelf;

    public void TogglePanelVisibility()
    {
        SetPanelVisible(!isPanelVisible);
    }

    public void SetPanelVisible(bool visible)
    {
        isPanelVisible = visible;

        if (visibilityController == null)
        {
            Hide();
            return;
        }

        HandleVisibilityChanged(visibilityController.VisibleElevation, visibilityController.MaximumElevation);
    }

    private void HandleVisibilityChanged(int visibleElevation, int maximumElevation)
    {
        EnsureBuilt();

        if (maximumElevation <= 0)
        {
            Hide();
            return;
        }

        panelRoot.SetActive(isPanelVisible);
        RebuildTicks(maximumElevation);

        suppressValueChanged = true;
        slider.minValue = 1f;
        slider.maxValue = maximumElevation;
        slider.wholeNumbers = true;
        slider.value = visibleElevation;
        suppressValueChanged = false;

        titleText.text = "Elevation";
        valueText.text = $"VISIBLE {visibleElevation}/{maximumElevation}";
    }

    private void Hide()
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
        rootCanvas.sortingOrder = 4998;

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

        panelRoot = CreateUiObject("ElevationPanel", transform);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-36f, -124f);
        panelRect.sizeDelta = new Vector2(188f, 248f);

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        Outline panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = panelEdgeColor;
        panelOutline.effectDistance = new Vector2(1f, -1f);

        Shadow panelShadow = panelRoot.AddComponent<Shadow>();
        panelShadow.effectColor = panelShadowColor;
        panelShadow.effectDistance = new Vector2(0f, -4f);

        titleText = CreateText("Title", panelRoot.transform, 18, FontStyle.Bold, accentColor);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(20f, -30f);
        titleRect.offsetMax = new Vector2(-20f, -8f);
        titleText.alignment = TextAnchor.UpperCenter;

        GameObject divider = CreateUiObject("Divider", panelRoot.transform);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.offsetMin = new Vector2(20f, -42f);
        dividerRect.offsetMax = new Vector2(-20f, -38f);

        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = accentColor;

        valueText = CreateText("Value", panelRoot.transform, 16, FontStyle.Bold, primaryTextColor);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(0f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(0.5f, 1f);
        valueRect.offsetMin = new Vector2(20f, -66f);
        valueRect.offsetMax = new Vector2(-20f, -44f);
        valueText.alignment = TextAnchor.MiddleCenter;

        GameObject sliderRoot = CreateUiObject("SliderRoot", panelRoot.transform);
        RectTransform sliderRootRect = sliderRoot.GetComponent<RectTransform>();
        sliderRootRect.anchorMin = new Vector2(0f, 0f);
        sliderRootRect.anchorMax = new Vector2(1f, 1f);
        sliderRootRect.offsetMin = new Vector2(26f, 28f);
        sliderRootRect.offsetMax = new Vector2(-26f, -86f);

        tickContainer = CreateUiObject("Ticks", sliderRoot.transform).GetComponent<RectTransform>();
        tickContainer.anchorMin = new Vector2(0f, 0f);
        tickContainer.anchorMax = new Vector2(0.45f, 1f);
        tickContainer.offsetMin = Vector2.zero;
        tickContainer.offsetMax = new Vector2(-8f, 0f);

        GameObject sliderObject = CreateUiObject("Slider", sliderRoot.transform);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.55f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        slider = sliderObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.BottomToTop;
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(HandleSliderValueChanged);

        GameObject background = CreateUiObject("Background", sliderObject.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(18f, 0f);

        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = trackColor;
        slider.targetGraphic = backgroundImage;

        GameObject fillArea = CreateUiObject("FillArea", sliderObject.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0.5f, 0f);
        fillAreaRect.anchorMax = new Vector2(0.5f, 1f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(18f, -16f);

        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = fillColor;
        slider.fillRect = fillRect;

        GameObject handleArea = CreateUiObject("HandleSlideArea", sliderObject.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(0f, -4f);
        handleAreaRect.offsetMax = new Vector2(0f, 4f);

        GameObject handle = CreateUiObject("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(34f, 18f);

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = accentColor;
        slider.handleRect = handleRect;

        Outline handleOutline = handle.AddComponent<Outline>();
        handleOutline.effectColor = panelEdgeColor;
        handleOutline.effectDistance = new Vector2(1f, -1f);
    }

    private void RebuildTicks(int maximumElevation)
    {
        while (tickPool.Count < maximumElevation)
        {
            GameObject tickRoot = CreateUiObject($"Tick{tickPool.Count + 1}", tickContainer);

            GameObject lineObject = CreateUiObject("Line", tickRoot.transform);
            RectTransform lineRect = lineObject.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0f, 0.5f);
            lineRect.anchorMax = new Vector2(0f, 0.5f);
            lineRect.pivot = new Vector2(0f, 0.5f);
            lineRect.sizeDelta = new Vector2(18f, 2f);
            lineRect.anchoredPosition = new Vector2(0f, 0f);

            Image lineImage = lineObject.AddComponent<Image>();
            lineImage.color = accentColor;

            Text tickText = CreateText("Label", tickRoot.transform, 14, FontStyle.Bold, secondaryTextColor);
            RectTransform tickTextRect = tickText.rectTransform;
            tickTextRect.anchorMin = new Vector2(0f, 0f);
            tickTextRect.anchorMax = new Vector2(1f, 1f);
            tickTextRect.offsetMin = new Vector2(24f, -10f);
            tickTextRect.offsetMax = Vector2.zero;
            tickText.alignment = TextAnchor.MiddleLeft;

            tickPool.Add(tickRoot);
        }

        for (int i = 0; i < tickPool.Count; i++)
        {
            GameObject tickRoot = tickPool[i];
            bool isActive = i < maximumElevation;
            tickRoot.SetActive(isActive);
            if (!isActive)
            {
                continue;
            }

            RectTransform tickRect = tickRoot.GetComponent<RectTransform>();
            float normalized = maximumElevation == 1 ? 0.5f : (float)i / (maximumElevation - 1);
            tickRect.anchorMin = new Vector2(0f, normalized);
            tickRect.anchorMax = new Vector2(1f, normalized);
            tickRect.pivot = new Vector2(0f, 0.5f);
            tickRect.anchoredPosition = Vector2.zero;
            tickRect.sizeDelta = new Vector2(0f, 20f);

            Text tickText = tickRoot.GetComponentInChildren<Text>();
            int displayedLevel = i + 1;
            tickText.text = $"L{displayedLevel}";
        }
    }

    private void HandleSliderValueChanged(float value)
    {
        if (suppressValueChanged || visibilityController == null)
        {
            return;
        }

        visibilityController.SetVisibleElevation(Mathf.RoundToInt(value));
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
}
