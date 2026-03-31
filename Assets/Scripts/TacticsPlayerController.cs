using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TacticsPlayerController : MonoBehaviour
{
    private enum SelectionState
    {
        None = 0,
        CharacterSelected = 1,
        AwaitingMoveTarget = 2,
        AwaitingAbilityTarget = 3,
        AwaitingThrowDestination = 4
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool blockWhenPointerOverUi = true;
    [SerializeField] private TacticsActionMenuView actionMenuView;
    [SerializeField] private TacticsSelectionPanelView activeCharacterPanelView;
    [SerializeField] private TacticsSelectionPanelView selectedCharacterPanelView;
    [SerializeField] private TacticsTurnManager turnManager;
    [SerializeField] private TacticsCombatSystem combatSystem;
    [SerializeField] private TacticsTileTargetOverlay tileTargetOverlay;
    [SerializeField] private TacticsCursorMovementCostView cursorMovementCostView;
    [SerializeField] private TacticsCoopSessionCoordinator coopSessionCoordinator;
    [SerializeField] private TacticsRoundProgressionService roundProgressionService;
    [SerializeField] private TacticsThrowTargetPreview throwTargetPreview;

    private TacticsCharacterController selectedCharacter;
    private SelectionState selectionState;
    private readonly List<TacticsActionMenuAbilityOption> reusableAbilityOptions = new();
    private readonly List<TacticsCharacterController> hoveredAbilityPreviewTargets = new();
    private readonly List<Vector2Int> reusableOverlayTiles = new();
    private readonly List<Vector2Int> reusableThrowDestinationTiles = new();
    private bool? lastCanOpenChest;
    private bool? lastCanDescend;
    private TacticsCharacterController pendingThrowTarget;
    private Vector2Int? hoveredThrowDestination;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (actionMenuView == null)
        {
            actionMenuView = FindFirstObjectByType<TacticsActionMenuView>();
        }

        CacheSelectionPanels();

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        if (combatSystem == null)
        {
            combatSystem = FindFirstObjectByType<TacticsCombatSystem>();
        }

        if (tileTargetOverlay == null)
        {
            tileTargetOverlay = FindFirstObjectByType<TacticsTileTargetOverlay>();
        }

        if (cursorMovementCostView == null)
        {
            cursorMovementCostView = FindFirstObjectByType<TacticsCursorMovementCostView>();
        }

        if (throwTargetPreview == null)
        {
            throwTargetPreview = FindFirstObjectByType<TacticsThrowTargetPreview>();
        }

        if (coopSessionCoordinator == null)
        {
            coopSessionCoordinator = FindFirstObjectByType<TacticsCoopSessionCoordinator>();
        }

        if (roundProgressionService == null)
        {
            roundProgressionService = FindFirstObjectByType<TacticsRoundProgressionService>();
        }

        EnsureCursorMovementCostView();
        EnsureThrowTargetPreview();
    }

    private void OnEnable()
    {
        if (actionMenuView == null)
        {
            actionMenuView = FindFirstObjectByType<TacticsActionMenuView>();
        }

        CacheSelectionPanels();

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        if (combatSystem == null)
        {
            combatSystem = FindFirstObjectByType<TacticsCombatSystem>();
        }

        if (tileTargetOverlay == null)
        {
            tileTargetOverlay = FindFirstObjectByType<TacticsTileTargetOverlay>();
        }

        if (cursorMovementCostView == null)
        {
            cursorMovementCostView = FindFirstObjectByType<TacticsCursorMovementCostView>();
        }

        if (throwTargetPreview == null)
        {
            throwTargetPreview = FindFirstObjectByType<TacticsThrowTargetPreview>();
        }

        if (coopSessionCoordinator == null)
        {
            coopSessionCoordinator = FindFirstObjectByType<TacticsCoopSessionCoordinator>();
        }

        if (roundProgressionService == null)
        {
            roundProgressionService = FindFirstObjectByType<TacticsRoundProgressionService>();
        }

        EnsureCursorMovementCostView();
        EnsureThrowTargetPreview();

        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
            actionMenuView.ActionSelected += HandleActionSelected;
            actionMenuView.AbilitySelected -= HandleAbilitySelected;
            actionMenuView.AbilitySelected += HandleAbilitySelected;
        }

        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.ActiveParticipantChanged += HandleActiveParticipantChanged;
            turnManager.TurnStateChanged -= HandleTurnStateChanged;
            turnManager.TurnStateChanged += HandleTurnStateChanged;
            HandleActiveParticipantChanged(turnManager.ActiveParticipant);
        }

        if (combatSystem != null)
        {
            combatSystem.StateChanged -= HandleCombatStateChanged;
            combatSystem.StateChanged += HandleCombatStateChanged;
        }
    }

    private void OnDisable()
    {
        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
            actionMenuView.AbilitySelected -= HandleAbilitySelected;
        }

        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.TurnStateChanged -= HandleTurnStateChanged;
        }

        if (combatSystem != null)
        {
            combatSystem.StateChanged -= HandleCombatStateChanged;
        }

        SubscribeToSelectedCharacter(null);
        SetHoveredAbilityTargets(null);
        cursorMovementCostView?.Hide();
        throwTargetPreview?.Hide();
        RefreshTargetIndicators();
    }

    private void Update()
    {
        if (turnManager != null && turnManager.IsTransitioningTurns)
        {
            ClearPendingThrowTarget();
            SetHoveredAbilityTargets(null);
            tileTargetOverlay?.Hide();
            cursorMovementCostView?.Hide();
            return;
        }

        RefreshChestActionAvailabilityIfNeeded();
        HandleCancelInput();
        RefreshHoveredAbilityTarget();
        RefreshHoveredMovementPath();

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (selectionState == SelectionState.AwaitingThrowDestination)
        {
            RefreshThrowDestinationPreview();
            HandleThrowDestinationInput(mouse);
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        if (!IsFinite(screenPosition))
        {
            return;
        }

        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D[] hits = Physics2D.OverlapPointAll(point);

        if (TryGetClickedStatusEffectIcon(hits, out TacticsStatusEffectTrayHitTarget statusEffectHitTarget))
        {
            TacticsStatusEffectTrayView.ToggleFor(statusEffectHitTarget.Character, targetCamera);
            return;
        }

        TacticsStatusEffectTrayView.HideTray();

        if (selectionState == SelectionState.AwaitingAbilityTarget &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()))
        {
            if (TryGetClickedCharacter(hits, out TacticsCharacterController targetedCharacter) &&
                targetedCharacter != null &&
                combatSystem != null &&
                combatSystem.TargetingAbility != null &&
                combatSystem.CanTargetFromTile(selectedCharacter, selectedCharacter.GridPosition, combatSystem.TargetingAbility, targetedCharacter))
            {
                if (combatSystem.TargetingAbility.AppliesThrowing)
                {
                    if (BeginThrowDestinationSelection(targetedCharacter))
                    {
                        RefreshHud();
                    }
                }
                else if (RequestUseAbility(selectedCharacter, combatSystem.TargetingAbility, targetedCharacter.GridPosition))
                {
                    selectionState = SelectionState.CharacterSelected;
                    RefreshHud();
                }

                return;
            }

            if (TryGetClickedTile(hits, out IsometricTileHoverInfo targetedTile))
            {
                if (combatSystem != null &&
                    combatSystem.TargetingAbility != null &&
                    RequestUseAbility(selectedCharacter, combatSystem.TargetingAbility, new Vector2Int(targetedTile.GridX, targetedTile.GridY)))
                {
                    selectionState = SelectionState.CharacterSelected;
                    RefreshHud();
                }

                return;
            }
        }

        if (TryGetClickedCharacter(hits, out TacticsCharacterController clickedCharacter))
        {
            SelectCharacter(clickedCharacter, GetSelectionStateForCharacter(clickedCharacter));

            return;
        }

        if (selectionState == SelectionState.AwaitingMoveTarget &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()) &&
            TryGetClickedTile(hits, out IsometricTileHoverInfo clickedTile))
        {
            if (RequestMove(selectedCharacter, new Vector2Int(clickedTile.GridX, clickedTile.GridY)))
            {
                selectionState = SelectionState.CharacterSelected;
                RefreshHud();
            }

            return;
        }

        SelectCharacter(null, SelectionState.None);
    }

    private void SelectCharacter(TacticsCharacterController character, SelectionState nextState)
    {
        if (character != null && nextState != SelectionState.None && !CanIssueCommandsTo(character))
        {
            nextState = SelectionState.None;
        }

        if (selectedCharacter == character && selectionState == nextState)
        {
            RefreshHud();
            return;
        }

        if (selectionState == SelectionState.AwaitingAbilityTarget ||
            selectionState == SelectionState.AwaitingThrowDestination)
        {
            combatSystem?.CancelTargeting();
        }

        ClearPendingThrowTarget();
        SetHoveredAbilityTargets(null);
        SubscribeToSelectedCharacter(null);

        if (selectedCharacter != null)
        {
            selectedCharacter.SetSelected(false);
        }

        selectedCharacter = character;

        if (selectedCharacter != null)
        {
            selectedCharacter.SetSelected(true);
            selectionState = nextState;
            SubscribeToSelectedCharacter(selectedCharacter);
        }
        else
        {
            selectionState = SelectionState.None;
        }

        RefreshHud();
    }

    private bool TryGetClickedCharacter(Collider2D[] hits, out TacticsCharacterController character)
    {
        character = null;
        int bestSortingOrder = int.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            TacticsCharacterController candidate = hits[i].GetComponentInParent<TacticsCharacterController>();
            if (candidate == null)
            {
                continue;
            }

            Renderer renderer = hits[i].GetComponent<Renderer>();
            int sortingOrder = renderer != null ? renderer.sortingOrder : 0;
            if (sortingOrder < bestSortingOrder)
            {
                continue;
            }

            bestSortingOrder = sortingOrder;
            character = candidate;
        }

        return character != null;
    }

    private bool TryGetClickedTile(Collider2D[] hits, out IsometricTileHoverInfo tile)
    {
        tile = null;
        int bestSortingOrder = int.MinValue;

        for (int i = 0; i < hits.Length; i++)
        {
            IsometricTileHoverInfo candidate = hits[i].GetComponent<IsometricTileHoverInfo>();
            if (candidate == null)
            {
                continue;
            }

            SpriteRenderer renderer = hits[i].GetComponent<SpriteRenderer>();
            int sortingOrder = renderer != null ? renderer.sortingOrder : 0;
            if (sortingOrder < bestSortingOrder)
            {
                continue;
            }

            bestSortingOrder = sortingOrder;
            tile = candidate;
        }

        return tile != null;
    }

    private bool TryGetClickedStatusEffectIcon(Collider2D[] hits, out TacticsStatusEffectTrayHitTarget hitTarget)
    {
        hitTarget = null;

        for (int i = 0; i < hits.Length; i++)
        {
            TacticsStatusEffectTrayHitTarget candidate = hits[i].GetComponentInParent<TacticsStatusEffectTrayHitTarget>();
            if (candidate == null || candidate.Character == null)
            {
                continue;
            }

            hitTarget = candidate;
            return true;
        }

        return false;
    }

    public void AssignHud(TacticsActionMenuView view)
    {
        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
            actionMenuView.AbilitySelected -= HandleAbilitySelected;
        }

        actionMenuView = view;

        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
            actionMenuView.ActionSelected += HandleActionSelected;
            actionMenuView.AbilitySelected -= HandleAbilitySelected;
            actionMenuView.AbilitySelected += HandleAbilitySelected;
        }

        RefreshHud();
    }

    public void AssignActiveCharacterHud(TacticsSelectionPanelView view)
    {
        activeCharacterPanelView = view;
        RefreshHud();
    }

    public void AssignSelectedCharacterHud(TacticsSelectionPanelView view)
    {
        selectedCharacterPanelView = view;
        RefreshHud();
    }

    public void AssignTurnManager(TacticsTurnManager manager)
    {
        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.TurnStateChanged -= HandleTurnStateChanged;
        }

        turnManager = manager;

        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.ActiveParticipantChanged += HandleActiveParticipantChanged;
            turnManager.TurnStateChanged -= HandleTurnStateChanged;
            turnManager.TurnStateChanged += HandleTurnStateChanged;
            HandleActiveParticipantChanged(turnManager.ActiveParticipant);
        }
        else
        {
            SelectCharacter(null, SelectionState.None);
        }
    }

    public void AssignCombatSystem(TacticsCombatSystem system)
    {
        if (combatSystem != null)
        {
            combatSystem.StateChanged -= HandleCombatStateChanged;
        }

        combatSystem = system;

        if (combatSystem != null)
        {
            combatSystem.StateChanged -= HandleCombatStateChanged;
            combatSystem.StateChanged += HandleCombatStateChanged;
        }

        RefreshHud();
    }

    public void AssignTileTargetOverlay(TacticsTileTargetOverlay overlay)
    {
        tileTargetOverlay = overlay;
        RefreshHud();
    }

    private void HandleActionSelected(TacticsHudActionType actionType)
    {
        TacticsCharacterController activePlayerCharacter = GetActiveOwnedPlayerCharacter();
        if (activePlayerCharacter == null)
        {
            return;
        }

        switch (actionType)
        {
            case TacticsHudActionType.Move:
                if (activePlayerCharacter.CanMoveThisTurn)
                {
                    if (!ReferenceEquals(selectedCharacter, activePlayerCharacter))
                    {
                        SelectCharacter(activePlayerCharacter, SelectionState.CharacterSelected);
                    }

                    combatSystem?.CancelTargeting();
                    selectionState = SelectionState.AwaitingMoveTarget;
                    RefreshHud();
                }

                break;
            case TacticsHudActionType.OpenChest:
                combatSystem?.CancelTargeting();
                if (RequestOpenAdjacentChest(activePlayerCharacter))
                {
                    selectionState = SelectionState.CharacterSelected;
                    RefreshHud();
                }

                break;
            case TacticsHudActionType.Descend:
                combatSystem?.CancelTargeting();
                if (RequestDescend(activePlayerCharacter))
                {
                    selectionState = SelectionState.CharacterSelected;
                    RefreshHud();
                }

                break;
            case TacticsHudActionType.Attack:
                break;
            case TacticsHudActionType.EndTurn:
                combatSystem?.CancelTargeting();
                RequestEndTurn(activePlayerCharacter);
                break;
        }
    }

    private void HandleAbilitySelected(TacticsAbilityDefinition ability)
    {
        TacticsCharacterController activePlayerCharacter = GetActiveOwnedPlayerCharacter();
        if (activePlayerCharacter == null || ability == null || combatSystem == null)
        {
            return;
        }

        if (!ReferenceEquals(selectedCharacter, activePlayerCharacter))
        {
            SelectCharacter(activePlayerCharacter, SelectionState.CharacterSelected);
        }

        if (!ability.RequiresTargetSelection)
        {
            if (RequestUseAbility(activePlayerCharacter, ability, activePlayerCharacter.GridPosition))
            {
                selectionState = SelectionState.CharacterSelected;
                ClearPendingThrowTarget();
            }
        }
        else if (combatSystem.BeginTargeting(activePlayerCharacter, ability))
        {
            selectionState = SelectionState.AwaitingAbilityTarget;
            ClearPendingThrowTarget();
        }

        RefreshHud();
    }

    private void HandleCancelInput()
    {
        bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool rightClickPressed = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        if (!escapePressed && !rightClickPressed)
        {
            return;
        }

        if (selectionState == SelectionState.AwaitingMoveTarget)
        {
            selectionState = SelectionState.CharacterSelected;
            ClearPendingThrowTarget();
            SetHoveredAbilityTargets(null);
            TacticsStatusEffectTrayView.HideTray();
            RefreshHud();
        }
        else if (selectionState == SelectionState.AwaitingAbilityTarget ||
                 selectionState == SelectionState.AwaitingThrowDestination)
        {
            combatSystem?.CancelTargeting();
            selectionState = SelectionState.CharacterSelected;
            ClearPendingThrowTarget();
            SetHoveredAbilityTargets(null);
            TacticsStatusEffectTrayView.HideTray();
            RefreshHud();
        }
    }

    private void RefreshHud()
    {
        TacticsCharacterController activeCharacter = turnManager != null ? turnManager.ActiveCharacter : null;
        TacticsCharacterController activePlayerCharacter = GetActiveOwnedPlayerCharacter();
        if (activeCharacter != null)
        {
            activeCharacterPanelView?.Show(BuildSelectionHudData(activeCharacter));
        }
        else
        {
            activeCharacterPanelView?.Hide();
        }

        if (selectedCharacter != null && selectedCharacter.isActiveAndEnabled && selectedCharacter.IsAlive)
        {
            selectedCharacterPanelView?.Show(BuildSelectionHudData(selectedCharacter));
        }
        else
        {
            selectedCharacterPanelView?.Hide();
        }

        bool showActionMenu =
            activePlayerCharacter != null &&
            activePlayerCharacter.IsTurnActive;

        if (actionMenuView != null)
        {
            if (showActionMenu)
            {
                BuildAbilityOptions(activePlayerCharacter, reusableAbilityOptions);
                bool canOpenChest = FindAdjacentOpenableChest(activePlayerCharacter) != null;
                bool canDescend = roundProgressionService != null && roundProgressionService.CanDescend(activePlayerCharacter);
                lastCanOpenChest = canOpenChest;
                lastCanDescend = canDescend;

                actionMenuView.ShowForCharacter(
                    activePlayerCharacter,
                    reusableAbilityOptions,
                    selectionState == SelectionState.AwaitingMoveTarget,
                    selectionState == SelectionState.AwaitingAbilityTarget ||
                    selectionState == SelectionState.AwaitingThrowDestination,
                    canOpenChest,
                    canDescend,
                    turnManager != null ? turnManager.RoundNumber : 1,
                    turnManager != null ? turnManager.TurnNumber : 1,
                    turnManager != null ? turnManager.ParticipantCount : 1);
            }
            else
            {
                lastCanOpenChest = null;
                lastCanDescend = null;
                actionMenuView.Hide();
            }
        }

        RefreshTargetOverlay();
    }

    private TacticsSelectionHudData BuildSelectionHudData(TacticsCharacterController character)
    {
        TacticsSelectionHudData hudData = character.BuildSelectionHudData();
        if (coopSessionCoordinator != null &&
            coopSessionCoordinator.TryGetOwningUsername(character, out string username))
        {
            hudData = hudData.WithOwnerDisplayName(username);
        }

        return hudData;
    }

    private void HandleActiveParticipantChanged(ITacticsTurnParticipant participant)
    {
        TacticsCharacterController activeCharacter = participant as TacticsCharacterController;
        if (selectionState == SelectionState.AwaitingAbilityTarget)
        {
            combatSystem?.CancelTargeting();
        }

        if (selectionState == SelectionState.AwaitingMoveTarget ||
            selectionState == SelectionState.AwaitingAbilityTarget ||
            selectionState == SelectionState.AwaitingThrowDestination)
        {
            selectionState = SelectionState.CharacterSelected;
            ClearPendingThrowTarget();
        }

        bool shouldSelectActiveCharacter =
            selectedCharacter == null ||
            !selectedCharacter.isActiveAndEnabled ||
            !selectedCharacter.IsAlive;

        if (shouldSelectActiveCharacter)
        {
            SelectCharacter(activeCharacter, GetSelectionStateForCharacter(activeCharacter));
            return;
        }

        RefreshHud();
    }

    private void HandleTurnStateChanged()
    {
        RefreshHud();
    }

    private void HandleCombatStateChanged()
    {
        if (combatSystem == null || combatSystem.State != TacticsCombatState.TargetingAbility)
        {
            if (selectionState == SelectionState.AwaitingAbilityTarget ||
                selectionState == SelectionState.AwaitingThrowDestination)
            {
                selectionState = SelectionState.CharacterSelected;
                ClearPendingThrowTarget();
            }

            SetHoveredAbilityTargets(null);
        }

        RefreshHud();
    }

    private void HandleSelectedCharacterStateChanged(ITacticsTurnParticipant participant)
    {
        RefreshHud();
    }

    private void HandleSelectedCharacterInventoryChanged(TacticsCharacterController character)
    {
        RefreshHud();
    }

    private void SubscribeToSelectedCharacter(TacticsCharacterController character)
    {
        if (selectedCharacter != null)
        {
            selectedCharacter.TurnStateChanged -= HandleSelectedCharacterStateChanged;
            selectedCharacter.InventoryChanged -= HandleSelectedCharacterInventoryChanged;
        }

        if (character != null)
        {
            character.TurnStateChanged -= HandleSelectedCharacterStateChanged;
            character.TurnStateChanged += HandleSelectedCharacterStateChanged;
            character.InventoryChanged -= HandleSelectedCharacterInventoryChanged;
            character.InventoryChanged += HandleSelectedCharacterInventoryChanged;
        }
    }

    private TacticsCharacterController GetActivePlayerCharacter()
    {
        TacticsCharacterController activeCharacter = turnManager != null ? turnManager.ActiveCharacter : null;
        return activeCharacter != null && activeCharacter.IsPlayerControlled ? activeCharacter : null;
    }

    private TacticsCharacterController GetActiveOwnedPlayerCharacter()
    {
        TacticsCharacterController activeCharacter = GetActivePlayerCharacter();
        return CanIssueCommandsTo(activeCharacter) ? activeCharacter : null;
    }

    private SelectionState GetSelectionStateForCharacter(TacticsCharacterController character)
    {
        return ReferenceEquals(character, GetActiveOwnedPlayerCharacter())
            ? SelectionState.CharacterSelected
            : SelectionState.None;
    }

    private bool CanIssueCommandsTo(TacticsCharacterController character)
    {
        if (character == null || !character.IsPlayerControlled)
        {
            return false;
        }

        return coopSessionCoordinator == null || coopSessionCoordinator.CanLocalPlayerControlCharacter(character);
    }

    private void CacheSelectionPanels()
    {
        TacticsSelectionPanelView[] panelViews = FindObjectsByType<TacticsSelectionPanelView>(FindObjectsSortMode.None);
        for (int i = 0; i < panelViews.Length; i++)
        {
            TacticsSelectionPanelView panelView = panelViews[i];
            if (panelView == null)
            {
                continue;
            }

            if (panelView.PanelRole == TacticsSelectionPanelRole.ActiveCharacter && activeCharacterPanelView == null)
            {
                activeCharacterPanelView = panelView;
            }
            else if (panelView.PanelRole == TacticsSelectionPanelRole.SelectedCharacter && selectedCharacterPanelView == null)
            {
                selectedCharacterPanelView = panelView;
            }
        }
    }

    private void EnsureCursorMovementCostView()
    {
        if (cursorMovementCostView != null)
        {
            return;
        }

        GameObject cursorCostObject = new GameObject("Cursor Movement Cost View");
        Canvas canvas = cursorCostObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5050;

        CanvasScaler scaler = cursorCostObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = cursorCostObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;
        cursorMovementCostView = cursorCostObject.AddComponent<TacticsCursorMovementCostView>();
    }

    private void EnsureThrowTargetPreview()
    {
        if (throwTargetPreview != null)
        {
            return;
        }

        GameObject previewObject = new GameObject("Throw Target Preview");
        throwTargetPreview = previewObject.AddComponent<TacticsThrowTargetPreview>();
    }

    private void RefreshTargetOverlay()
    {
        if (tileTargetOverlay == null)
        {
            RefreshTargetIndicators();
            return;
        }

        if (selectionState == SelectionState.AwaitingAbilityTarget &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()) &&
            combatSystem != null &&
            combatSystem.TargetingAbility != null)
        {
            tileTargetOverlay.ShowTiles(BuildOverlayTiles(selectedCharacter, combatSystem.TargetingAbility));
            RefreshTargetIndicators();
            return;
        }

        if (selectionState == SelectionState.AwaitingThrowDestination &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()) &&
            pendingThrowTarget != null)
        {
            tileTargetOverlay.ShowTiles(reusableThrowDestinationTiles);
            RefreshTargetIndicators();
            return;
        }

        tileTargetOverlay.Hide();
        RefreshTargetIndicators();
    }

    private void RefreshTargetIndicators()
    {
        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        if (characters.Length == 0)
        {
            return;
        }

        HashSet<Vector2Int> validTargetTiles = null;
        if ((selectionState == SelectionState.AwaitingAbilityTarget ||
             selectionState == SelectionState.AwaitingThrowDestination) &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()) &&
            combatSystem != null &&
            combatSystem.TargetingAbility != null)
        {
            IReadOnlyList<Vector2Int> tiles = selectionState == SelectionState.AwaitingThrowDestination
                ? reusableThrowDestinationTiles
                : combatSystem.GetValidTargetTiles(selectedCharacter, combatSystem.TargetingAbility);
            validTargetTiles = new HashSet<Vector2Int>(tiles);
        }

        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character == null)
            {
                continue;
            }

            bool showOwnedIndicator = coopSessionCoordinator != null &&
                                      coopSessionCoordinator.ShouldShowLocalOwnershipIndicator(character);
            bool isTargeted = validTargetTiles != null && validTargetTiles.Contains(character.GridPosition);
            character.SetLocallyOwned(showOwnedIndicator);
            character.SetTargeted(isTargeted);
        }
    }

    private void RefreshHoveredAbilityTarget()
    {
        if (selectionState != SelectionState.AwaitingAbilityTarget ||
            !ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()) ||
            combatSystem == null ||
            combatSystem.TargetingAbility == null)
        {
            if (selectionState == SelectionState.AwaitingThrowDestination && pendingThrowTarget != null)
            {
                SetHoveredAbilityTargets(new[] { pendingThrowTarget });
                return;
            }

            SetHoveredAbilityTargets(null);
            return;
        }

        if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetHoveredAbilityTargets(null);
            return;
        }

        if (!TryGetPointerHits(out Collider2D[] hits))
        {
            SetHoveredAbilityTargets(null);
            return;
        }

        TacticsAbilityDefinition targetingAbility = combatSystem.TargetingAbility;
        if (targetingAbility.UsesAreaOfEffect)
        {
            if (!TryGetClickedTile(hits, out IsometricTileHoverInfo hoveredTile) || hoveredTile == null)
            {
                SetHoveredAbilityTargets(null);
                return;
            }

            IReadOnlyList<TacticsCharacterController> previewTargets = combatSystem.GetPreviewTargets(
                selectedCharacter,
                targetingAbility,
                new Vector2Int(hoveredTile.GridX, hoveredTile.GridY));
            SetHoveredAbilityTargets(previewTargets);
            return;
        }

        if (!TryGetClickedCharacter(hits, out TacticsCharacterController hoveredCharacter) ||
            hoveredCharacter == null ||
            !combatSystem.CanTargetFromTile(selectedCharacter, selectedCharacter.GridPosition, targetingAbility, hoveredCharacter))
        {
            SetHoveredAbilityTargets(null);
            return;
        }

        SetHoveredAbilityTargets(new[] { hoveredCharacter });
    }

    private void RefreshHoveredMovementPath()
    {
        if (tileTargetOverlay == null)
        {
            cursorMovementCostView?.Hide();
            return;
        }

        if (selectionState != SelectionState.AwaitingMoveTarget ||
            !ReferenceEquals(selectedCharacter, GetActiveOwnedPlayerCharacter()))
        {
            cursorMovementCostView?.Hide();
            return;
        }

        if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            tileTargetOverlay.Hide();
            cursorMovementCostView?.Hide();
            return;
        }

        if (!TryGetPointerHits(out Collider2D[] hits) ||
            !TryGetClickedTile(hits, out IsometricTileHoverInfo hoveredTile) ||
            hoveredTile == null)
        {
            tileTargetOverlay.Hide();
            cursorMovementCostView?.Hide();
            return;
        }

        if (selectedCharacter.TryBuildMovementPreview(
                new Vector2Int(hoveredTile.GridX, hoveredTile.GridY),
                reusableOverlayTiles,
                out int movementCost))
        {
            tileTargetOverlay.ShowTiles(reusableOverlayTiles);
            ShowMovementCostLabel(movementCost, selectedCharacter.MoveRange);
            return;
        }

        tileTargetOverlay.Hide();
        cursorMovementCostView?.Hide();
    }

    private void RefreshThrowDestinationPreview()
    {
        hoveredThrowDestination = null;

        if (selectionState != SelectionState.AwaitingThrowDestination ||
            pendingThrowTarget == null)
        {
            throwTargetPreview?.Hide();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            throwTargetPreview?.Hide();
            return;
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        if (!IsFinite(screenPosition))
        {
            throwTargetPreview?.Hide();
            return;
        }

        bool isValidThrowDestination = false;
        if (TryGetPointerHits(out Collider2D[] hits) &&
            TryGetClickedTile(hits, out IsometricTileHoverInfo hoveredTile) &&
            hoveredTile != null)
        {
            Vector2Int hoveredTilePosition = new Vector2Int(hoveredTile.GridX, hoveredTile.GridY);
            if (reusableThrowDestinationTiles.Contains(hoveredTilePosition))
            {
                hoveredThrowDestination = hoveredTilePosition;
                isValidThrowDestination = true;
            }
        }

        throwTargetPreview?.Show(pendingThrowTarget, screenPosition, isValidThrowDestination);
    }

    private void HandleThrowDestinationInput(Mouse mouse)
    {
        if (mouse == null ||
            selectionState != SelectionState.AwaitingThrowDestination ||
            selectedCharacter == null ||
            pendingThrowTarget == null ||
            combatSystem == null ||
            combatSystem.TargetingAbility == null)
        {
            return;
        }

        if (!mouse.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (!hoveredThrowDestination.HasValue)
        {
            return;
        }

        Vector2Int throwDestination = hoveredThrowDestination.Value;

        if (RequestUseAbility(selectedCharacter, combatSystem.TargetingAbility, pendingThrowTarget.GridPosition, throwDestination))
        {
            selectionState = SelectionState.CharacterSelected;
            ClearPendingThrowTarget();
            RefreshHud();
        }
    }

    private void ShowMovementCostLabel(int movementCost, int moveRange)
    {
        if (cursorMovementCostView == null || Mouse.current == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (!IsFinite(screenPosition))
        {
            cursorMovementCostView.Hide();
            return;
        }

        cursorMovementCostView.Show($"{movementCost}/{moveRange}", screenPosition);
    }

    private bool TryGetPointerHits(out Collider2D[] hits)
    {
        hits = null;

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return false;
            }
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        if (!IsFinite(screenPosition))
        {
            return false;
        }

        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        hits = Physics2D.OverlapPointAll(point);
        return hits != null && hits.Length > 0;
    }

    private void SetHoveredAbilityTargets(IReadOnlyList<TacticsCharacterController> characters)
    {
        bool matchesExistingTargets = characters != null && characters.Count == hoveredAbilityPreviewTargets.Count;
        if (matchesExistingTargets)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (!ReferenceEquals(hoveredAbilityPreviewTargets[i], characters[i]))
                {
                    matchesExistingTargets = false;
                    break;
                }
            }
        }

        if (matchesExistingTargets || (characters == null && hoveredAbilityPreviewTargets.Count == 0))
        {
            return;
        }

        for (int i = 0; i < hoveredAbilityPreviewTargets.Count; i++)
        {
            TacticsCharacterController previousTarget = hoveredAbilityPreviewTargets[i];
            if (previousTarget != null)
            {
                previousTarget.SetTargetHoverPreview(false);
            }
        }

        hoveredAbilityPreviewTargets.Clear();

        if (characters == null)
        {
            return;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character == null)
            {
                continue;
            }

            hoveredAbilityPreviewTargets.Add(character);
            character.SetTargetHoverPreview(true);
        }
    }

    private IReadOnlyList<Vector2Int> BuildOverlayTiles(
        TacticsCharacterController source,
        TacticsAbilityDefinition ability)
    {
        reusableOverlayTiles.Clear();

        if (source == null || ability == null || combatSystem == null)
        {
            return reusableOverlayTiles;
        }

        if (selectionState == SelectionState.AwaitingThrowDestination &&
            pendingThrowTarget != null)
        {
            AddUniqueTiles(reusableOverlayTiles, reusableThrowDestinationTiles);
            return reusableOverlayTiles;
        }

        AddUniqueTiles(reusableOverlayTiles, combatSystem.GetTargetableTiles(source, ability));

        if (ability.RangeType == TacticsAbilityRangeType.SurroundingAoE)
        {
            AddUniqueTiles(reusableOverlayTiles, combatSystem.GetAreaTiles(source, ability, source.GridPosition));
        }

        return reusableOverlayTiles;
    }

    private bool BeginThrowDestinationSelection(TacticsCharacterController target)
    {
        if (selectedCharacter == null ||
            target == null ||
            combatSystem == null ||
            combatSystem.TargetingAbility == null)
        {
            return false;
        }

        IReadOnlyList<Vector2Int> destinations = combatSystem.GetValidThrowDestinations(
            selectedCharacter,
            target,
            combatSystem.TargetingAbility,
            reusableThrowDestinationTiles);
        if (destinations.Count == 0)
        {
            return false;
        }

        pendingThrowTarget = target;
        hoveredThrowDestination = null;
        selectionState = SelectionState.AwaitingThrowDestination;
        SetHoveredAbilityTargets(new[] { pendingThrowTarget });
        return true;
    }

    private void ClearPendingThrowTarget()
    {
        pendingThrowTarget = null;
        hoveredThrowDestination = null;
        reusableThrowDestinationTiles.Clear();
        throwTargetPreview?.Hide();
    }

    private static void AddUniqueTiles(List<Vector2Int> destination, IReadOnlyList<Vector2Int> tiles)
    {
        if (tiles == null)
        {
            return;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            Vector2Int tile = tiles[i];
            if (!destination.Contains(tile))
            {
                destination.Add(tile);
            }
        }
    }

    private void BuildAbilityOptions(
        TacticsCharacterController character,
        List<TacticsActionMenuAbilityOption> abilityOptions)
    {
        abilityOptions.Clear();

        if (character == null)
        {
            return;
        }

        bool isTargetingWithCharacter = combatSystem != null &&
                                        ReferenceEquals(combatSystem.TargetingCharacter, character);

        IReadOnlyList<TacticsAbilityDefinition> abilities = character.Abilities;
        for (int i = 0; i < abilities.Count; i++)
        {
            TacticsAbilityDefinition ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            bool hasResources = character.HasResourcesForAbility(ability);
            bool canPayCost = character.CanPayAbilityCost(ability);
            bool hasTargets = combatSystem != null &&
                              character.CanUseAbilitiesThisTurn &&
                              canPayCost &&
                              combatSystem.GetValidTargetTiles(character, ability).Count > 0;
            bool isInteractable = combatSystem != null &&
                                  character.CanUseAbilitiesThisTurn &&
                                  canPayCost &&
                                  hasTargets;
            bool isSelected = isTargetingWithCharacter &&
                              ReferenceEquals(combatSystem.TargetingAbility, ability);
            string statusText = BuildAbilityStatusText(character, ability, hasResources, canPayCost, hasTargets);

            abilityOptions.Add(new TacticsActionMenuAbilityOption(ability, isInteractable, isSelected, statusText));
        }
    }

    private static string BuildAbilityStatusText(
        TacticsCharacterController character,
        TacticsAbilityDefinition ability,
        bool hasResources,
        bool canPayCost,
        bool hasTargets)
    {
        if (character == null || ability == null)
        {
            return "Unavailable";
        }

        if (!character.CanUseAbilitiesThisTurn)
        {
            if (character.IsActionLockedThisTurn)
            {
                return "Stunned";
            }

            return character.IsTurnActive ? "Action spent" : "Not your turn";
        }

        if (!canPayCost)
        {
            return ability.CostResourceType switch
            {
                TacticsAbilityResourceType.Stamina => "Not enough stamina",
                TacticsAbilityResourceType.Mana => "Not enough mana",
                TacticsAbilityResourceType.Movement => "Move already spent",
                _ => "Not enough resources"
            };
        }

        if (ability.HasMovementCost)
        {
            return hasTargets ? "Costs movement" : "No targets";
        }

        if (!hasResources && ability.AllowsMovementAsAlternateCost)
        {
            return hasTargets ? "Use movement instead" : "No targets";
        }

        return hasTargets ? "Ready" : "No targets";
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.x) &&
               !float.IsInfinity(value.y);
    }

    private void RefreshChestActionAvailabilityIfNeeded()
    {
        TacticsCharacterController activePlayerCharacter = GetActiveOwnedPlayerCharacter();
        if (activePlayerCharacter == null || !activePlayerCharacter.IsTurnActive)
        {
            lastCanOpenChest = null;
            lastCanDescend = null;
            return;
        }

        bool canOpenChest = FindAdjacentOpenableChest(activePlayerCharacter) != null;
        bool canDescend = roundProgressionService != null && roundProgressionService.CanDescend(activePlayerCharacter);
        if ((!lastCanOpenChest.HasValue || lastCanOpenChest.Value == canOpenChest) &&
            (!lastCanDescend.HasValue || lastCanDescend.Value == canDescend))
        {
            return;
        }

        RefreshHud();
    }

    private bool RequestMove(TacticsCharacterController character, Vector2Int destination)
    {
        if (character == null)
        {
            return false;
        }

        return coopSessionCoordinator != null
            ? coopSessionCoordinator.RequestMove(character, destination)
            : character.TryMoveTo(destination);
    }

    private bool RequestUseAbility(TacticsCharacterController character, TacticsAbilityDefinition ability, Vector2Int targetTile)
    {
        return RequestUseAbility(character, ability, targetTile, null);
    }

    private bool RequestUseAbility(
        TacticsCharacterController character,
        TacticsAbilityDefinition ability,
        Vector2Int targetTile,
        Vector2Int? throwDestination)
    {
        if (character == null || ability == null)
        {
            return false;
        }

        if (coopSessionCoordinator != null)
        {
            return coopSessionCoordinator.RequestUseAbility(character, ability, targetTile, throwDestination);
        }

        return combatSystem != null && combatSystem.TryUseAbility(character, ability, targetTile, throwDestination);
    }

    private bool RequestEndTurn(TacticsCharacterController character)
    {
        if (character == null)
        {
            return false;
        }

        return coopSessionCoordinator != null
            ? coopSessionCoordinator.RequestEndTurn(character)
            : turnManager != null && turnManager.TryEndActiveTurn();
    }

    private bool RequestOpenAdjacentChest(TacticsCharacterController character)
    {
        if (character == null)
        {
            return false;
        }

        TacticsChestController chest = FindAdjacentOpenableChest(character);
        if (chest == null)
        {
            return false;
        }

        return coopSessionCoordinator != null
            ? coopSessionCoordinator.RequestOpenChest(character, chest)
            : false;
    }

    private bool RequestDescend(TacticsCharacterController character)
    {
        return roundProgressionService != null && roundProgressionService.RequestDescend(character);
    }

    private static TacticsChestController FindAdjacentOpenableChest(TacticsCharacterController character)
    {
        return TacticsChestController.FindBestAdjacentClosedChest(character);
    }
}

