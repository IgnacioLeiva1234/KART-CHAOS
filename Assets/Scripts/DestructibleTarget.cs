
using System;
using UnityEngine;

/// <summary>
/// Gestiona los impactos que recibe un kart y avisa a la barra de vida.
/// También permite registrar impactos desde otras armas, como las minas.
/// </summary>
public class DestructibleTarget : MonoBehaviour
{
    [Header("Vida por impactos")]
    [Tooltip("Cantidad de impactos necesarios para destruir el kart.")]
    [SerializeField] private int hitsToDestroy = 7;

    [Tooltip("Tiempo de invulnerabilidad después de cada impacto.")]
    [SerializeField] private float invulnerabilityAfterHit = 0.15f;

    [Header("Feedback (opcional)")]
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private GameObject destroyVFXPrefab;

    private int hitsTaken;
    private float invulnerabilityTimer;
    private bool isDestroyed;

    public event Action<int, int> OnHitsChanged;
    public event Action OnDestroyed;

    public int HitsRemaining =>
        Mathf.Max(0, hitsToDestroy - hitsTaken);

    public int MaxHits => hitsToDestroy;

    // Raíz del kart al que pertenece este componente.
    public Transform KartRoot => transform.root;

    private void Update()
    {
        if (invulnerabilityTimer > 0f)
        {
            invulnerabilityTimer -= Time.deltaTime;
        }
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
        if (isDestroyed || invulnerabilityTimer > 0f)
            return;

        // Busca el proyectil en el objeto que chocó o en sus padres.
        Projectile projectile =
            hitBy.GetComponentInParent<Projectile>();

        if (projectile == null)
            return;

        RegisterHit();
    }

    /// <summary>
    /// Registra un impacto desde un proyectil, una mina u otra arma.
    /// </summary>
    public void RegisterHit()
    {
        if (isDestroyed || invulnerabilityTimer > 0f)
            return;

        hitsTaken++;
        invulnerabilityTimer = invulnerabilityAfterHit;

        // Actualiza la barra de vida.
        OnHitsChanged?.Invoke(HitsRemaining, hitsToDestroy);

        Debug.Log(
            $"{KartRoot.name} recibió un impacto. " +
            $"Vida: {HitsRemaining}/{hitsToDestroy}"
        );

        if (hitVFXPrefab != null)
        {
            Instantiate(
                hitVFXPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        if (HitsRemaining <= 0)
        {
            isDestroyed = true;

            // Avisa a la barra antes de destruir el kart.
            OnDestroyed?.Invoke();

            if (destroyVFXPrefab != null)
            {
                Instantiate(
                    destroyVFXPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }

            // Destruye el kart completo, no solo este componente auxiliar.
            Destroy(KartRoot.gameObject);
        }
    }
}
