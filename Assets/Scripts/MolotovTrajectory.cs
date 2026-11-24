using UnityEngine;

/// <summary>La vista previa y la botella usan la misma ecuación y la misma detección de obstáculos.</summary>
public static class MolotovTrajectory
{
    public static Vector3 Position(Vector3 origin, Vector3 velocity, Vector3 gravity, float time)
        => origin + velocity * time + gravity * (.5f * time * time);

    public static bool FirstHit(Vector3 from, Vector3 to, float radius, GameObject owner, Transform projectile, out RaycastHit closest)
    {
        closest = default;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .0000001f) return false;
        float best = float.MaxValue;
        foreach (var hit in Physics.SphereCastAll(from, radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;
            if (projectile != null && hit.transform.IsChildOf(projectile)) continue;
            if (hit.collider.GetComponentInParent<NetworkHazard>() != null) continue;
            if (hit.distance >= best) continue;
            best = hit.distance;
            closest = hit;
        }
        return best < float.MaxValue;
    }
}
