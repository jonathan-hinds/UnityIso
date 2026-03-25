using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsAbilityProjectile : MonoBehaviour
{
    private const int FallbackSpriteTextureSize = 16;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private TacticsAbilityProjectileMotion motion;

    [Header("Legacy Flight Fallback")]
    [SerializeField, Min(0.01f)] private float travelUnitsPerSecond = 8f;
    [SerializeField, Min(0f)] private float arcHeight = 0.15f;
    [SerializeField, Min(0f)] private float arrivalPause = 0.02f;
    [SerializeField] private bool orientToVelocity = true;

    [Header("Legacy Offsets")]
    [SerializeField] private Vector3 launchOffset = new(0f, 0.25f, 0f);
    [SerializeField] private Vector3 impactOffset = new(0f, 0.2f, 0f);

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrderOffset = 8;

    private static Material fallbackTrailMaterial;
    private static Sprite fallbackSprite;

    public IEnumerator Play(TacticsAbilityProjectileFlight flight)
    {
        EnsureDefaultVisuals();
        int sortingLayerId = ResolveSortingLayerId(flight.SortingLayerId);
        int sortingOrder = flight.SortingOrder + sortingOrderOffset;

        ApplySorting(sortingLayerId, sortingOrder);

        motion ??= GetComponent<TacticsAbilityProjectileMotion>();
        if (motion != null && motion.isActiveAndEnabled)
        {
            yield return motion.Play(this, flight);
            yield break;
        }

        yield return PlayLegacyArcRoutine(flight);
    }

    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public TrailRenderer TrailRenderer => trailRenderer;
    public Vector3 LaunchOffset => launchOffset;
    public Vector3 ImpactOffset => impactOffset;

    public void ClearTrail()
    {
        if (trailRenderer == null)
        {
            return;
        }

        trailRenderer.Clear();
        trailRenderer.emitting = true;
    }

    public void StopTrail()
    {
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
        }
    }

    public void ResetVisualState(Vector3 position)
    {
        transform.position = position;
        transform.rotation = Quaternion.identity;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
    }

    public void FaceVelocity(Vector3 velocity)
    {
        if (velocity.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    public void BeginCleanup()
    {
        BeginCleanupInternal();
    }

    private IEnumerator PlayLegacyArcRoutine(TacticsAbilityProjectileFlight flight)
    {
        Vector3 start = flight.StartWorldPosition + launchOffset;
        Vector3 end = flight.EndWorldPosition + impactOffset;

        ResetVisualState(start);
        ClearTrail();

        float distance = Vector3.Distance(start, end);
        float duration = distance <= 0.001f ? 0f : distance / Mathf.Max(0.01f, travelUnitsPerSecond);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPosition = Vector3.Lerp(start, end, t);
            nextPosition.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            Vector3 velocity = nextPosition - transform.position;
            transform.position = nextPosition;

            if (orientToVelocity && velocity.sqrMagnitude > 0.000001f)
            {
                FaceVelocity(velocity);
            }

            yield return null;
        }

        transform.position = end;

        if (arrivalPause > 0f)
        {
            yield return new WaitForSeconds(arrivalPause);
        }

        BeginCleanupInternal();
    }

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        trailRenderer = GetComponentInChildren<TrailRenderer>();
        motion = GetComponent<TacticsAbilityProjectileMotion>();
    }

    private void ApplySorting(int sortingLayerId, int sortingOrder)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerID = sortingLayerId;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        if (trailRenderer != null)
        {
            trailRenderer.sortingLayerID = sortingLayerId;
            trailRenderer.sortingOrder = sortingOrder;
        }
    }

    private void EnsureDefaultVisuals()
    {
        if (spriteRenderer != null && spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = GetFallbackSprite();
        }

        if (trailRenderer != null && trailRenderer.sharedMaterial == null)
        {
            trailRenderer.sharedMaterial = GetFallbackTrailMaterial();
        }
    }

    private int ResolveSortingLayerId(int fallbackSortingLayerId)
    {
        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return fallbackSortingLayerId;
        }

        int explicitSortingLayerId = SortingLayer.NameToID(sortingLayerName);
        return explicitSortingLayerId == 0 && sortingLayerName != "Default"
            ? fallbackSortingLayerId
            : explicitSortingLayerId;
    }

    private void BeginCleanupInternal()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        float cleanupDelay = 0.01f;
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            cleanupDelay = Mathf.Max(cleanupDelay, trailRenderer.time);
        }

        Destroy(gameObject, cleanupDelay);
    }

    private static Material GetFallbackTrailMaterial()
    {
        if (fallbackTrailMaterial != null)
        {
            return fallbackTrailMaterial;
        }

        Shader fallbackShader = Shader.Find("Sprites/Default");
        if (fallbackShader == null)
        {
            return null;
        }

        fallbackTrailMaterial = new Material(fallbackShader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return fallbackTrailMaterial;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
        {
            return fallbackSprite;
        }

        Texture2D texture = new Texture2D(FallbackSpriteTextureSize, FallbackSpriteTextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "TacticsProjectileFallbackTexture"
        };

        Color clear = new(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        int center = FallbackSpriteTextureSize / 2;
        float radius = FallbackSpriteTextureSize * 0.35f;

        for (int y = 0; y < FallbackSpriteTextureSize; y++)
        {
            for (int x = 0; x < FallbackSpriteTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, distance <= radius ? fill : clear);
            }
        }

        texture.Apply();
        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, FallbackSpriteTextureSize, FallbackSpriteTextureSize),
            new Vector2(0.5f, 0.5f),
            FallbackSpriteTextureSize);
        fallbackSprite.name = "TacticsProjectileFallbackSprite";
        return fallbackSprite;
    }
}

public readonly struct TacticsAbilityProjectileFlight
{
    public TacticsAbilityProjectileFlight(Vector3 startWorldPosition, Vector3 endWorldPosition, int sortingLayerId, int sortingOrder)
    {
        StartWorldPosition = startWorldPosition;
        EndWorldPosition = endWorldPosition;
        SortingLayerId = sortingLayerId;
        SortingOrder = sortingOrder;
    }

    public Vector3 StartWorldPosition { get; }
    public Vector3 EndWorldPosition { get; }
    public int SortingLayerId { get; }
    public int SortingOrder { get; }
}
