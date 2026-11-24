using System.Collections;
using Photon.Pun;
using UnityEngine;

public abstract class NetworkHazard : MonoBehaviour, IPunInstantiateMagicCallback
{
    protected PhotonView NetworkView { get; private set; }
    private GameObject owner;
    private int ownerViewId;
    public GameObject Owner
    {
        get
        {
            if (owner == null && ownerViewId != 0)
            {
                var view = PhotonView.Find(ownerViewId);
                if (view != null) owner = view.gameObject;
            }
            return owner;
        }
    }
    public bool IsNetworked => PhotonNetwork.InRoom && NetworkView != null && NetworkView.ViewID != 0;
    public bool HasAuthority => IsNetworked ? NetworkView.IsMine : !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
    public bool IsInitialized { get; private set; }
    public double SpawnTime { get; private set; }
    protected double Now => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
    public float Age => Mathf.Max(0f, (float)(Now - SpawnTime));

    protected virtual void Awake()
    {
        NetworkView = GetComponent<PhotonView>();
        SpawnTime = Now;
    }

    public void SetOwner(GameObject kart)
    {
        owner = kart;
        var view = kart != null ? kart.GetComponent<PhotonView>() : null;
        ownerViewId = view != null ? view.ViewID : 0;
    }

    public void Initialize(GameObject kart, object[] payload)
    {
        SetOwner(kart);
        SpawnTime = Now;
        IsInitialized = true;
        OnSpawnData(payload);
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        var data = info.photonView.InstantiationData;
        if (data == null || data.Length < 2) return;
        ownerViewId = (int)data[0];
        SpawnTime = (double)data[1];
        var payload = new object[data.Length - 2];
        System.Array.Copy(data, 2, payload, 0, payload.Length);
        IsInitialized = true;
        OnSpawnData(payload);
    }

    protected virtual void OnSpawnData(object[] payload) { }
    public bool BelongsToOwner(Transform other)
    {
        return Owner != null && (other == Owner.transform || other.IsChildOf(Owner.transform));
    }
    protected bool ValidSender(PhotonMessageInfo info) => !IsNetworked || info.Sender == NetworkView.Controller;

    protected void Despawn(float delay = 0f)
    {
        if (HasAuthority) StartCoroutine(RemoveAfter(delay));
    }
    private IEnumerator RemoveAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (IsNetworked)
        {
            if (NetworkView.IsMine) PhotonNetwork.Destroy(gameObject);
        }
        else Destroy(gameObject);
    }
}
