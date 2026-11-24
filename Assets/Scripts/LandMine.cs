using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Mina armada en el suelo. Activa Trial+explosion una sola vez y conserva el efecto hasta que termina.</summary>
public class LandMine : NetworkHazard
{
    [Header("Comportamiento")]
    [SerializeField] private float armDelay = 1f;
    [SerializeField] private bool ownerCanTrigger;
    [SerializeField] private float lifeTime;
    [SerializeField] private float triggerRadius = 1.2f;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private int explosionDamage = 3;
    [Header("Explosión")]
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private float effectDuration = 3f;
    [SerializeField] private AudioClip explosionSound;
    public bool HasExploded => exploded;
    private bool exploded;
    private bool explosionRequested;

    protected override void Awake()
    {
        base.Awake();
        if (explosionEffect == null)
        {
            var child = transform.Find("Trial+explosion");
            if (child != null) explosionEffect = child.gameObject;
        }
        if (explosionEffect != null) explosionEffect.SetActive(false);
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = triggerRadius / Mathf.Max(.001f, Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)));
            col = sphere;
        }
        col.isTrigger = true;
        var body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (exploded || explosionRequested || !HasAuthority) return;
        if (lifeTime > 0f && Age >= lifeTime) { exploded = true; Despawn(); return; }
        if (Age < armDelay) return;
        // También detecta las copias cinemáticas de otros jugadores.
        foreach (var col in Physics.OverlapSphere(transform.position + Vector3.up * .2f, triggerRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            TryTrigger(col);
            if (exploded) break;
        }
    }
    private void OnTriggerEnter(Collider other) => TryTrigger(other);
    private void OnTriggerStay(Collider other) => TryTrigger(other);
    private void TryTrigger(Collider other)
    {
        if (exploded || explosionRequested || !HasAuthority || Age < armDelay || other.isTrigger || other.transform.IsChildOf(transform)) return;
        if (!ownerCanTrigger && BelongsToOwner(other.transform)) return;
        var target = DestructibleTarget.Find(other.transform);
        if (target == null || !target.IsAlive || target.IsProtected) return;
        if (!ownerCanTrigger && PowerUpDamage.IsOwner(target, Owner)) return;
        explosionRequested = true;
        Vector3 position = transform.position;
        // La autoridad decide a quién alcanzó la explosión una sola vez. El RPC guardado
        // reproduce únicamente el efecto para quienes entren tarde a la sala.
        var targets = new HashSet<DestructibleTarget>();
        foreach (var col in Physics.OverlapSphere(position, explosionRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var victim = DestructibleTarget.Find(col.transform);
            if (victim != null && targets.Add(victim))
                PowerUpDamage.RequestHits(victim, explosionDamage, ownerCanTrigger ? null : Owner);
        }
        if (IsNetworked) NetworkView.RPC(nameof(Explode), RpcTarget.AllBuffered, position, Now);
        else Explode(position, Now, default);
    }
    [PunRPC]
    private void Explode(Vector3 position, double when, PhotonMessageInfo info)
    {
        if (exploded || !ValidSender(info)) return;
        exploded = true;
        explosionRequested = true;
        foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            if (explosionEffect == null || !renderer.transform.IsChildOf(explosionEffect.transform)) renderer.enabled = false;
        float duration = Mathf.Max(.2f, effectDuration);
        float elapsed = Mathf.Max(0f, (float)(Now - when));
        if (explosionEffect != null)
        {
            var systems = explosionEffect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false;
                duration = Mathf.Max(duration, main.duration + main.startLifetime.constantMax + .2f);
            }
            if (elapsed < duration)
            {
                explosionEffect.SetActive(true);
                foreach (var ps in systems)
                {
                    ps.Simulate(elapsed, false, true);
                    // No reiniciar una ráfaga que ya terminó al recibir el RPC tarde.
                    if (elapsed <= 0f || ps.IsAlive(false)) ps.Play(false);
                }
            }
        }
        if (explosionSound != null && elapsed < .5f) AudioSource.PlayClipAtPoint(explosionSound, position);
        Despawn(Mathf.Max(0f, duration - elapsed));
    }
}
