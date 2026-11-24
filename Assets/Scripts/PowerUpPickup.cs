using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Caja de objetos. La sala decide un único ganador y elige un arma que su kart pueda usar.</summary>
public class PowerUpPickup : MonoBehaviour, IPunObservable
{
    [Header("Ítem")]
    [SerializeField] private bool randomPowerUp = true;
    [SerializeField] private PowerUpType type = PowerUpType.Cannon;
    [SerializeField] private PowerUpType[] availablePowerUps = { PowerUpType.Cannon, PowerUpType.Mine, PowerUpType.Molotov, PowerUpType.Turret };
    [SerializeField] private int amount = 3;
    [SerializeField] private float respawnTime = 10f;
    [SerializeField] private float triggerRadius = 1.5f;
    [Header("Efecto visual")]
    [SerializeField] private float rotateSpeed = 90f;
    [SerializeField] private float bobAmplitude = .2f;
    [SerializeField] private float bobSpeed = 2f;
    [Header("Feedback opcional")]
    [SerializeField] private GameObject pickupVFXPrefab;
    [SerializeField] private AudioClip pickupSound;

    public bool IsCollected => collected;
    private Vector3 startLocalPos;
    private bool collected;
    private double availableAt;
    private float nextRequestTime;
    private Collider col;
    private Renderer[] renderers;
    private PhotonView view;
    private readonly List<PowerUpType> candidates = new List<PowerUpType>();
    private bool Networked => PhotonNetwork.InRoom && view != null && view.ViewID != 0;
    private double Now => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    private void Awake()
    {
        startLocalPos = transform.localPosition;
        view = GetComponent<PhotonView>();
        col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = triggerRadius;
            col = sphere;
        }
        col.isTrigger = true;
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (collected)
        {
            if (Now >= availableAt) SetCollected(false);
            else return;
        }
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float phase = PhotonNetwork.InRoom ? (float)(PhotonNetwork.Time % 10000d) : Time.time;
        transform.localPosition = startLocalPos + Vector3.up * (Mathf.Sin(phase * bobSpeed) * bobAmplitude);
    }

    private void OnTriggerEnter(Collider other) => TryTrigger(other);
    private void OnTriggerStay(Collider other) => TryTrigger(other);
    private void TryTrigger(Collider other)
    {
        if (other.isTrigger) return;
        var holder = other.GetComponentInParent<KartPowerUpHolder>();
        if (holder != null) TryCollect(holder);
    }

    public bool TryCollect(KartPowerUpHolder holder)
    {
        if (collected || holder == null || !holder.IsLocallyControlled || amount <= 0) return false;
        var health = holder.GetComponent<DestructibleTarget>();
        if (health != null && !health.IsAlive) return false;
        if (Networked)
        {
            var playerView = holder.GetComponent<PhotonView>();
            if (playerView == null || playerView.ViewID == 0 || Time.unscaledTime < nextRequestTime) return false;
            nextRequestTime = Time.unscaledTime + .25f;
            view.RPC(nameof(RequestCollect), RpcTarget.MasterClient, playerView.ViewID);
            return true;
        }
        if (PhotonNetwork.InRoom) return false; // Una caja de sala necesita su PhotonView de escena.
        if (!ChoosePowerUp(holder, out var selected) || !holder.Collect(selected, holder.GetPickupAmmo(selected, amount))) return false;
        availableAt = Now + Mathf.Max(.1f, respawnTime);
        SetCollected(true);
        Feedback();
        return true;
    }

    public bool ChoosePowerUp(KartPowerUpHolder holder, out PowerUpType selected)
    {
        candidates.Clear();
        if (randomPowerUp && availablePowerUps != null)
        {
            foreach (var candidate in availablePowerUps)
                if (!candidates.Contains(candidate) && holder.SupportsPowerUp(candidate)) candidates.Add(candidate);
        }
        else if (holder.SupportsPowerUp(type)) candidates.Add(type);
        selected = PowerUpType.None;
        if (candidates.Count == 0) return false;
        selected = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    [PunRPC]
    private void RequestCollect(int kartViewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || collected || amount <= 0) return;
        var playerView = PhotonView.Find(kartViewId);
        if (playerView == null || info.Sender != playerView.Controller) return;
        var holder = playerView.GetComponent<KartPowerUpHolder>();
        var health = playerView.GetComponent<DestructibleTarget>();
        if (holder == null || (health != null && !health.IsAlive)) return;
        // Admitir una pequeña tolerancia por la interpolación de los autos remotos.
        bool nearby = false;
        foreach (var bodyCollider in holder.GetComponentsInChildren<Collider>())
        {
            if (bodyCollider.isTrigger || !bodyCollider.enabled) continue;
            if (Vector3.Distance(bodyCollider.ClosestPoint(col.bounds.center), col.bounds.center) <= col.bounds.extents.magnitude + 2f)
            { nearby = true; break; }
        }
        if (!nearby || !ChoosePowerUp(holder, out var selected)) return;
        availableAt = Now + Mathf.Max(.1f, respawnTime);
        // Reservar antes del RPC: dos contactos durante el mismo paso no reciben la misma caja.
        SetCollected(true);
        view.RPC(nameof(GrantPickup), RpcTarget.All, kartViewId, (int)selected, holder.GetPickupAmmo(selected, amount), availableAt);
    }

    [PunRPC]
    private void GrantPickup(int kartViewId, int selected, int grantedAmmo, double respawnAt, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient) return;
        availableAt = respawnAt;
        SetCollected(true);
        var playerView = PhotonView.Find(kartViewId);
        if (playerView != null && playerView.IsMine)
        {
            var holder = playerView.GetComponent<KartPowerUpHolder>();
            if (holder != null) holder.Collect((PowerUpType)selected, grantedAmmo);
        }
        Feedback();
    }

    private void Feedback()
    {
        if (pickupVFXPrefab != null) Instantiate(pickupVFXPrefab, transform.position, Quaternion.identity);
        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
    }

    private void SetCollected(bool value)
    {
        collected = value;
        if (col != null) col.enabled = !value;
        if (renderers == null) return;
        foreach (var renderer in renderers) if (renderer != null) renderer.enabled = !value;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting) stream.SendNext(availableAt);
        else
        {
            availableAt = (double)stream.ReceiveNext();
            SetCollected(Now < availableAt);
        }
    }
}
