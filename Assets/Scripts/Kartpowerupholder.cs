using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventario del kart. Guarda UN power-up a la vez (como Mario Kart) con
/// su munición, y lo usa cuando apretás ESPACIO.
///
/// SETUP:
///  1. Seleccioná la RAÍZ de tu kart.
///  2. Add Component > "Kart Power Up Holder".
///  3. Agregale también los power-ups que quieras que pueda usar
///     (CannonPowerUp, MinePowerUp...) en ese mismo objeto.
///  4. Para probar sin pickups: poné "Starting Power Up" = Cannon o Mine.
/// </summary>
public class KartPowerUpHolder : MonoBehaviour
{
    [Header("Control")]
    [SerializeField] private KeyCode useKey = KeyCode.Space;

    [Header("Para probar sin pickups")]
    [SerializeField] private PowerUpType startingPowerUp = PowerUpType.None;
    [SerializeField] private int startingAmmo = 5;

    [Header("Texto en pantalla (para probar)")]
    [SerializeField] private bool showDebugLabel = true;

    [Header("Solo lectura (para ver en Play)")]
    [SerializeField] private PowerUpType currentType = PowerUpType.None;
    [SerializeField] private int ammo;

    /// <summary>Tipo actual y munición. Útil para un HUD con íconos.</summary>
    public event Action<PowerUpType, int> OnPowerUpChanged;

    public PowerUpType CurrentType => currentType;
    public int Ammo => ammo;

    private readonly Dictionary<PowerUpType, KartPowerUp> powerUps = new Dictionary<PowerUpType, KartPowerUp>();
    private GUIStyle labelStyle;

    private void Awake()
    {
        foreach (var p in GetComponentsInChildren<KartPowerUp>())
        {
            powerUps[p.Type] = p;
        }
    }

    private void Start()
    {
        if (startingPowerUp != PowerUpType.None)
        {
            Collect(startingPowerUp, startingAmmo);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(useKey))
        {
            TryUse();
        }
    }

    /// <summary>
    /// La llama el pickup al recoger un power-up. Si ya tenías ese mismo
    /// tipo, suma munición; si tenías otro, lo reemplaza. Devuelve false si
    /// este kart no tiene el script de ese power-up.
    /// </summary>
    public bool Collect(PowerUpType type, int amount)
    {
        if (!powerUps.TryGetValue(type, out KartPowerUp p))
        {
            Debug.LogWarning("[" + name + "] recogió '" + type + "' pero no tiene el script de ese power-up. " +
                             "Agregalo en la raíz del kart (ej: CannonPowerUp o MinePowerUp).");
            return false;
        }

        if (currentType == type)
        {
            ammo = Mathf.Min(p.MaxAmmo, ammo + amount);
        }
        else
        {
            currentType = type;
            ammo = Mathf.Min(p.MaxAmmo, amount);
        }

        Debug.Log("Recogiste: " + p.DisplayName + " (x" + ammo + ")");
        OnPowerUpChanged?.Invoke(currentType, ammo);
        return true;
    }

    public bool TryUse()
    {
        if (currentType == PowerUpType.None || ammo <= 0) return false;
        if (!powerUps.TryGetValue(currentType, out KartPowerUp p)) return false;

        if (!p.Use(gameObject)) return false; // cooldown o falta configurar algo: no gasta munición

        ammo--;
        if (ammo <= 0)
        {
            ammo = 0;
            currentType = PowerUpType.None;
        }

        OnPowerUpChanged?.Invoke(currentType, ammo);
        return true;
    }

    private void OnGUI()
    {
        if (!showDebugLabel) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
        }

        string text = "Power-up: ninguno";
        if (currentType != PowerUpType.None && powerUps.TryGetValue(currentType, out KartPowerUp p))
        {
            text = "Power-up: " + p.DisplayName + "  x" + ammo;
        }

        GUI.Label(new Rect(20f, Screen.height - 60f, 600f, 40f), text, labelStyle);
    }
}