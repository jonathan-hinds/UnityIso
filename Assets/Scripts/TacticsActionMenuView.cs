using System;
using System.Collections.Generic;
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
    [SerializeField] private Color flyoutShadowColor = new Color(0f, 0f, 0f, 0.24f);
    [SerializeField] private Color selectedAbilityColor = new Color(0.4f, 0.32f, 0.16f, 1f);
    [SerializeField] private Color disabledTextColor = new Color(0.45f, 0.46f, 0.5f, 1f);

    private Canvas rootCanvas;
    private GameObject panelRoot;
    private RectTransform panelRect;
    private Text characterNameText;
    private Button moveButton;
    private Button abilitiesButton;
    private Button endTurnButton;
    private LayoutElement footerSpacer;
    private Font sharedFont;
    private GameObject flyoutRoot;
    private Button flyoutDismissButton;
    private Text flyoutTitleText;
    private Text emptyStateText;
    private RectTransform abilityContentRoot;
    private readonly List<AbilityEntryWidgets> abilityEntryPool = new();
    private TacticsCharacterController displayedCharacter;
    private IReadOnlyList<TacticsActionMenuAbilityOption> displayedAbilityOptions = Array.Empty<TacticsActionMenuAbilityOption>();
    private bool isFlyoutOpen;

    public event Action<TacticsHudActionType> ActionSelected;
    public event Action<TacticsAbilityDefinition> AbilitySelected;

    private void Awake()
    {
        EnsureBuilt();
        Hide();
    }

    public void ShowForCharacter(
        TacticsCharacterController character,
        IReadOnlyList<TacticsActionMenuAbilityOption> abilityOptions,
        bool awaitingMoveTarget,
        bool awaitingAbilityTarget,
        int roundNumber,
        int turnNumber,
        int participantCount)
    {
        EnsureBuilt();

        if (character == null)
        {
            Hide();
            return;
        }

        panelRoot.SetActive(true);
        flyoutRoot.SetActive(isFlyoutOpen);

        if (!ReferenceEquals(displayedCharacter, character))
        {
            displayedCharacter = character;
            isFlyoutOpen = false;
            flyoutRoot.SetActive(false);
        }

        displayedAbilityOptions = abilityOptions ?? Array.Empty<TacticsActionMenuAbilityOption>();
        characterNameText.text = character.DisplayName.ToUpperInvariant();
        moveButton.interactable = character.CanMoveThisTurn && !awaitingMoveTarget && !awaitingAbilityTarget;
        abilitiesButton.interactable = displayedAbilityOptions.Count > 0 &&
                                       character.CanUseAbilitiesThisTurn &&
                                       !awaitingMoveTarget;
        endTurnButton.interactable = character.CanEndTurn;

        if (!abilitiesButton.interactable)
        {
            isFlyoutOpen = false;
            flyoutRoot.SetActive(false);
        }

        RebuildAbilityEntries();
    }

    public void Hide()
    {
        EnsureBuilt();
        panelRoot.SetActive(false);
        flyoutRoot.SetActive(false);
        displayedCharacter = null;
        displayedAbilityOptions = Array.Empty<TacticsActionMenuAbilityOption>();
        isFlyoutOpen = false;
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
        panelRect = panelRoot.GetComponent<RectTransform>();
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
        abilitiesButton = CreateButton("AbilitiesButton", "ABILITIES", actionsRoot.transform, HandleAbilitiesClicked);
        endTurnButton = CreateButton("EndTurnButton", "END TURN", actionsRoot.transform, HandleEndTurnClicked);

        GameObject footerSpacerObject = CreateUiObject("FooterSpacer", panelRoot.transform);
        footerSpacer = footerSpacerObject.AddComponent<LayoutElement>();
        footerSpacer.preferredHeight = 20f;
        footerSpacer.minHeight = 20f;
        footerSpacer.flexibleHeight = 0f;

        BuildFlyout();
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

    private void HandleAbilitiesClicked()
    {
        if (!abilitiesButton.interactable)
        {
            return;
        }

        isFlyoutOpen = !isFlyoutOpen;
        flyoutRoot.SetActive(isFlyoutOpen);

        if (isFlyoutOpen)
        {
            RebuildAbilityEntries();
        }
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

    private void BuildFlyout()
    {
        flyoutRoot = CreateUiObject("AbilitiesFlyout", transform);
        RectTransform flyoutRect = flyoutRoot.GetComponent<RectTransform>();
        flyoutRect.anchorMin = new Vector2(0f, 1f);
        flyoutRect.anchorMax = new Vector2(0f, 1f);
        flyoutRect.pivot = new Vector2(0f, 1f);
        flyoutRect.sizeDelta = new Vector2(320f, 228f);

        Image flyoutImage = flyoutRoot.AddComponent<Image>();
        flyoutImage.color = panelColor;

        Outline flyoutOutline = flyoutRoot.AddComponent<Outline>();
        flyoutOutline.effectColor = panelBorderColor;
        flyoutOutline.effectDistance = new Vector2(1f, -1f);

        Shadow flyoutShadow = flyoutRoot.AddComponent<Shadow>();
        flyoutShadow.effectColor = flyoutShadowColor;
        flyoutShadow.effectDistance = new Vector2(0f, -4f);

        flyoutDismissButton = flyoutRoot.AddComponent<Button>();
        flyoutDismissButton.transition = Selectable.Transition.None;
        flyoutDismissButton.onClick.AddListener(HandleFlyoutBackgroundClicked);

        flyoutTitleText = CreateText("FlyoutTitle", flyoutRoot.transform, 18, FontStyle.Bold, accentColor);
        RectTransform titleRect = flyoutTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(20f, -34f);
        titleRect.offsetMax = new Vector2(-20f, -8f);
        flyoutTitleText.text = "ABILITIES";

        GameObject divider = CreateUiObject("FlyoutDivider", flyoutRoot.transform);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.offsetMin = new Vector2(20f, -48f);
        dividerRect.offsetMax = new Vector2(-20f, -44f);

        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = accentColor;

        GameObject scrollRoot = CreateUiObject("AbilityScrollView", flyoutRoot.transform);
        RectTransform scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(18f, 18f);
        scrollRectTransform.offsetMax = new Vector2(-18f, -58f);

        ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 22f;

        GameObject viewport = CreateUiObject("Viewport", scrollRoot.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(0f, 0f);
        viewportRect.offsetMax = new Vector2(-18f, 0f);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.32f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUiObject("Content", viewport.transform);
        abilityContentRoot = content.GetComponent<RectTransform>();
        abilityContentRoot.anchorMin = new Vector2(0f, 1f);
        abilityContentRoot.anchorMax = new Vector2(1f, 1f);
        abilityContentRoot.pivot = new Vector2(0.5f, 1f);
        abilityContentRoot.offsetMin = Vector2.zero;
        abilityContentRoot.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = abilityContentRoot;

        GameObject scrollbarObject = CreateUiObject("Scrollbar", scrollRoot.transform);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.sizeDelta = new Vector2(12f, 0f);
        scrollbarRect.anchoredPosition = Vector2.zero;

        Image scrollbarTrack = scrollbarObject.AddComponent<Image>();
        scrollbarTrack.color = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.6f);

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.25f;
        scrollbar.targetGraphic = scrollbarTrack;

        GameObject handleSlideArea = CreateUiObject("SlidingArea", scrollbarObject.transform);
        RectTransform slidingAreaRect = handleSlideArea.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(2f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handleObject = CreateUiObject("Handle", handleSlideArea.transform);
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = accentColor;
        scrollbar.handleRect = handleObject.GetComponent<RectTransform>();

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        emptyStateText = CreateText("EmptyState", viewport.transform, 16, FontStyle.Italic, secondaryTextColor);
        RectTransform emptyStateRect = emptyStateText.rectTransform;
        emptyStateRect.anchorMin = new Vector2(0f, 0f);
        emptyStateRect.anchorMax = new Vector2(1f, 1f);
        emptyStateRect.offsetMin = new Vector2(24f, 24f);
        emptyStateRect.offsetMax = new Vector2(-24f, -24f);
        emptyStateText.alignment = TextAnchor.MiddleCenter;
        emptyStateText.text = "No abilities available.";

        flyoutRoot.SetActive(false);
        RefreshFlyoutPosition();
    }

    private void RebuildAbilityEntries()
    {
        if (abilityContentRoot == null)
        {
            return;
        }

        int optionCount = displayedAbilityOptions != null ? displayedAbilityOptions.Count : 0;
        EnsureAbilityEntryPool(optionCount);

        for (int i = 0; i < abilityEntryPool.Count; i++)
        {
            bool shouldShow = i < optionCount;
            abilityEntryPool[i].Root.SetActive(shouldShow);

            if (!shouldShow)
            {
                continue;
            }

            ApplyAbilityEntry(abilityEntryPool[i], displayedAbilityOptions[i]);
        }

        bool hasOptions = optionCount > 0;
        emptyStateText.gameObject.SetActive(!hasOptions);
        RefreshFlyoutPosition();
        LayoutRebuilder.ForceRebuildLayoutImmediate(abilityContentRoot);
    }

    private void EnsureAbilityEntryPool(int count)
    {
        while (abilityEntryPool.Count < count)
        {
            abilityEntryPool.Add(CreateAbilityEntryWidget(abilityEntryPool.Count));
        }
    }

    private AbilityEntryWidgets CreateAbilityEntryWidget(int index)
    {
        GameObject entryRoot = CreateUiObject($"AbilityEntry_{index}", abilityContentRoot);
        LayoutElement layoutElement = entryRoot.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 68f;
        layoutElement.minHeight = 68f;

        Image entryImage = entryRoot.AddComponent<Image>();
        entryImage.color = buttonColor;

        Button entryButton = entryRoot.AddComponent<Button>();
        ColorBlock colors = entryButton.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHighlightedColor;
        colors.selectedColor = buttonHighlightedColor;
        colors.pressedColor = accentColor;
        colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.4f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        entryButton.colors = colors;
        entryButton.targetGraphic = entryImage;

        Outline entryOutline = entryRoot.AddComponent<Outline>();
        entryOutline.effectColor = panelBorderColor;
        entryOutline.effectDistance = new Vector2(1f, -1f);

        Text nameText = CreateText("Name", entryRoot.transform, 18, FontStyle.Bold, primaryTextColor);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.offsetMin = new Vector2(14f, -30f);
        nameRect.offsetMax = new Vector2(-14f, -6f);
        nameText.alignment = TextAnchor.UpperLeft;

        Text detailText = CreateText("Detail", entryRoot.transform, 13, FontStyle.Normal, secondaryTextColor);
        RectTransform detailRect = detailText.rectTransform;
        detailRect.anchorMin = new Vector2(0f, 0f);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(14f, 8f);
        detailRect.offsetMax = new Vector2(-14f, -32f);
        detailText.alignment = TextAnchor.LowerLeft;
        detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailText.verticalOverflow = VerticalWrapMode.Truncate;

        return new AbilityEntryWidgets(entryRoot, entryButton, entryImage, nameText, detailText);
    }

    private void ApplyAbilityEntry(AbilityEntryWidgets widgets, TacticsActionMenuAbilityOption option)
    {
        TacticsAbilityDefinition ability = option.Ability;
        widgets.Name.text = ability != null ? ability.DisplayName.ToUpperInvariant() : "ABILITY";
        widgets.Detail.text = BuildAbilityDetailText(ability, option.IsInteractable);
        widgets.Button.interactable = option.IsInteractable;

        Color backgroundColor = option.IsSelected ? selectedAbilityColor : buttonColor;
        widgets.Background.color = backgroundColor;
        widgets.Name.color = option.IsInteractable ? primaryTextColor : disabledTextColor;
        widgets.Detail.color = option.IsInteractable ? secondaryTextColor : disabledTextColor;

        widgets.Button.onClick.RemoveAllListeners();
        if (ability != null)
        {
            widgets.Button.onClick.AddListener(() => HandleAbilityEntryClicked(ability));
        }
    }

    private string BuildAbilityDetailText(TacticsAbilityDefinition ability, bool isInteractable)
    {
        if (ability == null)
        {
            return "No data";
        }

        string description = string.IsNullOrWhiteSpace(ability.Description)
            ? "No description."
            : ability.Description.Trim();

        string availability = isInteractable ? "Ready" : "No targets";
        return $"RANGE {ability.Range}  |  {availability}\n{description}";
    }

    private void HandleFlyoutBackgroundClicked()
    {
        if (!isFlyoutOpen)
        {
            return;
        }

        isFlyoutOpen = false;
        flyoutRoot.SetActive(false);
    }

    private void HandleAbilityEntryClicked(TacticsAbilityDefinition ability)
    {
        isFlyoutOpen = false;
        flyoutRoot.SetActive(false);
        AbilitySelected?.Invoke(ability);
    }

    private void RefreshFlyoutPosition()
    {
        if (panelRect == null || flyoutRoot == null)
        {
            return;
        }

        RectTransform flyoutRect = flyoutRoot.GetComponent<RectTransform>();
        flyoutRect.anchoredPosition = new Vector2(
            panelRect.anchoredPosition.x + panelRect.sizeDelta.x + 18f,
            panelRect.anchoredPosition.y);
    }

    private readonly struct AbilityEntryWidgets
    {
        public AbilityEntryWidgets(
            GameObject root,
            Button button,
            Image background,
            Text name,
            Text detail)
        {
            Root = root;
            Button = button;
            Background = background;
            Name = name;
            Detail = detail;
        }

        public GameObject Root { get; }
        public Button Button { get; }
        public Image Background { get; }
        public Text Name { get; }
        public Text Detail { get; }
    }
}

public readonly struct TacticsActionMenuAbilityOption
{
    public TacticsActionMenuAbilityOption(
        TacticsAbilityDefinition ability,
        bool isInteractable,
        bool isSelected)
    {
        Ability = ability;
        IsInteractable = isInteractable;
        IsSelected = isSelected;
    }

    public TacticsAbilityDefinition Ability { get; }
    public bool IsInteractable { get; }
    public bool IsSelected { get; }
}
