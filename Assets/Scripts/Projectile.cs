using UnityEngine;

/// <summary>
/// Proyectil simple estilo Mario Kart: viaja en línea recta a velocidad
/// constante en la dirección en la que fue lanzado (no le afecta la
/// gravedad ni la física del kart que lo disparó).
///
/// SETUP EN EL EDITOR:
/// 1. Creá un GameObject para el proyectil (una esfera, un cono, o tu
///    modelo de caparazón/misil). Escalalo a un tamaño razonable (chico).
/// 2. Agregale un Rigidbody: Use Gravity = DESTILDADO (para que vaya recto
///    sin caer), Collision Detection = Continuous (para que no atraviese
///    paredes finas a alta velocidad).
/// 3. Agregale un Collider marcado como "Is Trigger" SI querés que atraviese
///    sin frenar (y detectar el golpe por script), o sin trigger si querés
///    que la física choque de verdad. Este script usa OnTriggerEnter, así
///    que dejalo tildado como Trigger.
/// 4. Agregale este script "Projectile.cs".
/// 5. Convertí ese GameObject en un Prefab (arrastralo a la carpeta
///    Assets/Prefabs) y borralo de la escena. Ese Prefab es lo que
///    vas a asignar en KartShooter.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float speed = 40f;
    [SerializeField] private float lifeTime = 5f; // se autodestruye si no choca con nada antes de esto

    [Header("Daño / impacto (opcional)")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private GameObject impactVFXPrefab; // partícula de explosión al chocar (opcional)

    [Header("Quién lo disparó")]
    [SerializeField] private GameObject owner; // se asigna desde KartShooter al instanciar, para no autoimpactarse

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        // Dispara en línea recta hacia donde apunta el transform (su "forward").
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime);
    }

    /// <summary>Llamado por KartShooter justo después de instanciar el proyectil.</summary>
    public void SetOwner(GameObject shooter)
    {
        owner = shooter;
    }

    private void OnTriggerEnter(Collider other)
    {
        // No impactar contra quien lo disparó.
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            return;

        // Ignorar otros proyectiles entre sí.
        if (other.GetComponent<Projectile>() != null)
            return;

        // Acá enganchás tu sistema de vida/daño si lo tenés, por ejemplo:
        // var health = other.GetComponentInParent<KartHealth>();
        // if (health != null) health.TakeDamage(damage);

        if (impactVFXPrefab != null)
        {
            Instantiate(impactVFXPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}