using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PowerUpPickup : MonoBehaviour
{
    [Header("Ítem")]
    [SerializeField] private string[] powerUpReferenceNames = {"Bomb", "Molotov", "Turret", "LandMine"}; //Nombre de los power ups, va en la lista de power ups asignados.
    /*
    No ponemos GameObjects como elementos del array porque no es necesario, tenemos que usar un string o numero que haga...
    //referencia a el gameObject que luego vamos a hacer aparecer en el otro script.
    */
    [SerializeField] private List<GameObject> toBeAssignedPowerUps = new List <GameObject>(); //Listado de nombres de refcia. ↑ para mostrar los powerups en la UI
    [SerializeField] private int amount = 6;
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
        toBeAssignedPowerUps = new List <GameObject>(powerUpReferenceNames ["Bomb", "Molotov", "LandMine", "Turret"]);

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

        string elegido = powerUpReferenceNames[Random.Range (0, powerUpReferenceNames.Length)];

        if (!holder.Collect(elegido, amount)) return;

        Debug.Log ("El power up elegido es " + elegido);

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