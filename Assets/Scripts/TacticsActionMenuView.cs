using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TacticsActionMenuView : MonoBehaviour
{
    [Header("Theme")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.96f);
    [SerializeField] private Color panelBorderColor = new Color(0.88f, 0.84f, 0.72f, 1f);
    [SerializeField] private Color accentColor = new Color(0.76f, 0.69f, 0.5f, 1f);
    [SerializeField] private Color primaryTextColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.68f, 0.69f, 0.72f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.17f, 0.18f, 0.2f, 1f);
    [SerializeField] private Color buttonHighlightedColor = new Color(0.27f, 0.28f, 0.31f, 1f);

    private Canvas rootCanvas;
    private GameObject panelRoot;
    private Text characterNameText;
    private Text statsText;
    private Button moveButton;
    private Button endTurnButton;
    private LayoutElement footerSpacer;
    private Font sharedFont;

    public event Action<TacticsHudActionType> ActionSelected;

    private void Awake()
    {
        EnsureBuilt();
        Hide();
    }

    public void ShowForCharacter(TacticsCharacterController character, bool awaitingMoveTarget, int roundNumber, int turnNumber, int participantCount)
    {
        EnsureBuilt();

        if (character == null)
        {
            Hide();
            return;
        }

        panelRoot.SetActive(true);
        characterNameText.text = character.DisplayName.ToUpperInvariant();
        moveButton.interactable = character.CanMoveThisTurn && !awaitingMoveTarget;
        endTurnButton.interactable = character.CanEndTurn;
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
        rootCanvas.sortingOrder = 5000;

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

        panelRoot = CreateUiObject("Panel", transform);
        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(36f, -36f);
        panelRect.sizeDelta = new Vector2(288f, 228f);

        Image panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        Outline panelOutline = panelRoot.AddComponent<Outline>();
        panelOutline.effectColor = panelBorderColor;
        panelOutline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup panelLayout = panelRoot.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(18, 18, 18, 18);
        panelLayout.spacing = 12f;
        panelLayout.childAlignment = TextAnchor.UpperLeft;
        panelLayout.childControlHeight = true;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandHeight = false;

        ContentSizeFitter panelFitter = panelRoot.AddComponent<ContentSizeFitter>();
        panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject headerRoot = CreateUiObject("Header", panelRoot.transform);
        VerticalLayoutGroup headerLayout = headerRoot.AddComponent<VerticalLayoutGroup>();
        headerLayout.spacing = 4f;
        headerLayout.childAlignment = TextAnchor.UpperLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = false;
        headerLayout.childForceExpandHeight = false;

        characterNameText = CreateText("CharacterName", headerRoot.transform, 24, FontStyle.Bold, primaryTextColor);

        GameObject divider = CreateUiObject("Divider", panelRoot.transform);
        LayoutElement dividerLayout = divider.AddComponent<LayoutElement>();
        dividerLayout.preferredHeight = 4f;
        dividerLayout.minHeight = 4f;
        dividerLayout.flexibleHeight = 0f;

        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = accentColor;

        GameObject actionsRoot = CreateUiObject("Actions", panelRoot.transform);
        VerticalLayoutGroup actionsLayout = actionsRoot.AddComponent<VerticalLayoutGroup>();
        actionsLayout.spacing = 8f;
        actionsLayout.childAlignment = TextAnchor.UpperLeft;
        actionsLayout.childControlHeight = false;
        actionsLayout.childControlWidth = true;
        actionsLayout.childForceExpandHeight = false;

        moveButton = CreateButton("MoveButton", "MOVE", actionsRoot.transform, HandleMoveClicked);
        endTurnButton = CreateButton("EndTurnButton", "END TURN", actionsRoot.transform, HandleEndTurnClicked);

        GameObject footerSpacerObject = CreateUiObject("FooterSpacer", panelRoot.transform);
        footerSpacer = footerSpacerObject.AddComponent<LayoutElement>();
        footerSpacer.preferredHeight = 20f;
        footerSpacer.minHeight = 20f;
        footerSpacer.flexibleHeight = 0f;
    }

    private Button CreateButton(string objectName, string label, Transform parent, UnityAction onClick)
    {
        GameObject buttonRoot = CreateUiObject(objectName, parent);
        LayoutElement layoutElement = buttonRoot.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 42f;

        Image buttonImage = buttonRoot.AddComponent<Image>();
        buttonImage.color = buttonColor;

        Button button = buttonRoot.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHighlightedColor;
        colors.selectedColor = buttonHighlightedColor;
        colors.pressedColor = accentColor;
        colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(onClick);

        Outline outline = buttonRoot.AddComponent<Outline>();
        outline.effectColor = panelBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        Text buttonText = CreateText("Label", buttonRoot.transform, 18, FontStyle.Bold, primaryTextColor);
        buttonText.text = label;
        buttonText.alignment = TextAnchor.MiddleLeft;

        RectTransform textRect = buttonText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);

        return button;
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
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = string.Empty;
        return text;
    }

    private void HandleMoveClicked()
    {
        ActionSelected?.Invoke(TacticsHudActionType.Move);
    }

    private void HandleEndTurnClicked()
    {
        ActionSelected?.Invoke(TacticsHudActionType.EndTurn);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
