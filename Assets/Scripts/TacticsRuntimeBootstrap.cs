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

    private void Start()
    {
        ConfigureTransparencySorting();

        mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        if (mapGenerator == null)
        {
            Debug.LogWarning("Tactics bootstrap could not find a ProceduralIsometricMapGenerator.");
            return;
        }

        if (mapGenerator.HasGeneratedMap)
        {
            SetupScene();
            return;
        }

        mapGenerator.MapGenerated += HandleMapGenerated;
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
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

    private void SetupScene()
    {
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
            tileTargetOverlay,
            turnManager,
            combatSystem);
        turnManager?.RefreshParticipantsAndStartBattle();
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

        if (topRightNavBarView != null)
        {
            topRightNavBarView.AssignElevationSliderView(elevationSliderView);
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
            TacticsCharacterRoster roster = Resources.Load<TacticsCharacterRoster>(CharacterRosterResourcePath);
            if (roster == null || roster.PlayableCharacters.Count == 0)
            {
                Debug.LogWarning($"Tactics bootstrap could not find a playable roster at Resources/{CharacterRosterResourcePath}.");
                return;
            }

            for (int i = 0; i < roster.PlayableCharacters.Count; i++)
            {
                TacticsCharacterDefinition definition = roster.PlayableCharacters[i];
                if (definition == null)
                {
                    continue;
                }

                Vector2Int spawnTile = FindSpawnTile(definition.PreferredSpawnTile, occupiedTiles);
                TacticsCharacterController character = TacticsCharacterSpawner.SpawnCharacter(mapGenerator, definition, spawnTile);
                if (character != null)
                {
                    occupiedTiles.Add(character.GridPosition);
                }
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
                    spawnTiles[tileIndex]);

                if (enemy != null)
                {
                    occupiedTiles.Add(enemy.GridPosition);
                }
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
}
