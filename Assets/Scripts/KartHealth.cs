using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sistema de vida del kart. Recibe daño de los proyectiles (u otras
/// fuentes), tiene invulnerabilidad breve tras cada golpe para que no te
/// maten con dos disparos pegados en el mismo frame, y maneja la muerte +
/// respawn.
///
/// SETUP EN EL EDITOR:
/// 1. Seleccioná el GameObject "Kart" (el que tiene KartController).
/// 2. Add Component > "Kart Health".
/// 3. (Opcional) Creá un GameObject vacío en el punto donde querés que
///    reaparezca el kart al morir, renombralo "RespawnPoint", y arrastralo
///    al campo "Respawn Point". Si lo dejás vacío, respawnea en el mismo
///    lugar donde murió.
/// </summary>
public class KartHealth : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invulnerabilityDuration = 1f; // segundos de "escudo" tras cada golpe

    [Header("Muerte / Respawn")]
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private Transform respawnPoint; // opcional; si es null, respawnea en el lugar donde murió

    [Header("Feedback (opcional)")]
    [SerializeField] private GameObject deathVFXPrefab;
    [SerializeField] private GameObject hitVFXPrefab; // partícula chiquita en cada golpe (no en la muerte)

    [Header("Vida (solo lectura, para ver en Play)")]
    [SerializeField] private int currentHealth;
    private float invulnerabilityTimer;
    private KartController kartController;
    private KartShooter kartShooter;
    private Rigidbody rb;

    /// <summary>current, max — para que un HUD dibuje una barra de vida.</summary>
    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;
    public event Action OnRespawn;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => invulnerabilityTimer > 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
        kartController = GetComponent<KartController>();
        kartShooter = GetComponent<KartShooter>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (invulnerabilityTimer > 0f)
        {
            invulnerabilityTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Aplica daño al kart. "instigator" es quién disparó (para que en un
    /// futuro puedas sumar kills/puntaje a ese jugador); puede ser null.
    /// </summary>
    public void TakeDamage(float amount, GameObject instigator = null)
    {
        if (IsDead || IsInvulnerable) return;

        currentHealth = Mathf.Max(0, currentHealth - Mathf.RoundToInt(amount));
        invulnerabilityTimer = invulnerabilityDuration;

        Debug.Log(gameObject.name + " recibió " + amount + " de daño. Vida actual: " + currentHealth); // borrar esta línea cuando confirmemos que anda

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (hitVFXPrefab != null)
        {
            Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();

        if (deathVFXPrefab != null)
        {
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        }

        // Apagar control y física mientras está "muerto", para que no
        // se pueda seguir manejando ni disparando.
        if (kartController != null) kartController.enabled = false;
        if (kartShooter != null) kartShooter.enabled = false;
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // Ocultar el modelo visual (todos los renderers hijos) sin
        // desactivar este GameObject (si lo desactivás, Update() deja de
        // correr y el respawn nunca se dispara).
        SetVisualsActive(false);

        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    public void Respawn()
    {
        currentHealth = maxHealth;
        IsDead = false;
        invulnerabilityTimer = invulnerabilityDuration; // un respiro al reaparecer

        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        SetVisualsActive(true);
        if (kartController != null) kartController.enabled = true;
        if (kartShooter != null) kartShooter.enabled = true;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnRespawn?.Invoke();
    }

    private void SetVisualsActive(bool active)
    {
        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = active;
        }
    }
}