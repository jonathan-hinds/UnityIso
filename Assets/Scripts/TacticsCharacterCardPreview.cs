using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TacticsCharacterCardPreview : IDisposable
{
    [Serializable]
    public struct PreviewSettings
    {
        [Min(128)] public int textureSize;
        [Min(0.1f)] public float orthographicSize;
        [Min(-1f)] public float cameraVerticalOffset;

        public static PreviewSettings Default()
        {
            return new PreviewSettings
            {
                textureSize = 320,
                orthographicSize = 0.32f,
                cameraVerticalOffset = -0.08f
            };
        }
    }

    private const int PreviewLayer = 31;
    private const float CameraDepthOffset = -10f;
    private static readonly Vector3 PreviewOriginOffset = new Vector3(1000f, -1000f, 0f);
    private static Material cachedPreviewMaterial;

    private readonly GameObject rootObject;
    private readonly Camera previewCamera;
    private readonly RenderTexture renderTexture;
    private readonly TacticsCharacterAnimator animator;
    private readonly PreviewSettings settings;

    private bool disposed;
    private bool isHovered;
    private bool idleApplied;

    public TacticsCharacterCardPreview(
        Transform parent,
        TacticsCharacterDefinition definition,
        ProceduralIsometricMapGenerator sourceGenerator,
        int previewIndex,
        PreviewSettings settings)
    {
        if (definition == null)
        {
            return;
        }

        this.settings = SanitizeSettings(settings);

        rootObject = new GameObject($"Team Preview {definition.DisplayName} {previewIndex}");
        rootObject.transform.SetParent(parent, false);
        rootObject.transform.position = PreviewOriginOffset + new Vector3(previewIndex * 12f, -previewIndex * 12f, 0f);

        ProceduralIsometricMapGenerator previewGenerator = rootObject.AddComponent<ProceduralIsometricMapGenerator>();
        if (sourceGenerator != null)
        {
            previewGenerator.ConfigureSingleTilePreview(sourceGenerator.CreateTileVisualProfile());
        }
        else
        {
            previewGenerator.ConfigureSingleTilePreview();
        }

        previewGenerator.GenerateMap();
        BuildPreviewCharacter(previewGenerator, definition, out animator);
        ApplyUnlitPreviewMaterial(rootObject.transform);
        SetLayerRecursively(rootObject.transform, PreviewLayer);

        renderTexture = new RenderTexture(this.settings.textureSize, this.settings.textureSize, 24, RenderTextureFormat.ARGB32)
        {
            name = $"RT_{definition.CharacterId}_{previewIndex}",
            antiAliasing = 1
        };
        renderTexture.Create();

        GameObject cameraObject = new GameObject("Preview Camera");
        cameraObject.transform.SetParent(rootObject.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 50f;
        previewCamera.targetTexture = renderTexture;

        FrameCamera(rootObject.transform, previewCamera, this.settings);
        ApplyIdlePose();
        previewCamera.Render();
    }

    public Texture Texture => renderTexture;

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        if (!hovered)
        {
            ApplyIdlePose();
        }
    }

    public void Tick(float deltaTime)
    {
        if (disposed || animator == null || previewCamera == null)
        {
            return;
        }

        if (isHovered)
        {
            idleApplied = false;
            animator.SetWalk(TacticsMovementDirection.SouthWest, Mathf.Max(0f, deltaTime));
        }
        else if (!idleApplied)
        {
            ApplyIdlePose();
        }

        previewCamera.Render();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (renderTexture != null)
        {
            renderTexture.Release();
            UnityEngine.Object.Destroy(renderTexture);
        }

        if (rootObject != null)
        {
            UnityEngine.Object.Destroy(rootObject);
        }
    }

    private void ApplyIdlePose()
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetWalkCycle();
        animator.SetIdle(TacticsMovementDirection.SouthWest);
        idleApplied = true;
    }

    private static void BuildPreviewCharacter(
        ProceduralIsometricMapGenerator previewGenerator,
        TacticsCharacterDefinition definition,
        out TacticsCharacterAnimator previewAnimator)
    {
        previewAnimator = null;
        if (previewGenerator == null || definition == null)
        {
            return;
        }

        TacticsCharacterData characterData = definition.BuildRuntimeData();
        if (!characterData.TryGetOrderedSprites(out _))
        {
            return;
        }

        GameObject characterRoot = new GameObject(definition.DisplayName);
        characterRoot.transform.SetParent(previewGenerator.transform, false);

        GameObject impactPivotObject = new GameObject("ImpactPivot");
        impactPivotObject.transform.SetParent(characterRoot.transform, false);

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(impactPivotObject.transform, false);

        SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = "Default";
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        SortingGroup sortingGroup = visualObject.AddComponent<SortingGroup>();
        sortingGroup.sortingLayerName = "Default";

        previewAnimator = characterRoot.AddComponent<TacticsCharacterAnimator>();
        previewAnimator.Initialize(spriteRenderer, characterData, previewGenerator, impactPivotObject.transform);

        if (!previewGenerator.TryGetTileWorldPosition(0, 0, out Vector3 tileWorldPosition))
        {
            tileWorldPosition = Vector3.zero;
        }

        characterRoot.transform.localPosition = tileWorldPosition + (Vector3)definition.TileAnchorOffset;
        previewAnimator.SetSorting(
            SortingLayer.NameToID("Default"),
            previewGenerator.GetCharacterSortingOrder(0, 0, previewGenerator.GetTileElevation(0, 0)));
    }

    private static void FrameCamera(Transform root, Camera camera, PreviewSettings settings)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0 || camera == null)
        {
            return;
        }

        List<SpriteRenderer> visibleRenderers = new List<SpriteRenderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            visibleRenderers.Add(renderer);
        }

        if (visibleRenderers.Count == 0)
        {
            return;
        }

        Bounds bounds = visibleRenderers[0].bounds;
        for (int i = 1; i < visibleRenderers.Count; i++)
        {
            bounds.Encapsulate(visibleRenderers[i].bounds);
        }

        PreviewSettings sanitizedSettings = SanitizeSettings(settings);
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y + sanitizedSettings.cameraVerticalOffset, CameraDepthOffset);
        camera.orthographicSize = sanitizedSettings.orthographicSize;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static void ApplyUnlitPreviewMaterial(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Material previewMaterial = GetPreviewMaterial();
        if (previewMaterial == null)
        {
            return;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.sharedMaterial = previewMaterial;
        }
    }

    private static Material GetPreviewMaterial()
    {
        if (cachedPreviewMaterial != null)
        {
            return cachedPreviewMaterial;
        }

        Shader previewShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (previewShader == null)
        {
            previewShader = Shader.Find("Sprites/Default");
        }

        if (previewShader == null)
        {
            Debug.LogWarning("TacticsCharacterCardPreview could not find an unlit sprite shader for team previews.");
            return null;
        }

        cachedPreviewMaterial = new Material(previewShader)
        {
            name = "Tactics Character Preview Unlit"
        };
        cachedPreviewMaterial.hideFlags = HideFlags.HideAndDontSave;
        return cachedPreviewMaterial;
    }

    private static PreviewSettings SanitizeSettings(PreviewSettings settings)
    {
        PreviewSettings fallback = PreviewSettings.Default();
        settings.textureSize = Mathf.Max(128, settings.textureSize <= 0 ? fallback.textureSize : settings.textureSize);
        settings.orthographicSize = Mathf.Max(0.1f, settings.orthographicSize <= 0f ? fallback.orthographicSize : settings.orthographicSize);
        settings.cameraVerticalOffset = settings.cameraVerticalOffset == 0f ? fallback.cameraVerticalOffset : settings.cameraVerticalOffset;
        return settings;
    }
}
