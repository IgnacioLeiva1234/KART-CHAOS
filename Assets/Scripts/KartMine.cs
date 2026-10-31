
using System.Collections;
using UnityEngine;

public class KartMine : MonoBehaviour
{
    [SerializeField] private float armingDelay = 1f;
    [SerializeField] private float lifetime = 20f;

    private Transform owner;
    private bool armed;
    private bool exploded;

    public void SetOwner(Transform kartOwner)
    {
        owner = kartOwner;
        StartCoroutine(ArmMine());
    }

    private IEnumerator ArmMine()
    {
        yield return new WaitForSeconds(armingDelay);
        armed = true;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!armed || exploded)
            return;

        DestructibleTarget target =
            other.GetComponentInParent<DestructibleTarget>();

        if (target == null)
            return;

        if (owner != null &&
            target.transform.root == owner.root)
            return;

        exploded = true;
        target.RegisterHit();

        Debug.Log($"¡La mina golpeó a {target.transform.root.name}!");
        Destroy(gameObject);
    }
}
