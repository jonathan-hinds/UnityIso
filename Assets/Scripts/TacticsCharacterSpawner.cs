using UnityEngine;
using UnityEngine.Rendering;

public static class TacticsCharacterSpawner
{
    public static TacticsCharacterController SpawnCharacter(
        ProceduralIsometricMapGenerator mapGenerator,
        TacticsCharacterDefinition definition,
        Vector2Int spawnTile,
        Transform parent = null,
        string runtimeCharacterId = "",
        TacticsCharacterProgressionSnapshot progression = default)
    {
        return SpawnCharacter(
            mapGenerator,
            definition != null ? definition.BuildRuntimeData() : null,
            spawnTile,
            parent,
            definition,
            runtimeCharacterId,
            progression);
    }

    public static TacticsCharacterController SpawnCharacter(
        ProceduralIsometricMapGenerator mapGenerator,
        TacticsCharacterData characterData,
        Vector2Int spawnTile,
        Transform parent = null,
        TacticsCharacterDefinition sourceDefinition = null,
        string runtimeCharacterId = "",
        TacticsCharacterProgressionSnapshot progression = default)
    {
        if (mapGenerator == null || characterData == null)
        {
            return null;
        }

        if (!characterData.TryGetOrderedSprites(out _))
        {
            Debug.LogWarning($"Tactics spawner skipped '{characterData.DisplayName}' because its sprite data is invalid.");
            return null;
        }

        GameObject characterRoot = new GameObject(characterData.DisplayName);
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
        spriteRenderer.sortingOrder = 0;
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        SortingGroup sortingGroup = visualObject.AddComponent<SortingGroup>();
        sortingGroup.sortingLayerName = spriteRenderer.sortingLayerName;
        sortingGroup.sortingOrder = 0;

        TacticsCharacterAnimator animator = characterRoot.AddComponent<TacticsCharacterAnimator>();
        animator.Initialize(spriteRenderer, characterData, mapGenerator, impactPivotObject.transform);

        BoxCollider2D selectionCollider = visualObject.AddComponent<BoxCollider2D>();
        Vector2 spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : new Vector2(0.2f, 0.3f);
        selectionCollider.size = spriteSize;
        selectionCollider.offset = Vector2.zero;

        TacticsCharacterController characterController = characterRoot.AddComponent<TacticsCharacterController>();
        characterController.Initialize(mapGenerator, animator, characterData, spawnTile, sourceDefinition, runtimeCharacterId, progression);
        characterRoot.AddComponent<TacticsCharacterElevationVisibility>();

        if (characterData.Team == TacticsUnitTeam.Enemy)
        {
            characterRoot.AddComponent<TacticsEnemyController>();
        }

        return characterController;
    }
}
