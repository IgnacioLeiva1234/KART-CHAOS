using UnityEngine;

/// <summary>Caer fuera del mapa cuenta como una muerte y usa la reaparición normal.</summary>
public class ArenaRules : MonoBehaviour
{
    public float killY = -40f;
    private void FixedUpdate()
    {
        foreach (var target in new System.Collections.Generic.List<DestructibleTarget>(DestructibleTarget.All))
            if (target != null && target.IsAlive && target.KartTransform.position.y < killY &&
                PowerUpDamage.IsLocallyControlled(target.gameObject)) target.Kill();
    }
}

