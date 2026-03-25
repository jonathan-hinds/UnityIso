using System.Collections;
using UnityEngine;

public abstract class TacticsAbilityProjectileMotion : MonoBehaviour
{
    public abstract IEnumerator Play(TacticsAbilityProjectile projectile, TacticsAbilityProjectileFlight flight);

    protected static float GetTravelDuration(Vector3 start, Vector3 end, float travelUnitsPerSecond)
    {
        float distance = Vector3.Distance(start, end);
        return distance <= 0.001f ? 0f : distance / Mathf.Max(0.01f, travelUnitsPerSecond);
    }
}
