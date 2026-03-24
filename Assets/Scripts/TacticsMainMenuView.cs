using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class TacticsMainMenuView : MonoBehaviour
{
    private const string PanelSettingsResourcePath = "UI/TacticsMainMenuPanelSettings";
    private const string VisualTreeResourcePath = "UI/TacticsMainMenu";
    private const string RootElementName = "main-menu-root";
    private const string MainPageName = "main-page";
    private const string EditPageName = "edit-team-page";
    private const string PlayButtonName = "play-button";
    private const string EditTeamButtonName = "edit-team-button";
    private const string StatusLabelName = "status-label";
    private const string TeamSummaryLabelName = "team-summary-label";
    private const string TeamSlotContainerName = "team-slot-container";
    private const string RosterGridContainerName = "character-grid-container";
    private const string EditorBackButtonName = "editor-back-button";
    private const string EditorSaveButtonName = "editor-save-button";
    private const string EditorStatusLabelName = "editor-status-label";
    private const string DragGhostName = "drag-ghost";

    [Header("Assets")]
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private VisualTreeAsset visualTreeAsset;

    [Header("Preview Tuning")]
    [SerializeField] private TacticsCharacterCardPreview.PreviewSettings previewSettings = default;
    [SerializeField] private Vector2 previewWindowSize = new Vector2(225f, 256f);

    private UIDocument uiDocument;
    private VisualElement rootElement;
    private VisualElement mainPage;
    private VisualElement editTeamPage;
    private Button playButton;
    private Button editTeamButton;
    private Label statusLabel;
    private Label teamSummaryLabel;
    private VisualElement teamSlotContainer;
    private VisualElement rosterGridContainer;
    private Button editorBackButton;
    private Button editorSaveButton;
    private Label editorStatusLabel;
    private Label dragGhost;

    private ProceduralIsometricMapGenerator sourceMapGenerator;
    private TacticsPartySelectionService partySelectionService;
    private TacticsCharacterRoster roster;
    private Dictionary<string, TacticsCharacterDefinition> definitionsById = new(StringComparer.OrdinalIgnoreCase);
    private TacticsPartySelection savedSelection;
    private TacticsPartySelection workingSelection;
    private readonly List<SlotWidget> slotWidgets = new();
    private readonly List<RosterCardWidget> rosterCardWidgets = new();
    private readonly List<TacticsCharacterCardPreview> previews = new();
    private bool isEditPageVisible;
    private bool isDragging;
    private string dragCharacterId = string.Empty;
    private int dragSourceSlotIndex = -1;
    private int previewCounter;

    public event Action PlayRequested;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        EnsurePreviewSettingsInitialized();
        uiDocument = GetComponent<UIDocument>();
        ResolveAssets();
        ApplyDocumentConfiguration();
        CacheElements();
    }

    private void OnEnable()
    {
        EnsurePreviewSettingsInitialized();
        CacheElements();
        RegisterCallbacks();
        RebuildEditor();
        RefreshAllUi();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
        DisposePreviews();
        CancelDrag();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        for (int i = 0; i < previews.Count; i++)
        {
            previews[i]?.Tick(deltaTime);
        }
    }

    public void AssignDependencies(ProceduralIsometricMapGenerator mapGenerator, TacticsPartySelectionService selectionService)
    {
        EnsurePreviewSettingsInitialized();
        sourceMapGenerator = mapGenerator;
        partySelectionService = selectionService ?? new TacticsPartySelectionService();
        roster = partySelectionService.LoadRoster();
        definitionsById = roster != null ? roster.BuildLookupById() : new Dictionary<string, TacticsCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        savedSelection = partySelectionService.LoadSelection();
        workingSelection = savedSelection;
        RebuildEditor();
        RefreshAllUi();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        CacheElements();
        RefreshAllUi();
        SetInteractable(true);

        if (rootElement != null)
        {
            rootElement.style.display = DisplayStyle.Flex;
        }

        ShowMainPage();
        playButton?.Focus();
    }

    public void Hide()
    {
        if (rootElement != null)
        {
            rootElement.style.display = DisplayStyle.None;
        }

        gameObject.SetActive(false);
    }

    public void SetInteractable(bool interactable)
    {
        CacheElements();
        playButton?.SetEnabled(interactable);
        editTeamButton?.SetEnabled(interactable);
        editorBackButton?.SetEnabled(interactable);
        editorSaveButton?.SetEnabled(interactable && CanSaveWorkingSelection());
    }

    public void SetStatusText(string text)
    {
        CacheElements();
        if (statusLabel == null)
        {
            return;
        }

        statusLabel.text = text ?? string.Empty;
        statusLabel.style.display = string.IsNullOrWhiteSpace(statusLabel.text)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private void ResolveAssets()
    {
        panelSettings ??= Resources.Load<PanelSettings>(PanelSettingsResourcePath);
        visualTreeAsset ??= Resources.Load<VisualTreeAsset>(VisualTreeResourcePath);
    }

    private void ApplyDocumentConfiguration()
    {
        if (uiDocument == null)
        {
            return;
        }

        if (panelSettings != null)
        {
            uiDocument.panelSettings = panelSettings;
        }

        if (visualTreeAsset != null)
        {
            uiDocument.visualTreeAsset = visualTreeAsset;
        }
    }

    private void CacheElements()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            return;
        }

        rootElement = uiDocument.rootVisualElement.Q<VisualElement>(RootElementName) ?? uiDocument.rootVisualElement;
        mainPage = rootElement.Q<VisualElement>(MainPageName);
        editTeamPage = rootElement.Q<VisualElement>(EditPageName);
        playButton = rootElement.Q<Button>(PlayButtonName);
        editTeamButton = rootElement.Q<Button>(EditTeamButtonName);
        statusLabel = rootElement.Q<Label>(StatusLabelName);
        teamSummaryLabel = rootElement.Q<Label>(TeamSummaryLabelName);
        teamSlotContainer = rootElement.Q<VisualElement>(TeamSlotContainerName);
        rosterGridContainer = rootElement.Q<VisualElement>(RosterGridContainerName);
        editorBackButton = rootElement.Q<Button>(EditorBackButtonName);
        editorSaveButton = rootElement.Q<Button>(EditorSaveButtonName);
        editorStatusLabel = rootElement.Q<Label>(EditorStatusLabelName);
        dragGhost = rootElement.Q<Label>(DragGhostName);
    }

    private void RegisterCallbacks()
    {
        if (playButton != null)
        {
            playButton.clicked -= HandlePlayButtonClicked;
            playButton.clicked += HandlePlayButtonClicked;
        }

        if (editTeamButton != null)
        {
            editTeamButton.clicked -= HandleEditTeamButtonClicked;
            editTeamButton.clicked += HandleEditTeamButtonClicked;
        }

        if (editorBackButton != null)
        {
            editorBackButton.clicked -= HandleEditorBackButtonClicked;
            editorBackButton.clicked += HandleEditorBackButtonClicked;
        }

        if (editorSaveButton != null)
        {
            editorSaveButton.clicked -= HandleEditorSaveButtonClicked;
            editorSaveButton.clicked += HandleEditorSaveButtonClicked;
        }
    }

    private void UnregisterCallbacks()
    {
        if (playButton != null)
        {
            playButton.clicked -= HandlePlayButtonClicked;
        }

        if (editTeamButton != null)
        {
            editTeamButton.clicked -= HandleEditTeamButtonClicked;
        }

        if (editorBackButton != null)
        {
            editorBackButton.clicked -= HandleEditorBackButtonClicked;
        }

        if (editorSaveButton != null)
        {
            editorSaveButton.clicked -= HandleEditorSaveButtonClicked;
        }
    }

    private void RebuildEditor()
    {
        CacheElements();
        if (teamSlotContainer == null || rosterGridContainer == null)
        {
            return;
        }

        DisposePreviews();
        previewCounter = 0;
        slotWidgets.Clear();
        rosterCardWidgets.Clear();
        teamSlotContainer.Clear();
        rosterGridContainer.Clear();

        int capacity = savedSelection != null ? savedSelection.Capacity : TacticsPartySelection.DefaultCapacity;
        for (int i = 0; i < capacity; i++)
        {
            slotWidgets.Add(CreateSlotWidget(i));
        }

        IReadOnlyList<TacticsCharacterDefinition> playableCharacters = roster?.PlayableCharacters ?? Array.Empty<TacticsCharacterDefinition>();
        for (int i = 0; i < playableCharacters.Count; i++)
        {
            TacticsCharacterDefinition definition = playableCharacters[i];
            if (definition == null)
            {
                continue;
            }

            rosterCardWidgets.Add(CreateRosterCardWidget(definition));
        }
    }

    private void RefreshAllUi()
    {
        savedSelection ??= partySelectionService != null ? partySelectionService.LoadSelection() : TacticsPartySelection.CreateDefault(roster);
        workingSelection ??= savedSelection;

        RefreshTeamSummary();
        RefreshSlotWidgets();
        RefreshRosterWidgets();
        RefreshEditorStatus();

        if (mainPage != null)
        {
            mainPage.style.display = isEditPageVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (editTeamPage != null)
        {
            editTeamPage.style.display = isEditPageVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void RefreshTeamSummary()
    {
        if (teamSummaryLabel == null)
        {
            return;
        }

        IReadOnlyList<TacticsCharacterDefinition> selectedParty = savedSelection != null
            ? savedSelection.ResolveDefinitions(roster)
            : Array.Empty<TacticsCharacterDefinition>();
        if (selectedParty.Count == 0)
        {
            teamSummaryLabel.text = "Current Team: No party saved yet";
            return;
        }

        List<string> names = new List<string>(selectedParty.Count);
        for (int i = 0; i < selectedParty.Count; i++)
        {
            if (selectedParty[i] == null)
            {
                continue;
            }

            names.Add(selectedParty[i].DisplayName);
        }

        teamSummaryLabel.text = $"Current Team: {string.Join("  /  ", names)}";
    }

    private void RefreshSlotWidgets()
    {
        for (int i = 0; i < slotWidgets.Count; i++)
        {
            SlotWidget widget = slotWidgets[i];
            string characterId = workingSelection != null ? workingSelection.GetCharacterId(widget.SlotIndex) : string.Empty;
            TacticsCharacterDefinition definition = null;
            bool hasAssignedCharacter = !string.IsNullOrEmpty(characterId) && definitionsById.TryGetValue(characterId, out definition);

            widget.Root.EnableInClassList("team-slot--active", hasAssignedCharacter);
            widget.Root.EnableInClassList("team-slot--empty", !hasAssignedCharacter);
            widget.PreviewImage.style.display = hasAssignedCharacter ? DisplayStyle.Flex : DisplayStyle.None;
            widget.ClearButton.style.display = hasAssignedCharacter ? DisplayStyle.Flex : DisplayStyle.None;
            widget.ClearButton.SetEnabled(hasAssignedCharacter);

            if (!hasAssignedCharacter)
            {
                widget.NameLabel.text = $"Open Slot {widget.SlotIndex + 1:00}";
                widget.MetaLabel.text = "Drag a party member here";
                widget.PreviewImage.image = null;
                widget.Preview?.SetHovered(false);
                widget.Preview?.Dispose();
                widget.Preview = null;
                widget.CurrentCharacterId = string.Empty;
                continue;
            }

            widget.NameLabel.text = definition.DisplayName.ToUpperInvariant();
            widget.MetaLabel.text = $"Slot {widget.SlotIndex + 1:00}  /  Ready";
            EnsureSlotPreview(widget, definition);
        }
    }

    private void RefreshRosterWidgets()
    {
        for (int i = 0; i < rosterCardWidgets.Count; i++)
        {
            RosterCardWidget widget = rosterCardWidgets[i];
            bool isAssigned = workingSelection != null && workingSelection.Contains(widget.Definition.CharacterId);
            widget.Root.EnableInClassList("roster-card--assigned", isAssigned);
            widget.StatusLabel.text = isAssigned ? "IN PARTY" : "AVAILABLE";
        }
    }

    private void RefreshEditorStatus()
    {
        if (editorStatusLabel == null)
        {
            return;
        }

        int assignedCount = CountAssignedMembers(workingSelection);
        int requiredCount = GetRequiredTeamSize();
        bool isDirty = !SelectionsMatch(savedSelection, workingSelection);

        editorStatusLabel.text = isDirty
            ? $"Unsaved team changes. {assignedCount}/{requiredCount} slot{(requiredCount == 1 ? string.Empty : "s")} locked in."
            : $"Choose up to {workingSelection.Capacity} party members. Hover a card to preview its movement stance.";
        editorSaveButton?.SetEnabled(CanSaveWorkingSelection());
    }

    private SlotWidget CreateSlotWidget(int slotIndex)
    {
        VisualElement root = new VisualElement();
        root.name = $"team-slot-{slotIndex}";
        root.AddToClassList("team-slot");

        Label slotIndexLabel = new Label($"SLOT {slotIndex + 1:00}");
        slotIndexLabel.AddToClassList("team-slot-index");
        root.Add(slotIndexLabel);

        Image previewImage = new Image
        {
            scaleMode = ScaleMode.ScaleToFit
        };
        previewImage.AddToClassList("team-slot-preview");
        ApplySharedPreviewImageSizing(previewImage);
        root.Add(previewImage);

        Label nameLabel = new Label();
        nameLabel.AddToClassList("team-slot-name");
        root.Add(nameLabel);

        Label metaLabel = new Label();
        metaLabel.AddToClassList("team-slot-meta");
        root.Add(metaLabel);

        Button clearButton = new Button(() =>
        {
            workingSelection = workingSelection.ClearSlot(slotIndex);
            RefreshAllUi();
        })
        {
            text = "Release"
        };
        clearButton.AddToClassList("team-slot-clear");
        root.Add(clearButton);

        teamSlotContainer.Add(root);

        SlotWidget widget = new SlotWidget
        {
            SlotIndex = slotIndex,
            Root = root,
            PreviewImage = previewImage,
            NameLabel = nameLabel,
            MetaLabel = metaLabel,
            ClearButton = clearButton
        };

        RegisterCardInteractions(
            root,
            () => workingSelection != null ? workingSelection.GetCharacterId(slotIndex) : string.Empty,
            () => slotIndex,
            hovered => widget.Preview?.SetHovered(hovered));

        return widget;
    }

    private RosterCardWidget CreateRosterCardWidget(TacticsCharacterDefinition definition)
    {
        VisualElement root = new VisualElement();
        root.name = $"roster-card-{definition.CharacterId}";
        root.AddToClassList("roster-card");

        Image previewImage = new Image
        {
            scaleMode = ScaleMode.ScaleToFit,
            image = null
        };
        previewImage.AddToClassList("roster-card-preview");
        ApplySharedPreviewImageSizing(previewImage);
        root.Add(previewImage);

        VisualElement footer = new VisualElement();
        footer.AddToClassList("roster-card-footer");

        Label nameLabel = new Label(definition.DisplayName.ToUpperInvariant());
        nameLabel.AddToClassList("roster-card-name");
        footer.Add(nameLabel);

        Label status = new Label("AVAILABLE");
        status.AddToClassList("roster-card-status");
        footer.Add(status);

        root.Add(footer);
        rosterGridContainer.Add(root);

        RosterCardWidget widget = new RosterCardWidget
        {
            Definition = definition,
            Root = root,
            PreviewImage = previewImage,
            StatusLabel = status,
            Preview = CreatePreview(definition)
        };
        widget.PreviewImage.image = widget.Preview?.Texture;

        RegisterCardInteractions(
            root,
            () => definition.CharacterId,
            () => -1,
            hovered => widget.Preview?.SetHovered(hovered));

        return widget;
    }

    private void EnsureSlotPreview(SlotWidget widget, TacticsCharacterDefinition definition)
    {
        if (widget.CurrentCharacterId == definition.CharacterId && widget.Preview != null)
        {
            widget.PreviewImage.image = widget.Preview.Texture;
            return;
        }

        widget.Preview?.Dispose();
        widget.CurrentCharacterId = definition.CharacterId;
        widget.Preview = CreatePreview(definition);
        widget.PreviewImage.image = widget.Preview?.Texture;
    }

    private TacticsCharacterCardPreview CreatePreview(TacticsCharacterDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        TacticsCharacterCardPreview preview = new TacticsCharacterCardPreview(transform, definition, sourceMapGenerator, previewCounter++, previewSettings);
        previews.Add(preview);
        return preview;
    }

    private void ApplySharedPreviewImageSizing(Image previewImage)
    {
        if (previewImage == null)
        {
            return;
        }

        Vector2 size = GetSanitizedPreviewWindowSize();
        previewImage.style.width = size.x;
        previewImage.style.height = size.y;
        previewImage.style.minWidth = size.x;
        previewImage.style.maxWidth = size.x;
        previewImage.style.minHeight = size.y;
        previewImage.style.maxHeight = size.y;
        previewImage.style.flexGrow = 0f;
        previewImage.style.flexShrink = 0f;
        previewImage.style.alignSelf = Align.Center;
    }

    private void RegisterCardInteractions(
        VisualElement element,
        Func<string> getCharacterId,
        Func<int> getSourceSlotIndex,
        Action<bool> setHovered)
    {
        element.RegisterCallback<PointerEnterEvent>(_ => setHovered?.Invoke(true));
        element.RegisterCallback<PointerLeaveEvent>(_ => setHovered?.Invoke(false));
        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
            {
                return;
            }

            string characterId = getCharacterId?.Invoke();
            if (string.IsNullOrEmpty(characterId))
            {
                return;
            }

            BeginDrag(characterId, getSourceSlotIndex != null ? getSourceSlotIndex.Invoke() : -1, evt.position);
            element.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });
        element.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!isDragging || !element.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            UpdateDrag(evt.position);
            evt.StopPropagation();
        });
        element.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (element.HasPointerCapture(evt.pointerId))
            {
                element.ReleasePointer(evt.pointerId);
            }

            if (!isDragging)
            {
                return;
            }

            EndDrag(evt.position);
            evt.StopPropagation();
        });
    }

    private void BeginDrag(string characterId, int sourceSlotIndex, Vector2 pointerPosition)
    {
        dragCharacterId = characterId;
        dragSourceSlotIndex = sourceSlotIndex;
        isDragging = true;

        if (dragGhost != null && definitionsById.TryGetValue(characterId, out TacticsCharacterDefinition definition))
        {
            dragGhost.text = definition.DisplayName.ToUpperInvariant();
            dragGhost.style.display = DisplayStyle.Flex;
        }

        UpdateDrag(pointerPosition);
    }

    private void UpdateDrag(Vector2 pointerPosition)
    {
        if (!isDragging || rootElement == null || dragGhost == null)
        {
            return;
        }

        Vector2 localPosition = rootElement.WorldToLocal(pointerPosition);
        dragGhost.style.left = localPosition.x + 18f;
        dragGhost.style.top = localPosition.y + 18f;

        int hoveredSlotIndex = FindHoveredSlotIndex(pointerPosition);
        for (int i = 0; i < slotWidgets.Count; i++)
        {
            slotWidgets[i].Root.EnableInClassList("team-slot--drop-target", i == hoveredSlotIndex);
        }
    }

    private void EndDrag(Vector2 pointerPosition)
    {
        int hoveredSlotIndex = FindHoveredSlotIndex(pointerPosition);
        if (hoveredSlotIndex >= 0)
        {
            workingSelection = workingSelection.AssignCharacter(hoveredSlotIndex, dragCharacterId);
        }
        else if (dragSourceSlotIndex >= 0 && rosterGridContainer != null && rosterGridContainer.worldBound.Contains(pointerPosition))
        {
            workingSelection = workingSelection.ClearSlot(dragSourceSlotIndex);
        }

        CancelDrag();
        RefreshAllUi();
    }

    private int FindHoveredSlotIndex(Vector2 pointerPosition)
    {
        for (int i = 0; i < slotWidgets.Count; i++)
        {
            if (slotWidgets[i].Root.worldBound.Contains(pointerPosition))
            {
                return slotWidgets[i].SlotIndex;
            }
        }

        return -1;
    }

    private void CancelDrag()
    {
        isDragging = false;
        dragCharacterId = string.Empty;
        dragSourceSlotIndex = -1;

        if (dragGhost != null)
        {
            dragGhost.style.display = DisplayStyle.None;
        }

        for (int i = 0; i < slotWidgets.Count; i++)
        {
            slotWidgets[i].Root.EnableInClassList("team-slot--drop-target", false);
        }
    }

    private void ShowMainPage()
    {
        isEditPageVisible = false;
        RefreshAllUi();
    }

    private void ShowEditPage()
    {
        workingSelection = savedSelection ?? TacticsPartySelection.CreateDefault(roster);
        isEditPageVisible = true;
        RefreshAllUi();
    }

    private void HandlePlayButtonClicked()
    {
        PlayRequested?.Invoke();
    }

    private void HandleEditTeamButtonClicked()
    {
        ShowEditPage();
    }

    private void HandleEditorBackButtonClicked()
    {
        workingSelection = savedSelection;
        ShowMainPage();
    }

    private void HandleEditorSaveButtonClicked()
    {
        if (!CanSaveWorkingSelection())
        {
            return;
        }

        savedSelection = workingSelection;
        partySelectionService?.SaveSelection(savedSelection);
        SetStatusText("Team formation saved.");
        ShowMainPage();
    }

    private bool CanSaveWorkingSelection()
    {
        return CountAssignedMembers(workingSelection) >= GetRequiredTeamSize();
    }

    private int GetRequiredTeamSize()
    {
        int availableCount = roster?.PlayableCharacters?.Count ?? 0;
        int capacity = workingSelection != null ? workingSelection.Capacity : TacticsPartySelection.DefaultCapacity;
        return Mathf.Min(capacity, availableCount);
    }

    private static int CountAssignedMembers(TacticsPartySelection selection)
    {
        if (selection == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < selection.Capacity; i++)
        {
            if (!string.IsNullOrEmpty(selection.GetCharacterId(i)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool SelectionsMatch(TacticsPartySelection left, TacticsPartySelection right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null || left.Capacity != right.Capacity)
        {
            return false;
        }

        for (int i = 0; i < left.Capacity; i++)
        {
            if (!string.Equals(left.GetCharacterId(i), right.GetCharacterId(i), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void DisposePreviews()
    {
        for (int i = 0; i < previews.Count; i++)
        {
            previews[i]?.Dispose();
        }

        previews.Clear();
    }

    private void EnsurePreviewSettingsInitialized()
    {
        if (previewSettings.textureSize <= 0)
        {
            previewSettings = TacticsCharacterCardPreview.PreviewSettings.Default();
        }

        Vector2 size = previewWindowSize;
        if (size.x <= 0f && size.y <= 0f)
        {
            size = new Vector2(225f, 256f);
        }

        previewWindowSize = new Vector2(Mathf.Max(96f, size.x), Mathf.Max(96f, size.y));
    }

    private void OnValidate()
    {
        EnsurePreviewSettingsInitialized();
    }

    private Vector2 GetSanitizedPreviewWindowSize()
    {
        return new Vector2(Mathf.Max(96f, previewWindowSize.x), Mathf.Max(96f, previewWindowSize.y));
    }

    private sealed class SlotWidget
    {
        public int SlotIndex;
        public string CurrentCharacterId;
        public VisualElement Root;
        public Image PreviewImage;
        public Label NameLabel;
        public Label MetaLabel;
        public Button ClearButton;
        public TacticsCharacterCardPreview Preview;
    }

    private sealed class RosterCardWidget
    {
        public TacticsCharacterDefinition Definition;
        public VisualElement Root;
        public Image PreviewImage;
        public Label StatusLabel;
        public TacticsCharacterCardPreview Preview;
    }
}
