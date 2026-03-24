using System;
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
    private Button characterButton;
    private Text characterButtonText;
    private Button menuButton;
    private Text menuButtonText;
    private GameObject pauseMenuRoot;
    private Button quitButton;
    private Font sharedFont;
    private TacticsElevationSliderView elevationSliderView;
    private TacticsCharacterMenuView characterMenuView;
    private bool isPauseMenuVisible;

    public event Action QuitRequested;

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

        if (menuButton != null)
        {
            menuButton.onClick.RemoveListener(HandleMenuButtonClicked);
        }

        if (characterButton != null)
        {
            characterButton.onClick.RemoveListener(HandleCharacterButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(HandleQuitButtonClicked);
        }
    }

    public void AssignElevationSliderView(TacticsElevationSliderView sliderView)
    {
        elevationSliderView = sliderView;
        RefreshButtonState();
    }

    public void AssignCharacterMenuView(TacticsCharacterMenuView menuView)
    {
        characterMenuView = menuView;
        RefreshButtonState();
    }

    private void HandleElevationButtonClicked()
    {
        if (elevationSliderView == null)
        {
            return;
        }

        isPauseMenuVisible = false;
        characterMenuView?.SetPanelVisible(false);
        elevationSliderView.TogglePanelVisibility();
        RefreshButtonState();
    }

    private void HandleCharacterButtonClicked()
    {
        if (characterMenuView == null)
        {
            return;
        }

        isPauseMenuVisible = false;
        elevationSliderView?.SetPanelVisible(false);
        characterMenuView.TogglePanelVisibility();
        RefreshButtonState();
    }

    private void HandleMenuButtonClicked()
    {
        isPauseMenuVisible = !isPauseMenuVisible;

        if (isPauseMenuVisible && elevationSliderView != null)
        {
            elevationSliderView.SetPanelVisible(false);
        }

        if (isPauseMenuVisible && characterMenuView != null)
        {
            characterMenuView.SetPanelVisible(false);
        }

        RefreshButtonState();
    }

    private void HandleQuitButtonClicked()
    {
        isPauseMenuVisible = false;
        RefreshButtonState();
        QuitRequested?.Invoke();
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
        navRect.sizeDelta = new Vector2(228f, 76f);

        Image navImage = navRoot.AddComponent<Image>();
        navImage.color = barColor;

        Outline navOutline = navRoot.AddComponent<Outline>();
        navOutline.effectColor = edgeColor;
        navOutline.effectDistance = new Vector2(1f, -1f);

        Shadow navShadow = navRoot.AddComponent<Shadow>();
        navShadow.effectColor = shadowColor;
        navShadow.effectDistance = new Vector2(0f, -4f);

        CreateNavButton(
            "ElevationToggleButton",
            navRoot.transform,
            new Vector2(-72f, 0f),
            "EL",
            HandleElevationButtonClicked,
            out elevationButton,
            out elevationButtonText);
        CreateNavButton(
            "CharacterToggleButton",
            navRoot.transform,
            Vector2.zero,
            "CH",
            HandleCharacterButtonClicked,
            out characterButton,
            out characterButtonText);
        CreateNavButton(
            "MenuToggleButton",
            navRoot.transform,
            new Vector2(72f, 0f),
            "II",
            HandleMenuButtonClicked,
            out menuButton,
            out menuButtonText);

        pauseMenuRoot = CreateUiObject("PauseMenu", navRoot.transform);
        RectTransform pauseMenuRect = pauseMenuRoot.GetComponent<RectTransform>();
        pauseMenuRect.anchorMin = new Vector2(1f, 1f);
        pauseMenuRect.anchorMax = new Vector2(1f, 1f);
        pauseMenuRect.pivot = new Vector2(1f, 1f);
        pauseMenuRect.anchoredPosition = new Vector2(0f, -88f);
        pauseMenuRect.sizeDelta = new Vector2(196f, 96f);

        Image pauseMenuImage = pauseMenuRoot.AddComponent<Image>();
        pauseMenuImage.color = barColor;

        Outline pauseMenuOutline = pauseMenuRoot.AddComponent<Outline>();
        pauseMenuOutline.effectColor = edgeColor;
        pauseMenuOutline.effectDistance = new Vector2(1f, -1f);

        Shadow pauseMenuShadow = pauseMenuRoot.AddComponent<Shadow>();
        pauseMenuShadow.effectColor = shadowColor;
        pauseMenuShadow.effectDistance = new Vector2(0f, -4f);

        GameObject quitButtonObject = CreateUiObject("QuitButton", pauseMenuRoot.transform);
        RectTransform quitButtonRect = quitButtonObject.GetComponent<RectTransform>();
        quitButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        quitButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        quitButtonRect.pivot = new Vector2(0.5f, 0.5f);
        quitButtonRect.sizeDelta = new Vector2(156f, 44f);
        quitButtonRect.anchoredPosition = Vector2.zero;

        Image quitButtonImage = quitButtonObject.AddComponent<Image>();
        quitButtonImage.color = buttonColor;

        quitButton = quitButtonObject.AddComponent<Button>();
        quitButton.targetGraphic = quitButtonImage;
        quitButton.colors = CreateButtonColors();
        quitButton.onClick.AddListener(HandleQuitButtonClicked);

        Outline quitButtonOutline = quitButtonObject.AddComponent<Outline>();
        quitButtonOutline.effectColor = edgeColor;
        quitButtonOutline.effectDistance = new Vector2(1f, -1f);

        Text quitButtonText = CreateText("Label", quitButtonObject.transform, 18, FontStyle.Bold, textColor);
        RectTransform quitLabelRect = quitButtonText.rectTransform;
        quitLabelRect.anchorMin = Vector2.zero;
        quitLabelRect.anchorMax = Vector2.one;
        quitLabelRect.offsetMin = Vector2.zero;
        quitLabelRect.offsetMax = Vector2.zero;
        quitButtonText.alignment = TextAnchor.MiddleCenter;
        quitButtonText.text = "Quit Match";

        pauseMenuRoot.SetActive(false);
    }

    private void RefreshButtonState()
    {
        if (elevationButtonText == null || menuButtonText == null || characterButtonText == null)
        {
            return;
        }

        bool isElevationVisible = elevationSliderView != null && elevationSliderView.IsPanelVisible;
        bool isCharacterVisible = characterMenuView != null && characterMenuView.IsPanelVisible;

        Image elevationImage = elevationButton != null ? elevationButton.targetGraphic as Image : null;
        if (elevationImage != null)
        {
            elevationImage.color = isElevationVisible ? buttonHighlightColor : buttonColor;
        }

        Image menuImage = menuButton != null ? menuButton.targetGraphic as Image : null;
        if (menuImage != null)
        {
            menuImage.color = isPauseMenuVisible ? buttonHighlightColor : buttonColor;
        }

        Image characterImage = characterButton != null ? characterButton.targetGraphic as Image : null;
        if (characterImage != null)
        {
            characterImage.color = isCharacterVisible ? buttonHighlightColor : buttonColor;
        }

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(isPauseMenuVisible);
        }
    }

    private void CreateNavButton(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        string labelText,
        UnityEngine.Events.UnityAction onClick,
        out Button button,
        out Text label)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(56f, 56f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = buttonColor;

        button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.colors = CreateButtonColors();
        button.onClick.AddListener(onClick);

        Outline buttonOutline = buttonObject.AddComponent<Outline>();
        buttonOutline.effectColor = edgeColor;
        buttonOutline.effectDistance = new Vector2(1f, -1f);

        label = CreateText("Label", buttonObject.transform, 20, FontStyle.Bold, textColor);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = labelText;
    }

    private ColorBlock CreateButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(buttonHighlightColor.r, buttonHighlightColor.g, buttonHighlightColor.b, 0.95f);
        colors.pressedColor = new Color(buttonHighlightColor.r, buttonHighlightColor.g, buttonHighlightColor.b, 0.82f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.45f);
        return colors;
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
