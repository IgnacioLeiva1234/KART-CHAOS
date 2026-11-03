using UnityEngine;

/// <summary>
/// Mina terrestre. Una vez colocada y armada, cualquier kart (que no sea
/// el dueño) que la pise es destruido al instante.
///
/// Se configura sola: si le falta un Collider le agrega uno, lo fuerza a
/// Trigger y le agrega un Rigidbody kinemático, así no depende de
/// checkboxes del Inspector.
///
/// SETUP: ver KartMineLayer.cs (la mina se crea como Prefab y se asigna ahí).
/// </summary>
public class LandMine : MonoBehaviour
{
    [Header("Comportamiento")]
    [Tooltip("Segundos hasta que la mina queda armada. Evita matar al instante a quien pasa justo al lado cuando se coloca.")]
    [SerializeField] private float armDelay = 1f;
    [Tooltip("Si está tildado, el dueño también puede morir con su propia mina.")]
    [SerializeField] private bool ownerCanTrigger = false;
    [Tooltip("0 = dura para siempre. Si no, desaparece sola después de estos segundos.")]
    [SerializeField] private float lifeTime = 0f;
    [Tooltip("Radio de activación si la mina no trae un Collider propio.")]
    [SerializeField] private float triggerRadius = 1.2f;

    [Header("Feedback (opcional)")]
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private AudioClip explosionSound;

    private GameObject owner;
    private float armTimer;
    private bool exploded;

    /// <summary>Lo llama KartMineLayer al colocarla.</summary>
    public void SetOwner(GameObject kart)
    {
        owner = kart;
    }

    private void Awake()
    {
        // Collider: si no tiene, le ponemos uno; y siempre trigger.
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = triggerRadius;
            col = sphere;
        }
        col.isTrigger = true;

        // Para que Unity dispare los eventos de trigger contra cualquier kart,
        // necesitamos un Rigidbody. Kinemático: no se mueve ni cae.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        armTimer = armDelay;
    }

    private void Start()
    {
        if (lifeTime > 0f) Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (armTimer > 0f) armTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTrigger(other);
    }

    // Por si un kart ya estaba encima cuando la mina terminó de armarse.
    private void OnTriggerStay(Collider other)
    {
        TryTrigger(other);
    }

    private void TryTrigger(Collider other)
    {
        if (exploded || armTimer > 0f) return;
        if (other.isTrigger) return; // ignorar otros triggers (pickups, proyectiles, etc.)

        // Ignorar proyectiles: la mina no explota con balas.
        if (other.GetComponentInParent<Projectile>() != null) return;

        // Inmunidad del dueño.
        if (!ownerCanTrigger && owner != null &&
            (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
            return;

        DestructibleTarget target = DestructibleTarget.Find(other.transform);
        if (target == null) return; // lo que pisó no es un kart

        if (!ownerCanTrigger && owner != null &&
            (target.transform.IsChildOf(owner.transform) || owner.transform.IsChildOf(target.transform)))
            return;

        exploded = true;
        Debug.Log("¡Mina activada! Eliminando a " + target.name);

        target.Kill();

        if (explosionVFXPrefab != null)
            Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);

        Destroy(gameObject);
    }
}