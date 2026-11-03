using System.Collections;
using UnityEngine;

/// <summary>
/// Ítem flotante que entrega un power-up al kart que lo toca. UN solo
/// script sirve para todas las armas: en el Inspector elegís el tipo
/// (Cannon, Mine...) y la cantidad de usos.
/// Reemplaza a AmmoPickup y MinePickup.
///
/// Se configura solo: fuerza el Collider a Trigger y agrega un Rigidbody
/// kinemático si falta.
///
/// SETUP:
///  1. Creá un objeto con el aspecto del ítem (un cubo chico, una caja).
///  2. Add Component > "Power Up Pickup" y elegí el Type.
///  3. Hacelo Prefab y repartilo por el mapa. Para otro tipo de arma,
///     duplicá el prefab y cambiá el Type (y el color del material).
/// </summary>
public class PowerUpPickup : MonoBehaviour
{
    [Header("Ítem")]
    [SerializeField] private PowerUpType type = PowerUpType.Cannon;
    [SerializeField] private int amount = 3;
    [SerializeField] private float respawnTime = 10f;
    [SerializeField] private float triggerRadius = 1.5f; // solo si no tiene Collider propio

    [Header("Efecto visual (flotar y girar)")]
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField] private float bobAmplitude = 0.2f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("Feedback (opcional)")]
    [SerializeField] private GameObject pickupVFXPrefab;
    [SerializeField] private AudioClip pickupSound;

    private Vector3 startLocalPos;
    private bool collected;
    private Collider col;
    private Renderer[] renderers;

    private void Awake()
    {
        startLocalPos = transform.localPosition;

        col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = triggerRadius;
            col = sphere;
        }
        col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        if (collected) return;

        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float y = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.localPosition = startLocalPos + new Vector3(0f, y, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || other.isTrigger) return;

        KartPowerUpHolder holder = other.GetComponentInParent<KartPowerUpHolder>();
        if (holder == null) return; // no es un kart con inventario

        if (!holder.Collect(type, amount)) return; // el kart no soporta este power-up

        if (pickupVFXPrefab != null)
            Instantiate(pickupVFXPrefab, transform.position, Quaternion.identity);
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        StartCoroutine(HideAndRespawn());
    }

    private IEnumerator HideAndRespawn()
    {
        collected = true;
        SetVisible(false);
        yield return new WaitForSeconds(respawnTime);
        collected = false;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (col != null) col.enabled = visible;
        foreach (var r in renderers) r.enabled = visible;
    }
}