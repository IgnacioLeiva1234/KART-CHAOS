using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MolotovProjectile : NetworkHazard
{
    [SerializeField] private GameObject flameStreamPrefab;
    [SerializeField] private float fireDuration = 6f;
    [SerializeField] private float burnDuration = 3f;
    [SerializeField] private float damageInterval = 1f;
    private Vector3 origin, velocity, gravity, previousPosition;
    private float flightTime, radius = .18f, fireRadius = 2.5f;
    private bool exploded;
    public bool HasExploded => exploded;
    public Vector3 ImpactPoint { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }
    protected override void OnSpawnData(object[] payload)
    {
        origin = previousPosition = transform.position;
        velocity = (Vector3)payload[0];
        gravity = (Vector3)payload[1];
        flightTime = (float)payload[2];
        radius = (float)payload[3];
        fireRadius = (float)payload[4];
    }
    private void FixedUpdate()
    {
        if (!IsInitialized || exploded) return;
        Vector3 position = MolotovTrajectory.Position(origin, velocity, gravity, Age);
        if (HasAuthority && MolotovTrajectory.FirstHit(previousPosition, position, radius, Owner, transform, out var hit))
        {
            if (IsNetworked) NetworkView.RPC(nameof(Explode), RpcTarget.AllBuffered, hit.point, hit.normal, Now);
            else Explode(hit.point, hit.normal, Now, default);
            return;
        }
        transform.position = previousPosition = position;
        Vector3 tangent = velocity + gravity * Age;
        if (tangent.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(tangent);
        if (HasAuthority && Age > flightTime + 5f) { exploded = true; Despawn(); }
    }
    [PunRPC]
    private void Explode(Vector3 point, Vector3 normal, double when, PhotonMessageInfo info)
    {
        if (exploded || !ValidSender(info)) return;
        exploded = true;
        ImpactPoint = point;
        foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        float remaining = Mathf.Max(0f, fireDuration - (float)(Now - when));
        if (remaining > 0f)
        {
            var area = new GameObject("MolotovFireArea").AddComponent<MolotovFireArea>();
            area.Initialize(point, normal, Owner, flameStreamPrefab, fireRadius, remaining, burnDuration, damageInterval);
        }
        Despawn(remaining + .2f);
    }
}
