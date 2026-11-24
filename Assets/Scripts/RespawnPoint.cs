using System.Collections.Generic;
using UnityEngine;

/// <summary>Lugar de aparición. Se elige al azar entre los puntos libres.</summary>
public class RespawnPoint : MonoBehaviour
{
    public static readonly List<RespawnPoint> All = new List<RespawnPoint>();

    [Tooltip("Distancia mínima a otro kart vivo para considerar libre este punto.")]
    [Min(0f)] public float clearanceRadius = 4f;

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    // Conserva la llamada utilizada por DestructibleTarget al reaparecer.
    public static RespawnPoint GetBest(DestructibleTarget who)
    {
        return GetRandom(who);
    }

    public static RespawnPoint GetRandom(DestructibleTarget who = null)
    {
        RespawnPoint selected = null;
        RespawnPoint farthest = null;
        float farthestDistance = -1f;
        int freeCount = 0;

        foreach (RespawnPoint point in All)
        {
            if (point == null || !point.isActiveAndEnabled) continue;

            float nearestSquared = float.PositiveInfinity;
            foreach (DestructibleTarget other in DestructibleTarget.All)
            {
                if (other == null || other == who || !other.isActiveAndEnabled || !other.IsAlive) continue;
                float distanceSquared = (point.transform.position - other.KartTransform.position).sqrMagnitude;
                nearestSquared = Mathf.Min(nearestSquared, distanceSquared);
            }

            if (nearestSquared > farthestDistance)
            {
                farthestDistance = nearestSquared;
                farthest = point;
            }

            if (nearestSquared < point.clearanceRadius * point.clearanceRadius) continue;
            // Selección uniforme sin crear una lista nueva en cada aparición.
            freeCount++;
            if (Random.Range(0, freeCount) == 0) selected = point;
        }

        // Si todos están ocupados, usamos el de mayor separación disponible.
        return selected != null ? selected : farthest;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.7f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2.5f);
        Gizmos.DrawSphere(transform.position + transform.forward * 2.5f, 0.2f);
    }
}
