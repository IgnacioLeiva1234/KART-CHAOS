using Photon.Pun;
using UnityEngine;

/// <summary>Cohete recto. Solo su dueño detecta el impacto; el daño llega al dueño del kart alcanzado.</summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : NetworkHazard
{
    [SerializeField] private float speed = 40f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float collisionRadius = .25f;
    [SerializeField] private GameObject impactVFXPrefab;
    [SerializeField, Min(.1f)] private float impactVFXLifetime = 4f;
    private Vector3 origin, velocity, previousPosition;
    private bool hasHit;
    private bool impactRequested;

    protected override void Awake()
    {
        base.Awake();
        var body = GetComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = true;
        body.useGravity = false;
    }
    protected override void OnSpawnData(object[] payload)
    {
        origin = previousPosition = transform.position;
        velocity = transform.forward * speed;
    }
    private void FixedUpdate()
    {
        if (!IsInitialized || hasHit || impactRequested) return;
        Vector3 position = origin + velocity * Age;
        if (HasAuthority && MolotovTrajectory.FirstHit(previousPosition, position, collisionRadius, Owner, transform, out var hit))
        {
            impactRequested = true;
            var target = DestructibleTarget.Find(hit.collider.transform);
            PowerUpDamage.RequestHits(target, 1, Owner);
            if (IsNetworked) NetworkView.RPC(nameof(ShowImpact), RpcTarget.All, hit.point);
            else ShowImpact(hit.point, default);
            Despawn(.1f);
            return;
        }
        transform.position = previousPosition = position;
        if (HasAuthority && Age >= lifeTime) { hasHit = true; Despawn(); }
    }
    [PunRPC]
    private void ShowImpact(Vector3 position, PhotonMessageInfo info)
    {
        if (hasHit || !ValidSender(info)) return;
        hasHit = true;
        foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        if (impactVFXPrefab != null)
        {
            var effect = Instantiate(impactVFXPrefab, position, Quaternion.identity);
            effect.SetActive(true);
            float duration = Mathf.Max(.1f, impactVFXLifetime);
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.loop = false;
                duration = Mathf.Max(duration, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
                particles.gameObject.SetActive(true);
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play(false);
            }
            Destroy(effect, duration);
        }
    }
}
