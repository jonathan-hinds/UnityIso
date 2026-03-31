using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    private const string LandingPanelName = "landing-panel";
    private const string SinglePlayerPanelName = "single-player-panel";
    private const string OnlinePanelName = "online-panel";
    private const string AuthDialogOverlayName = "auth-dialog-overlay";
    private const string HostSetupPageName = "host-setup-page";
    private const string EditPageName = "edit-team-page";
    private const string SinglePlayerModeButtonName = "single-player-mode-button";
    private const string OnlineCoopModeButtonName = "online-coop-mode-button";
    private const string SinglePlayerBackButtonName = "single-player-back-button";
    private const string OnlineBackButtonName = "online-back-button";
    private const string PlayButtonName = "play-button";
    private const string HostOnlineButtonName = "host-online-button";
    private const string JoinOnlineButtonName = "join-online-button";
    private const string JoinCodeFieldName = "join-address-field";
    private const string EditTeamButtonName = "edit-team-button";
    private const string EditOnlineTeamButtonName = "edit-online-team-button";
    private const string StatusLabelName = "status-label";
    private const string TeamSummaryLabelName = "team-summary-label";
    private const string OnlineTeamSummaryLabelName = "online-team-summary-label";
    private const string TeamSlotContainerName = "team-slot-container";
    private const string RosterGridContainerName = "character-grid-container";
    private const string CharacterInspectorPanelName = "character-inspector-panel";
    private const string CharacterDetailSubtitleName = "character-detail-subtitle";
    private const string CharacterDetailPreviewName = "character-detail-preview";
    private const string CharacterDetailKickerName = "character-detail-kicker";
    private const string CharacterDetailNameName = "character-detail-name";
    private const string CharacterDetailLevelName = "character-detail-level";
    private const string CharacterDetailSummaryName = "character-detail-summary";
    private const string CharacterDetailPrimaryStatsName = "character-detail-primary-stats";
    private const string CharacterDetailDerivedStatsName = "character-detail-derived-stats";
    private const string CharacterDetailAbilitiesName = "character-detail-abilities";
    private const string EditorBackButtonName = "editor-back-button";
    private const string EditorSaveButtonName = "editor-save-button";
    private const string EditorStatusLabelName = "editor-status-label";
    private const string HostSetupBackButtonName = "host-setup-back-button";
    private const string HostSetupConfirmButtonName = "host-setup-confirm-button";
    private const string HostSetupStatusLabelName = "host-setup-status-label";
    private const string SeedFieldName = "host-seed-field";
    private const string WidthFieldName = "host-width-field";
    private const string LengthFieldName = "host-length-field";
    private const string NoiseScaleFieldName = "host-noise-scale-field";
    private const string NoiseOctavesSliderName = "host-noise-octaves-slider";
    private const string NoiseOctavesValueLabelName = "host-noise-octaves-value-label";
    private const string MinElevationFieldName = "host-min-elevation-field";
    private const string MaxElevationFieldName = "host-max-elevation-field";
    private const string EnemyEntryContainerName = "host-enemy-entry-container";
    private const string AddEnemyButtonName = "host-add-enemy-button";
    private const string RelayCodeContainerName = "relay-code-container";
    private const string RelayCodeLabelName = "relay-code-label";
    private const string CopyRelayCodeButtonName = "copy-relay-code-button";
    private const string LobbyReadyButtonName = "lobby-ready-button";
    private const string LobbyPlayerCardContainerName = "lobby-player-card-container";
    private const string DragGhostName = "drag-ghost";
    private const string AccountStatusLabelName = "account-status-label";
    private const string AccountUsernameFieldName = "account-username-field";
    private const string AccountPasswordFieldName = "account-password-field";
    private const string AccountSignInButtonName = "account-sign-in-button";
    private const string AccountRegisterButtonName = "account-register-button";
    private const string AuthCancelButtonName = "auth-cancel-button";

    [Header("Assets")]
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private VisualTreeAsset visualTreeAsset;

    [Header("Preview Tuning")]
    [SerializeField] private TacticsCharacterCardPreview.PreviewSettings previewSettings = default;
    [SerializeField] private Vector2 previewWindowSize = new Vector2(176f, 196f);
    [SerializeField] private Vector2 slotPreviewWindowSize = new Vector2(128f, 148f);

    private UIDocument uiDocument;
    private VisualElement rootElement;
    private VisualElement mainPage;
    private VisualElement landingPanel;
    private VisualElement singlePlayerPanel;
    private VisualElement onlinePanel;
    private VisualElement authDialogOverlay;
    private VisualElement hostSetupPage;
    private VisualElement editTeamPage;
    private Button singlePlayerModeButton;
    private Button onlineCoopModeButton;
    private Button singlePlayerBackButton;
    private Button onlineBackButton;
    private Button playButton;
    private Button hostOnlineButton;
    private Button joinOnlineButton;
    private TextField joinCodeField;
    private Button editTeamButton;
    private Button editOnlineTeamButton;
    private Label statusLabel;
    private Label teamSummaryLabel;
    private Label onlineTeamSummaryLabel;
    private VisualElement teamSlotContainer;
    private VisualElement rosterGridContainer;
    private CharacterInspectorWidget characterInspector;
    private Button editorBackButton;
    private Button editorSaveButton;
    private Label editorStatusLabel;
    private Button hostSetupBackButton;
    private Button hostSetupConfirmButton;
    private Label hostSetupStatusLabel;
    private IntegerField seedField;
    private IntegerField widthField;
    private IntegerField lengthField;
    private FloatField noiseScaleField;
    private SliderInt noiseOctavesSlider;
    private Label noiseOctavesValueLabel;
    private IntegerField minElevationField;
    private IntegerField maxElevationField;
    private VisualElement enemyEntryContainer;
    private Button addEnemyButton;
    private VisualElement relayCodeContainer;
    private Label relayCodeLabel;
    private Button copyRelayCodeButton;
    private Button lobbyReadyButton;
    private VisualElement lobbyPlayerCardContainer;
    private Label dragGhost;
    private Label accountStatusLabel;
    private TextField accountUsernameField;
    private TextField accountPasswordField;
    private Button accountSignInButton;
    private Button accountRegisterButton;
    private Button authCancelButton;

    private ProceduralIsometricMapGenerator sourceMapGenerator;
    private TacticsPartySelectionService localPartySelectionService;
    private TacticsPartySelectionService onlinePartySelectionService;
    private TacticsCharacterProgressionService localProgressionService;
    private TacticsCharacterProgressionService onlineProgressionService;
    private TacticsCharacterInventoryService localInventoryService;
    private TacticsCharacterInventoryService onlineInventoryService;
    private ITacticsAccountSessionService accountSessionService;
    private TacticsCoopSessionCoordinator coopSessionCoordinator;
    private TacticsEnemyTable enemyTable;
    private TacticsCharacterRoster roster;
    private Dictionary<string, TacticsCharacterDefinition> definitionsById = new(StringComparer.OrdinalIgnoreCase);
    private TacticsPartySelection savedLocalSelection;
    private TacticsPartySelection savedOnlineSelection;
    private TacticsPartySelection workingSelection;
    private readonly List<SlotWidget> slotWidgets = new();
    private readonly List<RosterCardWidget> rosterCardWidgets = new();
    private readonly List<HostEnemyEntryWidget> hostEnemyEntryWidgets = new();
    private readonly List<LobbyPlayerCardWidget> lobbyPlayerCardWidgets = new();
    private readonly List<EnemyCatalogOption> enemyCatalogOptions = new();
    private readonly List<TacticsCharacterCardPreview> previews = new();
    private TacticsMatchGenerationSettings workingMatchSettings;
    private MainMenuPage currentMainMenuPage;
    private bool isEditPageVisible;
    private bool isHostSetupPageVisible;
    private bool isHostSessionStarted;
    private bool isEditingOnlineParty;
    private bool isAuthDialogVisible;
    private bool suppressHostFieldCallbacks;
    private bool hostFieldCallbacksRegistered;
    private bool isMenuInteractable = true;
    private bool isDragging;
    private string hostRelayJoinCode = string.Empty;
    private TacticsCoopLobbyState lobbyState;
    private string dragCharacterId = string.Empty;
    private string selectedInspectorCharacterId = string.Empty;
    private string hoveredInspectorCharacterId = string.Empty;
    private int dragSourceSlotIndex = -1;
    private int previewCounter;

    public event Action<TacticsSessionStartRequest> SessionStartRequested;
    public event Action QuitRequested;

    public bool IsVisible => gameObject.activeSelf;

    private enum MainMenuPage
    {
        Landing = 0,
        SinglePlayer = 1,
        Online = 2
    }

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

    public void AssignDependencies(
        ProceduralIsometricMapGenerator mapGenerator,
        TacticsPartySelectionService localSelectionService,
        TacticsPartySelectionService onlineSelectionService,
        TacticsCharacterProgressionService localCharacterProgressionService,
        TacticsCharacterProgressionService onlineCharacterProgressionService,
        TacticsCharacterInventoryService localCharacterInventoryService,
        TacticsCharacterInventoryService onlineCharacterInventoryService,
        TacticsEnemyTable availableEnemyTable,
        ITacticsAccountSessionService sessionService,
        TacticsCoopSessionCoordinator sessionCoordinator)
    {
        EnsurePreviewSettingsInitialized();
        sourceMapGenerator = mapGenerator;
        localPartySelectionService = localSelectionService ?? new TacticsPartySelectionService(new TacticsSinglePlayerPartySelectionStore());
        this.onlinePartySelectionService = onlineSelectionService;
        localProgressionService = localCharacterProgressionService ?? new TacticsCharacterProgressionService(new TacticsSinglePlayerCharacterProgressionStore());
        onlineProgressionService = onlineCharacterProgressionService;
        localInventoryService = localCharacterInventoryService ?? new TacticsCharacterInventoryService(new TacticsSinglePlayerCharacterInventoryStore());
        onlineInventoryService = onlineCharacterInventoryService;
        enemyTable = availableEnemyTable;
        if (!ReferenceEquals(coopSessionCoordinator, sessionCoordinator))
        {
            if (coopSessionCoordinator != null)
            {
                coopSessionCoordinator.LobbyStateChanged -= HandleLobbyStateChanged;
            }

            coopSessionCoordinator = sessionCoordinator;
            if (coopSessionCoordinator != null)
            {
                coopSessionCoordinator.LobbyStateChanged -= HandleLobbyStateChanged;
                coopSessionCoordinator.LobbyStateChanged += HandleLobbyStateChanged;
                lobbyState = coopSessionCoordinator.CurrentLobbyState;
            }
            else
            {
                lobbyState = null;
            }
        }

        if (!ReferenceEquals(accountSessionService, sessionService))
        {
            if (accountSessionService != null)
            {
                accountSessionService.StateChanged -= HandleAccountSessionStateChanged;
            }

            accountSessionService = sessionService;
            if (accountSessionService != null)
            {
                accountSessionService.StateChanged -= HandleAccountSessionStateChanged;
                accountSessionService.StateChanged += HandleAccountSessionStateChanged;
            }
        }

        roster = localPartySelectionService.LoadRoster();
        definitionsById = roster != null ? roster.BuildLookupById() : new Dictionary<string, TacticsCharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        savedLocalSelection = localPartySelectionService.LoadSelection();
        savedOnlineSelection = this.onlinePartySelectionService != null ? this.onlinePartySelectionService.LoadSelection() : null;
        if (isEditingOnlineParty && this.onlinePartySelectionService == null)
        {
            isEditingOnlineParty = false;
        }

        workingSelection = GetSavedSelectionForCurrentEditor();
        BuildEnemyCatalog();
        ResetHostSetupState();
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

        ShowLandingPage();
        singlePlayerModeButton?.Focus();
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
        isMenuInteractable = interactable;
        singlePlayerModeButton?.SetEnabled(interactable);
        onlineCoopModeButton?.SetEnabled(interactable);
        singlePlayerBackButton?.SetEnabled(interactable);
        onlineBackButton?.SetEnabled(interactable);
        playButton?.SetEnabled(interactable);
        editTeamButton?.SetEnabled(interactable);
        editorBackButton?.SetEnabled(interactable);
        editorSaveButton?.SetEnabled(interactable && CanSaveWorkingSelection());
        editOnlineTeamButton?.SetEnabled(interactable);
        RefreshHostSetupUi();
        RefreshAccountUi();
    }

    public void SetStatusText(string text)
    {
        CacheElements();
        SetLabelText(statusLabel, text);
        SetLabelText(hostSetupStatusLabel, text);
    }

    public void HandleHostStarted(string relayJoinCode)
    {
        hostRelayJoinCode = string.IsNullOrWhiteSpace(relayJoinCode)
            ? string.Empty
            : relayJoinCode.Trim().ToUpperInvariant();
        isHostSessionStarted = !string.IsNullOrWhiteSpace(hostRelayJoinCode);
        RefreshHostSetupUi();
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
        landingPanel = rootElement.Q<VisualElement>(LandingPanelName);
        singlePlayerPanel = rootElement.Q<VisualElement>(SinglePlayerPanelName);
        onlinePanel = rootElement.Q<VisualElement>(OnlinePanelName);
        authDialogOverlay = rootElement.Q<VisualElement>(AuthDialogOverlayName);
        hostSetupPage = rootElement.Q<VisualElement>(HostSetupPageName);
        editTeamPage = rootElement.Q<VisualElement>(EditPageName);
        singlePlayerModeButton = rootElement.Q<Button>(SinglePlayerModeButtonName);
        onlineCoopModeButton = rootElement.Q<Button>(OnlineCoopModeButtonName);
        singlePlayerBackButton = rootElement.Q<Button>(SinglePlayerBackButtonName);
        onlineBackButton = rootElement.Q<Button>(OnlineBackButtonName);
        playButton = rootElement.Q<Button>(PlayButtonName);
        hostOnlineButton = rootElement.Q<Button>(HostOnlineButtonName);
        joinOnlineButton = rootElement.Q<Button>(JoinOnlineButtonName);
        joinCodeField = rootElement.Q<TextField>(JoinCodeFieldName);
        editTeamButton = rootElement.Q<Button>(EditTeamButtonName);
        editOnlineTeamButton = rootElement.Q<Button>(EditOnlineTeamButtonName);
        statusLabel = rootElement.Q<Label>(StatusLabelName);
        teamSummaryLabel = rootElement.Q<Label>(TeamSummaryLabelName);
        onlineTeamSummaryLabel = rootElement.Q<Label>(OnlineTeamSummaryLabelName);
        teamSlotContainer = rootElement.Q<VisualElement>(TeamSlotContainerName);
        rosterGridContainer = rootElement.Q<VisualElement>(RosterGridContainerName);
        characterInspector = new CharacterInspectorWidget
        {
            Root = rootElement.Q<VisualElement>(CharacterInspectorPanelName),
            SubtitleLabel = rootElement.Q<Label>(CharacterDetailSubtitleName),
            PreviewImage = rootElement.Q<Image>(CharacterDetailPreviewName),
            KickerLabel = rootElement.Q<Label>(CharacterDetailKickerName),
            NameLabel = rootElement.Q<Label>(CharacterDetailNameName),
            LevelLabel = rootElement.Q<Label>(CharacterDetailLevelName),
            SummaryContainer = rootElement.Q<VisualElement>(CharacterDetailSummaryName),
            PrimaryStatsContainer = rootElement.Q<VisualElement>(CharacterDetailPrimaryStatsName),
            DerivedStatsContainer = rootElement.Q<VisualElement>(CharacterDetailDerivedStatsName),
            AbilitiesContainer = rootElement.Q<VisualElement>(CharacterDetailAbilitiesName)
        };
        editorBackButton = rootElement.Q<Button>(EditorBackButtonName);
        editorSaveButton = rootElement.Q<Button>(EditorSaveButtonName);
        editorStatusLabel = rootElement.Q<Label>(EditorStatusLabelName);
        hostSetupBackButton = rootElement.Q<Button>(HostSetupBackButtonName);
        hostSetupConfirmButton = rootElement.Q<Button>(HostSetupConfirmButtonName);
        hostSetupStatusLabel = rootElement.Q<Label>(HostSetupStatusLabelName);
        seedField = rootElement.Q<IntegerField>(SeedFieldName);
        widthField = rootElement.Q<IntegerField>(WidthFieldName);
        lengthField = rootElement.Q<IntegerField>(LengthFieldName);
        noiseScaleField = rootElement.Q<FloatField>(NoiseScaleFieldName);
        noiseOctavesSlider = rootElement.Q<SliderInt>(NoiseOctavesSliderName);
        noiseOctavesValueLabel = rootElement.Q<Label>(NoiseOctavesValueLabelName);
        minElevationField = rootElement.Q<IntegerField>(MinElevationFieldName);
        maxElevationField = rootElement.Q<IntegerField>(MaxElevationFieldName);
        enemyEntryContainer = rootElement.Q<VisualElement>(EnemyEntryContainerName);
        addEnemyButton = rootElement.Q<Button>(AddEnemyButtonName);
        relayCodeContainer = rootElement.Q<VisualElement>(RelayCodeContainerName);
        relayCodeLabel = rootElement.Q<Label>(RelayCodeLabelName);
        copyRelayCodeButton = rootElement.Q<Button>(CopyRelayCodeButtonName);
        lobbyReadyButton = rootElement.Q<Button>(LobbyReadyButtonName);
        lobbyPlayerCardContainer = rootElement.Q<VisualElement>(LobbyPlayerCardContainerName);
        dragGhost = rootElement.Q<Label>(DragGhostName);
        accountStatusLabel = rootElement.Q<Label>(AccountStatusLabelName);
        accountUsernameField = rootElement.Q<TextField>(AccountUsernameFieldName);
        accountPasswordField = rootElement.Q<TextField>(AccountPasswordFieldName);
        accountSignInButton = rootElement.Q<Button>(AccountSignInButtonName);
        accountRegisterButton = rootElement.Q<Button>(AccountRegisterButtonName);
        authCancelButton = rootElement.Q<Button>(AuthCancelButtonName);

        if (accountPasswordField != null)
        {
            accountPasswordField.isPasswordField = true;
        }
    }

    private void RegisterCallbacks()
    {
        if (singlePlayerModeButton != null)
        {
            singlePlayerModeButton.clicked -= HandleSinglePlayerModeButtonClicked;
            singlePlayerModeButton.clicked += HandleSinglePlayerModeButtonClicked;
        }

        if (onlineCoopModeButton != null)
        {
            onlineCoopModeButton.clicked -= HandleOnlineCoopModeButtonClicked;
            onlineCoopModeButton.clicked += HandleOnlineCoopModeButtonClicked;
        }

        if (singlePlayerBackButton != null)
        {
            singlePlayerBackButton.clicked -= HandleSinglePlayerBackButtonClicked;
            singlePlayerBackButton.clicked += HandleSinglePlayerBackButtonClicked;
        }

        if (onlineBackButton != null)
        {
            onlineBackButton.clicked -= HandleOnlineBackButtonClicked;
            onlineBackButton.clicked += HandleOnlineBackButtonClicked;
        }

        if (playButton != null)
        {
            playButton.clicked -= HandleSinglePlayerButtonClicked;
            playButton.clicked += HandleSinglePlayerButtonClicked;
        }

        if (hostOnlineButton != null)
        {
            hostOnlineButton.clicked -= HandleHostOnlineButtonClicked;
            hostOnlineButton.clicked += HandleHostOnlineButtonClicked;
        }

        if (joinOnlineButton != null)
        {
            joinOnlineButton.clicked -= HandleJoinOnlineButtonClicked;
            joinOnlineButton.clicked += HandleJoinOnlineButtonClicked;
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

        if (hostSetupBackButton != null)
        {
            hostSetupBackButton.clicked -= HandleHostSetupBackButtonClicked;
            hostSetupBackButton.clicked += HandleHostSetupBackButtonClicked;
        }

        if (hostSetupConfirmButton != null)
        {
            hostSetupConfirmButton.clicked -= HandleHostSetupConfirmButtonClicked;
            hostSetupConfirmButton.clicked += HandleHostSetupConfirmButtonClicked;
        }

        if (addEnemyButton != null)
        {
            addEnemyButton.clicked -= HandleAddEnemyButtonClicked;
            addEnemyButton.clicked += HandleAddEnemyButtonClicked;
        }

        if (copyRelayCodeButton != null)
        {
            copyRelayCodeButton.clicked -= HandleCopyRelayCodeButtonClicked;
            copyRelayCodeButton.clicked += HandleCopyRelayCodeButtonClicked;
        }

        if (lobbyReadyButton != null)
        {
            lobbyReadyButton.clicked -= HandleLobbyReadyButtonClicked;
            lobbyReadyButton.clicked += HandleLobbyReadyButtonClicked;
        }

        if (editOnlineTeamButton != null)
        {
            editOnlineTeamButton.clicked -= HandleEditOnlineTeamButtonClicked;
            editOnlineTeamButton.clicked += HandleEditOnlineTeamButtonClicked;
        }

        if (accountSignInButton != null)
        {
            accountSignInButton.clicked -= HandleAccountSignInButtonClicked;
            accountSignInButton.clicked += HandleAccountSignInButtonClicked;
        }

        if (accountRegisterButton != null)
        {
            accountRegisterButton.clicked -= HandleAccountRegisterButtonClicked;
            accountRegisterButton.clicked += HandleAccountRegisterButtonClicked;
        }

        if (authCancelButton != null)
        {
            authCancelButton.clicked -= HandleAuthCancelButtonClicked;
            authCancelButton.clicked += HandleAuthCancelButtonClicked;
        }

        RegisterHostFieldCallbacks();
    }

    private void RegisterHostFieldCallbacks()
    {
        if (hostFieldCallbacksRegistered)
        {
            return;
        }

        hostFieldCallbacksRegistered = true;

        if (seedField != null)
        {
            seedField.RegisterValueChangedCallback(evt =>
            {
                if (suppressHostFieldCallbacks || workingMatchSettings == null)
                {
                    return;
                }

                workingMatchSettings.seed = evt.newValue;
                HandleHostSettingsEdited();
            });
        }

        if (widthField != null)
        {
            widthField.RegisterValueChangedCallback(evt =>
            {
                if (suppressHostFieldCallbacks || workingMatchSettings == null)
                {
                    return;
                }

                workingMatchSettings.width = evt.newValue;
                HandleHostSettingsEdited();
            });
        }

        if (lengthField != null)
        {
            lengthField.RegisterValueChangedCallback(evt =>
            {
                if (suppressHostFieldCallbacks || workingMatchSettings == null)
                {
                    return;
                }

                workingMatchSettings.length = evt.newValue;
                HandleHostSettingsEdited();
            });
        }

        if (noiseScaleField != null)
        {
            noiseScaleField.RegisterValueChangedCallback(evt =>
            {
                if (suppressHostFieldCallbacks || workingMatchSettings == null)
                {
                    return;
                }

                workingMatchSettings.noiseScale = evt.newValue;
                HandleHostSettingsEdited();
            });
        }

        if (noiseOctavesSlider != null)
        {
            noiseOctavesSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressHostFieldCallbacks || workingMatchSettings == null)
                {
                    return;
                }

                workingMatchSettings.noiseOctaves = evt.newValue;
                HandleHostSettingsEdited();
            });
        }

        if (minElevationField != null)
        {
            minElevationField.RegisterValueChangedCallback(evt =>
            {
                if (suppressHostFieldCallbacks || workingMatchSettings == null)
                {
                    return;
                }

                workingMatchSettings.minElevation = evt.newValue;
                HandleHostSettingsEdited();
            });
        }

        if (maxElevationField != null)
        {
            maxElevationField.RegisterValueChangedCallback(evt =>
            {
                if (suppressHostFieldCallbacks || workingMatchSettings == null)
                {
                    return;
                }

                workingMatchSettings.maxElevation = evt.newValue;
                HandleHostSettingsEdited();
            });
        }
    }

    private void UnregisterCallbacks()
    {
        if (singlePlayerModeButton != null)
        {
            singlePlayerModeButton.clicked -= HandleSinglePlayerModeButtonClicked;
        }

        if (onlineCoopModeButton != null)
        {
            onlineCoopModeButton.clicked -= HandleOnlineCoopModeButtonClicked;
        }

        if (singlePlayerBackButton != null)
        {
            singlePlayerBackButton.clicked -= HandleSinglePlayerBackButtonClicked;
        }

        if (onlineBackButton != null)
        {
            onlineBackButton.clicked -= HandleOnlineBackButtonClicked;
        }

        if (playButton != null)
        {
            playButton.clicked -= HandleSinglePlayerButtonClicked;
        }

        if (hostOnlineButton != null)
        {
            hostOnlineButton.clicked -= HandleHostOnlineButtonClicked;
        }

        if (joinOnlineButton != null)
        {
            joinOnlineButton.clicked -= HandleJoinOnlineButtonClicked;
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

        if (hostSetupBackButton != null)
        {
            hostSetupBackButton.clicked -= HandleHostSetupBackButtonClicked;
        }

        if (hostSetupConfirmButton != null)
        {
            hostSetupConfirmButton.clicked -= HandleHostSetupConfirmButtonClicked;
        }

        if (addEnemyButton != null)
        {
            addEnemyButton.clicked -= HandleAddEnemyButtonClicked;
        }

        if (copyRelayCodeButton != null)
        {
            copyRelayCodeButton.clicked -= HandleCopyRelayCodeButtonClicked;
        }

        if (lobbyReadyButton != null)
        {
            lobbyReadyButton.clicked -= HandleLobbyReadyButtonClicked;
        }

        if (editOnlineTeamButton != null)
        {
            editOnlineTeamButton.clicked -= HandleEditOnlineTeamButtonClicked;
        }

        if (accountSignInButton != null)
        {
            accountSignInButton.clicked -= HandleAccountSignInButtonClicked;
        }

        if (accountRegisterButton != null)
        {
            accountRegisterButton.clicked -= HandleAccountRegisterButtonClicked;
        }

        if (authCancelButton != null)
        {
            authCancelButton.clicked -= HandleAuthCancelButtonClicked;
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

        TacticsPartySelection savedSelection = GetSavedSelectionForCurrentEditor();
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
        savedLocalSelection ??= localPartySelectionService != null ? localPartySelectionService.LoadSelection() : TacticsPartySelection.CreateDefault(roster);
        savedOnlineSelection = onlinePartySelectionService != null ? onlinePartySelectionService.LoadSelection() : null;
        workingSelection ??= GetSavedSelectionForCurrentEditor();
        workingMatchSettings ??= sourceMapGenerator != null ? sourceMapGenerator.CreateMatchGenerationSettings() : new TacticsMatchGenerationSettings();

        RefreshTeamSummary();
        RefreshAccountUi();
        RefreshSlotWidgets();
        RefreshRosterWidgets();
        RefreshCharacterInspector();
        RefreshEditorStatus();
        RefreshHostSetupUi();
        RefreshLobbyUi();

        if (mainPage != null)
        {
            mainPage.style.display = isEditPageVisible || isHostSetupPageVisible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (landingPanel != null)
        {
            landingPanel.style.display = !isEditPageVisible && !isHostSetupPageVisible && currentMainMenuPage == MainMenuPage.Landing
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (singlePlayerPanel != null)
        {
            singlePlayerPanel.style.display = !isEditPageVisible && !isHostSetupPageVisible && currentMainMenuPage == MainMenuPage.SinglePlayer
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (onlinePanel != null)
        {
            onlinePanel.style.display = !isEditPageVisible && !isHostSetupPageVisible && currentMainMenuPage == MainMenuPage.Online
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (authDialogOverlay != null)
        {
            authDialogOverlay.style.display = isAuthDialogVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (hostSetupPage != null)
        {
            hostSetupPage.style.display = isHostSetupPageVisible ? DisplayStyle.Flex : DisplayStyle.None;
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

        IReadOnlyList<TacticsCharacterDefinition> selectedParty = savedLocalSelection != null
            ? savedLocalSelection.ResolveDefinitions(roster)
            : Array.Empty<TacticsCharacterDefinition>();
        if (selectedParty.Count == 0)
        {
            teamSummaryLabel.text = "Local Team: No local party saved yet";
        }
        else
        {
            List<string> names = new List<string>(selectedParty.Count);
            for (int i = 0; i < selectedParty.Count; i++)
            {
                if (selectedParty[i] == null)
                {
                    continue;
                }

                names.Add(selectedParty[i].DisplayName);
            }

            teamSummaryLabel.text = $"Local Team: {string.Join("  /  ", names)}";
        }

        if (onlineTeamSummaryLabel == null)
        {
            return;
        }

        if (onlinePartySelectionService == null || !(accountSessionService?.IsSignedIn ?? false))
        {
            onlineTeamSummaryLabel.text = "Online Team: Sign in to manage cloud party data";
            return;
        }

        IReadOnlyList<TacticsCharacterDefinition> onlineParty = savedOnlineSelection != null
            ? savedOnlineSelection.ResolveDefinitions(roster)
            : Array.Empty<TacticsCharacterDefinition>();
        if (onlineParty.Count == 0)
        {
            onlineTeamSummaryLabel.text = "Online Team: No online party saved yet";
            return;
        }

        List<string> onlineNames = new List<string>(onlineParty.Count);
        for (int i = 0; i < onlineParty.Count; i++)
        {
            if (onlineParty[i] == null)
            {
                continue;
            }

            onlineNames.Add(onlineParty[i].DisplayName);
        }

        onlineTeamSummaryLabel.text = $"Online Team: {string.Join("  /  ", onlineNames)}";
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
                DisposePreview(widget.Preview);
                widget.Preview = null;
                widget.CurrentCharacterId = string.Empty;
                continue;
            }

            TacticsCharacterProgressionSnapshot progression = GetProgressionSnapshot(definition);
            widget.NameLabel.text = definition.DisplayName.ToUpperInvariant();
            widget.MetaLabel.text = $"LV {progression.Level:00}  /  READY";
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
            widget.StatusLabel.text = $"{(isAssigned ? "IN PARTY" : "AVAILABLE")}  /  LV {GetProgressionSnapshot(widget.Definition).Level:00}";
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
        TacticsPartySelection savedSelection = GetSavedSelectionForCurrentEditor();
        bool isDirty = !SelectionsMatch(savedSelection, workingSelection);
        string profileLabel = isEditingOnlineParty ? "online" : "local";

        editorStatusLabel.text = isDirty
            ? $"Unsaved {profileLabel} team changes. {assignedCount}/{requiredCount} slot{(requiredCount == 1 ? string.Empty : "s")} locked in."
            : $"Choose up to {workingSelection.Capacity} {profileLabel} party members. Hover a card to preview its movement stance.";
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
        ApplySlotPreviewImageSizing(previewImage);
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

        DisposePreview(widget.Preview);
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
        ApplyPreviewImageSizing(previewImage, GetSanitizedPreviewWindowSize());
    }

    private void ApplySlotPreviewImageSizing(Image previewImage)
    {
        ApplyPreviewImageSizing(previewImage, GetSanitizedSlotPreviewWindowSize());
    }

    private static void ApplyPreviewImageSizing(Image previewImage, Vector2 size)
    {
        if (previewImage == null)
        {
            return;
        }

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
        element.RegisterCallback<PointerEnterEvent>(_ =>
        {
            string characterId = NormalizeCharacterId(getCharacterId?.Invoke());
            setHovered?.Invoke(true);
            if (!string.IsNullOrEmpty(characterId))
            {
                SetHoveredInspectorCharacter(characterId);
            }
        });
        element.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            string characterId = NormalizeCharacterId(getCharacterId?.Invoke());
            setHovered?.Invoke(false);
            if (!string.IsNullOrEmpty(characterId))
            {
                ClearHoveredInspectorCharacter(characterId);
            }
        });
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

            SetSelectedInspectorCharacter(characterId);
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

    private void ShowLandingPage()
    {
        currentMainMenuPage = MainMenuPage.Landing;
        isEditPageVisible = false;
        isHostSetupPageVisible = false;
        isAuthDialogVisible = false;
        RefreshAllUi();
    }

    private void ShowSinglePlayerPage()
    {
        currentMainMenuPage = MainMenuPage.SinglePlayer;
        isEditPageVisible = false;
        isHostSetupPageVisible = false;
        isAuthDialogVisible = false;
        RefreshAllUi();
    }

    private void ShowOnlinePage()
    {
        currentMainMenuPage = MainMenuPage.Online;
        isEditPageVisible = false;
        isHostSetupPageVisible = false;
        isAuthDialogVisible = false;
        RefreshAllUi();
    }

    private void ShowAuthDialog()
    {
        currentMainMenuPage = MainMenuPage.Landing;
        isEditPageVisible = false;
        isHostSetupPageVisible = false;
        isAuthDialogVisible = true;
        if (accountPasswordField != null)
        {
            accountPasswordField.SetValueWithoutNotify(string.Empty);
        }

        RefreshAllUi();
        accountUsernameField?.Focus();
    }

    private bool EnsureSignedInForOnlineFlow(string unsignedInStatusMessage)
    {
        if (accountSessionService?.IsSignedIn ?? false)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(unsignedInStatusMessage))
        {
            SetLabelText(accountStatusLabel, unsignedInStatusMessage);
        }

        ShowAuthDialog();
        return false;
    }

    private void ShowEditPage(bool editOnlineParty)
    {
        isEditingOnlineParty = editOnlineParty && onlinePartySelectionService != null;
        workingSelection = GetSavedSelectionForCurrentEditor() ?? TacticsPartySelection.CreateDefault(roster);
        selectedInspectorCharacterId = string.Empty;
        hoveredInspectorCharacterId = string.Empty;
        isEditPageVisible = true;
        isHostSetupPageVisible = false;
        isAuthDialogVisible = false;
        RefreshAllUi();
    }

    private void ShowHostSetupPage()
    {
        ResetHostSetupState();
        currentMainMenuPage = MainMenuPage.Online;
        isEditPageVisible = false;
        isHostSetupPageVisible = true;
        isAuthDialogVisible = false;
        RefreshAllUi();
    }

    private void HandleSinglePlayerModeButtonClicked()
    {
        ShowSinglePlayerPage();
    }

    private void HandleOnlineCoopModeButtonClicked()
    {
        if (EnsureSignedInForOnlineFlow("Sign in or register before continuing to online co-op."))
        {
            ShowOnlinePage();
        }
    }

    private void HandleSinglePlayerBackButtonClicked()
    {
        ShowLandingPage();
    }

    private void HandleOnlineBackButtonClicked()
    {
        ShowLandingPage();
    }

    private void HandleSinglePlayerButtonClicked()
    {
        SessionStartRequested?.Invoke(new TacticsSessionStartRequest(
            TacticsSessionStartMode.SinglePlayer,
            string.Empty));
    }

    private void HandleHostOnlineButtonClicked()
    {
        if (!EnsureSignedInForOnlineFlow("Sign in or register before hosting an online co-op match."))
        {
            return;
        }

        ShowHostSetupPage();
    }

    private void HandleJoinOnlineButtonClicked()
    {
        if (!EnsureSignedInForOnlineFlow("Sign in or register before joining an online co-op match."))
        {
            return;
        }

        ShowHostSetupPage();

        SessionStartRequested?.Invoke(new TacticsSessionStartRequest(
            TacticsSessionStartMode.JoinCoop,
            GetJoinCode()));
    }

    private void HandleEditTeamButtonClicked()
    {
        ShowEditPage(false);
    }

    private void HandleEditOnlineTeamButtonClicked()
    {
        if (onlinePartySelectionService == null)
        {
            SetStatusText("Online party storage is unavailable.");
            return;
        }

        if (!EnsureSignedInForOnlineFlow("Sign in or register before editing your online team."))
        {
            return;
        }

        ShowEditPage(true);
    }

    private void HandleQuitButtonClicked()
    {
        QuitRequested?.Invoke();
    }

    private void HandleEditorBackButtonClicked()
    {
        workingSelection = GetSavedSelectionForCurrentEditor();
        if (isEditingOnlineParty)
        {
            ShowOnlinePage();
        }
        else
        {
            ShowSinglePlayerPage();
        }
    }

    private void HandleEditorSaveButtonClicked()
    {
        if (!CanSaveWorkingSelection())
        {
            return;
        }

        TacticsPartySelectionService selectionService = GetEditorSelectionService();
        if (selectionService == null)
        {
            SetStatusText("Party selection storage is unavailable.");
            return;
        }

        TacticsPartySelection committedSelection = workingSelection;
        selectionService.SaveSelection(committedSelection);
        if (isEditingOnlineParty)
        {
            savedOnlineSelection = committedSelection;
            SetStatusText("Online team formation saved.");
        }
        else
        {
            savedLocalSelection = committedSelection;
            SetStatusText("Local team formation saved.");
        }

        if (isEditingOnlineParty)
        {
            ShowOnlinePage();
        }
        else
        {
            ShowSinglePlayerPage();
        }
    }

    private async void HandleAccountSignInButtonClicked()
    {
        if (accountSessionService == null)
        {
            return;
        }

        bool signedIn = await accountSessionService.SignInAsync(accountUsernameField?.value, accountPasswordField?.value);
        if (signedIn)
        {
            ShowOnlinePage();
        }

        RefreshAllUi();
    }

    private async void HandleAccountRegisterButtonClicked()
    {
        if (accountSessionService == null)
        {
            return;
        }

        bool registered = await accountSessionService.RegisterAsync(accountUsernameField?.value, accountPasswordField?.value);
        if (registered)
        {
            ShowOnlinePage();
        }

        RefreshAllUi();
    }

    private void HandleAuthCancelButtonClicked()
    {
        isAuthDialogVisible = false;
        RefreshAllUi();
    }

    private void HandleHostSetupBackButtonClicked()
    {
        if (coopSessionCoordinator != null && coopSessionCoordinator.IsOnlineSession)
        {
            coopSessionCoordinator.RequestReturnToHome();
            return;
        }

        ShowOnlinePage();
    }

    private void HandleHostSetupConfirmButtonClicked()
    {
        if (coopSessionCoordinator != null && coopSessionCoordinator.IsOnlineSession)
        {
            if (coopSessionCoordinator.IsHostAuthority)
            {
                coopSessionCoordinator.RequestStartMatch();
            }

            return;
        }

        if (!TryBuildHostRequest(out TacticsSessionStartRequest request, out string validationMessage))
        {
            SetStatusText(validationMessage);
            RefreshHostSetupUi();
            return;
        }

        SessionStartRequested?.Invoke(request);
    }

    private void HandleAddEnemyButtonClicked()
    {
        if (workingMatchSettings == null || !CanAddMoreEnemies())
        {
            return;
        }

        string nextEnemyId = GetFirstUnusedEnemyId();
        if (string.IsNullOrWhiteSpace(nextEnemyId))
        {
            SetStatusText("All enemy types are already listed.");
            RefreshHostSetupUi();
            return;
        }

        workingMatchSettings.enemies.Add(new TacticsMatchEnemySettings
        {
            enemyId = nextEnemyId,
            count = 1
        });
        HandleHostSettingsEdited();
    }

    private void HandleCopyRelayCodeButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(hostRelayJoinCode))
        {
            return;
        }

        GUIUtility.systemCopyBuffer = hostRelayJoinCode;
        SetStatusText($"Relay code {hostRelayJoinCode} copied to clipboard.");
        RefreshHostSetupUi();
    }

    private void HandleLobbyReadyButtonClicked()
    {
        if (coopSessionCoordinator == null || !coopSessionCoordinator.IsOnlineSession)
        {
            return;
        }

        bool isReady = GetLocalLobbyPlayerState()?.isReady ?? false;
        coopSessionCoordinator.SetLocalReadyState(!isReady);
    }

    private void HandleLobbyStateChanged(TacticsCoopLobbyState state)
    {
        lobbyState = state?.Clone();
        hostRelayJoinCode = string.IsNullOrWhiteSpace(lobbyState?.relayJoinCode)
            ? string.Empty
            : lobbyState.relayJoinCode.Trim().ToUpperInvariant();
        isHostSessionStarted = coopSessionCoordinator != null && coopSessionCoordinator.IsOnlineSession;

        if (lobbyState?.matchSettings != null && (!IsLocalLobbyHost() || workingMatchSettings == null))
        {
            workingMatchSettings = lobbyState.matchSettings.Clone();
        }

        RefreshAllUi();
    }

    private void BuildEnemyCatalog()
    {
        enemyCatalogOptions.Clear();

        IReadOnlyList<TacticsEnemyTableEntry> enemyEntries = enemyTable?.Enemies ?? Array.Empty<TacticsEnemyTableEntry>();
        for (int i = 0; i < enemyEntries.Count; i++)
        {
            TacticsEnemyTableEntry entry = enemyEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.EnemyId))
            {
                continue;
            }

            enemyCatalogOptions.Add(new EnemyCatalogOption(entry.EnemyId, entry.DisplayName));
        }
    }

    private void ResetHostSetupState()
    {
        workingMatchSettings = sourceMapGenerator != null
            ? sourceMapGenerator.CreateMatchGenerationSettings()
            : new TacticsMatchGenerationSettings();
        workingMatchSettings.Sanitize();
        hostRelayJoinCode = string.Empty;
        isHostSessionStarted = false;
    }

    private void HandleHostSettingsEdited()
    {
        if (workingMatchSettings == null)
        {
            return;
        }

        NormalizeEditableMatchSettings(workingMatchSettings);
        if (IsLocalLobbyHost())
        {
            coopSessionCoordinator?.UpdateLobbyMatchSettings(workingMatchSettings);
        }

        RefreshHostSetupUi();
    }

    private void RefreshHostSetupUi()
    {
        CacheElements();
        if (workingMatchSettings == null)
        {
            return;
        }

        if (lobbyState?.matchSettings != null && !IsLocalLobbyHost())
        {
            workingMatchSettings = lobbyState.matchSettings.Clone();
        }

        NormalizeEditableMatchSettings(workingMatchSettings);
        suppressHostFieldCallbacks = true;

        seedField?.SetValueWithoutNotify(workingMatchSettings.seed);
        widthField?.SetValueWithoutNotify(workingMatchSettings.width);
        lengthField?.SetValueWithoutNotify(workingMatchSettings.length);
        noiseScaleField?.SetValueWithoutNotify(workingMatchSettings.noiseScale);
        noiseOctavesSlider?.SetValueWithoutNotify(workingMatchSettings.noiseOctaves);
        minElevationField?.SetValueWithoutNotify(workingMatchSettings.minElevation);
        maxElevationField?.SetValueWithoutNotify(workingMatchSettings.maxElevation);

        if (noiseOctavesValueLabel != null)
        {
            noiseOctavesValueLabel.text = workingMatchSettings.noiseOctaves.ToString();
        }

        suppressHostFieldCallbacks = false;

        RebuildHostEnemyEntries();
        if (relayCodeLabel != null)
        {
            relayCodeLabel.text = string.IsNullOrWhiteSpace(hostRelayJoinCode) ? "---- ----" : hostRelayJoinCode;
        }

        if (relayCodeContainer != null)
        {
            relayCodeContainer.style.display = IsLocalLobbyHost() && !string.IsNullOrWhiteSpace(hostRelayJoinCode)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        bool lobbyConnected = coopSessionCoordinator != null && coopSessionCoordinator.IsOnlineSession;
        bool lobbyHost = IsLocalLobbyHost();
        bool matchStarting = lobbyState != null && lobbyState.isMatchStarting;
        bool canEditSettings = isMenuInteractable && (!lobbyConnected || lobbyHost) && !matchStarting;
        bool canHost = ValidateHostMatchSettings(workingMatchSettings, out string validationMessage);
        if (!lobbyConnected)
        {
            SetLabelText(hostSetupStatusLabel, validationMessage);
        }
        else if (lobbyState != null)
        {
            SetLabelText(hostSetupStatusLabel, BuildLobbyStatusMessage(lobbyState));
        }

        hostSetupBackButton?.SetEnabled(isMenuInteractable);
        seedField?.SetEnabled(canEditSettings);
        widthField?.SetEnabled(canEditSettings);
        lengthField?.SetEnabled(canEditSettings);
        noiseScaleField?.SetEnabled(canEditSettings);
        noiseOctavesSlider?.SetEnabled(canEditSettings);
        minElevationField?.SetEnabled(canEditSettings);
        maxElevationField?.SetEnabled(canEditSettings);
        addEnemyButton?.SetEnabled(canEditSettings && CanAddMoreEnemies());
        copyRelayCodeButton?.SetEnabled(isMenuInteractable && IsLocalLobbyHost() && !string.IsNullOrWhiteSpace(hostRelayJoinCode));

        if (hostSetupConfirmButton != null)
        {
            hostSetupConfirmButton.text = lobbyConnected ? "Start Match" : "Host Match";
            hostSetupConfirmButton.style.display = lobbyConnected && !lobbyHost ? DisplayStyle.None : DisplayStyle.Flex;
            hostSetupConfirmButton.SetEnabled(isMenuInteractable && (!lobbyConnected ? canHost : lobbyHost && !matchStarting));
        }

        if (lobbyReadyButton != null)
        {
            TacticsCoopLobbyPlayerState localPlayer = GetLocalLobbyPlayerState();
            bool hasCompleteLocalParty = TacticsPartyCompositionRules.HasRequiredMembers(
                localPlayer?.partyMembers,
                roster,
                TacticsPartySelection.DefaultCapacity);
            bool canReady = lobbyConnected && !matchStarting && hasCompleteLocalParty;
            lobbyReadyButton.style.display = canReady ? DisplayStyle.Flex : DisplayStyle.None;
            lobbyReadyButton.text = localPlayer != null && localPlayer.isReady ? "Unready" : "Ready Up";
            lobbyReadyButton.SetEnabled(isMenuInteractable && canReady);
        }
    }

    private void RebuildHostEnemyEntries()
    {
        if (enemyEntryContainer == null)
        {
            return;
        }

        hostEnemyEntryWidgets.Clear();
        enemyEntryContainer.Clear();

        for (int i = 0; i < workingMatchSettings.enemies.Count; i++)
        {
            TacticsMatchEnemySettings enemySettings = workingMatchSettings.enemies[i];
            if (enemySettings == null)
            {
                continue;
            }

            hostEnemyEntryWidgets.Add(CreateHostEnemyEntryWidget(i, enemySettings));
        }
    }

    private void RefreshLobbyUi()
    {
        if (lobbyPlayerCardContainer == null)
        {
            return;
        }

        lobbyPlayerCardWidgets.Clear();
        lobbyPlayerCardContainer.Clear();

        IReadOnlyList<TacticsCoopLobbyPlayerState> players = lobbyState != null && lobbyState.players != null
            ? lobbyState.players
            : Array.Empty<TacticsCoopLobbyPlayerState>();
        for (int i = 0; i < players.Count; i++)
        {
            TacticsCoopLobbyPlayerState player = players[i];
            if (player == null)
            {
                continue;
            }

            lobbyPlayerCardWidgets.Add(CreateLobbyPlayerCard(player));
        }
    }

    private LobbyPlayerCardWidget CreateLobbyPlayerCard(TacticsCoopLobbyPlayerState player)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("lobby-player-card");
        if (player.isHost)
        {
            card.AddToClassList("lobby-player-card--host");
        }

        if (player.clientId == coopSessionCoordinator?.LocalClientId)
        {
            card.AddToClassList("lobby-player-card--local");
        }

        VisualElement header = new VisualElement();
        header.AddToClassList("lobby-player-card-header");

        Label usernameLabel = new Label(player.username);
        usernameLabel.AddToClassList("lobby-player-name");
        header.Add(usernameLabel);

        Label readyLabel = new Label(player.isReady ? "READY" : "NOT READY");
        readyLabel.AddToClassList("lobby-player-ready");
        readyLabel.AddToClassList(player.isReady ? "lobby-player-ready--yes" : "lobby-player-ready--no");
        header.Add(readyLabel);

        card.Add(header);

        Label roleLabel = new Label(player.isHost ? "Party Leader" : "Allied Member");
        roleLabel.AddToClassList("lobby-player-role");
        card.Add(roleLabel);

        VisualElement teamList = new VisualElement();
        teamList.AddToClassList("lobby-player-team-list");
        if (player.partyMembers == null || player.partyMembers.Count == 0)
        {
            Label emptyLabel = new Label("No operatives selected yet.");
            emptyLabel.AddToClassList("lobby-player-team-empty");
            teamList.Add(emptyLabel);
        }
        else
        {
            for (int i = 0; i < player.partyMembers.Count; i++)
            {
                TacticsCoopCharacterLoadout loadout = player.partyMembers[i];
                if (loadout == null || string.IsNullOrWhiteSpace(loadout.characterId))
                {
                    continue;
                }

                Label operativeLabel = new Label(BuildLobbyOperativeSummary(loadout));
                operativeLabel.AddToClassList("lobby-player-operative");
                teamList.Add(operativeLabel);
            }
        }

        card.Add(teamList);
        lobbyPlayerCardContainer.Add(card);

        return new LobbyPlayerCardWidget
        {
            Root = card,
            UsernameLabel = usernameLabel,
            ReadyLabel = readyLabel
        };
    }

    private string BuildLobbyStatusMessage(TacticsCoopLobbyState state)
    {
        if (state == null)
        {
            return string.Empty;
        }

        if (state.isMatchStarting)
        {
            return "Synchronizing the full party and launching the match...";
        }

        int playerCount = state.players != null ? state.players.Count : 0;
        int readyCount = 0;
        int requiredPartySize = TacticsPartyCompositionRules.ResolveRequiredMemberCount(
            roster,
            TacticsPartySelection.DefaultCapacity);
        if (state.players != null)
        {
            for (int i = 0; i < state.players.Count; i++)
            {
                TacticsCoopLobbyPlayerState player = state.players[i];
                if (player == null)
                {
                    continue;
                }

                if (!TacticsPartyCompositionRules.HasRequiredMembers(player.partyMembers, roster, TacticsPartySelection.DefaultCapacity))
                {
                    int assignedCount = TacticsPartyCompositionRules.CountValidLoadoutMembers(player.partyMembers, roster);
                    return $"{player.username} needs a full party before the match can start ({assignedCount}/{requiredPartySize} selected).";
                }

                if (player.isReady)
                {
                    readyCount++;
                }
            }
        }

        if (playerCount < Mathf.Max(2, state.minPlayersToStart))
        {
            return $"Lobby open. {playerCount}/{state.maxPlayers} players connected. Waiting for at least one ally.";
        }

        return IsLocalLobbyHost()
            ? $"Lobby ready check: {readyCount}/{playerCount} players ready. Start the match when everyone is set."
            : $"Lobby ready check: {readyCount}/{playerCount} players ready. Waiting for the host to launch the match.";
    }

    private bool IsLocalLobbyHost()
    {
        return coopSessionCoordinator != null &&
               coopSessionCoordinator.IsOnlineSession &&
               coopSessionCoordinator.IsHostAuthority;
    }

    private TacticsCoopLobbyPlayerState GetLocalLobbyPlayerState()
    {
        if (lobbyState?.players == null || coopSessionCoordinator == null)
        {
            return null;
        }

        ulong localClientId = coopSessionCoordinator.LocalClientId;
        for (int i = 0; i < lobbyState.players.Count; i++)
        {
            TacticsCoopLobbyPlayerState player = lobbyState.players[i];
            if (player != null && player.clientId == localClientId)
            {
                return player;
            }
        }

        return null;
    }

    private string BuildLobbyOperativeSummary(TacticsCoopCharacterLoadout loadout)
    {
        string characterId = NormalizeCharacterId(loadout.characterId);
        string displayName = definitionsById != null && definitionsById.TryGetValue(characterId, out TacticsCharacterDefinition definition) && definition != null
            ? definition.DisplayName
            : characterId;
        int level = loadout.progression.WithCharacterId(characterId).Sanitize().Level;
        return $"{displayName.ToUpperInvariant()}  LV {level:00}";
    }

    private HostEnemyEntryWidget CreateHostEnemyEntryWidget(int index, TacticsMatchEnemySettings enemySettings)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("host-enemy-row");

        List<string> enemyDisplayNames = GetEnemyDisplayNames();
        DropdownField dropdown = new DropdownField("Enemy", enemyDisplayNames, 0);
        dropdown.AddToClassList("host-enemy-dropdown");
        dropdown.SetValueWithoutNotify(GetEnemyDisplayName(enemySettings.enemyId));
        dropdown.SetEnabled(isMenuInteractable && !isHostSessionStarted);
        dropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressHostFieldCallbacks || index < 0 || index >= workingMatchSettings.enemies.Count)
            {
                return;
            }

            workingMatchSettings.enemies[index].enemyId = GetEnemyIdFromDisplayName(evt.newValue);
            HandleHostSettingsEdited();
        });

        IntegerField countField = new IntegerField("Count");
        countField.AddToClassList("host-enemy-count-field");
        countField.SetValueWithoutNotify(enemySettings.count);
        countField.SetEnabled(isMenuInteractable && !isHostSessionStarted);
        countField.RegisterValueChangedCallback(evt =>
        {
            if (suppressHostFieldCallbacks || index < 0 || index >= workingMatchSettings.enemies.Count)
            {
                return;
            }

            workingMatchSettings.enemies[index].count = evt.newValue;
            HandleHostSettingsEdited();
        });

        Button removeButton = new Button(() =>
        {
            if (index < 0 || index >= workingMatchSettings.enemies.Count)
            {
                return;
            }

            workingMatchSettings.enemies.RemoveAt(index);
            HandleHostSettingsEdited();
        })
        {
            text = "Remove"
        };
        removeButton.AddToClassList("host-enemy-remove-button");
        removeButton.SetEnabled(isMenuInteractable && !isHostSessionStarted);

        row.Add(dropdown);
        row.Add(countField);
        row.Add(removeButton);
        enemyEntryContainer.Add(row);

        return new HostEnemyEntryWidget
        {
            Root = row,
            EnemyDropdown = dropdown,
            CountField = countField,
            RemoveButton = removeButton
        };
    }

    private bool TryBuildHostRequest(out TacticsSessionStartRequest request, out string validationMessage)
    {
        request = default;
        if (workingMatchSettings == null)
        {
            validationMessage = "Match settings are unavailable.";
            return false;
        }

        TacticsMatchGenerationSettings settings = workingMatchSettings.Clone();
        NormalizeEditableMatchSettings(settings);
        if (!ValidateHostMatchSettings(settings, out validationMessage))
        {
            return false;
        }

        settings.Sanitize();

        request = new TacticsSessionStartRequest(TacticsSessionStartMode.HostCoop, string.Empty, settings);
        return true;
    }

    private bool ValidateHostMatchSettings(TacticsMatchGenerationSettings settings, out string message)
    {
        message = "Tune the battlefield, then host your co-op match.";
        if (settings == null)
        {
            message = "Match settings are unavailable.";
            return false;
        }

        HashSet<string> uniqueEnemyIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < settings.enemies.Count; i++)
        {
            TacticsMatchEnemySettings entry = settings.enemies[i];
            if (entry == null || !entry.IsValid)
            {
                message = "Every enemy entry needs a valid enemy type and count.";
                return false;
            }

            if (!uniqueEnemyIds.Add(entry.enemyId))
            {
                message = "Each enemy row must use a different enemy type.";
                return false;
            }
        }

        return true;
    }

    private bool CanAddMoreEnemies()
    {
        return workingMatchSettings != null &&
               enemyCatalogOptions.Count > 0 &&
               workingMatchSettings.enemies.Count < TacticsMatchGenerationSettings.MaxEnemyKinds &&
               workingMatchSettings.enemies.Count < enemyCatalogOptions.Count;
    }

    private List<string> GetEnemyDisplayNames()
    {
        List<string> names = new(enemyCatalogOptions.Count);
        for (int i = 0; i < enemyCatalogOptions.Count; i++)
        {
            names.Add(enemyCatalogOptions[i].DisplayName);
        }

        return names;
    }

    private string GetEnemyDisplayName(string enemyId)
    {
        for (int i = 0; i < enemyCatalogOptions.Count; i++)
        {
            if (string.Equals(enemyCatalogOptions[i].EnemyId, enemyId, StringComparison.OrdinalIgnoreCase))
            {
                return enemyCatalogOptions[i].DisplayName;
            }
        }

        return enemyCatalogOptions.Count > 0 ? enemyCatalogOptions[0].DisplayName : string.Empty;
    }

    private string GetEnemyIdFromDisplayName(string displayName)
    {
        for (int i = 0; i < enemyCatalogOptions.Count; i++)
        {
            if (string.Equals(enemyCatalogOptions[i].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
            {
                return enemyCatalogOptions[i].EnemyId;
            }
        }

        return enemyCatalogOptions.Count > 0 ? enemyCatalogOptions[0].EnemyId : string.Empty;
    }

    private string GetFirstUnusedEnemyId()
    {
        HashSet<string> usedEnemyIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < workingMatchSettings.enemies.Count; i++)
        {
            TacticsMatchEnemySettings entry = workingMatchSettings.enemies[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.enemyId))
            {
                continue;
            }

            usedEnemyIds.Add(entry.enemyId);
        }

        for (int i = 0; i < enemyCatalogOptions.Count; i++)
        {
            if (!usedEnemyIds.Contains(enemyCatalogOptions[i].EnemyId))
            {
                return enemyCatalogOptions[i].EnemyId;
            }
        }

        return string.Empty;
    }

    private static void NormalizeEditableMatchSettings(TacticsMatchGenerationSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.width = Mathf.Max(1, settings.width);
        settings.length = Mathf.Max(1, settings.length);
        settings.noiseScale = Mathf.Max(0.01f, settings.noiseScale);
        settings.noiseOctaves = Mathf.Clamp(settings.noiseOctaves, 1, 6);
        settings.minElevation = Mathf.Max(0, settings.minElevation);
        settings.maxElevation = Mathf.Max(settings.minElevation, settings.maxElevation);
        settings.enemies ??= new List<TacticsMatchEnemySettings>();

        for (int i = 0; i < settings.enemies.Count; i++)
        {
            TacticsMatchEnemySettings entry = settings.enemies[i];
            if (entry == null)
            {
                continue;
            }

            entry.enemyId = string.IsNullOrWhiteSpace(entry.enemyId) ? string.Empty : entry.enemyId.Trim();
            entry.count = Mathf.Max(1, entry.count);
        }
    }

    private void RefreshAccountUi()
    {
        bool signedIn = accountSessionService?.IsSignedIn ?? false;
        bool busy = accountSessionService?.IsBusy ?? false;
        string status = accountSessionService?.ErrorMessage;
        if (string.IsNullOrWhiteSpace(status))
        {
            status = accountSessionService?.StatusMessage;
        }

        SetLabelText(accountStatusLabel, status);

        if (accountUsernameField != null)
        {
            if (signedIn && !string.IsNullOrWhiteSpace(accountSessionService?.Username))
            {
                accountUsernameField.SetValueWithoutNotify(accountSessionService.Username);
            }

            accountUsernameField.SetEnabled(isMenuInteractable && !busy && !signedIn);
        }

        if (accountPasswordField != null)
        {
            accountPasswordField.SetEnabled(isMenuInteractable && !busy && !signedIn);
            if (signedIn && !busy)
            {
                accountPasswordField.SetValueWithoutNotify(string.Empty);
            }
        }

        accountSignInButton?.SetEnabled(isMenuInteractable && !busy && !signedIn);
        accountRegisterButton?.SetEnabled(isMenuInteractable && !busy && !signedIn);

        if (accountSignInButton != null)
        {
            accountSignInButton.style.display = signedIn ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (accountRegisterButton != null)
        {
            accountRegisterButton.style.display = signedIn ? DisplayStyle.None : DisplayStyle.Flex;
        }

        bool canUseOnline = isMenuInteractable && !busy && signedIn && onlinePartySelectionService != null;
        hostOnlineButton?.SetEnabled(canUseOnline);
        joinOnlineButton?.SetEnabled(canUseOnline);
        joinCodeField?.SetEnabled(canUseOnline);
        editOnlineTeamButton?.SetEnabled(canUseOnline);
    }

    private void HandleAccountSessionStateChanged()
    {
        if (!isEditingOnlineParty && onlinePartySelectionService == null)
        {
            savedOnlineSelection = null;
        }

        if (!(accountSessionService?.IsSignedIn ?? false) && currentMainMenuPage == MainMenuPage.Online)
        {
            currentMainMenuPage = MainMenuPage.Landing;
        }

        RefreshAllUi();
    }

    private static void SetLabelText(Label label, string text)
    {
        if (label == null)
        {
            return;
        }

        label.text = text ?? string.Empty;
        label.style.display = string.IsNullOrWhiteSpace(label.text)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private string GetJoinCode()
    {
        return string.IsNullOrWhiteSpace(joinCodeField?.value)
            ? string.Empty
            : joinCodeField.value.Trim().ToUpperInvariant();
    }

    private TacticsPartySelectionService GetEditorSelectionService()
    {
        return isEditingOnlineParty ? onlinePartySelectionService : localPartySelectionService;
    }

    private TacticsCharacterProgressionService GetEditorProgressionService()
    {
        return isEditingOnlineParty ? onlineProgressionService : localProgressionService;
    }

    private TacticsPartySelection GetSavedSelectionForCurrentEditor()
    {
        return (isEditingOnlineParty ? savedOnlineSelection : savedLocalSelection) ?? TacticsPartySelection.CreateDefault(roster);
    }

    private void RefreshCharacterInspector()
    {
        if (characterInspector?.Root == null)
        {
            return;
        }

        NormalizeInspectorState();
        TacticsCharacterDefinition definition = ResolveInspectorDefinition();
        if (definition == null)
        {
            ApplyEmptyInspectorState();
            return;
        }

        TacticsCharacterProgressionSnapshot progression = GetProgressionSnapshot(definition);
        TacticsCharacterInventorySnapshot inventory = GetInventorySnapshot(definition);
        TacticsCharacterStats effectiveStats = TacticsCharacterEquipmentStatUtility.BuildEffectiveStats(definition.BuildRuntimeData(), progression, inventory);
        TacticsCharacterDerivedStats derivedStats = TacticsCharacterEquipmentStatUtility.BuildDerivedStats(effectiveStats, inventory);

        characterInspector.KickerLabel.text = workingSelection != null && workingSelection.Contains(definition.CharacterId)
            ? "DEPLOYMENT READY"
            : "ROSTER ANALYSIS";
        characterInspector.NameLabel.text = definition.DisplayName.ToUpperInvariant();
        characterInspector.LevelLabel.text = $"LV. {progression.Level:00}";
        characterInspector.SubtitleLabel.text = BuildInspectorSubtitle(definition, progression, effectiveStats);

        EnsureInspectorPreview(definition);
        PopulateInspectorSummary(definition, progression, effectiveStats, derivedStats);
        PopulatePrimaryStats(definition, progression, effectiveStats);
        PopulateDerivedStats(effectiveStats, derivedStats);
        PopulateAbilities(definition);
    }

    private void ApplyEmptyInspectorState()
    {
        if (characterInspector == null)
        {
            return;
        }

        characterInspector.KickerLabel.text = "ROSTER ANALYSIS";
        characterInspector.NameLabel.text = "NO OPERATIVE SELECTED";
        characterInspector.LevelLabel.text = "LV. --";
        characterInspector.SubtitleLabel.text = "Hover or select an operative to review their battle readiness, growth, and core abilities.";
        characterInspector.SummaryContainer?.Clear();
        characterInspector.PrimaryStatsContainer?.Clear();
        characterInspector.DerivedStatsContainer?.Clear();
        characterInspector.AbilitiesContainer?.Clear();

        if (characterInspector.PreviewImage != null)
        {
            characterInspector.PreviewImage.image = null;
        }

        DisposePreview(characterInspector.Preview);
        characterInspector.Preview = null;
        characterInspector.CurrentCharacterId = string.Empty;
        AddSummaryLine(characterInspector.SummaryContainer, "No unit focus is active yet.");
        AddStatRow(characterInspector.PrimaryStatsContainer, "STAMINA", "--");
        AddStatRow(characterInspector.DerivedStatsContainer, "MELEE", "--");
        AddAbilityCard("No abilities available to preview yet.", string.Empty, string.Empty);
    }

    private void PopulateInspectorSummary(
        TacticsCharacterDefinition definition,
        TacticsCharacterProgressionSnapshot progression,
        TacticsCharacterStats effectiveStats,
        TacticsCharacterDerivedStats derivedStats)
    {
        if (characterInspector?.SummaryContainer == null)
        {
            return;
        }

        characterInspector.SummaryContainer.Clear();
        AddSummaryLine(characterInspector.SummaryContainer, $"Move {effectiveStats.MoveRange}  /  Jump {effectiveStats.JumpHeight}  /  XP {progression.CurrentExperience}/{definition.ExperienceToNextLevel}");
        AddSummaryLine(characterInspector.SummaryContainer, $"HP {derivedStats.maxHitPoints}  /  Stamina {derivedStats.maxStamina}  /  Mana {derivedStats.maxMana}");
        AddSummaryLine(characterInspector.SummaryContainer, workingSelection != null && workingSelection.Contains(definition.CharacterId)
            ? "Assigned to the current battle plan."
            : "Available for deployment in the current battle plan.");
    }

    private void PopulatePrimaryStats(
        TacticsCharacterDefinition definition,
        TacticsCharacterProgressionSnapshot progression,
        TacticsCharacterStats effectiveStats)
    {
        if (characterInspector?.PrimaryStatsContainer == null)
        {
            return;
        }

        characterInspector.PrimaryStatsContainer.Clear();
        AddStatRow(characterInspector.PrimaryStatsContainer, "STAMINA", FormatStatValue(effectiveStats.primaryStats.stamina, progression.GetAllocatedValue(TacticsAbilityScalingStat.Stamina)));
        AddStatRow(characterInspector.PrimaryStatsContainer, "STRENGTH", FormatStatValue(effectiveStats.primaryStats.strength, progression.GetAllocatedValue(TacticsAbilityScalingStat.Strength)));
        AddStatRow(characterInspector.PrimaryStatsContainer, "AGILITY", FormatStatValue(effectiveStats.primaryStats.agility, progression.GetAllocatedValue(TacticsAbilityScalingStat.Agility)));
        AddStatRow(characterInspector.PrimaryStatsContainer, "WISDOM", FormatStatValue(effectiveStats.primaryStats.wisdom, progression.GetAllocatedValue(TacticsAbilityScalingStat.Wisdom)));
        AddStatRow(characterInspector.PrimaryStatsContainer, "INT", FormatStatValue(effectiveStats.primaryStats.intelligence, progression.GetAllocatedValue(TacticsAbilityScalingStat.Intelligence)));
        AddStatRow(characterInspector.PrimaryStatsContainer, "POINTS", progression.UnspentAttributePoints.ToString("00"));
    }

    private void PopulateDerivedStats(TacticsCharacterStats effectiveStats, TacticsCharacterDerivedStats derivedStats)
    {
        if (characterInspector?.DerivedStatsContainer == null)
        {
            return;
        }

        characterInspector.DerivedStatsContainer.Clear();
        AddStatRow(characterInspector.DerivedStatsContainer, "MOVE", effectiveStats.MoveRange.ToString("00"));
        AddStatRow(characterInspector.DerivedStatsContainer, "JUMP", effectiveStats.JumpHeight.ToString("00"));
        AddStatRow(characterInspector.DerivedStatsContainer, "MELEE", $"{derivedStats.baseMeleeDamageMin}-{derivedStats.baseMeleeDamageMax}");
        AddStatRow(characterInspector.DerivedStatsContainer, "MAGIC", $"{derivedStats.baseMagicDamageMin}-{derivedStats.baseMagicDamageMax}");
        AddStatRow(characterInspector.DerivedStatsContainer, "MELEE CRIT", FormatPercent(derivedStats.meleeCriticalHitChance));
        AddStatRow(characterInspector.DerivedStatsContainer, "MAGIC CRIT", FormatPercent(derivedStats.magicCriticalHitChance));
    }

    private void PopulateAbilities(TacticsCharacterDefinition definition)
    {
        if (characterInspector?.AbilitiesContainer == null)
        {
            return;
        }

        characterInspector.AbilitiesContainer.Clear();
        IReadOnlyList<TacticsAbilityDefinition> abilities = definition?.StartingAbilities ?? Array.Empty<TacticsAbilityDefinition>();
        if (abilities.Count == 0)
        {
            AddAbilityCard("No abilities configured.", string.Empty, "This operative relies on their standard combat actions.");
            return;
        }

        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            string meta = $"{DescribeAbilityRange(ability)}  /  {DescribeAbilityTarget(ability)}";
            string description = ability.HasResourceCost
                ? $"{ability.Description} Cost: {DescribeAbilityCost(ability)}."
                : ability.Description;
            AddAbilityCard(ability.DisplayName.ToUpperInvariant(), meta, description);
        }
    }

    private void EnsureInspectorPreview(TacticsCharacterDefinition definition)
    {
        if (characterInspector == null || characterInspector.PreviewImage == null)
        {
            return;
        }

        if (characterInspector.CurrentCharacterId == definition.CharacterId && characterInspector.Preview != null)
        {
            characterInspector.PreviewImage.image = characterInspector.Preview.Texture;
            return;
        }

        DisposePreview(characterInspector.Preview);
        characterInspector.CurrentCharacterId = definition.CharacterId;
        characterInspector.Preview = CreatePreview(definition);
        characterInspector.PreviewImage.image = characterInspector.Preview?.Texture;
    }

    private void AddSummaryLine(VisualElement container, string text)
    {
        if (container == null)
        {
            return;
        }

        Label label = new Label(text);
        label.AddToClassList("character-detail-summary-line");
        container.Add(label);
    }

    private void AddStatRow(VisualElement container, string label, string value)
    {
        if (container == null)
        {
            return;
        }

        VisualElement row = new VisualElement();
        row.AddToClassList("character-stat-row");

        Label labelElement = new Label(label);
        labelElement.AddToClassList("character-stat-row-label");
        row.Add(labelElement);

        Label valueElement = new Label(value);
        valueElement.AddToClassList("character-stat-row-value");
        row.Add(valueElement);

        container.Add(row);
    }

    private void AddAbilityCard(string name, string meta, string description)
    {
        if (characterInspector?.AbilitiesContainer == null)
        {
            return;
        }

        VisualElement card = new VisualElement();
        card.AddToClassList("character-ability-card");

        Label nameLabel = new Label(name);
        nameLabel.AddToClassList("character-ability-name");
        card.Add(nameLabel);

        if (!string.IsNullOrWhiteSpace(meta))
        {
            Label metaLabel = new Label(meta);
            metaLabel.AddToClassList("character-ability-meta");
            card.Add(metaLabel);
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            Label descriptionLabel = new Label(description);
            descriptionLabel.AddToClassList("character-ability-description");
            card.Add(descriptionLabel);
        }

        characterInspector.AbilitiesContainer.Add(card);
    }

    private void SetSelectedInspectorCharacter(string characterId)
    {
        selectedInspectorCharacterId = NormalizeCharacterId(characterId);
        RefreshCharacterInspector();
    }

    private void SetHoveredInspectorCharacter(string characterId)
    {
        hoveredInspectorCharacterId = NormalizeCharacterId(characterId);
        RefreshCharacterInspector();
    }

    private void ClearHoveredInspectorCharacter(string characterId)
    {
        string normalizedCharacterId = NormalizeCharacterId(characterId);
        if (!string.Equals(hoveredInspectorCharacterId, normalizedCharacterId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        hoveredInspectorCharacterId = string.Empty;
        RefreshCharacterInspector();
    }

    private void NormalizeInspectorState()
    {
        if (!TryGetDefinition(hoveredInspectorCharacterId, out _))
        {
            hoveredInspectorCharacterId = string.Empty;
        }

        if (!TryGetDefinition(selectedInspectorCharacterId, out _))
        {
            selectedInspectorCharacterId = FindFallbackInspectorCharacterId();
        }
    }

    private TacticsCharacterDefinition ResolveInspectorDefinition()
    {
        if (TryGetDefinition(hoveredInspectorCharacterId, out TacticsCharacterDefinition hoveredDefinition))
        {
            return hoveredDefinition;
        }

        if (TryGetDefinition(selectedInspectorCharacterId, out TacticsCharacterDefinition selectedDefinition))
        {
            return selectedDefinition;
        }

        string fallbackCharacterId = FindFallbackInspectorCharacterId();
        return TryGetDefinition(fallbackCharacterId, out TacticsCharacterDefinition fallbackDefinition)
            ? fallbackDefinition
            : null;
    }

    private string FindFallbackInspectorCharacterId()
    {
        if (workingSelection != null)
        {
            for (int i = 0; i < workingSelection.Capacity; i++)
            {
                string assignedCharacterId = NormalizeCharacterId(workingSelection.GetCharacterId(i));
                if (TryGetDefinition(assignedCharacterId, out _))
                {
                    return assignedCharacterId;
                }
            }
        }

        IReadOnlyList<TacticsCharacterDefinition> playableCharacters = roster?.PlayableCharacters ?? Array.Empty<TacticsCharacterDefinition>();
        for (int i = 0; i < playableCharacters.Count; i++)
        {
            TacticsCharacterDefinition definition = playableCharacters[i];
            if (definition != null)
            {
                return NormalizeCharacterId(definition.CharacterId);
            }
        }

        return string.Empty;
    }

    private bool TryGetDefinition(string characterId, out TacticsCharacterDefinition definition)
    {
        definition = null;
        string normalizedCharacterId = NormalizeCharacterId(characterId);
        return !string.IsNullOrEmpty(normalizedCharacterId) &&
               definitionsById != null &&
               definitionsById.TryGetValue(normalizedCharacterId, out definition);
    }

    private TacticsCharacterProgressionSnapshot GetProgressionSnapshot(TacticsCharacterDefinition definition)
    {
        if (definition == null)
        {
            return TacticsCharacterProgressionSnapshot.CreateDefault(string.Empty);
        }

        TacticsCharacterProgressionService progressionService = GetEditorProgressionService();
        return progressionService != null
            ? progressionService.GetProgression(definition)
            : TacticsCharacterProgressionSnapshot.CreateDefault(definition.CharacterId);
    }

    private TacticsCharacterInventorySnapshot GetInventorySnapshot(TacticsCharacterDefinition definition)
    {
        if (definition == null)
        {
            return TacticsCharacterInventorySnapshot.CreateDefault(string.Empty);
        }

        TacticsCharacterInventoryService inventoryService = isEditingOnlineParty ? onlineInventoryService : localInventoryService;
        return inventoryService != null
            ? inventoryService.GetInventory(definition.CharacterId)
            : TacticsCharacterInventorySnapshot.CreateDefault(definition.CharacterId);
    }

    private static string NormalizeCharacterId(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
    }

    private static string BuildInspectorSubtitle(
        TacticsCharacterDefinition definition,
        TacticsCharacterProgressionSnapshot progression,
        TacticsCharacterStats effectiveStats)
    {
        return $"Level {progression.Level} tactician with {effectiveStats.MoveRange} move, {effectiveStats.JumpHeight} jump, and {definition.StartingAbilities.Count} combat art{(definition.StartingAbilities.Count == 1 ? string.Empty : "s")}.";
    }

    private static string FormatStatValue(int value, int bonusValue)
    {
        return bonusValue > 0 ? $"{value:00}  +{bonusValue}" : value.ToString("00");
    }

    private static string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }

    private static string DescribeAbilityRange(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return "Standard";
        }

        return ability.RangeType switch
        {
            TacticsAbilityRangeType.Melee => "Melee",
            TacticsAbilityRangeType.Ranged => $"Range {ability.Range}",
            TacticsAbilityRangeType.AbsoluteRanged => $"Absolute {ability.Range}",
            TacticsAbilityRangeType.SurroundingAoE => $"AoE {ability.AreaOfEffectSize}",
            TacticsAbilityRangeType.RangedAoE => $"Range {ability.Range} AoE {ability.AreaOfEffectSize}",
            TacticsAbilityRangeType.AbsoluteAoE => $"Absolute {ability.Range} AoE {ability.AreaOfEffectSize}",
            _ => "Standard"
        };
    }

    private static string DescribeAbilityTarget(TacticsAbilityDefinition ability)
    {
        if (ability == null)
        {
            return "Target";
        }

        return ability.TargetRule switch
        {
            TacticsAbilityTargetRule.HostileUnit => ability.DamageType == TacticsAbilityDamageType.Magic ? "Hostile / Magic" : "Hostile / Melee",
            TacticsAbilityTargetRule.AlliedUnit => "Ally",
            TacticsAbilityTargetRule.AlliedUnitOrSelf => "Ally or Self",
            TacticsAbilityTargetRule.Self => "Self",
            _ => "Target"
        };
    }

    private static string DescribeAbilityCost(TacticsAbilityDefinition ability)
    {
        if (ability == null || !ability.HasResourceCost)
        {
            return ability != null && ability.HasMovementCost ? "Movement" : "No cost";
        }

        string resourceLabel = ability.CostResourceType switch
        {
            TacticsAbilityResourceType.Stamina => "Stamina",
            TacticsAbilityResourceType.Mana => "Mana",
            TacticsAbilityResourceType.Movement => "Movement",
            _ => "Resource"
        };

        string costText = $"{ability.CostAmount} {resourceLabel}";
        if (ability.AllowsMovementAsAlternateCost)
        {
            return $"{costText} or Movement";
        }

        return costText;
    }

    private bool CanSaveWorkingSelection()
    {
        return TacticsPartyCompositionRules.HasRequiredMembers(
            workingSelection,
            roster,
            workingSelection != null ? workingSelection.Capacity : TacticsPartySelection.DefaultCapacity);
    }

    private int GetRequiredTeamSize()
    {
        int capacity = workingSelection != null ? workingSelection.Capacity : TacticsPartySelection.DefaultCapacity;
        return TacticsPartyCompositionRules.ResolveRequiredMemberCount(roster, capacity);
    }

    private static int CountAssignedMembers(TacticsPartySelection selection)
    {
        return TacticsPartyCompositionRules.CountAssignedMembers(selection);
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

        if (characterInspector != null)
        {
            characterInspector.Preview = null;
            characterInspector.CurrentCharacterId = string.Empty;
            if (characterInspector.PreviewImage != null)
            {
                characterInspector.PreviewImage.image = null;
            }
        }
    }

    private void DisposePreview(TacticsCharacterCardPreview preview)
    {
        if (preview == null)
        {
            return;
        }

        preview.Dispose();
        previews.Remove(preview);
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

        Vector2 slotSize = slotPreviewWindowSize;
        if (slotSize.x <= 0f && slotSize.y <= 0f)
        {
            slotSize = new Vector2(128f, 148f);
        }

        slotPreviewWindowSize = new Vector2(Mathf.Max(72f, slotSize.x), Mathf.Max(72f, slotSize.y));
    }

    private void OnValidate()
    {
        EnsurePreviewSettingsInitialized();
    }

    private Vector2 GetSanitizedPreviewWindowSize()
    {
        return new Vector2(Mathf.Max(96f, previewWindowSize.x), Mathf.Max(96f, previewWindowSize.y));
    }

    private Vector2 GetSanitizedSlotPreviewWindowSize()
    {
        return new Vector2(Mathf.Max(72f, slotPreviewWindowSize.x), Mathf.Max(72f, slotPreviewWindowSize.y));
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

    private sealed class HostEnemyEntryWidget
    {
        public VisualElement Root;
        public DropdownField EnemyDropdown;
        public IntegerField CountField;
        public Button RemoveButton;
    }

    private sealed class LobbyPlayerCardWidget
    {
        public VisualElement Root;
        public Label UsernameLabel;
        public Label ReadyLabel;
    }

    private sealed class CharacterInspectorWidget
    {
        public string CurrentCharacterId;
        public VisualElement Root;
        public Label SubtitleLabel;
        public Image PreviewImage;
        public Label KickerLabel;
        public Label NameLabel;
        public Label LevelLabel;
        public VisualElement SummaryContainer;
        public VisualElement PrimaryStatsContainer;
        public VisualElement DerivedStatsContainer;
        public VisualElement AbilitiesContainer;
        public TacticsCharacterCardPreview Preview;
    }

    private readonly struct EnemyCatalogOption
    {
        public EnemyCatalogOption(string enemyId, string displayName)
        {
            EnemyId = enemyId;
            DisplayName = displayName;
        }

        public string EnemyId { get; }
        public string DisplayName { get; }
    }
}
