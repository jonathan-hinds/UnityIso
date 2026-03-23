using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsTopRightNavBarView : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private Color barColor = new Color(0.08f, 0.09f, 0.11f, 0.94f);
    [SerializeField] private Color edgeColor = new Color(0.88f, 0.84f, 0.72f, 1f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.22f);
    [SerializeField] private Color buttonColor = new Color(0.16f, 0.17f, 0.2f, 1f);
    [SerializeField] private Color buttonHighlightColor = new Color(0.76f, 0.69f, 0.5f, 1f);
    [SerializeField] private Color textColor = new Color(0.96f, 0.94f, 0.89f, 1f);

    private Canvas rootCanvas;
    private GameObject navRoot;
    private Button elevationButton;
    private Text elevationButtonText;
    private Font sharedFont;
    private TacticsElevationSliderView elevationSliderView;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnDestroy()
    {
        if (elevationButton != null)
        {
            elevationButton.onClick.RemoveListener(HandleElevationButtonClicked);
        }
    }

    public void AssignElevationSliderView(TacticsElevationSliderView sliderView)
    {
        elevationSliderView = sliderView;
        RefreshButtonState();
    }

    private void HandleElevationButtonClicked()
    {
        if (elevationSliderView == null)
        {
            return;
        }

        elevationSliderView.TogglePanelVisibility();
        RefreshButtonState();
    }

    private void EnsureBuilt()
    {
        if (navRoot != null)
        {
            return;
        }

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 4999;

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

        navRoot = CreateUiObject("TopRightNavBar", transform);
        RectTransform navRect = navRoot.GetComponent<RectTransform>();
        navRect.anchorMin = new Vector2(1f, 1f);
        navRect.anchorMax = new Vector2(1f, 1f);
        navRect.pivot = new Vector2(1f, 1f);
        navRect.anchoredPosition = new Vector2(-36f, -36f);
        navRect.sizeDelta = new Vector2(76f, 76f);

        Image navImage = navRoot.AddComponent<Image>();
        navImage.color = barColor;

        Outline navOutline = navRoot.AddComponent<Outline>();
        navOutline.effectColor = edgeColor;
        navOutline.effectDistance = new Vector2(1f, -1f);

        Shadow navShadow = navRoot.AddComponent<Shadow>();
        navShadow.effectColor = shadowColor;
        navShadow.effectDistance = new Vector2(0f, -4f);

        GameObject buttonObject = CreateUiObject("ElevationToggleButton", navRoot.transform);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(56f, 56f);
        buttonRect.anchoredPosition = Vector2.zero;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = buttonColor;

        elevationButton = buttonObject.AddComponent<Button>();
        elevationButton.targetGraphic = buttonImage;
        ColorBlock colors = elevationButton.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(buttonHighlightColor.r, buttonHighlightColor.g, buttonHighlightColor.b, 0.95f);
        colors.pressedColor = new Color(buttonHighlightColor.r, buttonHighlightColor.g, buttonHighlightColor.b, 0.82f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.45f);
        elevationButton.colors = colors;
        elevationButton.onClick.AddListener(HandleElevationButtonClicked);

        Outline buttonOutline = buttonObject.AddComponent<Outline>();
        buttonOutline.effectColor = edgeColor;
        buttonOutline.effectDistance = new Vector2(1f, -1f);

        elevationButtonText = CreateText("Label", buttonObject.transform, 20, FontStyle.Bold, textColor);
        RectTransform labelRect = elevationButtonText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        elevationButtonText.alignment = TextAnchor.MiddleCenter;
        elevationButtonText.text = "EL";
    }

    private void RefreshButtonState()
    {
        if (elevationButtonText == null)
        {
            return;
        }

        bool isVisible = elevationSliderView != null && elevationSliderView.IsPanelVisible;
        elevationButtonText.text = isVisible ? "EL" : "EL";

        Image buttonImage = elevationButton != null ? elevationButton.targetGraphic as Image : null;
        if (buttonImage != null)
        {
            buttonImage.color = isVisible ? buttonHighlightColor : buttonColor;
        }
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, FontStyle fontStyle, Color labelColor)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = sharedFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = labelColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
