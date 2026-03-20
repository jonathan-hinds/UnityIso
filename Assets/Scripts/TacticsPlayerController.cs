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
        AwaitingMoveTarget = 2
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool blockWhenPointerOverUi = true;
    [SerializeField] private TacticsActionMenuView actionMenuView;
    [SerializeField] private TacticsSelectionPanelView selectionPanelView;
    [SerializeField] private TacticsTurnManager turnManager;

    private TacticsCharacterController selectedCharacter;
    private SelectionState selectionState;

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

        if (selectionPanelView == null)
        {
            selectionPanelView = FindFirstObjectByType<TacticsSelectionPanelView>();
        }

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }
    }

    private void OnEnable()
    {
        if (actionMenuView == null)
        {
            actionMenuView = FindFirstObjectByType<TacticsActionMenuView>();
        }

        if (selectionPanelView == null)
        {
            selectionPanelView = FindFirstObjectByType<TacticsSelectionPanelView>();
        }

        if (turnManager == null)
        {
            turnManager = FindFirstObjectByType<TacticsTurnManager>();
        }

        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
            actionMenuView.ActionSelected += HandleActionSelected;
        }

        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.ActiveParticipantChanged += HandleActiveParticipantChanged;
            turnManager.TurnStateChanged -= HandleTurnStateChanged;
            turnManager.TurnStateChanged += HandleTurnStateChanged;
            HandleActiveParticipantChanged(turnManager.ActiveParticipant);
        }
    }

    private void OnDisable()
    {
        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
        }

        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
            turnManager.TurnStateChanged -= HandleTurnStateChanged;
        }

        SubscribeToSelectedCharacter(null);
    }

    private void Update()
    {
        if (turnManager != null && turnManager.IsTransitioningTurns)
        {
            return;
        }

        HandleCancelInput();

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

        if (TryGetClickedCharacter(hits, out TacticsCharacterController clickedCharacter))
        {
            if (ReferenceEquals(clickedCharacter, GetActivePlayerCharacter()))
            {
                SelectCharacter(clickedCharacter, SelectionState.CharacterSelected);
            }

            return;
        }

        if (selectionState == SelectionState.AwaitingMoveTarget &&
            ReferenceEquals(selectedCharacter, GetActivePlayerCharacter()) &&
            TryGetClickedTile(hits, out IsometricTileHoverInfo clickedTile))
        {
            if (selectedCharacter.TryMoveTo(new Vector2Int(clickedTile.GridX, clickedTile.GridY)))
            {
                selectionState = SelectionState.CharacterSelected;
                RefreshHud();
            }

            return;
        }

    }

    private void SelectCharacter(TacticsCharacterController character, SelectionState nextState)
    {
        if (selectedCharacter == character && selectionState == nextState)
        {
            RefreshHud();
            return;
        }

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
        }

        actionMenuView = view;

        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
            actionMenuView.ActionSelected += HandleActionSelected;
        }

        RefreshHud();
    }

    public void AssignSelectionHud(TacticsSelectionPanelView view)
    {
        selectionPanelView = view;
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

    private void HandleActionSelected(TacticsHudActionType actionType)
    {
        TacticsCharacterController activePlayerCharacter = GetActivePlayerCharacter();
        if (!ReferenceEquals(selectedCharacter, activePlayerCharacter))
        {
            return;
        }

        switch (actionType)
        {
            case TacticsHudActionType.Move:
                if (selectedCharacter.CanMoveThisTurn)
                {
                    selectionState = SelectionState.AwaitingMoveTarget;
                    RefreshHud();
                }

                break;
            case TacticsHudActionType.EndTurn:
                turnManager?.TryEndActiveTurn();
                break;
        }
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
            RefreshHud();
        }
    }

    private void RefreshHud()
    {
        if (selectedCharacter == null)
        {
            actionMenuView?.Hide();
            selectionPanelView?.Hide();
            return;
        }

        TacticsCharacterController activeCharacter = turnManager != null ? turnManager.ActiveCharacter : null;
        bool showActionMenu =
            ReferenceEquals(selectedCharacter, activeCharacter) &&
            selectedCharacter.IsPlayerControlled &&
            selectedCharacter.IsTurnActive;

        if (actionMenuView != null)
        {
            if (showActionMenu)
            {
                actionMenuView.ShowForCharacter(
                    selectedCharacter,
                    selectionState == SelectionState.AwaitingMoveTarget,
                    turnManager != null ? turnManager.RoundNumber : 1,
                    turnManager != null ? turnManager.TurnNumber : 1,
                    turnManager != null ? turnManager.ParticipantCount : 1);
            }
            else
            {
                actionMenuView.Hide();
            }
        }

        selectionPanelView?.Show(selectedCharacter);
    }

    private void HandleActiveParticipantChanged(ITacticsTurnParticipant participant)
    {
        TacticsCharacterController activeCharacter = participant as TacticsCharacterController;
        SelectionState nextState = activeCharacter != null && activeCharacter.IsPlayerControlled
            ? SelectionState.CharacterSelected
            : SelectionState.None;
        SelectCharacter(activeCharacter, nextState);
    }

    private void HandleTurnStateChanged()
    {
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
}
