using Photon.Pun;
using UnityEngine;

/// <summary>Las copias de un efecto pueden verse en todos los equipos; cada dueño calcula su daño una sola vez.</summary>
public static class PowerUpDamage
{
    public static bool IsLocallyControlled(GameObject kart)
    {
        if (kart == null) return false;
        if (!PhotonNetwork.InRoom) return true;
        var view = kart.GetComponentInParent<PhotonView>();
        return view == null || (view.ViewID != 0 && view.IsMine);
    }

    public static bool IsOwner(DestructibleTarget target, GameObject owner)
    {
        if (target == null || owner == null) return false;
        return target.KartTransform == owner.transform || target.transform.IsChildOf(owner.transform) ||
               owner.transform.IsChildOf(target.KartTransform);
    }

    public static void ApplyHits(DestructibleTarget target, int hits, GameObject owner)
    {
        if (target == null || hits <= 0 || !target.IsAlive || target.IsProtected || IsOwner(target, owner) ||
            !IsLocallyControlled(target.gameObject)) return;
        for (int i = 0; i < hits && target.IsAlive; i++) target.TakeHit();
    }

    public static void RequestHits(DestructibleTarget target, int hits, GameObject owner)
    {
        if (target == null || IsOwner(target, owner)) return;
        var combat = target.GetComponent<KartCombat>();
        if (combat != null) combat.RequestDamage(hits);
        else ApplyHits(target, hits, owner);
    }

    public static void Ignite(DestructibleTarget target, GameObject owner, GameObject flamePrefab,
        float duration, float interval, int damagePerTick)
    {
        if (target == null || !target.IsAlive || target.IsProtected || IsOwner(target, owner) ||
            !IsLocallyControlled(target.gameObject)) return;
        var combat = target.GetComponent<KartCombat>();
        if (combat == null) combat = target.gameObject.AddComponent<KartCombat>();
        combat.Ignite(flamePrefab, duration, interval, damagePerTick);
    }
}
