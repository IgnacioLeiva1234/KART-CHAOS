using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca un lugar del mapa donde pueden reaparecer los karts al morir.
/// Cuando un kart muere, elige el punto MÁS ALEJADO de los otros karts
/// vivos, para que nadie reaparezca encima de un enemigo.
///
/// SETUP:
///  1. GameObject > Create Empty. Renombralo "RespawnPoint".
///  2. Add Component > "Respawn Point".
///  3. Ubicalo en el mapa, apenas por encima del piso (unos 0.5 de altura).
///     La flecha verde que ves en la Scene indica hacia dónde va a mirar
///     el kart al reaparecer (rotalo con la herramienta de rotación).
///  4. Duplicalo (Ctrl+D) y repartí varios por el mapa.
///
/// Si no hay ningún RespawnPoint en la escena, el kart reaparece en el
/// lugar donde empezó la partida.
/// </summary>
public class RespawnPoint : MonoBehaviour
{
    /// <summary>Todos los puntos activos de la escena.</summary>
    public static readonly List<RespawnPoint> All = new List<RespawnPoint>();

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    /// <summary>
    /// Devuelve el punto más alejado de los demás karts vivos. Si no hay
    /// otros karts, elige uno al azar. Devuelve null si no hay puntos.
    /// </summary>
    public static RespawnPoint GetBest(DestructibleTarget who)
    {
        if (All.Count == 0) return null;

        RespawnPoint best = null;
        float bestScore = -1f;

        foreach (RespawnPoint point in All)
        {
            // Distancia de este punto al kart vivo más cercano.
            float nearest = float.MaxValue;
            foreach (DestructibleTarget other in DestructibleTarget.All)
            {
                if (other == who || !other.IsAlive) continue;
                float d = Vector3.Distance(point.transform.position, other.KartTransform.position);
                if (d < nearest) nearest = d;
            }

            // Sin otros karts, todos valen lo mismo: elegimos al azar.
            float score = nearest == float.MaxValue ? Random.value * 1000f : nearest;

            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    // Para ver los puntos en la ventana Scene.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.7f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2.5f);
        Gizmos.DrawSphere(transform.position + transform.forward * 2.5f, 0.2f);
    }
}