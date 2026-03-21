using UnityEngine;

public static class TacticsCharacterSpawner
{
    public static TacticsCharacterController SpawnCharacter(
        ProceduralIsometricMapGenerator mapGenerator,
        TacticsCharacterDefinition definition,
        Vector2Int spawnTile,
        Transform parent = null)
    {
        if (mapGenerator == null || definition == null)
        {
            return null;
        }

        if (!definition.TryGetOrderedSprites(out _))
        {
            Debug.LogWarning($"Tactics spawner skipped '{definition.name}' because its sprite data is invalid.");
            return null;
        }

        GameObject characterRoot = new GameObject(definition.DisplayName);
        if (parent != null)
        {
            characterRoot.transform.SetParent(parent, false);
        }

        GameObject impactPivotObject = new GameObject("ImpactPivot");
        impactPivotObject.transform.SetParent(characterRoot.transform, false);

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(impactPivotObject.transform, false);

        SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = "Default";

        TacticsCharacterAnimator animator = characterRoot.AddComponent<TacticsCharacterAnimator>();
        animator.Initialize(spriteRenderer, definition, mapGenerator, impactPivotObject.transform);

        BoxCollider2D selectionCollider = visualObject.AddComponent<BoxCollider2D>();
        Vector2 spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : new Vector2(0.2f, 0.3f);
        selectionCollider.size = spriteSize;
        selectionCollider.offset = Vector2.zero;

        TacticsCharacterController characterController = characterRoot.AddComponent<TacticsCharacterController>();
        characterController.Initialize(mapGenerator, animator, definition, spawnTile);

        if (definition.Team == TacticsUnitTeam.Enemy)
        {
            characterRoot.AddComponent<TacticsEnemyController>();
        }

        return characterController;
    }
}
