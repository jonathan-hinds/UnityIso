using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsInventoryMenuView : MonoBehaviour
{
    public const string InventoryPanelPrefabResourcePath = "UI/TacticsInventoryPanel";
    public const string InventoryItemCardPrefabResourcePath = "UI/TacticsInventoryItemCard";

    [SerializeField] private int sortingOrder = 4997;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasScaler rootScaler;
    [SerializeField] private GraphicRaycaster raycaster;
    [SerializeField] private TacticsInventoryPanelPrefab inventoryPanel;
    [SerializeField] private TacticsInventoryItemCardView itemCardPrefab;
    [SerializeField] private TacticsAbilityTooltipView tooltipView;

    private readonly Dictionary<string, Button> characterButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TacticsEquipmentSlot, TacticsInventoryItemCardView> equipmentCards = new();
    private readonly List<TacticsInventoryItemCardView> inventoryCardPool = new();
    private TacticsCharacterController selectedCharacter;
    private TacticsCharacterInventoryService inventoryService;
    private TacticsCoopSessionCoordinator coopSessionCoordinator;
    private TacticsTurnManager turnManager;
    private bool loggedMissingPrefabWarning;

    public bool IsPanelVisible => inventoryPanel != null && inventoryPanel.Root != null && inventoryPanel.Root.gameObject.activeSelf;

    private void Awake()
    {
        EnsureBuilt();
        SetPanelVisible(false);
    }

    private void Update()
    {
        if (!IsPanelVisible)
        {
            return;
        }

        RefreshCharacterList();
    }

    public void AssignDependencies(
        TacticsCharacterInventoryService service,
        TacticsCoopSessionCoordinator coordinator,
        TacticsTurnManager assignedTurnManager)
    {
        inventoryService = service;
        coopSessionCoordinator = coordinator;
        turnManager = assignedTurnManager;
        RefreshCharacterList();
    }

    public void TogglePanelVisibility()
    {
        SetPanelVisible(!IsPanelVisible);
    }

    public void SetPanelVisible(bool visible)
    {
        EnsureBuilt();
        if (inventoryPanel?.Root == null)
        {
            return;
        }

        inventoryPanel.Root.gameObject.SetActive(visible);
        if (visible)
        {
            selectedCharacter = ResolvePreferredCharacter(preserveCurrentSelection: false);
            RefreshCharacterList();
        }
        else
        {
            tooltipView?.Hide();
        }
    }

    public void RefreshCharacterList()
    {
        EnsureBuilt();
        if (!HasRequiredBindings())
        {
            return;
        }

        RebuildCharacterButtons();
        RefreshSelection();
        RebuildEquipmentCards();
        RebuildInventoryCards();
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

        inventoryPanel = inventoryPanel != null
            ? inventoryPanel
            : GetComponentInChildren<TacticsInventoryPanelPrefab>(true);
        if (inventoryPanel == null)
        {
            TacticsInventoryPanelPrefab prefab = Resources.Load<TacticsInventoryPanelPrefab>(InventoryPanelPrefabResourcePath);
            if (prefab != null)
            {
                inventoryPanel = Instantiate(prefab, transform);
                inventoryPanel.gameObject.name = "Inventory Panel";
            }
        }

        if (itemCardPrefab == null)
        {
            itemCardPrefab = Resources.Load<TacticsInventoryItemCardView>(InventoryItemCardPrefabResourcePath);
        }

        if (inventoryPanel?.DismissButton != null)
        {
            inventoryPanel.DismissButton.onClick.RemoveListener(HandleDismissClicked);
            inventoryPanel.DismissButton.onClick.AddListener(HandleDismissClicked);
        }
    }

    private bool HasRequiredBindings()
    {
        bool hasBindings = inventoryPanel != null &&
                           inventoryPanel.HasRequiredBindings &&
                           itemCardPrefab != null;
        if (!hasBindings && !loggedMissingPrefabWarning)
        {
            loggedMissingPrefabWarning = true;
            Debug.LogWarning("Inventory menu prefabs are missing. Rebuild them from Tools/Tactics/Rebuild Inventory Menu Prefabs.");
        }

        return hasBindings;
    }

    private void HandleDismissClicked()
    {
        SetPanelVisible(false);
    }

    private void RebuildCharacterButtons()
    {
        characterButtons.Clear();
        for (int i = inventoryPanel.CharacterListRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(inventoryPanel.CharacterListRoot.GetChild(i).gameObject);
        }

        List<TacticsCharacterController> characters = GetControllablePlayerCharacters();
        if (selectedCharacter != null && !characters.Contains(selectedCharacter))
        {
            selectedCharacter = null;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            TacticsCharacterController character = characters[i];
            Button button = CreateCharacterButton(character);
            characterButtons[character.RuntimeCharacterId] = button;
        }

        if (selectedCharacter == null && characters.Count > 0)
        {
            selectedCharacter = ResolvePreferredCharacter(characters, preserveCurrentSelection: false) ?? characters[0];
        }
    }

    private void RefreshSelection()
    {
        inventoryPanel.TitleText.text = "INVENTORY";
        inventoryPanel.SubtitleText.text = selectedCharacter != null
            ? $"{selectedCharacter.DisplayName.ToUpperInvariant()} PACK"
            : "NO CHARACTER";

        foreach (KeyValuePair<string, Button> pair in characterButtons)
        {
            Image image = pair.Value.targetGraphic as Image;
            if (image == null)
            {
                continue;
            }

            bool isSelected = selectedCharacter != null &&
                              string.Equals(selectedCharacter.RuntimeCharacterId, pair.Key, StringComparison.OrdinalIgnoreCase);
            image.color = isSelected
                ? new Color(0.76f, 0.69f, 0.5f, 1f)
                : new Color(0.16f, 0.17f, 0.2f, 1f);
        }
    }

    private void RebuildEquipmentCards()
    {
        EnsureEquipmentCards();
        foreach (KeyValuePair<TacticsEquipmentSlot, TacticsInventoryItemCardView> pair in equipmentCards)
        {
            TacticsEquipmentSlot slot = pair.Key;
            TacticsInventoryItemCardView card = pair.Value;
            if (selectedCharacter != null && selectedCharacter.TryGetEquippedItem(slot, out TacticsEquipmentRuntimeSummary summary))
            {
                BindItemCard(
                    card,
                    summary.SaveData.instanceId,
                    summary.Equipment,
                    summary.SaveData != null ? summary.SaveData.quantity : 1,
                    isEquipped: true,
                    onLeftClick: null,
                    onRightClick: _ => HandleEquipmentSlotRightClick(slot),
                    emptyState: false);
            }
            else
            {
                card.BindEmpty();
            }
        }
    }

    private void RebuildInventoryCards()
    {
        IReadOnlyList<TacticsInventoryResolvedItem> items = selectedCharacter != null
            ? selectedCharacter.GetResolvedInventoryItems()
            : Array.Empty<TacticsInventoryResolvedItem>();
        EnsureInventoryCardPool(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            TacticsInventoryResolvedItem item = items[i];
            TacticsInventoryItemCardView card = inventoryCardPool[i];
            card.transform.SetSiblingIndex(i);
            BindItemCard(
                card,
                item.InstanceId,
                item.Definition,
                item.Quantity,
                item.IsEquipped,
                () => { },
                _ => HandleInventoryItemRightClick(item),
                emptyState: false);
        }

        for (int i = items.Count; i < inventoryCardPool.Count; i++)
        {
            inventoryCardPool[i].BindEmpty();
        }

        inventoryPanel.EmptyStateText.gameObject.SetActive(items.Count == 0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryPanel.InventoryContentRoot);
    }

    private void BindItemCard(
        TacticsInventoryItemCardView card,
        string instanceId,
        TacticsItemDefinition item,
        int quantity,
        bool isEquipped,
        Action onLeftClick,
        Action<PointerEventData> onRightClick,
        bool emptyState)
    {
        card.Bind(
            item != null ? item.Thumbnail : null,
            quantity,
            isEquipped,
            onLeftClick,
            onRightClick,
            item != null ? eventData => HandleItemPointerEnter(item, eventData) : null,
            item != null ? _ => tooltipView?.Hide() : null,
            item != null ? HandlePointerMove : null);
    }

    private void HandleItemPointerEnter(TacticsItemDefinition item, PointerEventData eventData)
    {
        if (tooltipView == null || item == null)
        {
            return;
        }

        Vector2 pointerPosition = eventData != null ? eventData.position : Input.mousePosition;
        tooltipView.Show(TacticsItemTooltipUtility.BuildTooltipContent(item), pointerPosition, rootCanvas);
    }

    private void HandlePointerMove(PointerEventData eventData)
    {
        if (tooltipView == null)
        {
            return;
        }

        tooltipView.UpdatePosition(eventData != null ? eventData.position : Input.mousePosition);
    }

    private void HandleInventoryItemRightClick(TacticsInventoryResolvedItem item)
    {
        if (selectedCharacter == null || item.Definition == null)
        {
            return;
        }

        TacticsInventoryActionKind action = selectedCharacter.GetDefaultInventoryAction(item.InstanceId);
        TacticsEquipmentSlot slot = item.Definition is TacticsEquipmentItemDefinition equipment
            ? equipment.Slot
            : TacticsEquipmentSlot.Weapon;
        coopSessionCoordinator?.RequestInventoryAction(selectedCharacter, action, item.InstanceId, slot);
        RefreshCharacterList();
    }

    private void HandleEquipmentSlotRightClick(TacticsEquipmentSlot slot)
    {
        if (selectedCharacter == null)
        {
            return;
        }

        coopSessionCoordinator?.RequestInventoryAction(selectedCharacter, TacticsInventoryActionKind.Unequip, string.Empty, slot);
        RefreshCharacterList();
    }

    private void EnsureEquipmentCards()
    {
        foreach (TacticsEquipmentSlot slot in Enum.GetValues(typeof(TacticsEquipmentSlot)))
        {
            if (equipmentCards.ContainsKey(slot))
            {
                continue;
            }

            TacticsInventoryItemCardView card = Instantiate(itemCardPrefab, inventoryPanel.EquipmentRoot);
            card.gameObject.name = $"{slot} Slot";
            equipmentCards[slot] = card;
        }
    }

    private void EnsureInventoryCardPool(int count)
    {
        while (inventoryCardPool.Count < count)
        {
            TacticsInventoryItemCardView instance = Instantiate(itemCardPrefab, inventoryPanel.InventoryContentRoot);
            instance.gameObject.name = $"Inventory Card {inventoryCardPool.Count + 1}";
            inventoryCardPool.Add(instance);
        }
    }

    private Button CreateCharacterButton(TacticsCharacterController character)
    {
        GameObject buttonObject = new GameObject($"{character.DisplayName} Button", typeof(RectTransform));
        buttonObject.transform.SetParent(inventoryPanel.CharacterListRoot, false);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 48f;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.2f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            selectedCharacter = character;
            RefreshCharacterList();
        });

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.84f, 0.72f, 1f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Text label = labelObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 16;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(0.96f, 0.94f, 0.89f, 1f);
        label.alignment = TextAnchor.MiddleLeft;
        label.text = character.DisplayName.ToUpperInvariant();
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 0f);
        labelRect.offsetMax = new Vector2(-14f, 0f);
        return button;
    }

    private List<TacticsCharacterController> GetControllablePlayerCharacters()
    {
        TacticsCharacterController[] allCharacters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        List<TacticsCharacterController> results = new List<TacticsCharacterController>();
        for (int i = 0; i < allCharacters.Length; i++)
        {
            TacticsCharacterController character = allCharacters[i];
            if (character == null || !character.IsPlayerControlled)
            {
                continue;
            }

            bool locallyOwned = coopSessionCoordinator == null || coopSessionCoordinator.CanLocalPlayerControlCharacter(character);
            if (!locallyOwned)
            {
                continue;
            }

            results.Add(character);
        }

        results.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    private TacticsCharacterController ResolvePreferredCharacter(bool preserveCurrentSelection)
    {
        return ResolvePreferredCharacter(GetControllablePlayerCharacters(), preserveCurrentSelection);
    }

    private TacticsCharacterController ResolvePreferredCharacter(
        List<TacticsCharacterController> controllableCharacters,
        bool preserveCurrentSelection)
    {
        if (preserveCurrentSelection &&
            selectedCharacter != null &&
            controllableCharacters.Contains(selectedCharacter))
        {
            return selectedCharacter;
        }

        TacticsCharacterController activeCharacter = turnManager != null ? turnManager.ActiveCharacter : null;
        if (activeCharacter != null &&
            activeCharacter.IsPlayerControlled &&
            controllableCharacters.Contains(activeCharacter))
        {
            return activeCharacter;
        }

        return controllableCharacters.Count > 0 ? controllableCharacters[0] : null;
    }
}
