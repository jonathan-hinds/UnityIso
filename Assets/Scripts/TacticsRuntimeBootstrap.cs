using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(1000)]
public class TacticsRuntimeBootstrap : MonoBehaviour
{
    private const string BootstrapName = "Tactics Runtime Bootstrap";
    private const string CharacterRosterResourcePath = "Tactics/CharacterRoster";
    private const string EnemyTableResourcePath = "Tactics/EnemyTable";
    private static readonly Vector3 IsometricTransparencySortAxis = new Vector3(0f, 1f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateBootstrap()
    {
        if (FindFirstObjectByType<TacticsRuntimeBootstrap>() != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject(BootstrapName);
        bootstrap.AddComponent<TacticsRuntimeBootstrap>();
    }

    private ProceduralIsometricMapGenerator mapGenerator;
    private TacticsMainMenuView mainMenuView;
    private TacticsPartySelectionService localPartySelectionService;
    private TacticsCharacterProgressionService localProgressionService;
    private TacticsPlayerCurrencyService localCurrencyService;
    private TacticsPartySelectionService accountPartySelectionService;
    private TacticsCharacterProgressionService accountProgressionService;
    private TacticsPlayerCurrencyService accountCurrencyService;
    private TacticsPartySelectionService partySelectionService;
    private TacticsCharacterProgressionService progressionService;
    private TacticsPlayerCurrencyService currencyService;
    private ITacticsAccountSessionService accountSessionService;
    private TacticsCoopSessionCoordinator coopSessionCoordinator;
    private TacticsEnemyTable enemyTable;
    private TacticsCoopBattleSetup pendingCoopBattleSetup;
    private bool sceneSetupComplete;
    private bool gameplayStartInProgress;

    private async void Start()
    {
        ConfigureTransparencySorting();
        EnsureEventSystem();

        mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        if (mapGenerator == null)
        {
            Debug.LogWarning("Tactics bootstrap could not find a ProceduralIsometricMapGenerator.");
            return;
        }

        TacticsLegacySaveCleanup.CleanupLegacyPlayerPrefs();
        localPartySelectionService = new TacticsPartySelectionService(new TacticsSinglePlayerPartySelectionStore());
        localProgressionService = new TacticsCharacterProgressionService(new TacticsSinglePlayerCharacterProgressionStore());
        localCurrencyService = new TacticsPlayerCurrencyService(new TacticsSinglePlayerCurrencyStore());
        partySelectionService = localPartySelectionService;
        progressionService = localProgressionService;
        currencyService = localCurrencyService;
        accountSessionService = new TacticsAccountSessionService();
        accountSessionService.StateChanged -= HandleAccountSessionStateChanged;
        accountSessionService.StateChanged += HandleAccountSessionStateChanged;
        enemyTable = Resources.Load<TacticsEnemyTable>(EnemyTableResourcePath);
        coopSessionCoordinator = EnsureCoopSessionCoordinator();
        coopSessionCoordinator.AssignAccountSessionService(accountSessionService);
        coopSessionCoordinator.StatusChanged -= HandleCoopStatusChanged;
        coopSessionCoordinator.StatusChanged += HandleCoopStatusChanged;
        coopSessionCoordinator.BattleSetupReady -= HandleCoopBattleSetupReady;
        coopSessionCoordinator.BattleSetupReady += HandleCoopBattleSetupReady;
        coopSessionCoordinator.SessionEnded -= HandleSessionEnded;
        coopSessionCoordinator.SessionEnded += HandleSessionEnded;
        mainMenuView = EnsureMainMenuView();
        mainMenuView.AssignDependencies(
            mapGenerator,
            localPartySelectionService,
            accountPartySelectionService,
            localProgressionService,
            accountProgressionService,
            enemyTable,
            accountSessionService,
            coopSessionCoordinator);
        ShowMainMenu();
        await accountSessionService.InitializeAsync();
        RefreshAccountScopedServices();
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
        }

        if (mainMenuView != null)
        {
            mainMenuView.SessionStartRequested -= HandleSessionStartRequested;
            mainMenuView.QuitRequested -= HandleApplicationQuitRequested;
        }

        if (accountSessionService != null)
        {
            accountSessionService.StateChanged -= HandleAccountSessionStateChanged;
        }

        if (coopSessionCoordinator != null)
        {
            coopSessionCoordinator.StatusChanged -= HandleCoopStatusChanged;
            coopSessionCoordinator.BattleSetupReady -= HandleCoopBattleSetupReady;
            coopSessionCoordinator.SessionEnded -= HandleSessionEnded;
        }
    }

    private void HandleMapGenerated()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
        }

