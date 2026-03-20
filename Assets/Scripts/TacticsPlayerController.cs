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
    }

    private void OnEnable()
    {
        if (actionMenuView == null)
        {
            actionMenuView = FindFirstObjectByType<TacticsActionMenuView>();
        }

        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
            actionMenuView.ActionSelected += HandleActionSelected;
        }
    }

    private void OnDisable()
    {
        if (actionMenuView != null)
        {
            actionMenuView.ActionSelected -= HandleActionSelected;
        }
    }

    private void Update()
    {
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
            SelectCharacter(clickedCharacter);
            return;
        }

        if (selectionState == SelectionState.AwaitingMoveTarget &&
            selectedCharacter != null &&
            TryGetClickedTile(hits, out IsometricTileHoverInfo clickedTile))
        {
            if (selectedCharacter.TryMoveTo(new Vector2Int(clickedTile.GridX, clickedTile.GridY)))
            {
                selectionState = SelectionState.CharacterSelected;
                RefreshHud();
            }

            return;
        }

        if (selectionState == SelectionState.CharacterSelected)
        {
            SelectCharacter(null);
        }
    }

    private void SelectCharacter(TacticsCharacterController character)
    {
        if (selectedCharacter == character && selectionState == SelectionState.CharacterSelected)
        {
            RefreshHud();
            return;
        }

        if (selectedCharacter != null)
        {
            selectedCharacter.SetSelected(false);
        }

        selectedCharacter = character;

        if (selectedCharacter != null)
        {
            selectedCharacter.SetSelected(true);
            selectionState = SelectionState.CharacterSelected;
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

    private void HandleActionSelected(TacticsHudActionType actionType)
    {
        if (selectedCharacter == null)
        {
            return;
        }

        switch (actionType)
        {
            case TacticsHudActionType.Move:
                if (selectedCharacter.CanReceiveCommands)
                {
                    selectionState = SelectionState.AwaitingMoveTarget;
                    RefreshHud();
                }

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
            return;
        }

        if (selectionState == SelectionState.CharacterSelected)
        {
            SelectCharacter(null);
        }
    }

    private void RefreshHud()
    {
        if (actionMenuView == null)
        {
            return;
        }

        if (selectedCharacter == null)
        {
            actionMenuView.Hide();
            return;
        }

        actionMenuView.ShowForCharacter(selectedCharacter, selectionState == SelectionState.AwaitingMoveTarget);
    }
}
