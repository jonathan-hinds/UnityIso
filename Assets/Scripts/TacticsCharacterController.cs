using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsCharacterController : MonoBehaviour, ITacticsSelectionHudTarget, ITacticsTurnParticipant
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
    [SerializeField, Range(0.5f, 0.95f)] private float movementSortHandoffNormalizedTime = 0.72f;

    private Coroutine movementRoutine;
    private TacticsMovementDirection currentDirection = TacticsMovementDirection.SouthWest;
    private Vector2 lastAppliedTileAnchorOffset;
    private TacticsCharacterDerivedStats derivedStats;
    private TacticsCharacterRuntimeResources runtimeResources;
    private TacticsTurnManager turnManager;
    private readonly List<TacticsAbilityDefinition> abilities = new();
    private bool isPerformingAction;

    public Vector2Int GridPosition { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsMoving => movementRoutine != null;
    public bool IsPerformingAction => isPerformingAction;
    public TacticsCharacterDefinition CharacterDefinition => characterDefinition;
    public string DisplayName => characterDefinition != null ? characterDefinition.DisplayName : name;
    public TacticsUnitTeam Team => characterDefinition != null ? characterDefinition.Team : TacticsUnitTeam.Player;
    public bool IsPlayerControlled => Team == TacticsUnitTeam.Player;
    public TacticsCharacterDerivedStats DerivedStats => derivedStats;
    public TacticsCharacterRuntimeResources RuntimeResources => runtimeResources;
    public int CurrentHitPoints => runtimeResources.hitPoints;
    public int MaxHitPoints => derivedStats.maxHitPoints;
    public int CurrentStamina => runtimeResources.stamina;
    public int MaxStamina => derivedStats.maxStamina;
    public int CurrentMana => runtimeResources.mana;
    public int MaxMana => derivedStats.maxMana;
    public int BaseMeleeDamageMin => derivedStats.baseMeleeDamageMin;
    public int BaseMeleeDamageMax => derivedStats.baseMeleeDamageMax;
    public int BaseMagicDamageMin => derivedStats.baseMagicDamageMin;
    public int BaseMagicDamageMax => derivedStats.baseMagicDamageMax;
    public float MeleeCriticalHitChance => derivedStats.meleeCriticalHitChance;
    public float MagicCriticalHitChance => derivedStats.magicCriticalHitChance;
    public TacticsCharacterStats BaseStats => characterDefinition != null ? characterDefinition.BaseStats : TacticsCharacterStats.Default();
    public int MoveRange => characterDefinition != null ? characterDefinition.BaseStats.MoveRange : 0;
    public int JumpHeight => characterDefinition != null ? characterDefinition.BaseStats.JumpHeight : 0;
    public int CurrentElevation => mapGenerator != null ? mapGenerator.GetTileElevation(GridPosition.x, GridPosition.y) : 0;
    public bool HasMovedThisTurn { get; private set; }
    public bool HasActedThisTurn { get; private set; }
    public bool IsTurnActive { get; private set; }
    public bool IsAlive => CurrentHitPoints > 0;
    public bool CanReceiveCommands => mapGenerator != null && mapGenerator.HasGeneratedMap && !IsMoving && !IsPerformingAction;
    public bool CanMoveThisTurn => IsTurnActive && !HasMovedThisTurn && CanReceiveCommands;
    public bool CanUseAbilitiesThisTurn => IsTurnActive && !HasActedThisTurn && CanReceiveCommands && IsAlive;
    public bool CanEndTurn => IsTurnActive && !IsMoving && !IsPerformingAction;
    public bool IsTurnEligible => isActiveAndEnabled && IsAlive;
    public Vector3 TurnFocusPoint => transform.position;
    public IReadOnlyList<TacticsAbilityDefinition> Abilities => abilities;

    public event Action<ITacticsTurnParticipant> TurnEnded;
    public event Action<ITacticsTurnParticipant> TurnStateChanged;

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
        RegisterWithTurnManager();
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
        RegisterWithTurnManager();
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
        isPerformingAction = false;
        StopMovementImmediately();
        UnsubscribeFromMap();
        if (turnManager != null)
        {
            turnManager.UnregisterParticipant(this);
        }
    }

    public void SetSelected(bool isSelected)
    {
        IsSelected = isSelected;
        characterAnimator?.SetSelected(isSelected);
    }

    public void SetTargeted(bool isTargeted)
    {
        characterAnimator?.SetTargeted(isTargeted);
    }

    public bool TryMoveTo(Vector2Int destination)
    {
        if (!CanMoveThisTurn || !IsAlive)
        {
            return false;
        }

        if (!TryGetPathTo(destination, out List<Vector2Int> path))
        {
            return false;
        }

        HasMovedThisTurn = true;
        NotifyTurnStateChanged();
        movementRoutine = StartCoroutine(FollowPath(path));
        return true;
    }

    public void BeginTurn()
    {
        if (!IsAlive)
        {
            return;
        }

        StopMovementImmediately();
        IsTurnActive = true;
        characterAnimator?.SetTurnHighlight(true);
        HasMovedThisTurn = false;
        HasActedThisTurn = false;
        NotifyTurnStateChanged();
    }

    public bool TryEndTurn()
    {
        if (!CanEndTurn)
        {
            return false;
        }

        IsTurnActive = false;
        characterAnimator?.SetTurnHighlight(false);
        NotifyTurnStateChanged();
        TurnEnded?.Invoke(this);
        return true;
    }

    public bool TryGetMovementPath(Vector2Int destination, out List<Vector2Int> path)
    {
        return TryGetPathTo(destination, out path, enforceMoveRange: true);
    }

    public TacticsAbilityDefinition GetPrimaryActionAbility()
    {
        return abilities.Count > 0 ? abilities[0] : null;
    }

    public int RollBaseDamage(TacticsAbilityDamageType damageType)
    {
        int minimumDamage = damageType == TacticsAbilityDamageType.Magic
            ? Mathf.Max(0, BaseMagicDamageMin)
            : Mathf.Max(0, BaseMeleeDamageMin);
        int maximumDamage = damageType == TacticsAbilityDamageType.Magic
            ? Mathf.Max(minimumDamage, BaseMagicDamageMax)
            : Mathf.Max(minimumDamage, BaseMeleeDamageMax);
        return UnityEngine.Random.Range(minimumDamage, maximumDamage + 1);
    }

    public bool RollCriticalHit(TacticsAbilityDamageType damageType)
    {
        float criticalHitChance = damageType == TacticsAbilityDamageType.Magic
            ? MagicCriticalHitChance
            : MeleeCriticalHitChance;
        return UnityEngine.Random.value < Mathf.Clamp01(criticalHitChance);
    }

    public int GetPrimaryStat(TacticsAbilityScalingStat stat)
    {
        return BaseStats.GetPrimaryStat(stat);
    }

    public bool ApplyDamage(int damageAmount, Vector3? damageSourceWorldPosition = null, bool isCriticalHit = false)
    {
        if (!IsAlive || damageAmount <= 0)
        {
            return false;
        }

        runtimeResources.hitPoints = Mathf.Max(0, runtimeResources.hitPoints - damageAmount);
        characterAnimator?.PlayDamageImpact(damageSourceWorldPosition);
        TacticsCombatTextSystem.ShowDamage(this, damageAmount, isCriticalHit);
        NotifyTurnStateChanged();

        if (runtimeResources.hitPoints == 0)
        {
            HandleDefeat();
        }

        return true;
    }

    public void CommitAbilityUse()
    {
        if (HasActedThisTurn)
        {
            return;
        }

        HasActedThisTurn = true;
        NotifyTurnStateChanged();
    }

    public IEnumerator PlayAttackAnimationTowards(Vector2Int targetTile)
    {
        currentDirection = GetDirection(GridPosition, targetTile);
        isPerformingAction = true;
        NotifyTurnStateChanged();

        if (characterAnimator != null)
        {
            yield return characterAnimator.PlayAttack(currentDirection);
        }

        isPerformingAction = false;
        characterAnimator?.SetIdle(currentDirection);
        NotifyTurnStateChanged();
    }

    public bool TryGetPathTo(Vector2Int destination, out List<Vector2Int> path, bool enforceMoveRange = true)
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

        if (enforceMoveRange && MoveRange > 0 && (path.Count - 1) > MoveRange)
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
        NotifyTurnStateChanged();
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

        int sortingLayerId = characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default");
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
            ApplySorting(sortingLayerId, ResolveMovementSortingOrder(startSortingOrder, endSortingOrder, t, movementSortHandoffNormalizedTime));
            yield return null;
        }

        transform.position = endPosition;
        ApplySorting(sortingLayerId, endSortingOrder);
    }

    private static int ResolveMovementSortingOrder(
        int startSortingOrder,
        int endSortingOrder,
        float normalizedProgress,
        float handoffNormalizedTime)
    {
        if (startSortingOrder == endSortingOrder)
        {
            return startSortingOrder;
        }

        // Use the front-most of the two valid standing buckets during the transition so
        // neither the tile we're leaving nor the tile we're entering briefly renders on top.
        int transitionSortingOrder = Mathf.Max(startSortingOrder, endSortingOrder);
        return normalizedProgress < handoffNormalizedTime ? transitionSortingOrder : endSortingOrder;
    }

    private void ApplySorting(int sortingLayerId, int sortingOrder)
    {
        if (characterAnimator == null)
        {
            return;
        }

        characterAnimator.SetSorting(sortingLayerId, sortingOrder);
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
        ApplySorting(
            characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default"),
            mapGenerator.GetCharacterSortingOrder(tile.x, tile.y, mapGenerator.GetTileElevation(tile.x, tile.y)));
        characterAnimator?.SetTurnHighlight(IsTurnActive);
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

    public Vector3 GetCombatTextSpawnPosition(float verticalPadding = 0.18f)
    {
        SpriteRenderer targetRenderer = characterAnimator != null ? characterAnimator.TargetRenderer : null;
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            return new Vector3(
                bounds.center.x,
                bounds.max.y + Mathf.Max(verticalPadding, bounds.size.y * 0.12f),
                transform.position.z);
        }

        return transform.position + new Vector3(0f, 0.75f + verticalPadding, 0f);
    }

    public int GetCombatTextSortingLayerId()
    {
        return characterAnimator != null ? characterAnimator.CurrentSortingLayerId : SortingLayer.NameToID("Default");
    }

    public int GetCombatTextSortingOrder()
    {
        return characterAnimator != null ? characterAnimator.CurrentSortingOrder : 0;
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
        derivedStats = definition.BaseStats.CalculateDerivedStats();
        runtimeResources = definition.BaseStats.CreateRuntimeResources();
        RebuildAbilities(definition);
    }

    private void RegisterWithTurnManager()
    {
        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        turnManager?.RegisterParticipant(this);
    }

    private void NotifyTurnStateChanged()
    {
        TurnStateChanged?.Invoke(this);
    }

    private void RebuildAbilities(TacticsCharacterDefinition definition)
    {
        abilities.Clear();

        if (definition != null)
        {
            IReadOnlyList<TacticsAbilityDefinition> configuredAbilities = definition.StartingAbilities;
            for (int i = 0; i < configuredAbilities.Count; i++)
            {
                AddAbilityIfMissing(configuredAbilities[i]);
            }
        }

        TacticsAbilityCatalog catalog = TacticsAbilityCatalogResources.LoadCatalog();
        if (catalog != null)
        {
            AddAbilityIfMissing(catalog.DefaultAttackAbility);
        }
    }

    private void AddAbilityIfMissing(TacticsAbilityDefinition ability)
    {
        if (ability == null || abilities.Contains(ability))
        {
            return;
        }

        abilities.Add(ability);
    }

    private void HandleDefeat()
    {
        StopMovementImmediately();
        isPerformingAction = false;

        IsTurnActive = false;
        HasMovedThisTurn = true;
        HasActedThisTurn = true;
        SetSelected(false);
        SetTargeted(false);
        characterAnimator?.SetTurnHighlight(false);
        NotifyTurnStateChanged();
        gameObject.SetActive(false);
    }

    private void StopMovementImmediately()
    {
        if (movementRoutine == null)
        {
            return;
        }

        StopCoroutine(movementRoutine);
        movementRoutine = null;
        SnapToTile(GridPosition);
        characterAnimator?.SetIdle(currentDirection);
    }
}
