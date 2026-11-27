using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Inventario local: un arma a la vez, con munición y carga opcional.</summary>
public class KartPowerUpHolder : MonoBehaviour
{
    [Header("Control")]
    [SerializeField] private KeyCode useKey = KeyCode.Space;
    [Header("Para probar sin pickups")]
    [SerializeField] private PowerUpType startingPowerUp = PowerUpType.None;
    [SerializeField] private int startingAmmo = 5;
    [Header("Texto en pantalla")]
    [SerializeField] private bool showDebugLabel = true;
    [Header("Solo lectura")]
    [SerializeField] private PowerUpType currentType = PowerUpType.None;
    [SerializeField] private int ammo;
    [Header("GUI")]
    [SerializeField] private Font fontFamily;

    public event Action<PowerUpType, int> OnPowerUpChanged;
    public PowerUpType CurrentType => currentType;
    public int Ammo => ammo;
    public bool IsCharging => chargingPowerUp != null;
    public bool IsFiringBurst => firingPowerUp != null;
    public bool IsLocallyControlled => PowerUpDamage.IsLocallyControlled(gameObject);

    private readonly Dictionary<PowerUpType, KartPowerUp> powerUps = new Dictionary<PowerUpType, KartPowerUp>();
    private KartPowerUp chargingPowerUp;
    private KartPowerUp firingPowerUp;
    private Coroutine burstRoutine;
    private DestructibleTarget health;
    private GUIStyle labelStyle;

    private void Awake()
    {
        health = GetComponent<DestructibleTarget>();
        foreach (var powerUp in GetComponentsInChildren<KartPowerUp>(true))
            powerUps[powerUp.Type] = powerUp;
    }

    private void Start()
    {
        if (IsLocallyControlled && startingPowerUp != PowerUpType.None)
            Collect(startingPowerUp, startingAmmo);
    }

    private void Update()
    {
        if (!CanControl()) { CancelUse(); return; }
        if (Input.GetKeyDown(useKey)) BeginUse();
        if (Input.GetKey(useKey)) ContinueUse(Time.deltaTime);
        if (Input.GetKeyUp(useKey)) ReleaseUse();
    }

    private bool CanControl() => isActiveAndEnabled && IsLocallyControlled && (health == null || health.IsAlive);

    public bool SupportsPowerUp(PowerUpType type)
    {
        return type != PowerUpType.None && powerUps.TryGetValue(type, out var powerUp) &&
               powerUp != null && powerUp.isActiveAndEnabled && powerUp.IsReady && powerUp.MaxAmmo > 0;
    }

    public int GetPickupAmmo(PowerUpType type, int baseAmount)
    {
        if (baseAmount <= 0 || !powerUps.TryGetValue(type, out var powerUp) || powerUp == null) return 0;
        long amount = (long)baseAmount * Mathf.Max(1, powerUp.PickupAmmoMultiplier);
        return (int)Math.Min(powerUp.MaxAmmo, amount);
    }

    public bool Collect(PowerUpType type, int amount)
    {
        if (!CanControl() || amount <= 0 || !SupportsPowerUp(type)) return false;
        CancelUse();
        var powerUp = powerUps[type];
        ammo = Mathf.Min(powerUp.MaxAmmo, currentType == type ? ammo + amount : amount);
        currentType = type;
        Debug.Log("Recogiste: " + powerUp.DisplayName + " (x" + ammo + ")");
        OnPowerUpChanged?.Invoke(currentType, ammo);
        return true;
    }

    private bool TryGetCurrent(out KartPowerUp powerUp)
    {
        powerUp = null;
        return CanControl() && ammo > 0 && SupportsPowerUp(currentType) &&
               powerUps.TryGetValue(currentType, out powerUp);
    }

    public bool BeginUse()
    {
        if (chargingPowerUp != null || firingPowerUp != null || !TryGetCurrent(out var powerUp)) return false;
        if (!powerUp.UsesCharge) return TryUse();
        if (!powerUp.BeginCharge(gameObject)) return false;
        chargingPowerUp = powerUp;
        return true;
    }

    public void ContinueUse(float dt)
    {
        if (!CanControl()) { CancelUse(); return; }
        if (chargingPowerUp != null) chargingPowerUp.UpdateCharge(gameObject, Mathf.Max(0f, dt));
    }

    public bool ReleaseUse()
    {
        var powerUp = chargingPowerUp;
        if (powerUp == null) return false;
        chargingPowerUp = null;
        if (!CanControl() || ammo <= 0 || currentType != powerUp.Type)
        {
            powerUp.CancelCharge();
            return false;
        }
        if (!powerUp.ReleaseCharge(gameObject)) { powerUp.CancelCharge(); return false; }
        ConsumeAmmo();
        return true;
    }

    public bool TryUse()
    {
        if (chargingPowerUp != null || firingPowerUp != null || !TryGetCurrent(out var powerUp)) return false;
        if (!powerUp.Use(gameObject)) return false;
        ConsumeAmmo();
        if (powerUp.ShotsPerUse > 1 && ammo > 0 && currentType == powerUp.Type && CanControl())
        {
            firingPowerUp = powerUp;
            burstRoutine = StartCoroutine(FireRemainingBurst(powerUp, powerUp.ShotsPerUse - 1));
        }
        return true;
    }

    private IEnumerator FireRemainingBurst(KartPowerUp powerUp, int remaining)
    {
        while (remaining-- > 0)
        {
            yield return new WaitForSeconds(Mathf.Max(.02f, powerUp.ShotInterval));
            if (firingPowerUp != powerUp || !TryGetCurrent(out var current) || current != powerUp) break;
            if (!powerUp.Use(gameObject)) break;
            ConsumeAmmo();
            if (ammo <= 0) break;
        }
        firingPowerUp = null;
        burstRoutine = null;
    }

    private void ConsumeAmmo()
    {
        ammo = Mathf.Max(0, ammo - 1);
        if (ammo == 0) currentType = PowerUpType.None;
        OnPowerUpChanged?.Invoke(currentType, ammo);
    }

    public void CancelUse()
    {
        if (chargingPowerUp != null) chargingPowerUp.CancelCharge();
        chargingPowerUp = null;
        if (burstRoutine != null) StopCoroutine(burstRoutine);
        burstRoutine = null;
        firingPowerUp = null;
    }

    private void OnDisable() => CancelUse();
    private void OnApplicationFocus(bool focused) { if (!focused) CancelUse(); }

    private void OnGUI()
    {
        if (!showDebugLabel || !IsLocallyControlled) return;
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
            labelStyle.font = fontFamily;
        }
        string text = "Power-up: ninguno";
        if (currentType != PowerUpType.None && powerUps.TryGetValue(currentType, out var powerUp))
        {
            text = powerUp.DisplayName + "  x" + ammo;
            text += powerUp.UsesCharge ? "  |  Mantené ESPACIO para apuntar; soltá para lanzar" :
                powerUp.ShotsPerUse > 1 ? "  |  ESPACIO: disparar toda la munición" : "  |  ESPACIO para usar";
        }
        GUI.Label(new Rect(20f, Screen.height - 65f, Screen.width - 40f, 60f), text, labelStyle);
    }
}
