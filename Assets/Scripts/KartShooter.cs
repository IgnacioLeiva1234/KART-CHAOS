using UnityEngine;

/// <summary>
/// Dispara un proyectil recto (estilo Mario Kart) al apretar la BARRA
/// ESPACIADORA. Se pone como componente aparte, al lado de KartController,
/// en el mismo GameObject "Kart" (o en cualquier hijo).
///
/// SETUP EN EL EDITOR:
/// 1. Creá un GameObject vacío como hijo de "Kart" (o "PrototipoAuto"),
///    ubicado en la punta delantera del kart, mirando hacia adelante.
///    Renombralo "FirePoint". Es el punto exacto donde va a "nacer" el
///    proyectil.
/// 2. Seleccioná "Kart" y agregale este script "KartShooter.cs".
/// 3. En el Inspector: arrastrá "FirePoint" al campo "Fire Point", y
///    arrastrá el Prefab del proyectil (el que armaste con Projectile.cs)
///    al campo "Projectile Prefab".
/// 4. Listo. Al apretar Espacio en Play, va a instanciar el proyectil en
///    FirePoint, apuntando hacia donde mira FirePoint.
/// </summary>
public class KartShooter : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punto exacto de donde sale el proyectil. Su 'forward' (eje Z azul) define la dirección del disparo.")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Disparo")]
    [Tooltip("Segundos mínimos entre un disparo y el siguiente. Evita que se pueda ametrallar tocando Espacio muy rápido.")]
    [SerializeField] private float fireCooldown = 0.6f;
    [Tooltip("Cuántos proyectiles puede tener guardados el jugador antes de tener que recargar/recoger otro. Poné 999 si no querés límite.")]
    [SerializeField] private int ammoCount = 999;

    [Header("Feedback (opcional)")]
    [SerializeField] private ParticleSystem muzzleFlash; // destello al disparar
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    private float cooldownTimer;

    /// <summary>Por si un HUD quiere mostrar cuántos proyectiles quedan.</summary>
    public int AmmoCount => ammoCount;
    public bool CanFire => cooldownTimer <= 0f && ammoCount != 0;

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Space) && CanFire)
        {
            Fire();
        }
    }

    private void Fire()
    {
        if (firePoint == null || projectilePrefab == null)
        {
            Debug.LogWarning("KartShooter: falta asignar Fire Point o Projectile Prefab en el Inspector.");
            return;
        }

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        // Le avisamos al proyectil quién lo disparó, para que no se autoimpacte.
        Projectile projScript = proj.GetComponent<Projectile>();
        if (projScript != null)
        {
            projScript.SetOwner(gameObject);
        }

        cooldownTimer = fireCooldown;

        if (ammoCount > 0) ammoCount--; // si ammoCount es 999 o negativo, tratalo como "infinito" y no lo descuentes

        if (muzzleFlash != null) muzzleFlash.Play();
        if (audioSource != null && fireSound != null) audioSource.PlayOneShot(fireSound);
    }
}