using UnityEngine;

/// <summary>
/// Power-up Mina: coloca una mina terrestre en el piso, detrás del kart.
/// Reemplaza al viejo KartMineLayer.
///
/// SETUP (en la raíz del kart):
///  1. Add Component > "Mine Power Up".
///  2. Mine Prefab: el prefab de la mina (el que tiene LandMine).
/// </summary>
public class MinePowerUp : KartPowerUp
{
    [Header("Mina")]
    [SerializeField] private GameObject minePrefab;
    [Tooltip("Qué tan atrás del kart cae la mina.")]
    [SerializeField] private float distanceBehind = 3f;
    [SerializeField] private float placeCooldown = 0.4f;

    [Header("Feedback (opcional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip placeSound;

    public override PowerUpType Type => PowerUpType.Mine;
    public override string DisplayName => "Mina";

    public override bool Use(GameObject owner)
    {
        if (OnCooldown) return false;

        if (minePrefab == null)
        {
            Debug.LogWarning("MinePowerUp: falta asignar el Mine Prefab en el Inspector.");
            return false;
        }

        Vector3 pos = GetGroundPositionBehind(owner.transform);
        GameObject mine = Instantiate(minePrefab, pos, Quaternion.identity);

        LandMine landMine = mine.GetComponent<LandMine>();
        if (landMine != null) landMine.SetOwner(owner);

        StartCooldown(placeCooldown);

        if (audioSource != null && placeSound != null) audioSource.PlayOneShot(placeSound);
        return true;
    }

    // Punto detrás del kart, apoyado sobre el piso.
    private Vector3 GetGroundPositionBehind(Transform kart)
    {
        Vector3 back = -kart.forward;
        back.y = 0f;
        back.Normalize();

        Vector3 pos = kart.position + back * distanceBehind;
        Vector3 origin = pos + Vector3.up * 3f;

        // Rayo hacia abajo; nos quedamos con el primer golpe que NO sea parte del kart.
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(kart)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                pos = h.point;
            }
        }
        return pos;
    }
}