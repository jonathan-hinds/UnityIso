using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsActionMenuView : MonoBehaviour
{
    public const string ActionMenuPrefabResourcePath = "UI/TacticsActionMenuPanel";
    public const string SpellMenuPrefabResourcePath = "UI/TacticsSpellMenuPanel";
    public const string SpellCardPrefabResourcePath = "UI/TacticsSpellCard";

    [Header("Canvas")]
    [SerializeField] private int sortingOrder = 5000;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasScaler rootScaler;
    [SerializeField] private GraphicRaycaster raycaster;

    [Header("Prefab Bindings")]
    [SerializeField] private TacticsActionMenuPanelPrefab actionMenuPanel;
    [SerializeField] private TacticsSpellMenuPanelPrefab spellMenuPanel;
    [SerializeField] private TacticsSpellCardView spellCardPrefab;
    [SerializeField] private TacticsAbilityTooltipView tooltipView;

    private readonly List<TacticsSpellCardView> spellCardPool = new();
    private TacticsCharacterController displayedCharacter;
    private IReadOnlyList<TacticsActionMenuAbilityOption> displayedAbilityOptions = Array.Empty<TacticsActionMenuAbilityOption>();
    private bool isSpellMenuOpen;
    private bool loggedMissingPrefabWarning;
    private Button descendButton;

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
        bool canDescend,
        int roundNumber,
        int turnNumber,
        int participantCount)
    {
        EnsureBuilt();
        if (!HasRequiredBindings())
        {
            return;
        }

        if (character == null)
        {
            Hide();
            return;
        }

        actionMenuPanel.Root.gameObject.SetActive(true);
        spellMenuPanel.Root.gameObject.SetActive(isSpellMenuOpen);

        if (!ReferenceEquals(displayedCharacter, character))
        {
            displayedCharacter = character;
            isSpellMenuOpen = false;
            spellMenuPanel.Root.gameObject.SetActive(false);
        }

        displayedAbilityOptions = abilityOptions ?? Array.Empty<TacticsActionMenuAbilityOption>();
        actionMenuPanel.CharacterNameText.text = character.DisplayName.ToUpperInvariant();
        actionMenuPanel.MoveButton.interactable = character.CanMoveThisTurn && !awaitingMoveTarget && !awaitingAbilityTarget;
        actionMenuPanel.OpenChestButton.gameObject.SetActive(canOpenChest);
        actionMenuPanel.OpenChestButton.interactable = canOpenChest && character.CanInteractThisTurn && !awaitingMoveTarget && !awaitingAbilityTarget;
        descendButton.gameObject.SetActive(canDescend);
        descendButton.interactable = canDescend && character.CanInteractThisTurn && !awaitingMoveTarget && !awaitingAbilityTarget;
        actionMenuPanel.AbilitiesButton.interactable = displayedAbilityOptions.Count > 0 &&
                                                       character.CanUseAbilitiesThisTurn &&
                                                       !awaitingMoveTarget;
        actionMenuPanel.EndTurnButton.interactable = character.CanEndTurn;

        if (!actionMenuPanel.AbilitiesButton.interactable)
        {
            CloseSpellMenu();
        }

        RefreshSpellMenuHeader();
        RebuildSpellCards();
    }

    public void Hide()
    {
        EnsureBuilt();
        if (!HasRequiredBindings())
        {
            return;
        }

        actionMenuPanel.Root.gameObject.SetActive(false);
        spellMenuPanel.Root.gameObject.SetActive(false);
        displayedCharacter = null;
        displayedAbilityOptions = Array.Empty<TacticsActionMenuAbilityOption>();
        isSpellMenuOpen = false;
        tooltipView?.Hide();
        ClearUnusedCards(0);
    }

    private void EnsureBuilt()
    {
        rootCanvas = rootCanvas != null ? rootCanvas : GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        rootScaler = rootScaler != null ? rootScaler : GetComponent<CanvasScaler>();
        if (rootScaler == null)
        {
            rootScaler = gameObject.AddComponent<CanvasScaler>();
        }

        raycaster = raycaster != null ? raycaster : GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = sortingOrder;

        rootScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        rootScaler.referenceResolution = new Vector2(1920f, 1080f);
        rootScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        rootScaler.matchWidthOrHeight = 0.5f;

        tooltipView = tooltipView != null ? tooltipView : GetComponent<TacticsAbilityTooltipView>();
        if (tooltipView == null)
        {
            tooltipView = gameObject.AddComponent<TacticsAbilityTooltipView>();
        }

        actionMenuPanel = actionMenuPanel != null
            ? actionMenuPanel
            : GetComponentInChildren<TacticsActionMenuPanelPrefab>(true);
        spellMenuPanel = spellMenuPanel != null
            ? spellMenuPanel
            : GetComponentInChildren<TacticsSpellMenuPanelPrefab>(true);

        if (actionMenuPanel == null)
        {
            actionMenuPanel = InstantiateUiPrefab(
                Resources.Load<TacticsActionMenuPanelPrefab>(ActionMenuPrefabResourcePath),
                "Action Menu");
        }

        if (spellMenuPanel == null)
        {
            spellMenuPanel = InstantiateUiPrefab(
                Resources.Load<TacticsSpellMenuPanelPrefab>(SpellMenuPrefabResourcePath),
                "Spell Menu");
        }

        if (spellCardPrefab == null)
        {
            spellCardPrefab = Resources.Load<TacticsSpellCardView>(SpellCardPrefabResourcePath);
        }

        BindStaticEvents();
        EnsureDescendButton();
    }

    private bool HasRequiredBindings()
    {
        bool hasBindings = actionMenuPanel != null &&
                           actionMenuPanel.HasRequiredBindings &&
                           spellMenuPanel != null &&
                           spellMenuPanel.HasRequiredBindings &&
                           spellCardPrefab != null;
        if (!hasBindings && !loggedMissingPrefabWarning)
        {
            loggedMissingPrefabWarning = true;
            Debug.LogWarning(
                "TacticsActionMenuView could not find its menu prefabs. Use Tools/Tactics/Rebuild Action Menu Prefabs to regenerate them.");
        }

        return hasBindings;
    }

    private void BindStaticEvents()
    {
        if (actionMenuPanel != null)
        {
            RebindButton(actionMenuPanel.MoveButton, HandleMoveClicked);
            RebindButton(actionMenuPanel.OpenChestButton, HandleOpenChestClicked);
            RebindButton(descendButton, HandleDescendClicked);
            RebindButton(actionMenuPanel.AbilitiesButton, HandleAbilitiesClicked);
            RebindButton(actionMenuPanel.EndTurnButton, HandleEndTurnClicked);
        }

        if (spellMenuPanel != null)
        {
            RebindButton(spellMenuPanel.DismissButton, HandleSpellMenuBackgroundClicked);
        }
    }

    private static void RebindButton(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private T InstantiateUiPrefab<T>(T prefab, string instanceName) where T : Component
    {
        if (prefab == null)
        {
            return null;
        }

        T instance = Instantiate(prefab, transform);
        instance.gameObject.name = instanceName;
        instance.transform.SetAsLastSibling();
        return instance;
    }

    private void HandleMoveClicked()
    {
        ActionSelected?.Invoke(TacticsHudActionType.Move);
    }

    private void HandleOpenChestClicked()
    {
        ActionSelected?.Invoke(TacticsHudActionType.OpenChest);
    }

    private void HandleDescendClicked()
    {
        ActionSelected?.Invoke(TacticsHudActionType.Descend);
    }

    private void HandleEndTurnClicked()
    {
        ActionSelected?.Invoke(TacticsHudActionType.EndTurn);
    }

    private void HandleAbilitiesClicked()
    {
        if (actionMenuPanel == null || !actionMenuPanel.AbilitiesButton.interactable)
        {
            return;
        }

        isSpellMenuOpen = !isSpellMenuOpen;
        spellMenuPanel.Root.gameObject.SetActive(isSpellMenuOpen);
        if (isSpellMenuOpen)
        {
            RefreshSpellMenuHeader();
            RebuildSpellCards();
        }
        else
        {
            tooltipView?.Hide();
        }
    }

    private void HandleSpellMenuBackgroundClicked()
    {
        if (!isSpellMenuOpen)
        {
            return;
        }

        CloseSpellMenu();
    }

    private void CloseSpellMenu()
    {
        isSpellMenuOpen = false;
        if (spellMenuPanel != null)
        {
            spellMenuPanel.Root.gameObject.SetActive(false);
        }

        tooltipView?.Hide();
    }

    private void RefreshSpellMenuHeader()
    {
        if (spellMenuPanel == null || spellMenuPanel.SubtitleText == null)
        {
            return;
        }

        spellMenuPanel.SubtitleText.text = displayedCharacter != null
            ? $"{displayedCharacter.DisplayName.ToUpperInvariant()} LOADOUT"
            : "ABILITY LOADOUT";
    }

    private void RebuildSpellCards()
    {
        if (spellMenuPanel == null || spellMenuPanel.ContentRoot == null || spellCardPrefab == null)
        {
            return;
        }

        int optionCount = displayedAbilityOptions != null ? displayedAbilityOptions.Count : 0;
        EnsureSpellCardPool(optionCount);
        RefreshSpellCardGridLayout();

        for (int i = 0; i < optionCount; i++)
        {
            TacticsSpellCardView card = spellCardPool[i];
            card.transform.SetSiblingIndex(i);
            ApplySpellCard(card, displayedAbilityOptions[i]);
        }

        ClearUnusedCards(optionCount);
        spellMenuPanel.EmptyStateText.gameObject.SetActive(optionCount == 0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(spellMenuPanel.ContentRoot);
    }

    private void RefreshSpellCardGridLayout()
    {
        GridLayoutGroup gridLayout = spellMenuPanel.ContentRoot.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            return;
        }

        int columnCount = gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? Mathf.Max(1, gridLayout.constraintCount)
            : 2;
        RectTransform viewportRect = spellMenuPanel.ContentRoot.parent as RectTransform;
        float availableWidth = viewportRect != null ? viewportRect.rect.width : spellMenuPanel.ContentRoot.rect.width;
        if (availableWidth <= 0f)
        {
            return;
        }

        float totalSpacing = gridLayout.spacing.x * Mathf.Max(0, columnCount - 1);
        float usableWidth = availableWidth - gridLayout.padding.left - gridLayout.padding.right - totalSpacing;
        float cardWidth = usableWidth / columnCount;
        if (cardWidth <= 0f)
        {
            return;
        }

        gridLayout.cellSize = new Vector2(cardWidth, gridLayout.cellSize.y);
    }

    private void EnsureSpellCardPool(int count)
    {
        while (spellCardPool.Count < count)
        {
            TacticsSpellCardView instance = Instantiate(spellCardPrefab, spellMenuPanel.ContentRoot);
            instance.gameObject.name = $"Spell Card {spellCardPool.Count + 1}";
            spellCardPool.Add(instance);
        }
    }

    private void ClearUnusedCards(int startIndex)
    {
        for (int i = startIndex; i < spellCardPool.Count; i++)
        {
            spellCardPool[i].Clear();
        }
    }

    private void ApplySpellCard(TacticsSpellCardView card, TacticsActionMenuAbilityOption option)
    {
        TacticsAbilityDefinition ability = option.Ability;
        TacticsAbilityCardContent content = TacticsAbilityPreviewCalculator.BuildCardContent(displayedCharacter, ability, option.StatusText);

        card.Bind(
            content,
            option.IsInteractable,
            option.IsSelected,
            ability != null ? () => HandleSpellCardClicked(ability) : null,
            ability != null ? eventData => HandleAbilityPointerEnter(ability, option.StatusText, eventData) : null,
            ability != null ? _ => tooltipView?.Hide() : null,
            ability != null ? HandleAbilityPointerMove : null);
    }

    private void HandleSpellCardClicked(TacticsAbilityDefinition ability)
    {
        CloseSpellMenu();
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

    private void EnsureDescendButton()
    {
        if (actionMenuPanel == null || actionMenuPanel.OpenChestButton == null)
        {
            return;
        }

        if (descendButton == null)
        {
            Transform existing = actionMenuPanel.Root != null
                ? actionMenuPanel.Root.Find("DescendButton")
                : null;
            if (existing != null)
            {
                descendButton = existing.GetComponent<Button>();
            }
        }

        if (descendButton == null)
        {
            descendButton = Instantiate(actionMenuPanel.OpenChestButton, actionMenuPanel.OpenChestButton.transform.parent);
            descendButton.gameObject.name = "DescendButton";
        }

        descendButton.transform.SetSiblingIndex(actionMenuPanel.OpenChestButton.transform.GetSiblingIndex() + 1);

        Text label = descendButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "Descend";
        }

        RebindButton(descendButton, HandleDescendClicked);
        descendButton.gameObject.SetActive(false);
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
