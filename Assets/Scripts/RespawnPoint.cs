using System.Collections.Generic;
using System;
using Photon.Pun;
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

        List<RespawnPoint> points = GetStablePoints();
        Dictionary<int, int> reservations = GetActiveReservations();
        int actor = PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        for (int index = 0; index < points.Count; index++)
        {
            RespawnPoint point = points[index];
            if (ReservedForOther(reservations, actor, index)) continue;

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
            if (UnityEngine.Random.Range(0, freeCount) == 0) selected = point;
        }

        // Si todos están ocupados, usamos el de mayor separación disponible.
        return selected != null ? selected : farthest;
    }

    public static List<RespawnPoint> GetStablePoints()
    {
        var points = All.FindAll(p => p != null && p.isActiveAndEnabled);
        points.Sort((a, b) => string.CompareOrdinal(HierarchyKey(a.transform), HierarchyKey(b.transform)));
        return points;
    }

    private static string HierarchyKey(Transform node)
    {
        string key = node.name + ":" + node.GetSiblingIndex();
        while (node.parent != null)
        {
            node = node.parent;
            key = node.name + ":" + node.GetSiblingIndex() + "/" + key;
        }
        return key;
    }

    public static Dictionary<int, int> GetActiveReservations()
    {
        string saved = PhotonNetwork.InRoom
            ? PhotonNetwork.CurrentRoom.CustomProperties[SpawnReservations.Property] as string : null;
        Dictionary<int, int> result = SpawnReservations.Parse(saved);
        var departed = new List<int>();
        foreach (int actor in result.Keys)
        {
            var player = PhotonNetwork.CurrentRoom?.GetPlayer(actor);
            if (player == null || player.IsInactive) departed.Add(actor);
        }
        foreach (int actor in departed) result.Remove(actor);
        return result;
    }

    public static bool IsFree(RespawnPoint point)
    {
        foreach (DestructibleTarget other in DestructibleTarget.All)
        {
            if (other == null || !other.isActiveAndEnabled || !other.IsAlive) continue;
            if ((point.transform.position - other.KartTransform.position).sqrMagnitude < point.clearanceRadius * point.clearanceRadius)
                return false;
        }
        return true;
    }

    public static bool ReservedForOther(Dictionary<int, int> reservations, int actor, int index)
    {
        foreach (var entry in reservations)
            if (entry.Key != actor && entry.Value == index) return true;
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.7f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2.5f);
        Gizmos.DrawSphere(transform.position + transform.forward * 2.5f, 0.2f);
    }
}

// Datos pequeños de sala: la reserva se confirma con CAS antes de crear el kart.
// El algoritmo no usa Unity.Random, por lo que cada cliente obtiene el mismo orden.
public static class SpawnReservations
{
    public const string Property = "spawnReservations";
    public const string SeedProperty = "spawnSeed";

    public static Dictionary<int, int> Parse(string value)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrEmpty(value)) return result;
        foreach (string item in value.Split(';'))
        {
            string[] pair = item.Split(':');
            if (pair.Length == 2 && int.TryParse(pair[0], out int actor) && actor > 0 &&
                int.TryParse(pair[1], out int point) && point >= 0) result[actor] = point;
        }
        return result;
    }

    public static string Encode(Dictionary<int, int> reservations)
    {
        var actors = new List<int>(reservations.Keys);
        actors.Sort();
        var entries = new List<string>();
        foreach (int actor in actors) entries.Add(actor + ":" + reservations[actor]);
        return string.Join(";", entries);
    }

    public static int[] Order(int count, int seed)
    {
        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        var random = new System.Random(seed);
        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int swap = order[i]; order[i] = order[j]; order[j] = swap;
        }
        return order;
    }
}
