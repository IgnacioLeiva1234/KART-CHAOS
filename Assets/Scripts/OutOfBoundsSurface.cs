using System.Collections.Generic;
using UnityEngine;
using System;
using System.Numerics;

/// <summary>Reaparece el kart al tocar el terreno que está fuera de la ruta.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class OutOfBoundsSurface : MonoBehaviour
{
    // Mantener el contacto permite esperar la protección de aparición aunque
    // el Rigidbody se duerma y Unity deje de enviar OnCollisionStay.
    private readonly Dictionary<Collider, DestructibleTarget> touching = new Dictionary<Collider, DestructibleTarget>();
    private readonly List<Collider> expired = new List<Collider>();
    private readonly List<DestructibleTarget> pending = new List<DestructibleTarget>();

    private void OnCollisionEnter(Collision collision) { Register(collision); }
    private void OnCollisionStay(Collision collision) { Register(collision); }
    private void OnCollisionExit(Collision collision) { touching.Remove(collision.collider); }

    //Para aplicar las físicas a un auto chocado o suspendido en el aire
    private Rigidbody rb;
    private bool withGravity;

    private void Register(Collision collision)
    {
        Collider other = collision.collider;
        Rigidbody body = collision.rigidbody;
        if (other == null || other.isTrigger || body == null) return;
        DestructibleTarget target = DestructibleTarget.Find(body.transform);
        if (target == null || !PowerUpDamage.IsLocallyControlled(target.KartTransform.gameObject)) return;
        touching[other] = target;
        TryRespawn(target);
    }

    private void FixedUpdate()
    {
        expired.Clear();
        pending.Clear();
        foreach (var contact in touching)
        {
            if (contact.Key == null || !contact.Key.enabled || !contact.Key.gameObject.activeInHierarchy ||
                contact.Value == null || !contact.Value.IsAlive)
                expired.Add(contact.Key);
            else
                pending.Add(contact.Value);
        }
        foreach (Collider collider in expired) touching.Remove(collider);
        // Kill desactiva colliders: hacerlo después del recorrido evita que un
        // OnCollisionExit modifique la colección que estamos recorriendo.
        foreach (DestructibleTarget target in pending) TryRespawn(target);
    }

    private static void TryRespawn(DestructibleTarget target)
    {
        if (target.IsAlive && !target.IsProtected && PowerUpDamage.IsLocallyControlled(target.KartTransform.gameObject))
            target.Kill();
    }

    private void OnAirApplyPhysics() {
        //arcoseno del valor del vector del eje x (0,1)

    }

    private void OnDisable() { touching.Clear(); }
}
