using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TacticsPlayerController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool blockWhenPointerOverUi = true;

    private TacticsCharacterController selectedCharacter;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
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

        if (selectedCharacter != null && TryGetClickedTile(hits, out IsometricTileHoverInfo clickedTile))
        {
            selectedCharacter.TryMoveTo(new Vector2Int(clickedTile.GridX, clickedTile.GridY));
            return;
        }

        SelectCharacter(null);
    }

    private void SelectCharacter(TacticsCharacterController character)
    {
        if (selectedCharacter == character)
        {
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
        }
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
}
