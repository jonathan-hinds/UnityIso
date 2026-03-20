using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[DefaultExecutionOrder(1000)]
public class TacticsRuntimeBootstrap : MonoBehaviour
{
    private const string BootstrapName = "Tactics Runtime Bootstrap";
    private const string CharacterRosterResourcePath = "Tactics/CharacterRoster";

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
        EnsureEventSystem();
        TacticsActionMenuView actionMenuView = EnsureActionMenuView();
        TacticsSelectionPanelView selectionPanelView = EnsureSelectionPanelView();
        EnsureTurnCameraDirector();
        TacticsTurnManager turnManager = EnsureTurnManager();
        EnsurePlayerController();
        EnsureCharacters();
        BindHud(actionMenuView, selectionPanelView, turnManager);
        turnManager?.RefreshParticipantsAndStartBattle();
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

    private TacticsSelectionPanelView EnsureSelectionPanelView()
    {
        TacticsSelectionPanelView existingView = FindFirstObjectByType<TacticsSelectionPanelView>();
        if (existingView != null)
        {
            return existingView;
        }

        GameObject hudObject = new GameObject("Tactics Selection Panel HUD");
        return hudObject.AddComponent<TacticsSelectionPanelView>();
    }

    private void BindHud(TacticsActionMenuView actionMenuView, TacticsSelectionPanelView selectionPanelView, TacticsTurnManager turnManager)
    {
        TacticsPlayerController playerController = FindFirstObjectByType<TacticsPlayerController>();
        if (playerController != null)
        {
            playerController.AssignHud(actionMenuView);
            playerController.AssignSelectionHud(selectionPanelView);
            playerController.AssignTurnManager(turnManager);
        }
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
        for (int i = 0; i < enemySpawnEntries.Count; i++)
        {
            TacticsEnemySpawnEntry spawnEntry = enemySpawnEntries[i];
            if (!spawnEntry.IsValid)
            {
                continue;
            }

            List<Vector2Int> spawnTiles = mapGenerator.GetRandomSpawnTiles(spawnEntry.Count, occupiedTiles);
            if (spawnTiles.Count < spawnEntry.Count)
            {
                Debug.LogWarning(
                    $"Tactics bootstrap could only find {spawnTiles.Count} valid spawn tiles for '{spawnEntry.CharacterDefinition.DisplayName}' " +
                    $"out of the requested {spawnEntry.Count}.");
            }

            for (int tileIndex = 0; tileIndex < spawnTiles.Count; tileIndex++)
            {
                TacticsCharacterController enemy = TacticsCharacterSpawner.SpawnCharacter(
                    mapGenerator,
                    spawnEntry.CharacterDefinition,
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
