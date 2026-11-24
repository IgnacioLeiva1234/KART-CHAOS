using System.Collections.Generic;
using UnityEngine;

/// <summary>Zona de fuego local de un lanzamiento compartido. No suma daño por cada rueda del mismo kart.</summary>
public class MolotovFireArea : MonoBehaviour
{
    private GameObject owner, flames;
    private float radius, burnDuration, interval, endsAt;
    private readonly HashSet<DestructibleTarget> targets = new HashSet<DestructibleTarget>();
    public float Radius => radius;
    public void Initialize(Vector3 position, Vector3 normal, GameObject shooter, GameObject flamePrefab,
        float areaRadius, float lifetime, float burningSeconds, float tickInterval)
    {
        transform.position = position + normal * .03f;
        owner = shooter;
        flames = flamePrefab;
        radius = areaRadius;
        burnDuration = burningSeconds;
        interval = tickInterval;
        endsAt = Time.time + lifetime;
        var sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;
        var body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        if (flames != null)
        {
            Quaternion facing = Quaternion.LookRotation(normal);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * .5f;
                Vector3 offset = i == 4 ? Vector3.zero : Quaternion.FromToRotation(Vector3.up, normal) * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * .5f;
                var effect = Instantiate(flames, transform.position + offset, facing, transform);
                effect.transform.localScale = Vector3.one * .5f;
                foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    ps.Play();
                }
            }
        }
    }
    private void FixedUpdate()
    {
        if (Time.time >= endsAt) { Destroy(gameObject); return; }
        targets.Clear();
        foreach (var col in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = DestructibleTarget.Find(col.transform);
            if (target != null && targets.Add(target))
                PowerUpDamage.Ignite(target, owner, flames, burnDuration, interval, 1);
        }
    }
}
