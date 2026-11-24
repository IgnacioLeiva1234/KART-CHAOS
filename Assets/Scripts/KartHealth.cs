using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Compatibilidad para referencias antiguas a KartHealth.
/// DestructibleTarget es el único contador de vida y gestiona la reaparición.
/// </summary>
[RequireComponent(typeof(DestructibleTarget))]
public class KartHealth : MonoBehaviour
{
    [FormerlySerializedAs("maxHealth")]
    [Tooltip("Escala usada por los antiguos valores de daño: 100 equivalen a toda la vida.")]
    [SerializeField] private int legacyDamageScale = 100;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;
    public event Action OnRespawn;

    private DestructibleTarget health;
    private bool wasAlive;

    public int CurrentHealth => Health != null ? Health.CurrentHealth : 0;
    public int MaxHealth => Health != null ? Health.MaxHealth : 0;
    public bool IsDead => Health == null || !Health.IsAlive;
    public bool IsInvulnerable => Health != null && Health.IsProtected;

    private DestructibleTarget Health
    {
        get
        {
            if (health == null) health = GetComponent<DestructibleTarget>();
            return health;
        }
    }

    private void Awake() { health = GetComponent<DestructibleTarget>(); }
    private void OnEnable()
    {
        if (Health == null) return;
        wasAlive = Health.IsAlive;
        Health.OnHealthChanged += ForwardHealthChanged;
    }
    private void Start() { wasAlive = Health != null && Health.IsAlive; }
    private void OnDisable()
    {
        if (health != null) health.OnHealthChanged -= ForwardHealthChanged;
    }

    public void TakeDamage(float amount, GameObject instigator = null)
    {
        if (Health == null || amount <= 0f || IsDead || IsInvulnerable) return;
        int hits = Mathf.CeilToInt(amount * Health.MaxHealth / Mathf.Max(1, legacyDamageScale));
        var combat = GetComponent<KartCombat>();
        if (combat != null) combat.RequestDamage(hits);
        else for (int i = 0; i < hits && Health.IsAlive; i++) Health.TakeHit();
    }

    public void Heal(int amount)
    {
        if (Health == null || amount <= 0) return;
        Health.HealHits(Mathf.CeilToInt(amount * Health.MaxHealth / (float)Mathf.Max(1, legacyDamageScale)));
    }

    /// <summary>La muerte ya programa la reaparición en DestructibleTarget; no inicia otra rutina.</summary>
    public void Respawn()
    {
        if (Health != null && Health.IsAlive) Health.HealHits(Health.MaxHealth);
    }

    private void ForwardHealthChanged(int current, int maximum)
    {
        bool alive = current > 0;
        OnHealthChanged?.Invoke(current, maximum);
        if (wasAlive && !alive) OnDeath?.Invoke();
        else if (!wasAlive && alive) OnRespawn?.Invoke();
        wasAlive = alive;
    }
}
