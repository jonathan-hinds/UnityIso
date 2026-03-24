using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TacticsPlayerController : MonoBehaviour
{
    private enum SelectionState
    {
        None = 0,
        CharacterSelected = 1,
        AwaitingMoveTarget = 2,
        AwaitingAbilityTarget = 3
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool blockWhenPointerOverUi = true;
    [SerializeField] private TacticsActionMenuView actionMenuView;
    [SerializeField] private TacticsSelectionPanelView activeCharacterPanelView;
    [SerializeField] private TacticsSelectionPanelView selectedCharacterPanelView;
    [SerializeField] private TacticsTurnManager turnManager;
    [SerializeField] private TacticsCombatSystem combatSystem;
    [SerializeField] private TacticsTileTargetOverlay tileTargetOverlay;
    [SerializeField] private TacticsCoopSessionCoordinator coopSessionCoordinator;

    private TacticsCharacterController selectedCharacter;
    private SelectionState selectionState;
    private readonly List<TacticsActionMenuAbilityOption> reusableAbilityOptions = new();
    private readonly List<TacticsCharacterController> hoveredAbilityPreviewTargets = new();
    private readonly List<Vector2Int> reusableOverlayTiles = new();

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

        if (coopSessionCoordinator == null)
        {
            coopSessionCoordinator = FindFirstObjectByType<TacticsCoopSessionCoordinator>();
        }
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

        if (coopSessionCoordinator == null)
        {
            coopSessionCoordinator = FindFirstObjectByType<TacticsCoopSessionCoordinator>();
        }

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
        RefreshTargetIndicators();
    }

    private void Update()
    {
        if (turnManager != null && turnManager.IsTransitioningTurns)
        {
            SetHoveredAbilityTargets(null);
            return;
        }

        HandleCancelInput();
        RefreshHoveredAbilityTarget();

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
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
        Vector3 worldPosition = targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D[] hits = Physics2D.OverlapPointAll(point);

        if (selectionState == SelectionState.AwaitingAbilityTarget &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()))
        {
            if (TryGetClickedCharacter(hits, out TacticsCharacterController targetedCharacter) &&
                targetedCharacter != null &&
                combatSystem != null &&
                combatSystem.TargetingAbility != null &&
                combatSystem.CanTargetFromTile(selectedCharacter, selectedCharacter.GridPosition, combatSystem.TargetingAbility, targetedCharacter) &&
                RequestUseAbility(selectedCharacter, combatSystem.TargetingAbility, targetedCharacter.GridPosition))
            {
                selectionState = SelectionState.CharacterSelected;
                RefreshHud();
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
        if (selectedCharacter == character && selectionState == nextState)
        {
            RefreshHud();
            return;
        }

        if (selectionState == SelectionState.AwaitingAbilityTarget)
        {
            combatSystem?.CancelTargeting();
        }

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
        TacticsCharacterController activePlayerCharacter = GetActivePlayerCharacter();
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
        TacticsCharacterController activePlayerCharacter = GetActivePlayerCharacter();
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
            }
        }
        else if (combatSystem.BeginTargeting(activePlayerCharacter, ability))
        {
            selectionState = SelectionState.AwaitingAbilityTarget;
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
            SetHoveredAbilityTargets(null);
            RefreshHud();
        }
        else if (selectionState == SelectionState.AwaitingAbilityTarget)
        {
            combatSystem?.CancelTargeting();
            selectionState = SelectionState.CharacterSelected;
            SetHoveredAbilityTargets(null);
            RefreshHud();
        }
    }

    private void RefreshHud()
    {
        TacticsCharacterController activeCharacter = turnManager != null ? turnManager.ActiveCharacter : null;
        TacticsCharacterController activePlayerCharacter = GetActivePlayerCharacter();
        if (activeCharacter != null)
        {
            activeCharacterPanelView?.Show(activeCharacter);
        }
        else
        {
            activeCharacterPanelView?.Hide();
        }

        if (selectedCharacter != null && selectedCharacter.isActiveAndEnabled && selectedCharacter.IsAlive)
        {
            selectedCharacterPanelView?.Show(selectedCharacter);
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

                actionMenuView.ShowForCharacter(
                    activePlayerCharacter,
                    reusableAbilityOptions,
                    selectionState == SelectionState.AwaitingMoveTarget,
                    selectionState == SelectionState.AwaitingAbilityTarget,
                    turnManager != null ? turnManager.RoundNumber : 1,
                    turnManager != null ? turnManager.TurnNumber : 1,
                    turnManager != null ? turnManager.ParticipantCount : 1);
            }
            else
            {
                actionMenuView.Hide();
            }
        }

        RefreshTargetOverlay();
    }

    private void HandleActiveParticipantChanged(ITacticsTurnParticipant participant)
    {
        TacticsCharacterController activeCharacter = participant as TacticsCharacterController;
        if (selectionState == SelectionState.AwaitingAbilityTarget)
        {
            combatSystem?.CancelTargeting();
        }

        if (selectionState == SelectionState.AwaitingMoveTarget ||
            selectionState == SelectionState.AwaitingAbilityTarget)
        {
            selectionState = SelectionState.CharacterSelected;
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
            if (selectionState == SelectionState.AwaitingAbilityTarget)
            {
                selectionState = SelectionState.CharacterSelected;
            }

            SetHoveredAbilityTargets(null);
        }

        RefreshHud();
    }

    private void HandleSelectedCharacterStateChanged(ITacticsTurnParticipant participant)
    {
        RefreshHud();
    }

    private void SubscribeToSelectedCharacter(TacticsCharacterController character)
    {
        if (selectedCharacter != null)
        {
            selectedCharacter.TurnStateChanged -= HandleSelectedCharacterStateChanged;
        }

        if (character != null)
        {
            character.TurnStateChanged -= HandleSelectedCharacterStateChanged;
            character.TurnStateChanged += HandleSelectedCharacterStateChanged;
        }
    }

    private TacticsCharacterController GetActivePlayerCharacter()
    {
        TacticsCharacterController activeCharacter = turnManager != null ? turnManager.ActiveCharacter : null;
        return activeCharacter != null && activeCharacter.IsPlayerControlled ? activeCharacter : null;
    }

    private SelectionState GetSelectionStateForCharacter(TacticsCharacterController character)
    {
        return ReferenceEquals(character, GetActivePlayerCharacter())
            ? SelectionState.CharacterSelected
            : SelectionState.None;
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
        if (selectionState == SelectionState.AwaitingAbilityTarget &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()) &&
            combatSystem != null &&
            combatSystem.TargetingAbility != null)
        {
            IReadOnlyList<Vector2Int> tiles = combatSystem.GetValidTargetTiles(selectedCharacter, combatSystem.TargetingAbility);
            validTargetTiles = new HashSet<Vector2Int>(tiles);
        }

        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character == null)
            {
                continue;
            }

            bool isTargeted = validTargetTiles != null && validTargetTiles.Contains(character.GridPosition);
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

        AddUniqueTiles(reusableOverlayTiles, combatSystem.GetTargetableTiles(source, ability));

        if (ability.RangeType == TacticsAbilityRangeType.SurroundingAoE)
        {
            AddUniqueTiles(reusableOverlayTiles, combatSystem.GetAreaTiles(source, ability, source.GridPosition));
        }

        return reusableOverlayTiles;
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
            bool hasTargets = combatSystem != null &&
                              character.CanUseAbilitiesThisTurn &&
                              hasResources &&
                              combatSystem.GetValidTargetTiles(character, ability).Count > 0;
            bool isInteractable = combatSystem != null &&
                                  character.CanUseAbilitiesThisTurn &&
                                  hasResources &&
                                  hasTargets;
            bool isSelected = isTargetingWithCharacter &&
                              ReferenceEquals(combatSystem.TargetingAbility, ability);
            string statusText = BuildAbilityStatusText(character, ability, hasResources, hasTargets);

            abilityOptions.Add(new TacticsActionMenuAbilityOption(ability, isInteractable, isSelected, statusText));
        }
    }

    private static string BuildAbilityStatusText(
        TacticsCharacterController character,
        TacticsAbilityDefinition ability,
        bool hasResources,
        bool hasTargets)
    {
        if (character == null || ability == null)
        {
            return "Unavailable";
        }

        if (!character.CanUseAbilitiesThisTurn)
        {
            return character.IsTurnActive ? "Action spent" : "Not your turn";
        }

        if (!hasResources)
        {
            return ability.CostResourceType switch
            {
                TacticsAbilityResourceType.Stamina => "Not enough stamina",
                TacticsAbilityResourceType.Mana => "Not enough mana",
                _ => "Not enough resources"
            };
        }

        return hasTargets ? "Ready" : "No targets";
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
        if (character == null || ability == null)
        {
            return false;
        }

        if (coopSessionCoordinator != null)
        {
            return coopSessionCoordinator.RequestUseAbility(character, ability, targetTile);
        }

        return combatSystem != null && combatSystem.TryUseAbility(character, ability, targetTile);
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
}
