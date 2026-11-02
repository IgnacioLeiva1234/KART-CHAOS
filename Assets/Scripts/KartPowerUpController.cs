
using UnityEngine;

public class KartPowerUpController : MonoBehaviour
{
    [Header("Puntos de uso")]
    [SerializeField] private Transform cannonFirePoint;
    [SerializeField] private Transform mineDropPoint;

    [Header("Prefabs")]
    [SerializeField] private GameObject cannonProjectilePrefab;
    [SerializeField] private GameObject minePrefab;

    [Header("Teclas")]
    [SerializeField] private KeyCode cannonKey = KeyCode.E;
    [SerializeField] private KeyCode mineKey = KeyCode.Q;

    [Header("Configuración")]
    [SerializeField] private float cannonSpeed = 35f;
    [SerializeField] private int maxCannonAmmo = 3;
    [SerializeField] private int maxMines = 3;

    private int cannonAmmo;
    private int mines;

    public int CannonAmmo => cannonAmmo;
    public int Mines => mines;

    public void CollectCannon()
    {
        if (cannonAmmo >= maxCannonAmmo)
            return;

        cannonAmmo++;
        Debug.Log($"{name}: cañón recogido. Munición: {cannonAmmo}");
    }

    public void CollectMine()
    {
        if (mines >= maxMines)
            return;

        mines++;
        Debug.Log($"{name}: mina recogida. Minas: {mines}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(cannonKey))
            UseCannon();

        if (Input.GetKeyDown(mineKey))
            UseMine();
    }

    private void UseCannon()
    {
        if (cannonAmmo <= 0)
        {
            Debug.Log("No tenés munición de cañón.");
            return;
        }

        if (cannonProjectilePrefab == null ||
            cannonFirePoint == null)
        {
            Debug.LogError(
                $"{name}: asigná Cannon Projectile Prefab y Cannon Fire Point.",
                this);
            return;
        }

        GameObject projectile = Instantiate(
            cannonProjectilePrefab,
            cannonFirePoint.position,
            cannonFirePoint.rotation);

        Rigidbody rb = projectile.GetComponent<Rigidbody>();

        if (rb != null)
            rb.linearVelocity = cannonFirePoint.forward * cannonSpeed;

        cannonAmmo--;
        Debug.Log($"{name}: cañón utilizado. Munición restante: {cannonAmmo}");
    }

    private void UseMine()
    {
        if (mines <= 0)
        {
            Debug.Log("No tenés minas.");
            return;
        }

        if (minePrefab == null || mineDropPoint == null)
        {
            Debug.LogError(
                $"{name}: asigná Mine Prefab y Mine Drop Point.",
                this);
            return;
        }

        GameObject mine = Instantiate(
            minePrefab,
            mineDropPoint.position,
            mineDropPoint.rotation);

        KartMine mineScript = mine.GetComponent<KartMine>();

        if (mineScript != null)
            mineScript.SetOwner(transform.root);

        mines--;
        Debug.Log($"{name}: mina colocada. Minas restantes: {mines}");
    }
}
