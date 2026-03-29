using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsStairsController : MonoBehaviour, ITacticsCombatTextAnchor, ITacticsTileBlocker
{
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D interactionCollider;

    public string RuntimeStairsId { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public bool BlocksTile => true;
    public int CurrentElevation => mapGenerator != null ? mapGenerator.GetTileElevation(GridPosition.x, GridPosition.y) : 0;
    public int CurrentSortingOrder => spriteRenderer != null ? spriteRenderer.sortingOrder : 0;

    public void Initialize(
        ProceduralIsometricMapGenerator generator,
        string runtimeStairsId,
        Vector2Int gridPosition)
    {
        mapGenerator = generator;
        RuntimeStairsId = string.IsNullOrWhiteSpace(runtimeStairsId)
            ? $"stairs_{gridPosition.x}_{gridPosition.y}"
            : runtimeStairsId.Trim();
        GridPosition = gridPosition;

        CacheComponents();
    }

    private void Awake()
    {
        CacheComponents();
    }

    public bool IsAdjacentAndInteractable(TacticsCharacterController character)
    {
        if (character == null || !character.IsAlive)
        {
            return false;
        }

        return Mathf.Abs(character.GridPosition.x - GridPosition.x) +
               Mathf.Abs(character.GridPosition.y - GridPosition.y) == 1 &&
               character.CurrentElevation == CurrentElevation;
    }

    public void SetPresentationVisible(bool isVisible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = isVisible;
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = isVisible;
        }
    }

    public Vector3 GetCombatTextSpawnPosition(float verticalPadding = 0.18f)
    {
        if (spriteRenderer != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + Mathf.Max(verticalPadding, bounds.size.y * 0.12f), 0f);
        }

        return transform.position + new Vector3(0f, 0.75f + verticalPadding, 0f);
    }

    public int GetCombatTextSortingLayerId()
    {
        return spriteRenderer != null ? spriteRenderer.sortingLayerID : SortingLayer.NameToID("Default");
    }

    public int GetCombatTextSortingOrder()
    {
        return spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
    }

    public static TacticsStairsController FindByRuntimeId(string runtimeStairsId)
    {
        if (string.IsNullOrWhiteSpace(runtimeStairsId))
        {
            return null;
        }

        TacticsStairsController[] stairsObjects = FindObjectsByType<TacticsStairsController>(FindObjectsSortMode.None);
        for (int i = 0; i < stairsObjects.Length; i++)
        {
            TacticsStairsController stairs = stairsObjects[i];
            if (stairs != null &&
                string.Equals(stairs.RuntimeStairsId, runtimeStairsId, StringComparison.OrdinalIgnoreCase))
            {
                return stairs;
            }
        }

        return null;
    }

    public static TacticsStairsController FindBestAdjacentStairs(TacticsCharacterController character)
    {
        if (character == null)
        {
            return null;
        }

        TacticsStairsController[] stairsObjects = FindObjectsByType<TacticsStairsController>(FindObjectsSortMode.None);
        TacticsStairsController bestStairs = null;
        int bestSortingOrder = int.MinValue;

        for (int i = 0; i < stairsObjects.Length; i++)
        {
            TacticsStairsController stairs = stairsObjects[i];
            if (stairs == null || !stairs.IsAdjacentAndInteractable(character))
            {
                continue;
            }

            if (bestStairs == null || stairs.CurrentSortingOrder > bestSortingOrder)
            {
                bestStairs = stairs;
                bestSortingOrder = stairs.CurrentSortingOrder;
            }
        }

        return bestStairs;
    }

    private void CacheComponents()
    {
        spriteRenderer = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
        interactionCollider = interactionCollider != null ? interactionCollider : GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
        {
            interactionCollider = gameObject.AddComponent<BoxCollider2D>();
        }
    }
}
