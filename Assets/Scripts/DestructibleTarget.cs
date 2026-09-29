using System;
using UnityEngine;

/// <summary>
/// Le hacen falta VARIOS impactos (no uno solo) para destruirlo. Expone
/// eventos para que una barra de vida (como KartHealthBarWorld.cs) se
/// actualice sola cada vez que recibe un golpe.
///
/// SETUP:
/// 1. Poné este componente en CADA kart del juego (el tuyo y los de los
///    demás jugadores), para que todos puedan recibir y hacer daño por igual.
/// 2. Confirmá que el kart tenga un Collider (el que sea). No hace falta
///    que sea Trigger, este script detecta ambos casos.
/// 3. Add Component > "Destructible Target".
/// 4. Ajustá "Hits To Destroy" al número de impactos que querés (default 7).
/// </summary>
public class DestructibleTarget : MonoBehaviour
{
    [Header("Vida por impactos")]
    [Tooltip("Cuántos disparos hacen falta para destruirlo.")]
    [SerializeField] private int hitsToDestroy = 7;
    [Tooltip("Segundos de invulnerabilidad tras cada golpe, para que dos proyectiles del mismo instante no cuenten como dos hits.")]
    [SerializeField] private float invulnerabilityAfterHit = 0.15f;

    [Header("Feedback (opcional)")]
    [SerializeField] private GameObject hitVFXPrefab;     // chispazo chico en cada golpe (no en la destrucción final)
    [SerializeField] private GameObject destroyVFXPrefab; // explosión grande al llegar a 0

    private int hitsTaken;
    private float invulnerabilityTimer;

    /// <summary>current = hits que le quedan por aguantar, max = hitsToDestroy. Para que la UI se dibuje.</summary>
    public event Action<int, int> OnHitsChanged;
    public event Action OnDestroyed;

    public int HitsRemaining => Mathf.Max(0, hitsToDestroy - hitsTaken);
    public int MaxHits => hitsToDestroy;

    private void Update()
    {
        if (invulnerabilityTimer > 0f) invulnerabilityTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandleHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHandleHit(collision.gameObject);
    }

    private void TryHandleHit(GameObject hitBy)
    {
        if (invulnerabilityTimer > 0f) return;

        // Solo reacciona si lo que lo tocó es un proyectil.
        Projectile proj = hitBy.GetComponent<Projectile>();
        if (proj == null) return;

        hitsTaken++;
        invulnerabilityTimer = invulnerabilityAfterHit;

        OnHitsChanged?.Invoke(HitsRemaining, hitsToDestroy);

        if (hitVFXPrefab != null)
        {
            Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);
        }

        if (hitsTaken >= hitsToDestroy)
        {
            OnDestroyed?.Invoke();

            if (destroyVFXPrefab != null)
            {
                Instantiate(destroyVFXPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }
}