using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsCharacterOcclusionSilhouette : MonoBehaviour
{
    private const string VisualChildName = "Foreground Silhouette";
    private const string SilhouetteShaderResourcePath = "Tactics/Rendering/TacticsForegroundSilhouette";
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");
    private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");

    [Header("Silhouette")]
    [SerializeField] private Color silhouetteColor = new Color(1f, 0.94f, 0.52f, 0.95f);
    [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.28f;
    [SerializeField, Min(0.5f)] private float outlineThickness = 1.5f;
    [SerializeField, Min(0f)] private float occlusionPadding = 0.08f;
    [SerializeField, Min(1)] private int silhouetteSortingOrder = 5000;

    private static Material sharedSilhouetteMaterial;

    private MaterialPropertyBlock propertyBlock;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer silhouetteRenderer;
    private TacticsCharacterAnimator characterAnimator;
    private TacticsCharacterController characterController;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolveReferences();
        EnsureSilhouetteRenderer();
        SyncSilhouetteVisuals();
        ApplyVisualProperties();
        SetSilhouetteVisible(false);
    }

    private void LateUpdate()
    {
        if (!ResolveReferences())
        {
            SetSilhouetteVisible(false);
            return;
        }

        EnsureSilhouetteRenderer();
        SyncSilhouetteVisuals();
        ApplyVisualProperties();
        SetSilhouetteVisible(ShouldDisplaySilhouette());
    }

    private bool ResolveReferences()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<TacticsCharacterAnimator>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<TacticsCharacterController>();
        }

        if (sourceRenderer == null && characterAnimator != null)
        {
            sourceRenderer = characterAnimator.TargetRenderer;
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        return sourceRenderer != null;
    }

    private void EnsureSilhouetteRenderer()
    {
        if (silhouetteRenderer != null)
        {
            return;
        }

        Transform existingChild = transform.Find(VisualChildName);
        GameObject silhouetteObject = existingChild != null ? existingChild.gameObject : new GameObject(VisualChildName);
        silhouetteObject.transform.SetParent(transform, false);

        silhouetteRenderer = silhouetteObject.GetComponent<SpriteRenderer>();
        if (silhouetteRenderer == null)
        {
            silhouetteRenderer = silhouetteObject.AddComponent<SpriteRenderer>();
        }

        silhouetteRenderer.enabled = false;
        silhouetteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        silhouetteRenderer.receiveShadows = false;
        silhouetteRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        silhouetteRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        silhouetteRenderer.allowOcclusionWhenDynamic = false;
        silhouetteRenderer.drawMode = SpriteDrawMode.Simple;

        Material silhouetteMaterial = GetSilhouetteMaterial();
        if (silhouetteMaterial != null)
        {
            silhouetteRenderer.sharedMaterial = silhouetteMaterial;
        }
    }

    private void SyncSilhouetteVisuals()
    {
        if (sourceRenderer == null || silhouetteRenderer == null)
        {
            return;
        }

        Transform sourceTransform = sourceRenderer.transform;
        Transform silhouetteTransform = silhouetteRenderer.transform;

        silhouetteTransform.localPosition = sourceTransform.localPosition;
        silhouetteTransform.localRotation = sourceTransform.localRotation;
        silhouetteTransform.localScale = sourceTransform.localScale;

        silhouetteRenderer.sprite = sourceRenderer.sprite;
        silhouetteRenderer.flipX = sourceRenderer.flipX;
        silhouetteRenderer.flipY = sourceRenderer.flipY;
        silhouetteRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        silhouetteRenderer.sortingOrder = silhouetteSortingOrder;
        silhouetteRenderer.maskInteraction = sourceRenderer.maskInteraction;
    }

    private void ApplyVisualProperties()
    {
        if (silhouetteRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetColor(OutlineColorId, silhouetteColor);
        propertyBlock.SetFloat(FillAlphaId, fillAlpha);
        propertyBlock.SetFloat(OutlineThicknessId, outlineThickness);
        silhouetteRenderer.SetPropertyBlock(propertyBlock);
        silhouetteRenderer.color = Color.white;
    }

    private bool ShouldDisplaySilhouette()
    {
        if (sourceRenderer == null ||
            sourceRenderer.sprite == null ||
            !sourceRenderer.enabled ||
            !sourceRenderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        Bounds sourceBounds = sourceRenderer.bounds;
        sourceBounds.Expand(occlusionPadding);
        Vector3[] samplePoints = BuildOcclusionSamplePoints(sourceBounds);

        IReadOnlyList<TacticsForegroundOccluderGroup> groups = TacticsForegroundOccluderRegistry.RegisteredGroups;
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            TacticsForegroundOccluderGroup group = groups[groupIndex];
            if (group == null || !group.isActiveAndEnabled)
            {
                continue;
            }

            IReadOnlyList<SpriteRenderer> occluders = group.Occluders;
            for (int rendererIndex = 0; rendererIndex < occluders.Count; rendererIndex++)
            {
                SpriteRenderer occluder = occluders[rendererIndex];
                if (occluder == null ||
                    !occluder.enabled ||
                    !occluder.gameObject.activeInHierarchy ||
                    !TacticsSpriteSortingUtility.SortsInFrontOf(occluder, sourceRenderer))
                {
                    continue;
                }

                Bounds occluderBounds = occluder.bounds;
                occluderBounds.Expand(0.03f);
                if (!sourceBounds.Intersects(occluderBounds))
                {
                    continue;
                }

                int occludedUpperBodyPoints = 0;
                for (int pointIndex = 0; pointIndex < samplePoints.Length; pointIndex++)
                {
                    if (occluderBounds.Contains(samplePoints[pointIndex]))
                    {
                        occludedUpperBodyPoints++;
                    }
                }

                if (occludedUpperBodyPoints >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void SetSilhouetteVisible(bool isVisible)
    {
        if (silhouetteRenderer != null)
        {
            silhouetteRenderer.enabled = isVisible;
        }
    }

    private Vector3[] BuildOcclusionSamplePoints(Bounds sourceBounds)
    {
        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;
        float widthInset = sourceBounds.extents.x * 0.35f;
        float upperMidY = Mathf.Lerp(min.y, max.y, 0.65f);
        float topY = Mathf.Lerp(min.y, max.y, 0.9f);

        return new[]
        {
            new Vector3(sourceBounds.center.x, topY, sourceBounds.center.z),
            new Vector3(min.x + widthInset, upperMidY, sourceBounds.center.z),
            new Vector3(max.x - widthInset, upperMidY, sourceBounds.center.z),
            new Vector3(sourceBounds.center.x, upperMidY, sourceBounds.center.z)
        };
    }

    private static Material GetSilhouetteMaterial()
    {
        if (sharedSilhouetteMaterial != null)
        {
            return sharedSilhouetteMaterial;
        }

        Shader silhouetteShader = Resources.Load<Shader>(SilhouetteShaderResourcePath);
        if (silhouetteShader == null)
        {
            Debug.LogWarning($"Tactics occlusion silhouette could not load shader at Resources/{SilhouetteShaderResourcePath}.");
            return null;
        }

        sharedSilhouetteMaterial = new Material(silhouetteShader)
        {
            name = "Runtime_TacticsForegroundSilhouette"
        };

        return sharedSilhouetteMaterial;
    }
}
