using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsArcProjectileMotion : TacticsAbilityProjectileMotion
{
    [Header("Flight")]
    [SerializeField, Min(0.01f)] private float travelUnitsPerSecond = 8f;
    [SerializeField, Min(0f)] private float arcHeight = 0.15f;
    [SerializeField, Min(0f)] private float arrivalPause = 0.02f;
    [SerializeField] private bool orientToVelocity = true;

    public override IEnumerator Play(TacticsAbilityProjectile projectile, TacticsAbilityProjectileFlight flight)
    {
        if (projectile == null)
        {
            yield break;
        }

        Vector3 start = flight.StartWorldPosition + projectile.LaunchOffset;
        Vector3 end = flight.EndWorldPosition + projectile.ImpactOffset;

        projectile.ResetVisualState(start);
        projectile.ClearTrail();

        float duration = GetTravelDuration(start, end, travelUnitsPerSecond);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPosition = Vector3.Lerp(start, end, t);
            nextPosition.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            Vector3 velocity = nextPosition - projectile.transform.position;
            projectile.transform.position = nextPosition;

            if (orientToVelocity)
            {
                projectile.FaceVelocity(velocity);
            }

            yield return null;
        }

        projectile.transform.position = end;

        if (arrivalPause > 0f)
        {
            yield return new WaitForSeconds(arrivalPause);
        }

        projectile.BeginCleanup();
    }
}
