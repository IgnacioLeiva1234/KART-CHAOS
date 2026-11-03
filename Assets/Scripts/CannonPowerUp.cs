using UnityEngine;

/// <summary>
/// Power-up Cañón: dispara un proyectil recto desde el FirePoint.
/// Reemplaza al viejo KartShooter.
///
/// SETUP (en la raíz del kart):
///  1. Add Component > "Cannon Power Up".
///  2. Fire Point: el objeto vacío en la punta del kart (mirando adelante).
///  3. Projectile Prefab: el prefab del proyectil (el que tiene Projectile).
/// </summary>
public class CannonPowerUp : KartPowerUp
{
    [Header("Cañón")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float fireCooldown = 0.4f;

    [Header("Feedback (opcional)")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    public override PowerUpType Type => PowerUpType.Cannon;
    public override string DisplayName => "Cañón";

    public override bool Use(GameObject owner)
    {
        if (OnCooldown) return false;

        if (firePoint == null || projectilePrefab == null)
        {
            Debug.LogWarning("CannonPowerUp: falta asignar Fire Point o Projectile Prefab en el Inspector.");
            return false;
        }

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        Projectile projectile = proj.GetComponent<Projectile>();
        if (projectile != null) projectile.SetOwner(owner);

        StartCooldown(fireCooldown);

        if (muzzleFlash != null) muzzleFlash.Play();
        if (audioSource != null && fireSound != null) audioSource.PlayOneShot(fireSound);

        return true;
    }
}