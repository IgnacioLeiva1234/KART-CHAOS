using UnityEngine;

/// <summary>Una pulsación inicia una ráfaga. El inventario descuenta cada proyectil que sale.</summary>
public class Turret : KartPowerUp
{
    [Header("Torreta")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(.02f)] private float shotInterval = .12f;
    [SerializeField, Min(1)] private int pickupAmmoMultiplier = 10;

    [Header("Feedback opcional")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    public override PowerUpType Type => PowerUpType.Turret;
    public override string DisplayName => "Torreta";
    public override bool IsReady => firePoint != null && projectilePrefab != null;
    // Una pulsación vacía toda la munición disponible, hasta el máximo del inventario.
    public override int ShotsPerUse => MaxAmmo;
    public override float ShotInterval => Mathf.Max(.02f, shotInterval);
    public override int PickupAmmoMultiplier => Mathf.Max(1, pickupAmmoMultiplier);

    public override bool Use(GameObject owner)
    {
        if (!IsReady || OnCooldown || owner == null || !PowerUpDamage.IsLocallyControlled(owner)) return false;
        var health = owner.GetComponent<DestructibleTarget>();
        if (health != null && !health.IsAlive) return false;
        var shot = PowerUpNetwork.Spawn(projectilePrefab, firePoint.position, firePoint.rotation, owner);
        if (shot == null) return false;
        StartCooldown(ShotInterval);
        if (muzzleFlash != null) muzzleFlash.Play();
        if (audioSource != null && fireSound != null) audioSource.PlayOneShot(fireSound);
        return true;
    }
}
