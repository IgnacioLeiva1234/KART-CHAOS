using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>Daño de fuego y estado de vida compartido; el dueño del kart decide su vida y reaparición.</summary>
[RequireComponent(typeof(DestructibleTarget))]
public class KartCombat : MonoBehaviour, IPunObservable
{
    [SerializeField] private GameObject burningEffectPrefab;
    [SerializeField] private Vector3 burningEffectOffset = new Vector3(0f, .4f, 0f);
    [SerializeField] private float burningEffectScale = .35f;

    public bool IsLocal => PowerUpDamage.IsLocallyControlled(gameObject);
    public bool IsBurning => burnUntil > Now;
    private double Now => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
    private PhotonView view;
    private DestructibleTarget health;
    private double burnUntil, nextBurnTick;
    private float tickInterval = 1f;
    private int tickDamage = 1;
    private GameObject burningVisual;
    private bool remoteHidden;
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Collider> hiddenColliders = new List<Collider>();

    private void Awake()
    {
        view = GetComponent<PhotonView>();
        health = GetComponent<DestructibleTarget>();
    }

    private void OnEnable() { if (health != null) health.OnHealthChanged += HealthChanged; }
    private void OnDisable()
    {
        if (health != null) health.OnHealthChanged -= HealthChanged;
        StopBurning(false);
    }
    private void OnDestroy() { if (burningVisual != null) Destroy(burningVisual); }

    public void RequestDamage(int hits)
    {
        if (hits <= 0 || health == null) return;
        if (IsLocal) ApplyDamage(hits);
        else if (PhotonNetwork.InRoom && view != null && view.Controller != null)
            view.RPC(nameof(ReceiveDamage), view.Controller, hits);
    }

    [PunRPC]
    private void ReceiveDamage(int hits, PhotonMessageInfo info)
    {
        if (IsLocal && info.Sender != null) ApplyDamage(Mathf.Clamp(hits, 0, health.MaxHealth));
    }

    private void ApplyDamage(int hits)
    {
        for (int i = 0; i < hits && health.IsAlive && !health.IsProtected; i++) health.TakeHit();
    }

    public void Ignite(GameObject effectPrefab, float duration, float interval, int damage)
    {
        if (!IsLocal || !health.IsAlive || health.IsProtected || duration <= 0f) return;
        bool wasBurning = IsBurning;
        tickInterval = Mathf.Max(.1f, interval);
        tickDamage = Mathf.Max(1, damage);
        burnUntil = System.Math.Max(burnUntil, Now + duration);
        if (!wasBurning) nextBurnTick = Now + tickInterval;
        if (burningEffectPrefab == null) burningEffectPrefab = effectPrefab;
        ShowBurning();
        if (!wasBurning) BroadcastState();
    }

    private void Update()
    {
        if (health == null) return;
        if (!health.IsAlive) { StopBurning(false); return; }
        if (IsLocal && burnUntil > 0d)
        {
            double now = Now;
            while (nextBurnTick <= now && nextBurnTick <= burnUntil && health.IsAlive)
            {
                nextBurnTick += tickInterval;
                ApplyDamage(tickDamage);
            }
        }
        if (burnUntil > 0d && Now >= burnUntil) StopBurning(IsLocal);
    }

    private void ShowBurning()
    {
        if (burningVisual != null || burningEffectPrefab == null || !health.IsAlive) return;
        burningVisual = Instantiate(burningEffectPrefab, transform);
        burningVisual.name = "KartBurningEffect";
        burningVisual.transform.localPosition = burningEffectOffset;
        burningVisual.transform.localRotation = Quaternion.identity;
        burningVisual.transform.localScale = Vector3.one * burningEffectScale;
        foreach (var col in burningVisual.GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var ps in burningVisual.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ps.Play();
        }
    }

    private void StopBurning(bool broadcast)
    {
        bool hadFire = burnUntil > 0d;
        burnUntil = 0d;
        if (burningVisual != null) Destroy(burningVisual);
        burningVisual = null;
        if (broadcast && hadFire) BroadcastState();
    }

    private void HealthChanged(int value, int max)
    {
        if (!IsLocal) return;
        if (value <= 0) StopBurning(false);
        BroadcastState();
    }

    private void BroadcastState()
    {
        if (PhotonNetwork.InRoom && view != null && view.ViewID != 0 && view.IsMine)
            view.RPC(nameof(ReceiveState), RpcTarget.Others, health.CurrentHealth, burnUntil);
    }

    [PunRPC]
    private void ReceiveState(int value, double burningUntil, PhotonMessageInfo info)
    {
        if (view == null || info.Sender != view.Controller || IsLocal) return;
        ApplyRemoteState(value, burningUntil);
    }

    private void ApplyRemoteState(int value, double burningUntil)
    {
        health.ApplyReplicatedHealth(value);
        burnUntil = value > 0 ? burningUntil : 0d;
        if (IsBurning) ShowBurning(); else StopBurning(false);
        SetRemoteHidden(value <= 0);
    }

    private void SetRemoteHidden(bool hidden)
    {
        if (hidden == remoteHidden) return;
        remoteHidden = hidden;
        if (hidden)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                if (renderer.enabled) { renderer.enabled = false; hiddenRenderers.Add(renderer); }
            foreach (var col in GetComponentsInChildren<Collider>())
                if (col.enabled) { col.enabled = false; hiddenColliders.Add(col); }
        }
        else
        {
            foreach (var renderer in hiddenRenderers) if (renderer != null) renderer.enabled = true;
            foreach (var col in hiddenColliders) if (col != null) col.enabled = true;
            hiddenRenderers.Clear();
            hiddenColliders.Clear();
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(health.CurrentHealth);
            stream.SendNext(burnUntil);
        }
        else
        {
            int value = (int)stream.ReceiveNext();
            double until = (double)stream.ReceiveNext();
            if (!IsLocal) ApplyRemoteState(value, until);
        }
    }
}
