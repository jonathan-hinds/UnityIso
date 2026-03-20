using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsCharacterController : MonoBehaviour, ITacticsSelectionHudTarget
{
    [Header("References")]
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private TacticsCharacterAnimator characterAnimator;
    [SerializeField] private TacticsCharacterDefinition characterDefinition;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.75f;
    [SerializeField, Min(0.01f)] private float jumpDuration = 0.25f;
    [SerializeField, Min(0f)] private float jumpArcHeight = 0.22f;
    [SerializeField, Min(0)] private int maxStepUp = 1;
    [SerializeField, Min(0)] private int maxStepDown = 1;

    [Header("Spawn")]
    [SerializeField] private Vector2Int startingGridPosition;

    [Header("Alignment")]
    [SerializeField] private Vector2 tileAnchorOffset = new Vector2(0.18f, 0.125f);

    private Coroutine movementRoutine;
    private TacticsMovementDirection currentDirection = TacticsMovementDirection.SouthWest;
    private Vector2 lastAppliedTileAnchorOffset;
    private TacticsCharacterDerivedStats derivedStats;
    private TacticsCharacterRuntimeResources runtimeResources;

    public Vector2Int GridPosition { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsMoving => movementRoutine != null;
    public TacticsCharacterDefinition CharacterDefinition => characterDefinition;
    public string DisplayName => characterDefinition != null ? characterDefinition.DisplayName : name;
    public TacticsCharacterDerivedStats DerivedStats => derivedStats;
    public TacticsCharacterRuntimeResources RuntimeResources => runtimeResources;
    public int CurrentHitPoints => runtimeResources.hitPoints;
    public int MaxHitPoints => derivedStats.maxHitPoints;
    public int CurrentStamina => runtimeResources.stamina;
    public int MaxStamina => derivedStats.maxStamina;
    public int CurrentMana => runtimeResources.mana;
    public int MaxMana => derivedStats.maxMana;
    public int BaseDamageMin => derivedStats.baseDamageMin;
    public int BaseDamageMax => derivedStats.baseDamageMax;
    public int MoveRange => characterDefinition != null ? characterDefinition.BaseStats.MoveRange : 0;
    public int JumpHeight => characterDefinition != null ? characterDefinition.BaseStats.JumpHeight : 0;
    public bool CanReceiveCommands => mapGenerator != null && mapGenerator.HasGeneratedMap && !IsMoving;

    public TacticsSelectionHudData BuildSelectionHudData()
    {
        return new TacticsSelectionHudData(
            DisplayName,
            new TacticsSelectionHudResourceData("HP", CurrentHitPoints, MaxHitPoints, new Color(0.72f, 0.23f, 0.27f, 1f)),
            new TacticsSelectionHudResourceData("MP", CurrentMana, MaxMana, new Color(0.25f, 0.49f, 0.77f, 1f)),
            new TacticsSelectionHudResourceData("ST", CurrentStamina, MaxStamina, new Color(0.34f, 0.62f, 0.42f, 1f)),
            characterDefinition != null ? characterDefinition.SelectedColor : Color.white);
    }

    public void Initialize(ProceduralIsometricMapGenerator generator, TacticsCharacterAnimator animator, TacticsCharacterDefinition definition, Vector2Int spawnTile)
    {
        mapGenerator = generator;
        characterAnimator = animator;
        characterDefinition = definition;
        ApplyDefinition(definition);
        startingGridPosition = spawnTile;
        SubscribeToMap();
        SnapToTile(GetBestValidTile(spawnTile));
    }

    private void OnEnable()
    {
        SubscribeToMap();
    }

    private void Start()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        }

        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<TacticsCharacterAnimator>();
        }

        ApplyDefinition(characterDefinition);

        if (mapGenerator == null || !mapGenerator.HasGeneratedMap)
        {
            return;
        }

        SnapToTile(GetBestValidTile(startingGridPosition));
    }

    private void Update()
    {
        if (mapGenerator == null || !mapGenerator.HasGeneratedMap || IsMoving)
        {
            return;
        }

        if (lastAppliedTileAnchorOffset != tileAnchorOffset)
        {
            SnapToTile(GridPosition);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromMap();
    }

    public void SetSelected(bool isSelected)
    {
        IsSelected = isSelected;
        characterAnimator?.SetSelected(isSelected);
    }

    public bool TryMoveTo(Vector2Int destination)
    {
        if (!TryGetMovementPath(destination, out List<Vector2Int> path))
        {
            return false;
        }

        movementRoutine = StartCoroutine(FollowPath(path));
        return true;
    }

    public bool TryGetMovementPath(Vector2Int destination, out List<Vector2Int> path)
    {
        path = null;

        if (mapGenerator == null || !mapGenerator.HasGeneratedMap || IsMoving)
        {
            return false;
        }

        path = IsometricAStarPathfinder.FindPath(
            mapGenerator,
            GridPosition,
            destination,
            maxStepUp,
            maxStepDown,
            IsTileOccupiedByAnotherCharacter);

        if (path == null || path.Count <= 1)
        {
            path = null;
            return false;
        }

        if (MoveRange > 0 && (path.Count - 1) > MoveRange)
        {
            path = null;
            return false;
        }

        return true;
    }

    private IEnumerator FollowPath(IReadOnlyList<Vector2Int> path)
    {
        characterAnimator?.ResetWalkCycle();

        for (int i = 1; i < path.Count; i++)
        {
            yield return MoveBetweenTiles(path[i - 1], path[i]);
            GridPosition = path[i];
        }

        movementRoutine = null;
        characterAnimator?.SetIdle(currentDirection);
    }

    private IEnumerator MoveBetweenTiles(Vector2Int from, Vector2Int to)
    {
        int startElevation = mapGenerator.GetTileElevation(from.x, from.y);
        int endElevation = mapGenerator.GetTileElevation(to.x, to.y);
        Vector3 startPosition = GetTileAnchorWorldPosition(from, startElevation);
        Vector3 endPosition = GetTileAnchorWorldPosition(to, endElevation);

        currentDirection = GetDirection(from, to);
        bool changesElevation = endElevation != startElevation;
        float duration = changesElevation ? jumpDuration : Vector3.Distance(startPosition, endPosition) / moveSpeed;
        duration = Mathf.Max(0.01f, duration);

        int startSortingOrder = mapGenerator.GetCharacterSortingOrder(from.x, from.y, startElevation);
        int endSortingOrder = mapGenerator.GetCharacterSortingOrder(to.x, to.y, endElevation);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            if (changesElevation)
            {
                position.y += Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
                characterAnimator?.SetJump(currentDirection);
            }
            else
            {
                characterAnimator?.SetWalk(currentDirection, Time.deltaTime);
            }

            transform.position = position;
            ApplySorting(Mathf.RoundToInt(Mathf.Lerp(startSortingOrder, endSortingOrder, t)));
            yield return null;
        }

        transform.position = endPosition;
        ApplySorting(endSortingOrder);
    }

    private void ApplySorting(int sortingOrder)
    {
        if (characterAnimator == null || characterAnimator.TargetRenderer == null)
        {
            return;
        }

        characterAnimator.TargetRenderer.sortingOrder = sortingOrder;
    }

    private bool IsTileOccupiedByAnotherCharacter(Vector2Int tile)
    {
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character == this)
            {
                continue;
            }

            if (character.GridPosition == tile)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleMapGenerated()
    {
        SnapToTile(GetBestValidTile(GridPosition == default ? startingGridPosition : GridPosition));
    }

    private void SnapToTile(Vector2Int tile)
    {
        if (mapGenerator == null || !mapGenerator.TryGetTileWorldPosition(tile.x, tile.y, out Vector3 worldPosition))
        {
            return;
        }

        GridPosition = tile;
        transform.position = worldPosition + GetTileAnchorOffset();
        lastAppliedTileAnchorOffset = tileAnchorOffset;
        ApplySorting(mapGenerator.GetCharacterSortingOrder(tile.x, tile.y, mapGenerator.GetTileElevation(tile.x, tile.y)));
        characterAnimator?.SetIdle(currentDirection);
    }

    private Vector2Int GetBestValidTile(Vector2Int requestedTile)
    {
        if (mapGenerator != null && mapGenerator.IsTraversable(requestedTile.x, requestedTile.y))
        {
            return requestedTile;
        }

        Vector2Int center = mapGenerator != null ? mapGenerator.GetCenterTile() : Vector2Int.zero;
        int maxRadius = mapGenerator != null ? Mathf.Max(mapGenerator.Width, mapGenerator.Length) : 0;

        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    if (mapGenerator != null && mapGenerator.IsTraversable(x, y))
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }

        return Vector2Int.zero;
    }

    private TacticsMovementDirection GetDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        if (delta.x > 0)
        {
            return TacticsMovementDirection.NorthEast;
        }

        if (delta.x < 0)
        {
            return TacticsMovementDirection.SouthWest;
        }

        if (delta.y > 0)
        {
            return TacticsMovementDirection.NorthWest;
        }

        return TacticsMovementDirection.SouthEast;
    }

    private void SubscribeToMap()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
            mapGenerator.MapGenerated += HandleMapGenerated;
        }
    }

    private void UnsubscribeFromMap()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
        }
    }

    private Vector3 GetTileAnchorWorldPosition(Vector2Int tile, int elevation)
    {
        return mapGenerator.GridToWorldPosition(tile.x, tile.y, elevation) + GetTileAnchorOffset();
    }

    private Vector3 GetTileAnchorOffset()
    {
        return new Vector3(tileAnchorOffset.x, tileAnchorOffset.y, 0f);
    }

    private void ApplyDefinition(TacticsCharacterDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        moveSpeed = definition.MoveSpeed;
        jumpDuration = definition.JumpDuration;
        jumpArcHeight = definition.JumpArcHeight;
        maxStepUp = definition.MaxStepUp;
        maxStepDown = definition.MaxStepDown;
        tileAnchorOffset = definition.TileAnchorOffset;
        startingGridPosition = definition.PreferredSpawnTile;
        derivedStats = definition.BaseStats.CalculateDerivedStats();
        runtimeResources = definition.BaseStats.CreateRuntimeResources();
    }
}
