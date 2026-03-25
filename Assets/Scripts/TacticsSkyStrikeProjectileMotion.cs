using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsSkyStrikeProjectileMotion : TacticsAbilityProjectileMotion
{
    [Header("Descent")]
    [SerializeField, Min(0.01f)] private float descentUnitsPerSecond = 14f;
    [SerializeField, Min(0f)] private float skyHeight = 4f;
    [SerializeField, Min(0f)] private float horizontalLead = 0.12f;
    [SerializeField] private Vector2 horizontalDirection = new(0.2f, 1f);
    [SerializeField] private bool orientToVelocity = true;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float arrivalPause = 0.05f;
    [SerializeField, Min(0f)] private float impactFlashScale = 1.3f;

    public override IEnumerator Play(TacticsAbilityProjectile projectile, TacticsAbilityProjectileFlight flight)
    {
        if (projectile == null)
        {
            yield break;
        }

        Vector3 end = flight.EndWorldPosition + projectile.ImpactOffset;
        Vector2 normalizedDirection = horizontalDirection.sqrMagnitude > 0.0001f
            ? horizontalDirection.normalized
            : Vector2.up;
        Vector3 start = end +
                        new Vector3(normalizedDirection.x, normalizedDirection.y, 0f) * horizontalLead +
                        Vector3.up * skyHeight;

        projectile.ResetVisualState(start);
        projectile.ClearTrail();

        float duration = GetTravelDuration(start, end, descentUnitsPerSecond);
        float elapsed = 0f;
        Vector3 initialScale = projectile.transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPosition = Vector3.Lerp(start, end, t);
            Vector3 velocity = nextPosition - projectile.transform.position;
            projectile.transform.position = nextPosition;

            if (orientToVelocity)
            {
                projectile.FaceVelocity(velocity);
            }

            yield return null;
        }

        projectile.transform.position = end;
        projectile.transform.localScale = initialScale * Mathf.Max(1f, impactFlashScale);

        if (arrivalPause > 0f)
        {
            yield return new WaitForSeconds(arrivalPause);
        }

        projectile.transform.localScale = initialScale;
        projectile.BeginCleanup();
    }
}
