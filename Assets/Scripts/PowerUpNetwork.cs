using Photon.Pun;
using UnityEngine;

public static class PowerUpNetwork
{
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, GameObject owner, params object[] payload)
    {
        if (prefab == null) return null;
        if (PhotonNetwork.InRoom)
        {
            var ownerView = owner != null ? owner.GetComponent<PhotonView>() : null;
            if (ownerView == null || !ownerView.IsMine || ownerView.ViewID == 0) return null;
            var data = new object[payload.Length + 2];
            data[0] = ownerView.ViewID;
            data[1] = PhotonNetwork.Time;
            System.Array.Copy(payload, 0, data, 2, payload.Length);
            return PhotonNetwork.Instantiate("PowerUps/" + prefab.name, position, rotation, 0, data);
        }
        var clone = Object.Instantiate(prefab, position, rotation);
        var hazard = clone.GetComponent<NetworkHazard>();
        if (hazard != null) hazard.Initialize(owner, payload);
        return clone;
    }
}
