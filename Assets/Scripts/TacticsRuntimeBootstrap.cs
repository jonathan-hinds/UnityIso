using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class TacticsRuntimeBootstrap : MonoBehaviour
{
    private const string BootstrapName = "Tactics Runtime Bootstrap";
    private static readonly string[] PreferredSpriteResourcePaths =
    {
        "Characters/sprite-sheet_export_8x4_48x64",
        "Characters/sprite-sheet_export_8x4_24x32"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateBootstrap()
    {
        if (FindFirstObjectByType<TacticsRuntimeBootstrap>() != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject(BootstrapName);
        bootstrap.AddComponent<TacticsRuntimeBootstrap>();
    }

    private ProceduralIsometricMapGenerator mapGenerator;

    private void Start()
    {
        mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        if (mapGenerator == null)
        {
            Debug.LogWarning("Tactics bootstrap could not find a ProceduralIsometricMapGenerator.");
            return;
        }

        if (mapGenerator.HasGeneratedMap)
        {
            SetupScene();
            return;
        }

        mapGenerator.MapGenerated += HandleMapGenerated;
    }

    private void OnDestroy()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
        }
    }

    private void HandleMapGenerated()
    {
        if (mapGenerator != null)
        {
            mapGenerator.MapGenerated -= HandleMapGenerated;
        }

        SetupScene();
    }

    private void SetupScene()
    {
        EnsurePlayerController();
        EnsureCharacter();
    }

    private void EnsurePlayerController()
    {
        if (FindFirstObjectByType<TacticsPlayerController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("Tactics Player Controller");
        controllerObject.AddComponent<TacticsPlayerController>();
    }

    private void EnsureCharacter()
    {
        if (FindFirstObjectByType<TacticsCharacterController>() != null)
        {
            return;
        }

        Sprite[] sheetSprites = LoadCharacterSprites();

        if (sheetSprites.Length < 10)
        {
            Debug.LogWarning("Tactics bootstrap could not find a sliced character sheet with at least 10 sprites in Assets/Resources/Characters.");
            return;
        }

        Vector2Int spawnTile = FindSpawnTile();

        GameObject characterRoot = new GameObject("Player Character");
        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(characterRoot.transform, false);

        SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = "Default";

        TacticsCharacterAnimator animator = characterRoot.AddComponent<TacticsCharacterAnimator>();
        animator.Initialize(spriteRenderer, sheetSprites);

        BoxCollider2D selectionCollider = visualObject.AddComponent<BoxCollider2D>();
        Vector2 spriteSize = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : new Vector2(0.2f, 0.3f);
        selectionCollider.size = spriteSize;
        selectionCollider.offset = new Vector2(spriteSize.x * 0.5f, spriteSize.y * 0.5f);

        TacticsCharacterController characterController = characterRoot.AddComponent<TacticsCharacterController>();
        characterController.Initialize(mapGenerator, animator, spawnTile);
    }

    private Vector2Int FindSpawnTile()
    {
        Vector2Int center = mapGenerator.GetCenterTile();
        int maxRadius = Mathf.Max(mapGenerator.Width, mapGenerator.Length);

        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    if (mapGenerator.IsTraversable(x, y))
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }

        return center;
    }

    private static int ParseSpriteIndex(Sprite sprite)
    {
        if (sprite == null)
        {
            return int.MaxValue;
        }

        string[] parts = sprite.name.Split('_');
        if (parts.Length == 0)
        {
            return int.MaxValue;
        }

        return int.TryParse(parts[parts.Length - 1], out int index) ? index : int.MaxValue;
    }

    private static Sprite[] LoadCharacterSprites()
    {
        for (int i = 0; i < PreferredSpriteResourcePaths.Length; i++)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(PreferredSpriteResourcePaths[i])
                .OrderBy(ParseSpriteIndex)
                .ToArray();

            if (sprites.Length >= 10)
            {
                return sprites;
            }
        }

        Sprite[] allSprites = Resources.LoadAll<Sprite>("Characters");
        if (allSprites.Length == 0)
        {
            return Array.Empty<Sprite>();
        }

        List<IGrouping<string, Sprite>> groups = allSprites
            .GroupBy(GetSpriteSheetPrefix)
            .OrderByDescending(group => group.Select(GetSpriteArea).DefaultIfEmpty(0f).Average())
            .ToList();

        for (int i = 0; i < groups.Count; i++)
        {
            Sprite[] sprites = groups[i]
                .OrderBy(ParseSpriteIndex)
                .ToArray();

            if (sprites.Length >= 10)
            {
                return sprites;
            }
        }

        return Array.Empty<Sprite>();
    }

    private static string GetSpriteSheetPrefix(Sprite sprite)
    {
        if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
        {
            return string.Empty;
        }

        int separatorIndex = sprite.name.LastIndexOf('_');
        return separatorIndex >= 0 ? sprite.name[..separatorIndex] : sprite.name;
    }

    private static float GetSpriteArea(Sprite sprite)
    {
        return sprite == null ? 0f : sprite.rect.width * sprite.rect.height;
    }
}
