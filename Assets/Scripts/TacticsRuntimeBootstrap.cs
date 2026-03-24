using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

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
    private TacticsPartySelectionService partySelectionService;
    private TacticsCharacterProgressionService progressionService;
    private TacticsCoopSessionCoordinator coopSessionCoordinator;
    private TacticsCoopBattleSetup pendingCoopBattleSetup;
    private bool sceneSetupComplete;
    private bool gameplayStartInProgress;

    private void Start()
    {
        ConfigureTransparencySorting();
        EnsureEventSystem();

        mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        if (mapGenerator == null)
        {
            Debug.LogWarning("Tactics bootstrap could not find a ProceduralIsometricMapGenerator.");
            return;
        }

        partySelectionService = new TacticsPartySelectionService();
        progressionService = new TacticsCharacterProgressionService();
        coopSessionCoordinator = EnsureCoopSessionCoordinator();
        coopSessionCoordinator.AssignPartySelectionService(partySelectionService);
        coopSessionCoordinator.AssignCharacterProgressionService(progressionService);
        coopSessionCoordinator.StatusChanged -= HandleCoopStatusChanged;
        coopSessionCoordinator.StatusChanged += HandleCoopStatusChanged;
        coopSessionCoordinator.BattleSetupReady -= HandleCoopBattleSetupReady;
        coopSessionCoordinator.BattleSetupReady += HandleCoopBattleSetupReady;
        coopSessionCoordinator.SessionEnded -= HandleSessionEnded;
        coopSessionCoordinator.SessionEnded += HandleSessionEnded;
        mainMenuView = EnsureMainMenuView();
        mainMenuView.AssignDependencies(mapGenerator, partySelectionService);
        ShowMainMenu();
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
                BeginGameplayStartup("Initializing battle systems...");
                break;

            case TacticsSessionStartMode.HostCoop:
                if (!await coopSessionCoordinator.StartHostAsync())
                {
                    mainMenuView.SetInteractable(true);
                }

                break;

            case TacticsSessionStartMode.JoinCoop:
                if (!await coopSessionCoordinator.StartClientAsync(request.Address))
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

        if (mapGenerator.HasGeneratedMap)
        {
            SetupScene();
            return;
        }

        mapGenerator.MapGenerated -= HandleMapGenerated;
        mapGenerator.MapGenerated += HandleMapGenerated;
        mapGenerator.GenerateMap();
    }

    private void HandleCoopStatusChanged(string statusText)
    {
        mainMenuView?.SetStatusText(statusText);
    }

    private void HandleCoopBattleSetupReady(TacticsCoopBattleSetup battleSetup)
    {
        pendingCoopBattleSetup = battleSetup;
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
            new Vector2(36f, 36f),
            new Vector2(576f, 268f));
        TacticsSelectionPanelView selectedCharacterPanelView = EnsureSelectionPanelView(
            TacticsSelectionPanelRole.SelectedCharacter,
            "Selected Character",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-36f, 36f),
            new Vector2(576f, 268f));
        IsometricMapLayerVisibilityController layerVisibilityController = EnsureLayerVisibilityController();
        TacticsElevationSliderView elevationSliderView = EnsureElevationSliderView();
        TacticsTopRightNavBarView topRightNavBarView = EnsureTopRightNavBarView();
        TacticsCharacterMenuView characterMenuView = EnsureCharacterMenuView();
        TacticsTileTargetOverlay tileTargetOverlay = EnsureTileTargetOverlay();
        EnsureCombatTextSystem();
        EnsureTurnCameraDirector();
        TacticsTurnManager turnManager = EnsureTurnManager();
        TacticsCombatSystem combatSystem = EnsureCombatSystem();
        EnsurePlayerController();
        EnsureCharacters();
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
        GameObject hudObject = new GameObject(hudName);
        TacticsSelectionPanelView view = hudObject.AddComponent<TacticsSelectionPanelView>();
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
            characterMenuView.AssignDependencies(progressionService, coopSessionCoordinator);
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

    private static void SetCameraInputEnabled(bool enabled)
    {
        MouseCameraController cameraController = FindFirstObjectByType<MouseCameraController>();
        cameraController?.SetInputEnabled(enabled);
    }

    private void HandleQuitRequested()
    {
        coopSessionCoordinator?.RequestReturnToHome();
    }

    private void ReturnToHomeScreen()
    {
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

    private void EnsureCharacters()
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

        if (!hasPlayerCharacters)
        {
            if (!TrySpawnPlayerCharacters(occupiedTiles))
            {
                return;
            }
        }

        if (hasEnemyCharacters)
        {
            return;
        }

        IReadOnlyList<TacticsEnemySpawnEntry> enemySpawnEntries = mapGenerator.EnemySpawnEntries;
        TacticsEnemyTable enemyTable = Resources.Load<TacticsEnemyTable>(EnemyTableResourcePath);
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
                occupiedTiles.Add(character.GridPosition);
            }
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
        List<TacticsCharacterDefinition> hostParty = ResolveDefinitions(definitionsById, pendingCoopBattleSetup.hostPartyMembers);
        List<TacticsCharacterDefinition> clientParty = ResolveDefinitions(definitionsById, pendingCoopBattleSetup.clientPartyMembers);
        List<TacticsCoopSpawnPlanner.PlannedCharacterSpawn> plannedSpawns = TacticsCoopSpawnPlanner.BuildPlayerSpawns(mapGenerator, hostParty, clientParty);
        if (plannedSpawns.Count == 0)
        {
            Debug.LogWarning("Tactics bootstrap could not create any co-op player spawn entries.");
            return false;
        }

        Dictionary<string, TacticsCharacterProgressionSnapshot> progressionByRuntimeId = BuildProgressionLookup(plannedSpawns);

        for (int i = 0; i < plannedSpawns.Count; i++)
        {
            TacticsCoopSpawnPlanner.PlannedCharacterSpawn plannedSpawn = plannedSpawns[i];
            if (!definitionsById.TryGetValue(plannedSpawn.CharacterId, out TacticsCharacterDefinition definition) || definition == null)
            {
                continue;
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
                occupiedTiles.Add(character.GridPosition);
            }
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

    private Dictionary<string, TacticsCharacterProgressionSnapshot> BuildProgressionLookup(
        IReadOnlyList<TacticsCoopSpawnPlanner.PlannedCharacterSpawn> plannedSpawns)
    {
        Dictionary<string, TacticsCharacterProgressionSnapshot> lookup = new Dictionary<string, TacticsCharacterProgressionSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (pendingCoopBattleSetup == null || plannedSpawns == null)
        {
            return lookup;
        }

        Dictionary<int, Queue<TacticsCharacterProgressionSnapshot>> progressionByParty = new Dictionary<int, Queue<TacticsCharacterProgressionSnapshot>>
        {
            [0] = BuildProgressionQueue(pendingCoopBattleSetup.hostPartyMembers),
            [1] = BuildProgressionQueue(pendingCoopBattleSetup.clientPartyMembers)
        };

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
}
