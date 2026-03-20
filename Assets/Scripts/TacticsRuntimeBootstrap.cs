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
        if (FindFirstObjectByType<TacticsCharacterController>() != null)
        {
            return;
        }

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

            SpawnCharacter(definition);
        }
    }

    private void SpawnCharacter(TacticsCharacterDefinition definition)
    {
        if (!definition.TryGetOrderedSprites(out _))
        {
            Debug.LogWarning($"Tactics bootstrap skipped '{definition.name}' because its sprite data is invalid.");
            return;
        }

        Vector2Int spawnTile = FindSpawnTile(definition.PreferredSpawnTile);

        GameObject characterRoot = new GameObject(definition.DisplayName);
        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(characterRoot.transform, false);

        SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = "Default";

        TacticsCharacterAnimator animator = characterRoot.AddComponent<TacticsCharacterAnimator>();
        animator.Initialize(spriteRenderer, definition);

        BoxCollider2D selectionCollider = visualObject.AddComponent<BoxCollider2D>();
        Vector2 spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : new Vector2(0.2f, 0.3f);
        selectionCollider.size = spriteSize;
        selectionCollider.offset = Vector2.zero;

        TacticsCharacterController characterController = characterRoot.AddComponent<TacticsCharacterController>();
        characterController.Initialize(mapGenerator, animator, definition, spawnTile);
    }

    private Vector2Int FindSpawnTile(Vector2Int requestedTile)
    {
        if (IsSpawnTileAvailable(requestedTile))
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
                    if (IsSpawnTileAvailable(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return mapGenerator.GetCenterTile();
    }

    private bool IsSpawnTileAvailable(Vector2Int tile)
    {
        if (!mapGenerator.IsTraversable(tile.x, tile.y))
        {
            return false;
        }

        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i].GridPosition == tile)
            {
                return false;
            }
        }

        return true;
    }
}