        SetupScene();
    }

    private void ShowMainMenu()
    {
        if (mainMenuView == null)
        {
            return;
        }

        mainMenuView.SessionStartRequested -= HandleSessionStartRequested;
        mainMenuView.SessionStartRequested += HandleSessionStartRequested;
        mainMenuView.QuitRequested -= HandleApplicationQuitRequested;
        mainMenuView.QuitRequested += HandleApplicationQuitRequested;
        mainMenuView.AssignDependencies(
            mapGenerator,
            localPartySelectionService,
            accountPartySelectionService,
            localProgressionService,
            accountProgressionService,
            enemyTable,
            accountSessionService,
            coopSessionCoordinator);
        mainMenuView.SetStatusText(string.Empty);
        mainMenuView.Show();
        SetCameraInputEnabled(false);
    }

    private async void HandleSessionStartRequested(TacticsSessionStartRequest request)
    {
        if (gameplayStartInProgress || sceneSetupComplete || mainMenuView == null)
        {
            return;
        }

        pendingCoopBattleSetup = null;
        mainMenuView.SetInteractable(false);

        switch (request.Mode)
        {
            case TacticsSessionStartMode.SinglePlayer:
                UseSinglePlayerServices();
                BeginGameplayStartup("Initializing battle systems...");
                break;

            case TacticsSessionStartMode.HostCoop:
                if (!UseAccountServicesForOnline())
                {
                    mainMenuView.SetInteractable(true);
                    break;
                }

                if (!await coopSessionCoordinator.StartHostAsync(request.MatchSettings))
                {
                    mainMenuView.SetInteractable(true);
                }
                else
                {
                    mainMenuView.SetInteractable(true);
                    mainMenuView.HandleHostStarted(coopSessionCoordinator.ActiveRelayJoinCode);
                }

                break;

            case TacticsSessionStartMode.JoinCoop:
                if (!UseAccountServicesForOnline())
                {
                    mainMenuView.SetInteractable(true);
                    break;
                }

                if (!await coopSessionCoordinator.StartClientAsync(request.Address))
                {
                    mainMenuView.SetInteractable(true);
                }
                else
                {
                    mainMenuView.SetInteractable(true);
                }

                break;
        }
    }

    private void BeginGameplayStartup(string statusText)
    {
        if (gameplayStartInProgress || sceneSetupComplete)
        {
            return;
        }

        gameplayStartInProgress = true;
        TacticsRuntimeStartupState.RequestGameplayStart();
        SetCameraInputEnabled(true);

        if (mainMenuView != null)
        {
            mainMenuView.SetStatusText(statusText);
        }

        if (mapGenerator == null)
        {
            Debug.LogWarning("Tactics bootstrap could not start gameplay because no ProceduralIsometricMapGenerator was found.");
            gameplayStartInProgress = false;

            if (mainMenuView != null)
            {
                mainMenuView.SetInteractable(true);
                mainMenuView.SetStatusText("Map generator missing.");
            }

            return;
        }

        mapGenerator.MapGenerated -= HandleMapGenerated;
        mapGenerator.MapGenerated += HandleMapGenerated;
        // Always rebuild the battlefield from the currently selected match settings.
        // Reusing an already-generated board leaves each peer free to keep a stale local
        // map, which breaks command replication because movement/ability resolution then
        // happens against different traversable layouts on each machine.
        mapGenerator.GenerateMap();
    }

    private void HandleCoopStatusChanged(string statusText)
    {
        mainMenuView?.SetStatusText(statusText);
    }

    private void HandleCoopBattleSetupReady(TacticsCoopBattleSetup battleSetup)
    {
        pendingCoopBattleSetup = battleSetup;
        mapGenerator?.ApplyMatchGenerationSettings(battleSetup?.matchSettings);
        BeginGameplayStartup("Synchronizing co-op battle...");
    }

    private void HandleSessionEnded()
    {
        ReturnToHomeScreen();
    }

    private void SetupScene()
    {
        if (sceneSetupComplete)
        {
            return;
        }

        sceneSetupComplete = true;
        ConfigureTransparencySorting();
        EnsureEventSystem();
        TacticsActionMenuView actionMenuView = EnsureActionMenuView();
        TacticsSelectionPanelView activeCharacterPanelView = EnsureSelectionPanelView(
            TacticsSelectionPanelRole.ActiveCharacter,
            "Active Character",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(28f, 28f),
            new Vector2(432f, 206f));
        TacticsSelectionPanelView selectedCharacterPanelView = EnsureSelectionPanelView(
            TacticsSelectionPanelRole.SelectedCharacter,
            "Selected Character",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-28f, 28f),
            new Vector2(432f, 206f));
        IsometricMapLayerVisibilityController layerVisibilityController = EnsureLayerVisibilityController();
        TacticsElevationSliderView elevationSliderView = EnsureElevationSliderView();
        TacticsTopRightNavBarView topRightNavBarView = EnsureTopRightNavBarView();
        TacticsCharacterMenuView characterMenuView = EnsureCharacterMenuView();
        TacticsTileTargetOverlay tileTargetOverlay = EnsureTileTargetOverlay();
        EnsureCombatTextSystem();
        EnsureAbilityHitEffectSystem();
        EnsureStatusEffectTrayView();
        EnsureTurnCameraDirector();
        TacticsCharacterRegistry characterRegistry = EnsureCharacterRegistry();
        TacticsTurnManager turnManager = EnsureTurnManager();
        TacticsCombatSystem combatSystem = EnsureCombatSystem();
        combatSystem?.AssignCharacterRegistry(characterRegistry);
        EnsureChestEncounterService(turnManager);
        EnsurePlayerController();
        EnsureChests();
        if (!EnsureCharacters())
        {
            AbortGameplayStartup("Failed to spawn the full party for this match. Check the console for the missing character.");
            return;
        }

        BindHud(
            actionMenuView,
            activeCharacterPanelView,
            selectedCharacterPanelView,
            layerVisibilityController,
            elevationSliderView,
            topRightNavBarView,
            characterMenuView,
            tileTargetOverlay,
            turnManager,
            combatSystem);
        turnManager?.RefreshParticipantsAndStartBattle();
        gameplayStartInProgress = false;

        if (mainMenuView != null)
        {
            mainMenuView.Hide();
        }
    }

    private void AbortGameplayStartup(string statusText)
    {
        sceneSetupComplete = false;
        gameplayStartInProgress = false;

        if (mainMenuView != null)
        {
            mainMenuView.SetInteractable(true);
            mainMenuView.SetStatusText(statusText);
        }
    }

    private static void ConfigureTransparencySorting()
    {
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = IsometricTransparencySortAxis;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = IsometricTransparencySortAxis;
        }
    }

    private void EnsurePlayerController()
    {
        if (FindFirstObjectByType<TacticsPlayerController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("Tactics Player Controller");
        controllerObject.AddComponent<TacticsPlayerController>();
    }

    private void EnsureStatusEffectTrayView()
    {
        if (FindFirstObjectByType<TacticsStatusEffectTrayView>() != null)
        {
            return;
        }

        GameObject trayObject = new GameObject("Status Effect Tray HUD");
        trayObject.AddComponent<TacticsStatusEffectTrayView>();
    }

    private void EnsureEventSystem()
    {
        EventSystem existingEventSystem = FindFirstObjectByType<EventSystem>();
        if (existingEventSystem != null)
        {
            if (existingEventSystem.GetComponent<BaseInputModule>() == null)
            {
                existingEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private TacticsMainMenuView EnsureMainMenuView()
    {
        TacticsMainMenuView existingView = FindFirstObjectByType<TacticsMainMenuView>();
        if (existingView != null)
        {
            return existingView;
        }

        GameObject menuObject = new GameObject("Tactics Main Menu");
        return menuObject.AddComponent<TacticsMainMenuView>();
    }

    private TacticsActionMenuView EnsureActionMenuView()
    {
        TacticsActionMenuView existingView = FindFirstObjectByType<TacticsActionMenuView>();
        if (existingView != null)
        {
            return existingView;
        }

        GameObject hudObject = new GameObject("Tactics Action Menu HUD");
        return hudObject.AddComponent<TacticsActionMenuView>();
    }

    private TacticsCoopSessionCoordinator EnsureCoopSessionCoordinator()
    {
        TacticsCoopSessionCoordinator existingCoordinator = FindFirstObjectByType<TacticsCoopSessionCoordinator>();
        if (existingCoordinator != null)
        {
            return existingCoordinator;
        }

        GameObject coordinatorObject = new GameObject("Tactics Coop Session Coordinator");
        return coordinatorObject.AddComponent<TacticsCoopSessionCoordinator>();
    }

    private TacticsSelectionPanelView EnsureSelectionPanelView(
        TacticsSelectionPanelRole role,
        string title,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        TacticsSelectionPanelView[] existingViews = FindObjectsByType<TacticsSelectionPanelView>(FindObjectsSortMode.None);
        for (int i = 0; i < existingViews.Length; i++)
        {
            TacticsSelectionPanelView existingView = existingViews[i];
            if (existingView == null || existingView.PanelRole != role)
            {
                continue;
            }

            existingView.Configure(role, title, anchorMin, anchorMax, pivot, position, size);
            return existingView;
        }

        string hudName = role == TacticsSelectionPanelRole.ActiveCharacter
            ? "Tactics Active Character Panel HUD"
            : "Tactics Selected Character Panel HUD";
        TacticsSelectionPanelView prefab = Resources.Load<TacticsSelectionPanelView>(TacticsSelectionPanelView.DefaultPrefabResourcePath);
        TacticsSelectionPanelView view;
        if (prefab != null)
        {
            view = Instantiate(prefab);
            view.gameObject.name = hudName;
        }
        else
        {
            GameObject hudObject = new GameObject(hudName);
            view = hudObject.AddComponent<TacticsSelectionPanelView>();
        }

        view.Configure(role, title, anchorMin, anchorMax, pivot, position, size);
        return view;
    }

    private void BindHud(
        TacticsActionMenuView actionMenuView,
        TacticsSelectionPanelView activeCharacterPanelView,
        TacticsSelectionPanelView selectedCharacterPanelView,
        IsometricMapLayerVisibilityController layerVisibilityController,
        TacticsElevationSliderView elevationSliderView,
        TacticsTopRightNavBarView topRightNavBarView,
        TacticsCharacterMenuView characterMenuView,
        TacticsTileTargetOverlay tileTargetOverlay,
        TacticsTurnManager turnManager,
        TacticsCombatSystem combatSystem)
    {
        if (layerVisibilityController != null)
        {
            layerVisibilityController.AssignMapGenerator(mapGenerator);
        }

        if (elevationSliderView != null)
        {
            elevationSliderView.AssignVisibilityController(layerVisibilityController);
        }

        EnsureCharacterElevationVisibility();

        if (topRightNavBarView != null)
        {
            topRightNavBarView.AssignElevationSliderView(elevationSliderView);
            topRightNavBarView.AssignCharacterMenuView(characterMenuView);
            topRightNavBarView.QuitRequested -= HandleQuitRequested;
            topRightNavBarView.QuitRequested += HandleQuitRequested;
        }

        if (characterMenuView != null)
        {
            characterMenuView.AssignDependencies(progressionService, currencyService, coopSessionCoordinator);
            characterMenuView.ProgressionCommitRequested -= HandleProgressionCommitRequested;
            characterMenuView.ProgressionCommitRequested += HandleProgressionCommitRequested;
            BindCharacterProgressionPersistence(characterMenuView);
            characterMenuView.RefreshCharacterList();
        }

        TacticsPlayerController playerController = FindFirstObjectByType<TacticsPlayerController>();
        if (playerController != null)
        {
            playerController.AssignHud(actionMenuView);
            playerController.AssignActiveCharacterHud(activeCharacterPanelView);
            playerController.AssignSelectedCharacterHud(selectedCharacterPanelView);
            playerController.AssignTurnManager(turnManager);
            playerController.AssignCombatSystem(combatSystem);
            playerController.AssignTileTargetOverlay(tileTargetOverlay);
        }
    }

    private IsometricMapLayerVisibilityController EnsureLayerVisibilityController()
    {
        IsometricMapLayerVisibilityController existingController = FindFirstObjectByType<IsometricMapLayerVisibilityController>();
        if (existingController != null)
        {
            existingController.AssignMapGenerator(mapGenerator);
            return existingController;
        }

        GameObject controllerObject = new GameObject("Isometric Map Layer Visibility Controller");
        IsometricMapLayerVisibilityController controller = controllerObject.AddComponent<IsometricMapLayerVisibilityController>();
        controller.AssignMapGenerator(mapGenerator);
        return controller;
    }

    private void EnsureCharacterElevationVisibility()
    {
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character == null)
            {
                continue;
            }

            if (character.TryGetComponent<TacticsCharacterElevationVisibility>(out _))
            {
                continue;
            }

            character.gameObject.AddComponent<TacticsCharacterElevationVisibility>();
        }
    }

    private TacticsElevationSliderView EnsureElevationSliderView()
    {
        TacticsElevationSliderView existingView = FindFirstObjectByType<TacticsElevationSliderView>();
        if (existingView != null)
        {
            return existingView;
        }

        GameObject hudObject = new GameObject("Tactics Elevation Slider HUD");
        return hudObject.AddComponent<TacticsElevationSliderView>();
    }

    private TacticsTopRightNavBarView EnsureTopRightNavBarView()
    {
        TacticsTopRightNavBarView existingView = FindFirstObjectByType<TacticsTopRightNavBarView>();
        if (existingView != null)
        {
            return existingView;
        }

        GameObject hudObject = new GameObject("Tactics Top Right Nav HUD");
        return hudObject.AddComponent<TacticsTopRightNavBarView>();
    }

    private TacticsCharacterMenuView EnsureCharacterMenuView()
    {
        TacticsCharacterMenuView existingView = FindFirstObjectByType<TacticsCharacterMenuView>();
        if (existingView != null)
        {
            return existingView;
        }

        GameObject hudObject = new GameObject("Tactics Character Menu HUD");
        return hudObject.AddComponent<TacticsCharacterMenuView>();
    }

    private TacticsTurnManager EnsureTurnManager()
    {
        TacticsTurnManager existingManager = FindFirstObjectByType<TacticsTurnManager>();
        if (existingManager != null)
        {
            return existingManager;
        }

        GameObject managerObject = new GameObject("Tactics Turn Manager");
        managerObject.AddComponent<TacticsTurnManager>();
        return managerObject.GetComponent<TacticsTurnManager>();
    }

    private TacticsCharacterRegistry EnsureCharacterRegistry()
    {
        TacticsCharacterRegistry existingRegistry = FindFirstObjectByType<TacticsCharacterRegistry>();
        if (existingRegistry != null)
        {
            return existingRegistry;
        }

        GameObject registryObject = new GameObject("Tactics Character Registry");
        return registryObject.AddComponent<TacticsCharacterRegistry>();
    }

    private void EnsureTurnCameraDirector()
    {
        if (FindFirstObjectByType<TacticsTurnCameraDirector>() != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.gameObject.AddComponent<TacticsTurnCameraDirector>();
        }
    }

    private TacticsCombatSystem EnsureCombatSystem()
    {
        TacticsCombatSystem existingSystem = FindFirstObjectByType<TacticsCombatSystem>();
        if (existingSystem != null)
        {
            existingSystem.AssignMapGenerator(mapGenerator);
            return existingSystem;
        }

        GameObject combatSystemObject = new GameObject("Tactics Combat System");
        TacticsCombatSystem combatSystem = combatSystemObject.AddComponent<TacticsCombatSystem>();
        combatSystem.AssignMapGenerator(mapGenerator);
        return combatSystem;
    }

    private TacticsTileTargetOverlay EnsureTileTargetOverlay()
    {
        TacticsTileTargetOverlay existingOverlay = FindFirstObjectByType<TacticsTileTargetOverlay>();
        if (existingOverlay != null)
        {
            return existingOverlay;
        }

        GameObject overlayObject = new GameObject("Tactics Tile Target Overlay");
        return overlayObject.AddComponent<TacticsTileTargetOverlay>();
    }

    private void EnsureCombatTextSystem()
    {
        if (FindFirstObjectByType<TacticsCombatTextSystem>() != null)
        {
            return;
        }

        GameObject combatTextObject = new GameObject("Tactics Combat Text System");
        combatTextObject.AddComponent<TacticsCombatTextSystem>();
    }

    private void EnsureAbilityHitEffectSystem()
    {
        if (FindFirstObjectByType<TacticsAbilityHitEffectSystem>() != null)
        {
            return;
        }

        GameObject hitEffectObject = new GameObject("Tactics Ability Hit Effect System");
        hitEffectObject.AddComponent<TacticsAbilityHitEffectSystem>();
    }

    private static void SetCameraInputEnabled(bool enabled)
    {
        MouseCameraController cameraController = FindFirstObjectByType<MouseCameraController>();
        cameraController?.SetInputEnabled(enabled);
    }

    private void HandleQuitRequested()
    {
        coopSessionCoordinator?.RequestReturnToHome();
    }

    private void HandleApplicationQuitRequested()
    {
        _ = accountSessionService?.FlushAsync();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ReturnToHomeScreen()
    {
        _ = accountSessionService?.FlushAsync();
        CleanupGameplayObjects();
        pendingCoopBattleSetup = null;
        sceneSetupComplete = false;
        gameplayStartInProgress = false;
        TacticsRuntimeStartupState.ResetGameplayStart();
        ShowMainMenu();
    }

    private void CleanupGameplayObjects()
    {
        DestroyAllOfType<TacticsCharacterController>();
        DestroyAllOfType<TacticsCharacterRegistry>();
        DestroyAllOfType<TacticsChestController>();
        DestroyAllOfType<TacticsPlayerController>();
        DestroyAllOfType<TacticsTurnManager>();
        DestroyAllOfType<TacticsCombatSystem>();
        DestroyAllOfType<TacticsTurnCameraDirector>();
        DestroyAllOfType<TacticsTileTargetOverlay>();
        DestroyAllOfType<TacticsActionMenuView>();
        DestroyAllOfType<TacticsSelectionPanelView>();
        DestroyAllOfType<IsometricMapLayerVisibilityController>();
        DestroyAllOfType<TacticsElevationSliderView>();
        DestroyAllOfType<TacticsTopRightNavBarView>();
        DestroyAllOfType<TacticsCharacterMenuView>();
        DestroyAllOfType<TacticsCombatTextSystem>();
        DestroyAllOfType<TacticsAbilityHitEffectSystem>();
        DestroyAllOfType<TacticsAbilityHitEffectInstance>();
        DestroyAllOfType<TacticsChestEncounterService>();
        DestroyAllOfType<TacticsFloatingCombatText>();
        DestroyAllOfType<TacticsOverheadHealthBar>();
    }

    private static void DestroyAllOfType<T>() where T : UnityEngine.Object
    {
        T[] objects = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            T instance = objects[i];
            if (instance == null ||
                instance is TacticsRuntimeBootstrap ||
                instance is TacticsMainMenuView ||
                instance is TacticsCoopSessionCoordinator)
            {
                continue;
            }

            if (instance is Component component)
            {
                if (component is TacticsTurnCameraDirector)
                {
                    Destroy(component);
                }
                else
                {
                    Destroy(component.gameObject);
                }
            }
            else if (instance is GameObject gameObject)
            {
                Destroy(gameObject);
            }
        }
    }

    private void EnsureChests()
    {
        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return;
        }

        TacticsChestController[] existingChests = FindObjectsByType<TacticsChestController>(FindObjectsSortMode.None);
        if (existingChests.Length > 0)
        {
            return;
        }

        List<ProceduralIsometricMapGenerator.ChestSpawnPlan> chestPlans = mapGenerator.CreateChestSpawnPlans();
        if (chestPlans.Count == 0)
        {
            return;
        }

        Transform chestRoot = mapGenerator.GetOrCreateGeneratedAttachmentRoot("Chests");
        for (int i = 0; i < chestPlans.Count; i++)
        {
            ProceduralIsometricMapGenerator.ChestSpawnPlan chestPlan = chestPlans[i];
            GameObject chestObject = new GameObject($"Chest_{i}");
            chestObject.transform.SetParent(chestRoot, false);

            TacticsChestController chest = chestObject.AddComponent<TacticsChestController>();
            chest.Initialize(
                mapGenerator,
                chestPlan.RuntimeChestId,
                chestPlan.Tile,
                chestPlan.Facing,
                opened: false,
                containsMimic: chestPlan.ContainsMimic);
            chestObject.AddComponent<TacticsChestElevationVisibility>();
        }
    }

    private TacticsChestEncounterService EnsureChestEncounterService(TacticsTurnManager turnManager)
    {
        TacticsChestEncounterService existingService = FindFirstObjectByType<TacticsChestEncounterService>();
        if (existingService != null)
        {
            existingService.AssignDependencies(mapGenerator, enemyTable, turnManager);
            return existingService;
        }

        GameObject serviceObject = new GameObject("Tactics Chest Encounter Service");
        TacticsChestEncounterService service = serviceObject.AddComponent<TacticsChestEncounterService>();
        service.AssignDependencies(mapGenerator, enemyTable, turnManager);
        return service;
    }

    private bool EnsureCharacters()
    {
        TacticsCharacterController[] existingCharacters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();
        bool hasPlayerCharacters = false;
        bool hasEnemyCharacters = false;

        for (int i = 0; i < existingCharacters.Length; i++)
        {
            TacticsCharacterController existingCharacter = existingCharacters[i];
            if (existingCharacter == null)
            {
                continue;
            }

            occupiedTiles.Add(existingCharacter.GridPosition);

            if (existingCharacter.Team == TacticsUnitTeam.Player)
            {
                hasPlayerCharacters = true;
            }
            else if (existingCharacter.Team == TacticsUnitTeam.Enemy)
            {
                hasEnemyCharacters = true;
            }
        }

        TacticsChestController[] existingChests = FindObjectsByType<TacticsChestController>(FindObjectsSortMode.None);
        for (int i = 0; i < existingChests.Length; i++)
        {
            TacticsChestController chest = existingChests[i];
            if (chest != null)
            {
                occupiedTiles.Add(chest.GridPosition);
            }
        }

        if (!hasPlayerCharacters)
        {
            if (!TrySpawnPlayerCharacters(occupiedTiles))
            {
                return false;
            }
        }

        if (hasEnemyCharacters)
        {
            return true;
        }

        IReadOnlyList<TacticsEnemySpawnEntry> enemySpawnEntries = mapGenerator.EnemySpawnEntries;
        for (int i = 0; i < enemySpawnEntries.Count; i++)
        {
            TacticsEnemySpawnEntry spawnEntry = enemySpawnEntries[i];
            if (!spawnEntry.IsValid)
            {
                continue;
            }

            if (enemyTable == null)
            {
                Debug.LogWarning($"Tactics bootstrap could not find an enemy table at Resources/{EnemyTableResourcePath}.");
                break;
            }

            if (!enemyTable.TryGetCharacterData(spawnEntry.EnemyId, out TacticsCharacterData enemyData))
            {
                Debug.LogWarning($"Tactics bootstrap skipped enemy spawn entry '{spawnEntry.EnemyId}' because no matching enemy table row was found.");
                continue;
            }

            List<Vector2Int> spawnTiles = mapGenerator.GetRandomSpawnTiles(spawnEntry.Count, occupiedTiles);
            if (spawnTiles.Count < spawnEntry.Count)
            {
                Debug.LogWarning(
                    $"Tactics bootstrap could only find {spawnTiles.Count} valid spawn tiles for '{enemyData.DisplayName}' " +
                    $"out of the requested {spawnEntry.Count}.");
            }

            for (int tileIndex = 0; tileIndex < spawnTiles.Count; tileIndex++)
            {
                TacticsCharacterController enemy = TacticsCharacterSpawner.SpawnCharacter(
                    mapGenerator,
                    enemyData,
                    spawnTiles[tileIndex],
                    runtimeCharacterId: $"enemy_{spawnEntry.EnemyId}_{tileIndex}");

                if (enemy != null)
                {
                    occupiedTiles.Add(enemy.GridPosition);
                }
            }
        }

        return true;
    }

    private bool TrySpawnPlayerCharacters(HashSet<Vector2Int> occupiedTiles)
    {
        if (pendingCoopBattleSetup != null)
        {
            return TrySpawnCoopPlayerCharacters(occupiedTiles);
        }

        IReadOnlyList<TacticsCharacterDefinition> selectedParty = partySelectionService != null
            ? partySelectionService.ResolveSelectedParty()
            : Array.Empty<TacticsCharacterDefinition>();
        if (selectedParty == null || selectedParty.Count == 0)
        {
            Debug.LogWarning($"Tactics bootstrap could not resolve a playable party from Resources/{CharacterRosterResourcePath}.");
            return false;
        }

        List<TacticsCharacterController> spawnedCharacters = new List<TacticsCharacterController>(selectedParty.Count);
        for (int i = 0; i < selectedParty.Count; i++)
        {
            TacticsCharacterDefinition definition = selectedParty[i];
            if (definition == null)
            {
                continue;
            }

            Vector2Int spawnTile = FindSpawnTile(definition.PreferredSpawnTile, occupiedTiles);
            TacticsCharacterController character = TacticsCharacterSpawner.SpawnCharacter(
                mapGenerator,
                definition,
                spawnTile,
                runtimeCharacterId: $"party_0_slot_{i}_{definition.CharacterId}",
                progression: progressionService != null
                    ? progressionService.GetProgression(definition)
                    : TacticsCharacterProgressionSnapshot.CreateDefault(definition.CharacterId));
            if (character != null)
            {
                spawnedCharacters.Add(character);
                occupiedTiles.Add(character.GridPosition);
                continue;
            }

            CleanupSpawnedCharacters(spawnedCharacters);
            Debug.LogError($"Tactics bootstrap failed to spawn party member '{definition.CharacterId}' during startup.");
            return false;
        }

        return true;
    }

    private bool TrySpawnCoopPlayerCharacters(HashSet<Vector2Int> occupiedTiles)
    {
        TacticsCharacterRoster roster = partySelectionService?.LoadRoster();
        if (roster == null)
        {
            Debug.LogWarning($"Tactics bootstrap could not resolve a playable party from Resources/{CharacterRosterResourcePath}.");
            return false;
        }

        Dictionary<string, TacticsCharacterDefinition> definitionsById = roster.BuildLookupById();
        int expectedSpawnCount = CountConfiguredCoopPartyMembers(pendingCoopBattleSetup);
        if (expectedSpawnCount <= 0)
        {
            Debug.LogWarning("Tactics bootstrap received a co-op battle setup with no configured player characters.");
            return false;
        }

        List<string> unresolvedCharacterIds = FindUnresolvedCoopCharacterIds(definitionsById, pendingCoopBattleSetup);
        if (unresolvedCharacterIds.Count > 0)
        {
            Debug.LogError($"Tactics bootstrap could not resolve co-op character definitions for: {string.Join(", ", unresolvedCharacterIds)}.");
            return false;
        }

        List<TacticsCoopSpawnPlanner.PlannedCharacterSpawn> plannedSpawns = TacticsCoopSpawnPlanner.BuildPlayerSpawns(
            mapGenerator,
            BuildCoopParties(definitionsById, pendingCoopBattleSetup),
            occupiedTiles);
        if (plannedSpawns.Count != expectedSpawnCount)
        {
            Debug.LogError(
                $"Tactics bootstrap planned {plannedSpawns.Count} co-op player spawns, but the battle setup requires {expectedSpawnCount}. " +
                "A party member was dropped before spawn planning completed.");
            return false;
        }

        Dictionary<string, TacticsCharacterProgressionSnapshot> progressionByRuntimeId = BuildProgressionLookup(plannedSpawns);
        List<TacticsCharacterController> spawnedCharacters = new List<TacticsCharacterController>(plannedSpawns.Count);

        for (int i = 0; i < plannedSpawns.Count; i++)
        {
            TacticsCoopSpawnPlanner.PlannedCharacterSpawn plannedSpawn = plannedSpawns[i];
            if (!definitionsById.TryGetValue(plannedSpawn.CharacterId, out TacticsCharacterDefinition definition) || definition == null)
            {
                CleanupSpawnedCharacters(spawnedCharacters);
                Debug.LogError($"Tactics bootstrap lost the co-op character definition for '{plannedSpawn.CharacterId}' during spawn execution.");
                return false;
            }

            TacticsCharacterController character = TacticsCharacterSpawner.SpawnCharacter(
                mapGenerator,
                definition,
                plannedSpawn.SpawnTile,
                runtimeCharacterId: plannedSpawn.RuntimeId,
                progression: progressionByRuntimeId.TryGetValue(plannedSpawn.RuntimeId, out TacticsCharacterProgressionSnapshot progression)
                    ? progression
                    : TacticsCharacterProgressionSnapshot.CreateDefault(definition.CharacterId));
            if (character != null)
            {
                spawnedCharacters.Add(character);
                occupiedTiles.Add(character.GridPosition);
                continue;
            }

            CleanupSpawnedCharacters(spawnedCharacters);
            Debug.LogError(
                $"Tactics bootstrap failed to spawn co-op party member '{plannedSpawn.CharacterId}' " +
                $"for party {plannedSpawn.PartyIndex} slot {plannedSpawn.SlotIndex}.");
            return false;
        }

        return true;
    }

    private static List<TacticsCharacterDefinition> ResolveDefinitions(
        Dictionary<string, TacticsCharacterDefinition> definitionsById,
        List<TacticsCoopCharacterLoadout> loadout)
    {
        List<TacticsCharacterDefinition> results = new();
        if (definitionsById == null || loadout == null)
        {
            return results;
        }

        for (int i = 0; i < loadout.Count; i++)
        {
            string characterId = TacticsPartySelection.NormalizeCharacterId(loadout[i]?.characterId);
            if (!string.IsNullOrEmpty(characterId) &&
                definitionsById.TryGetValue(characterId, out TacticsCharacterDefinition definition) &&
                definition != null)
            {
                results.Add(definition);
            }
        }

        return results;
    }

    private static int CountConfiguredCoopPartyMembers(TacticsCoopBattleSetup battleSetup)
    {
        if (battleSetup?.players == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < battleSetup.players.Count; i++)
        {
            List<TacticsCoopCharacterLoadout> loadout = battleSetup.players[i]?.partyMembers;
            if (loadout == null)
            {
                continue;
            }

            for (int j = 0; j < loadout.Count; j++)
            {
                if (!string.IsNullOrWhiteSpace(loadout[j]?.characterId))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static List<string> FindUnresolvedCoopCharacterIds(
        Dictionary<string, TacticsCharacterDefinition> definitionsById,
        TacticsCoopBattleSetup battleSetup)
    {
        List<string> unresolvedIds = new List<string>();
        if (battleSetup?.players == null)
        {
            return unresolvedIds;
        }

        for (int i = 0; i < battleSetup.players.Count; i++)
        {
            List<TacticsCoopCharacterLoadout> loadout = battleSetup.players[i]?.partyMembers;
            if (loadout == null)
            {
                continue;
            }

            for (int j = 0; j < loadout.Count; j++)
            {
                string characterId = TacticsPartySelection.NormalizeCharacterId(loadout[j]?.characterId);
                if (!string.IsNullOrEmpty(characterId) &&
                    (definitionsById == null || !definitionsById.ContainsKey(characterId)))
                {
                    unresolvedIds.Add(characterId);
                }
            }
        }

        return unresolvedIds;
    }

    private static void CleanupSpawnedCharacters(List<TacticsCharacterController> spawnedCharacters)
    {
        if (spawnedCharacters == null)
        {
            return;
        }

        for (int i = 0; i < spawnedCharacters.Count; i++)
        {
            if (spawnedCharacters[i] != null)
            {
                Destroy(spawnedCharacters[i].gameObject);
            }
        }
    }

    private Vector2Int FindSpawnTile(Vector2Int requestedTile, HashSet<Vector2Int> occupiedTiles)
    {
        if (IsSpawnTileAvailable(requestedTile, occupiedTiles))
        {
            return requestedTile;
        }

        Vector2Int center = requestedTile == default ? mapGenerator.GetCenterTile() : requestedTile;
        int maxRadius = Mathf.Max(mapGenerator.Width, mapGenerator.Length);

        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (IsSpawnTileAvailable(candidate, occupiedTiles))
                    {
                        return candidate;
                    }
                }
            }
        }

        return mapGenerator.GetCenterTile();
    }

    private bool IsSpawnTileAvailable(Vector2Int tile, HashSet<Vector2Int> occupiedTiles)
    {
        if (!mapGenerator.IsTraversable(tile.x, tile.y))
        {
            return false;
        }

        if (occupiedTiles != null && occupiedTiles.Contains(tile))
        {
            return false;
        }

        TacticsCharacterController[] existingCharacters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < existingCharacters.Length; i++)
        {
            if (existingCharacters[i] != null && existingCharacters[i].GridPosition == tile)
            {
                return false;
            }
        }

        return true;
    }

    private static List<IReadOnlyList<TacticsCharacterDefinition>> BuildCoopParties(
        Dictionary<string, TacticsCharacterDefinition> definitionsById,
        TacticsCoopBattleSetup battleSetup)
    {
        List<IReadOnlyList<TacticsCharacterDefinition>> parties = new();
        if (battleSetup?.players == null)
        {
            return parties;
        }

        for (int i = 0; i < battleSetup.players.Count; i++)
        {
            TacticsCoopBattlePlayer player = battleSetup.players[i];
            parties.Add(ResolveDefinitions(definitionsById, player?.partyMembers));
        }

        return parties;
    }

    private Dictionary<string, TacticsCharacterProgressionSnapshot> BuildProgressionLookup(
        IReadOnlyList<TacticsCoopSpawnPlanner.PlannedCharacterSpawn> plannedSpawns)
    {
        Dictionary<string, TacticsCharacterProgressionSnapshot> lookup = new Dictionary<string, TacticsCharacterProgressionSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (pendingCoopBattleSetup == null || plannedSpawns == null)
        {
            return lookup;
        }

        Dictionary<int, Queue<TacticsCharacterProgressionSnapshot>> progressionByParty = new Dictionary<int, Queue<TacticsCharacterProgressionSnapshot>>();
        for (int i = 0; i < pendingCoopBattleSetup.players.Count; i++)
        {
            TacticsCoopBattlePlayer player = pendingCoopBattleSetup.players[i];
            progressionByParty[i] = BuildProgressionQueue(player?.partyMembers);
        }

        for (int i = 0; i < plannedSpawns.Count; i++)
        {
            TacticsCoopSpawnPlanner.PlannedCharacterSpawn plannedSpawn = plannedSpawns[i];
            if (!progressionByParty.TryGetValue(plannedSpawn.PartyIndex, out Queue<TacticsCharacterProgressionSnapshot> queue) ||
                queue.Count == 0)
            {
                continue;
            }

            lookup[plannedSpawn.RuntimeId] = queue.Dequeue();
        }

        return lookup;
    }

    private static Queue<TacticsCharacterProgressionSnapshot> BuildProgressionQueue(List<TacticsCoopCharacterLoadout> loadout)
    {
        Queue<TacticsCharacterProgressionSnapshot> queue = new Queue<TacticsCharacterProgressionSnapshot>();
        if (loadout == null)
        {
            return queue;
        }

        for (int i = 0; i < loadout.Count; i++)
        {
            TacticsCoopCharacterLoadout entry = loadout[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.characterId))
            {
                continue;
            }

            queue.Enqueue(entry.progression.WithCharacterId(entry.characterId).Sanitize());
        }

        return queue;
    }

    private void BindCharacterProgressionPersistence(TacticsCharacterMenuView characterMenuView)
    {
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character == null || !character.IsPlayerControlled)
            {
                continue;
            }

            character.ProgressionChanged -= HandleCharacterProgressionChanged;
            character.ProgressionChanged += HandleCharacterProgressionChanged;
        }
    }

    private void HandleCharacterProgressionChanged(TacticsCharacterController character)
    {
        if (character == null || progressionService == null || !character.IsPlayerControlled)
        {
            return;
        }

        bool isLocallyOwned = coopSessionCoordinator == null || coopSessionCoordinator.CanLocalPlayerControlCharacter(character);
        if (isLocallyOwned)
        {
            progressionService.SaveProgression(character.Progression.WithCharacterId(character.CharacterData != null ? character.CharacterData.CharacterId : string.Empty));
        }

        TacticsCharacterMenuView characterMenuView = FindFirstObjectByType<TacticsCharacterMenuView>();
        characterMenuView?.MarkProgressionCommitted(character);
    }

    private void HandleProgressionCommitRequested(TacticsCharacterController character, TacticsCharacterProgressionSnapshot snapshot)
    {
        if (character == null)
        {
            return;
        }

        if (coopSessionCoordinator != null)
        {
            coopSessionCoordinator.RequestCommitProgression(character, snapshot);
            return;
        }

        character.TryCommitProgression(snapshot);
    }

    private void HandleAccountSessionStateChanged()
    {
        RefreshAccountScopedServices();
    }

    private void RefreshAccountScopedServices()
    {
        if (accountSessionService != null && accountSessionService.IsSignedIn && accountSessionService.Profile != null)
        {
            accountPartySelectionService = new TacticsPartySelectionService(new TacticsCloudSavePartySelectionStore(accountSessionService.Profile));
            accountProgressionService = new TacticsCharacterProgressionService(new TacticsCloudSaveCharacterProgressionStore(accountSessionService.Profile));
            accountCurrencyService = new TacticsPlayerCurrencyService(new TacticsCloudSaveCurrencyStore(accountSessionService.Profile));
            coopSessionCoordinator?.AssignPartySelectionService(accountPartySelectionService);
            coopSessionCoordinator?.AssignCharacterProgressionService(accountProgressionService);
            coopSessionCoordinator?.AssignCurrencyService(accountCurrencyService);
            coopSessionCoordinator?.AssignAccountSessionService(accountSessionService);
        }
        else
        {
            accountPartySelectionService = null;
            accountProgressionService = null;
            accountCurrencyService = null;
            coopSessionCoordinator?.AssignPartySelectionService(null);
            coopSessionCoordinator?.AssignCharacterProgressionService(null);
            coopSessionCoordinator?.AssignCurrencyService(null);
            coopSessionCoordinator?.AssignAccountSessionService(accountSessionService);
        }

        mainMenuView?.AssignDependencies(
            mapGenerator,
            localPartySelectionService,
            accountPartySelectionService,
            localProgressionService,
            accountProgressionService,
            enemyTable,
            accountSessionService,
            coopSessionCoordinator);
    }

    private void UseSinglePlayerServices()
    {
        partySelectionService = localPartySelectionService;
        progressionService = localProgressionService;
        currencyService = localCurrencyService;
        coopSessionCoordinator?.AssignPartySelectionService(partySelectionService);
        coopSessionCoordinator?.AssignCharacterProgressionService(progressionService);
        coopSessionCoordinator?.AssignCurrencyService(currencyService);
    }

    private bool UseAccountServicesForOnline()
    {
        if (accountPartySelectionService == null || accountProgressionService == null || accountCurrencyService == null)
        {
            mainMenuView?.SetStatusText(accountSessionService?.ErrorMessage ?? "Sign in before starting online co-op.");
            return false;
        }

        partySelectionService = accountPartySelectionService;
        progressionService = accountProgressionService;
        currencyService = accountCurrencyService;
        coopSessionCoordinator?.AssignPartySelectionService(partySelectionService);
        coopSessionCoordinator?.AssignCharacterProgressionService(progressionService);
        coopSessionCoordinator?.AssignCurrencyService(currencyService);
        return true;
    }
}
