
using UnityEngine;

public class PowerUpPickup : MonoBehaviour
{
    public enum PowerUpType
    {
        Cannon,
        Mine
    }

    [SerializeField] private PowerUpType powerUpType;

    private bool collected;

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        KartPowerUpController controller =
            other.GetComponentInParent<KartPowerUpController>();

        if (controller == null)
            return;

        if (powerUpType == PowerUpType.Cannon)
            controller.CollectCannon();
        else
            controller.CollectMine();

        collected = true;
        Destroy(gameObject);
    }
}