[DisallowMultipleComponent]
public sealed class TacticsThrowTargetPreview : MonoBehaviour
{
    [SerializeField] private Color previewColor = new Color(1f, 0.12f, 0.12f, 0.88f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0.12f, 0.12f, 0.45f);
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 36f);
    [SerializeField] private int canvasSortingOrder = 7000;

    private Canvas previewCanvas;
    private RectTransform canvasRectTransform;
    private RectTransform previewRectTransform;
    private Image previewImage;
    private void Awake()
    {
        EnsureVisuals();
        Hide();
    }

    public void Show(TacticsCharacterController target, Vector2 screenPosition, bool isValidDrop)
    {
        if (!TrySyncFromTarget(target))
        {
            Hide();
            return;
        }

        previewImage.color = isValidDrop ? previewColor : invalidPreviewColor;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform,
                screenPosition,
                null,
                out Vector2 localPosition))
        {
            previewRectTransform.anchoredPosition = localPosition + screenOffset;
        }

        previewImage.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (previewImage != null)
        {
            previewImage.gameObject.SetActive(false);
        }
    }

    private bool TrySyncFromTarget(TacticsCharacterController target)
    {
        EnsureVisuals();

        if (target == null)
        {
            return false;
        }

        SpriteRenderer sourceRenderer = target.GetPreviewSpriteRenderer();
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            return false;
        }

        previewImage.sprite = sourceRenderer.sprite;
        previewImage.preserveAspect = true;
        previewRectTransform.localScale = Vector3.one;
        previewRectTransform.sizeDelta = ResolveScreenSize(sourceRenderer);
        return true;
    }

    private static Vector2 ResolveScreenSize(SpriteRenderer sourceRenderer)
    {
        if (sourceRenderer == null)
        {
            return Vector2.zero;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Sprite sprite = sourceRenderer.sprite;
            return sprite != null ? sprite.bounds.size * sprite.pixelsPerUnit : Vector2.zero;
        }

        Bounds bounds = sourceRenderer.bounds;
        Vector3 min = camera.WorldToScreenPoint(bounds.min);
        Vector3 max = camera.WorldToScreenPoint(bounds.max);
        float width = Mathf.Abs(max.x - min.x);
        float height = Mathf.Abs(max.y - min.y);

        if (width <= 0.01f || height <= 0.01f)
        {
            Sprite sprite = sourceRenderer.sprite;
            return sprite != null ? sprite.bounds.size * sprite.pixelsPerUnit : Vector2.zero;
        }

        return new Vector2(width, height);
    }

    private void EnsureVisuals()
    {
        if (previewImage != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Throw Target Preview Canvas");
        canvasObject.transform.SetParent(transform, false);
        previewCanvas = canvasObject.AddComponent<Canvas>();
        previewCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        previewCanvas.sortingOrder = canvasSortingOrder;
        canvasObject.AddComponent<CanvasScaler>();
        GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;
        canvasRectTransform = canvasObject.GetComponent<RectTransform>();

        GameObject imageObject = new GameObject("Throw Target Preview Image");
        imageObject.transform.SetParent(canvasObject.transform, false);
        previewRectTransform = imageObject.AddComponent<RectTransform>();
        previewImage = imageObject.AddComponent<Image>();
        previewImage.raycastTarget = false;
        previewImage.color = previewColor;
    }
}
