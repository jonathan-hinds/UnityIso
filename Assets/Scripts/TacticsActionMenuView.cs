using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TacticsActionMenuView : MonoBehaviour
{
    private const float FlyoutWidth = 1180f;
    private const float FlyoutHeight = 760f;
    private const float FlyoutCardHeight = 180f;
    private const int FlyoutColumnCount = 2;

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
    [SerializeField] private Color flyoutBodyColor = new Color(0.07f, 0.08f, 0.1f, 0.985f);
    [SerializeField] private Color flyoutChromeColor = new Color(1f, 1f, 1f, 0.035f);
    [SerializeField] private Color cardColor = new Color(0.11f, 0.13f, 0.16f, 0.98f);
    [SerializeField] private Color cardHoverColor = new Color(0.16f, 0.18f, 0.22f, 1f);
    [SerializeField] private Color cardDisabledColor = new Color(0.12f, 0.13f, 0.15f, 0.72f);
    [SerializeField] private Color cardDividerColor = new Color(0.76f, 0.69f, 0.5f, 0.34f);
    [SerializeField] private Color cardMetaColor = new Color(0.86f, 0.80f, 0.68f, 0.92f);
    [SerializeField] private Color cardDescriptionColor = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color cardGeneratedColor = new Color(0.72f, 0.75f, 0.8f, 0.92f);
    [SerializeField] private Color cardStatusColor = new Color(0.98f, 0.83f, 0.58f, 1f);
    [SerializeField] private Color viewportColor = new Color(1f, 1f, 1f, 0.025f);

    private Canvas rootCanvas;
    private GameObject panelRoot;
    private RectTransform panelRect;
    private Text characterNameText;
    private Button moveButton;
    private Button openChestButton;
    private Button abilitiesButton;
    private Button endTurnButton;
    private LayoutElement footerSpacer;
    private Font sharedFont;
    private GameObject flyoutRoot;
    private Button flyoutDismissButton;
    private Text flyoutTitleText;
    private Text flyoutSubtitleText;
    private Text emptyStateText;
    private RectTransform abilityContentRoot;
    private GridLayoutGroup abilityGridLayout;
    private readonly List<AbilityEntryWidgets> abilityEntryPool = new();
    private TacticsCharacterController displayedCharacter;
    private IReadOnlyList<TacticsActionMenuAbilityOption> displayedAbilityOptions = Array.Empty<TacticsActionMenuAbilityOption>();
    private bool isFlyoutOpen;
    private TacticsAbilityTooltipView tooltipView;

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
        bool canOpenChest,
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
        openChestButton.gameObject.SetActive(canOpenChest);
        openChestButton.interactable = canOpenChest && character.CanInteractThisTurn && !awaitingMoveTarget && !awaitingAbilityTarget;
        abilitiesButton.interactable = displayedAbilityOptions.Count > 0 &&
                                       character.CanUseAbilitiesThisTurn &&
                                       !awaitingMoveTarget;
        endTurnButton.interactable = character.CanEndTurn;

        if (!abilitiesButton.interactable)
        {
            isFlyoutOpen = false;
            flyoutRoot.SetActive(false);
            tooltipView?.Hide();
        }

        RefreshFlyoutHeader();
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
        tooltipView?.Hide();
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

        tooltipView = GetComponent<TacticsAbilityTooltipView>();
        if (tooltipView == null)
        {
            tooltipView = gameObject.AddComponent<TacticsAbilityTooltipView>();
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
        openChestButton = CreateButton("OpenChestButton", "OPEN CHEST", actionsRoot.transform, HandleOpenChestClicked);
        abilitiesButton = CreateButton("AbilitiesButton", "ABILITIES", actionsRoot.transform, HandleAbilitiesClicked);
        endTurnButton = CreateButton("EndTurnButton", "END TURN", actionsRoot.transform, HandleEndTurnClicked);
        openChestButton.gameObject.SetActive(false);

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
            RefreshFlyoutHeader();
            RebuildAbilityEntries();
        }
        else
        {
            tooltipView?.Hide();
        }
    }

    private void HandleOpenChestClicked()
    {
        ActionSelected?.Invoke(TacticsHudActionType.OpenChest);
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
        flyoutRect.anchorMin = new Vector2(1f, 1f);
        flyoutRect.anchorMax = new Vector2(1f, 1f);
        flyoutRect.pivot = new Vector2(1f, 1f);
        flyoutRect.anchoredPosition = new Vector2(-36f, -124f);
        flyoutRect.sizeDelta = new Vector2(FlyoutWidth, FlyoutHeight);

        Image flyoutImage = flyoutRoot.AddComponent<Image>();
        flyoutImage.color = flyoutBodyColor;

        Outline flyoutOutline = flyoutRoot.AddComponent<Outline>();
        flyoutOutline.effectColor = panelBorderColor;
        flyoutOutline.effectDistance = new Vector2(1f, -1f);

        Shadow flyoutShadow = flyoutRoot.AddComponent<Shadow>();
        flyoutShadow.effectColor = flyoutShadowColor;
        flyoutShadow.effectDistance = new Vector2(0f, -4f);

        flyoutDismissButton = flyoutRoot.AddComponent<Button>();
        flyoutDismissButton.transition = Selectable.Transition.None;
        flyoutDismissButton.onClick.AddListener(HandleFlyoutBackgroundClicked);

        GameObject chromeObject = CreateUiObject("FlyoutChrome", flyoutRoot.transform);
        RectTransform chromeRect = chromeObject.GetComponent<RectTransform>();
        chromeRect.anchorMin = Vector2.zero;
        chromeRect.anchorMax = Vector2.one;
        chromeRect.offsetMin = new Vector2(12f, 12f);
        chromeRect.offsetMax = new Vector2(-12f, -12f);

        Image chromeImage = chromeObject.AddComponent<Image>();
        chromeImage.color = flyoutChromeColor;
        chromeImage.raycastTarget = false;

        Outline chromeOutline = chromeObject.AddComponent<Outline>();
        chromeOutline.effectColor = new Color(panelBorderColor.r, panelBorderColor.g, panelBorderColor.b, 0.16f);
        chromeOutline.effectDistance = new Vector2(1f, -1f);

        flyoutTitleText = CreateText("FlyoutTitle", flyoutRoot.transform, 18, FontStyle.Bold, accentColor);
        RectTransform titleRect = flyoutTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(32f, -42f);
        titleRect.offsetMax = new Vector2(-32f, -16f);
        flyoutTitleText.text = "ABILITIES";

        flyoutSubtitleText = CreateText("FlyoutSubtitle", flyoutRoot.transform, 34, FontStyle.Bold, primaryTextColor);
        RectTransform subtitleRect = flyoutSubtitleText.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.offsetMin = new Vector2(32f, -98f);
        subtitleRect.offsetMax = new Vector2(-32f, -42f);

        GameObject divider = CreateUiObject("FlyoutDivider", flyoutRoot.transform);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.offsetMin = new Vector2(32f, -114f);
        dividerRect.offsetMax = new Vector2(-32f, -110f);

        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = accentColor;

        GameObject scrollRoot = CreateUiObject("AbilityScrollView", flyoutRoot.transform);
        RectTransform scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(32f, 32f);
        scrollRectTransform.offsetMax = new Vector2(-32f, -130f);

        ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        GameObject viewport = CreateUiObject("Viewport", scrollRoot.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(0f, 0f);
        viewportRect.offsetMax = new Vector2(-18f, 0f);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = viewportColor;
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUiObject("Content", viewport.transform);
        abilityContentRoot = content.GetComponent<RectTransform>();
        abilityContentRoot.anchorMin = new Vector2(0f, 1f);
        abilityContentRoot.anchorMax = new Vector2(1f, 1f);
        abilityContentRoot.pivot = new Vector2(0.5f, 1f);
        abilityContentRoot.offsetMin = Vector2.zero;
        abilityContentRoot.offsetMax = Vector2.zero;

        abilityGridLayout = content.AddComponent<GridLayoutGroup>();
        abilityGridLayout.padding = new RectOffset(4, 4, 4, 4);
        abilityGridLayout.spacing = new Vector2(20f, 20f);
        abilityGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        abilityGridLayout.constraintCount = FlyoutColumnCount;
        abilityGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        abilityGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        abilityGridLayout.childAlignment = TextAnchor.UpperLeft;

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
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(1f, 0f);
        handleRect.pivot = new Vector2(0.5f, 0f);
        handleRect.sizeDelta = new Vector2(0f, 48f);
        handleRect.anchoredPosition = Vector2.zero;

        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = accentColor;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

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
        RefreshAbilityGridLayout();

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

        Image entryImage = entryRoot.AddComponent<Image>();
        entryImage.color = cardColor;

        Button entryButton = entryRoot.AddComponent<Button>();
        ColorBlock colors = entryButton.colors;
        colors.normalColor = cardColor;
        colors.highlightedColor = cardHoverColor;
        colors.selectedColor = cardHoverColor;
        colors.pressedColor = accentColor;
        colors.disabledColor = cardDisabledColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        entryButton.colors = colors;
        entryButton.targetGraphic = entryImage;

        Outline entryOutline = entryRoot.AddComponent<Outline>();
        entryOutline.effectColor = panelBorderColor;
        entryOutline.effectDistance = new Vector2(1f, -1f);

        TacticsAbilityTooltipTrigger tooltipTrigger = entryRoot.AddComponent<TacticsAbilityTooltipTrigger>();

        VerticalLayoutGroup layout = entryRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        GameObject headerRow = CreateUiObject("HeaderRow", entryRoot.transform);
        HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 0f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandHeight = false;
        headerLayout.childForceExpandWidth = true;

        LayoutElement headerRowLayout = headerRow.AddComponent<LayoutElement>();
        headerRowLayout.preferredHeight = 28f;
        headerRowLayout.minHeight = 28f;

        GameObject nameRegion = CreateUiObject("NameRegion", headerRow.transform);
        LayoutElement nameRegionLayout = nameRegion.AddComponent<LayoutElement>();
        nameRegionLayout.flexibleWidth = 1f;
        nameRegionLayout.minWidth = 0f;
        nameRegionLayout.preferredHeight = 28f;
        nameRegionLayout.minHeight = 28f;

        Text nameText = CreateText("Name", nameRegion.transform, 18, FontStyle.Bold, primaryTextColor);
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        GameObject summaryRegion = CreateUiObject("SummaryRegion", headerRow.transform);
        LayoutElement summaryRegionLayout = summaryRegion.AddComponent<LayoutElement>();
        summaryRegionLayout.flexibleWidth = 1f;
        summaryRegionLayout.minWidth = 0f;
        summaryRegionLayout.preferredHeight = 28f;
        summaryRegionLayout.minHeight = 28f;

        Text headerSummaryText = CreateText("HeaderSummary", summaryRegion.transform, 18, FontStyle.Bold, cardMetaColor);
        headerSummaryText.alignment = TextAnchor.MiddleRight;
        headerSummaryText.horizontalOverflow = HorizontalWrapMode.Overflow;
        headerSummaryText.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform headerSummaryRect = headerSummaryText.rectTransform;
        headerSummaryRect.anchorMin = Vector2.zero;
        headerSummaryRect.anchorMax = Vector2.one;
        headerSummaryRect.offsetMin = Vector2.zero;
        headerSummaryRect.offsetMax = Vector2.zero;

        GameObject topDivider = CreateDivider(entryRoot.transform, "TopDivider");

        Text metaText = CreateText("Meta", entryRoot.transform, 12, FontStyle.Bold, cardMetaColor);
        metaText.alignment = TextAnchor.MiddleLeft;

        GameObject middleDivider = CreateDivider(entryRoot.transform, "MiddleDivider");

        Text descriptionText = CreateText("Description", entryRoot.transform, 15, FontStyle.Normal, cardDescriptionColor);
        descriptionText.alignment = TextAnchor.UpperLeft;

        Text generatedText = CreateText("Generated", entryRoot.transform, 12, FontStyle.Italic, cardGeneratedColor);
        generatedText.alignment = TextAnchor.UpperLeft;

        Text statusText = CreateText("Status", entryRoot.transform, 11, FontStyle.Bold, cardStatusColor);
        statusText.alignment = TextAnchor.LowerLeft;

        LayoutElement descriptionLayout = descriptionText.gameObject.AddComponent<LayoutElement>();
        descriptionLayout.flexibleHeight = 1f;
        LayoutElement generatedLayout = generatedText.gameObject.AddComponent<LayoutElement>();
        generatedLayout.flexibleHeight = 1f;

        return new AbilityEntryWidgets(
            entryRoot,
            entryButton,
            entryImage,
            entryOutline,
            nameText,
            headerSummaryText,
            metaText,
            descriptionText,
            generatedText,
            statusText,
            topDivider,
            middleDivider,
            tooltipTrigger);
    }

    private void ApplyAbilityEntry(AbilityEntryWidgets widgets, TacticsActionMenuAbilityOption option)
    {
        TacticsAbilityDefinition ability = option.Ability;
        TacticsAbilityCardContent content = TacticsAbilityPreviewCalculator.BuildCardContent(displayedCharacter, ability, option.StatusText);
        widgets.Name.text = ability != null ? content.Title : "ABILITY";
        widgets.HeaderSummary.text = content.HeaderCombatSummary;
        widgets.HeaderSummary.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.HeaderCombatSummary));
        widgets.Meta.text = ability != null ? $"{content.Cost}    |    {content.Range}" : "No data";
        widgets.Description.text = ability != null ? content.Description : "No description.";
        widgets.Generated.text = content.GeneratedDescription;
        widgets.Generated.gameObject.SetActive(content.HasGeneratedDescription);
        widgets.MiddleDivider.SetActive(content.HasGeneratedDescription);
        widgets.Status.text = content.Status;
        widgets.Status.gameObject.SetActive(content.HasStatus);
        widgets.Button.interactable = option.IsInteractable;

        Color backgroundColor = option.IsSelected ? selectedAbilityColor : cardColor;
        if (!option.IsInteractable)
        {
            backgroundColor = cardDisabledColor;
        }

        widgets.Background.color = backgroundColor;
        widgets.Outline.effectColor = option.IsSelected
            ? accentColor
            : new Color(panelBorderColor.r, panelBorderColor.g, panelBorderColor.b, option.IsInteractable ? 0.9f : 0.3f);
        widgets.Name.color = option.IsInteractable ? primaryTextColor : disabledTextColor;
        widgets.HeaderSummary.color = option.IsInteractable ? cardMetaColor : disabledTextColor;
        widgets.Meta.color = option.IsInteractable ? cardMetaColor : disabledTextColor;
        widgets.Description.color = option.IsInteractable ? cardDescriptionColor : disabledTextColor;
        widgets.Generated.color = option.IsInteractable ? cardGeneratedColor : disabledTextColor;
        widgets.Status.color = option.IsInteractable ? cardStatusColor : disabledTextColor;

        widgets.Button.onClick.RemoveAllListeners();
        widgets.TooltipTrigger.Initialize(null, null, null);
        if (ability != null)
        {
            widgets.Button.onClick.AddListener(() => HandleAbilityEntryClicked(ability));
            widgets.TooltipTrigger.Initialize(
                eventData => HandleAbilityPointerEnter(ability, option.StatusText, eventData),
                _ => tooltipView?.Hide(),
                eventData => HandleAbilityPointerMove(eventData));
        }
    }

    private void HandleFlyoutBackgroundClicked()
    {
        if (!isFlyoutOpen)
        {
            return;
        }

        isFlyoutOpen = false;
        flyoutRoot.SetActive(false);
        tooltipView?.Hide();
    }

    private void HandleAbilityEntryClicked(TacticsAbilityDefinition ability)
    {
        isFlyoutOpen = false;
        flyoutRoot.SetActive(false);
        tooltipView?.Hide();
        AbilitySelected?.Invoke(ability);
    }

    private void HandleAbilityPointerEnter(TacticsAbilityDefinition ability, string statusText, PointerEventData eventData)
    {
        if (ability == null || tooltipView == null)
        {
            return;
        }

        Vector2 pointerPosition = eventData != null ? eventData.position : Input.mousePosition;
        tooltipView.Show(
            TacticsAbilityPreviewCalculator.BuildTooltipContent(displayedCharacter, ability, statusText),
            pointerPosition,
            rootCanvas);
    }

    private void HandleAbilityPointerMove(PointerEventData eventData)
    {
        if (tooltipView == null)
        {
            return;
        }

        Vector2 pointerPosition = eventData != null ? eventData.position : Input.mousePosition;
        tooltipView.UpdatePosition(pointerPosition);
    }

    private void RefreshFlyoutPosition()
    {
        if (flyoutRoot == null)
        {
            return;
        }

        RectTransform flyoutRect = flyoutRoot.GetComponent<RectTransform>();
        flyoutRect.anchoredPosition = new Vector2(-36f, -124f);
    }

    private void RefreshFlyoutHeader()
    {
        if (flyoutSubtitleText == null)
        {
            return;
        }

        flyoutSubtitleText.text = displayedCharacter != null
            ? $"{displayedCharacter.DisplayName.ToUpperInvariant()} LOADOUT"
            : "ABILITY LOADOUT";
    }

    private void RefreshAbilityGridLayout()
    {
        if (abilityGridLayout == null || abilityContentRoot == null)
        {
            return;
        }

        float contentWidth = abilityContentRoot.rect.width;
        if (contentWidth <= 0f)
        {
            contentWidth = FlyoutWidth - 96f;
        }

        float spacing = abilityGridLayout.spacing.x;
        float usableWidth = Mathf.Max(0f, contentWidth - abilityGridLayout.padding.left - abilityGridLayout.padding.right - spacing);
        float cellWidth = usableWidth / FlyoutColumnCount;
        abilityGridLayout.cellSize = new Vector2(Mathf.Max(320f, cellWidth), FlyoutCardHeight);
    }

    private GameObject CreateDivider(Transform parent, string objectName)
    {
        GameObject divider = CreateUiObject(objectName, parent);
        LayoutElement layoutElement = divider.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 1f;
        layoutElement.minHeight = 1f;

        Image dividerImage = divider.AddComponent<Image>();
        dividerImage.color = cardDividerColor;
        dividerImage.raycastTarget = false;
        return divider;
    }

    private readonly struct AbilityEntryWidgets
    {
        public AbilityEntryWidgets(
            GameObject root,
            Button button,
            Image background,
            Outline outline,
            Text name,
            Text headerSummary,
            Text meta,
            Text description,
            Text generated,
            Text status,
            GameObject topDivider,
            GameObject middleDivider,
            TacticsAbilityTooltipTrigger tooltipTrigger)
        {
            Root = root;
            Button = button;
            Background = background;
            Outline = outline;
            Name = name;
            HeaderSummary = headerSummary;
            Meta = meta;
            Description = description;
            Generated = generated;
            Status = status;
            TopDivider = topDivider;
            MiddleDivider = middleDivider;
            TooltipTrigger = tooltipTrigger;
        }

        public GameObject Root { get; }
        public Button Button { get; }
        public Image Background { get; }
        public Outline Outline { get; }
        public Text Name { get; }
        public Text HeaderSummary { get; }
        public Text Meta { get; }
        public Text Description { get; }
        public Text Generated { get; }
        public Text Status { get; }
        public GameObject TopDivider { get; }
        public GameObject MiddleDivider { get; }
        public TacticsAbilityTooltipTrigger TooltipTrigger { get; }
    }
}

public readonly struct TacticsActionMenuAbilityOption
{
    public TacticsActionMenuAbilityOption(
        TacticsAbilityDefinition ability,
        bool isInteractable,
        bool isSelected,
        string statusText)
    {
        Ability = ability;
        IsInteractable = isInteractable;
        IsSelected = isSelected;
        StatusText = statusText;
    }

    public TacticsAbilityDefinition Ability { get; }
    public bool IsInteractable { get; }
    public bool IsSelected { get; }
    public string StatusText { get; }
}
