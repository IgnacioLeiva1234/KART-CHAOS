using UnityEngine;


/// <summary>
/// Lista de todos los power-ups del juego. Para agregar uno nuevo:
///  1. Sumá un nombre acá (ej: Shield = 3).
///  2. Copiá CannonPowerUp.cs, renombralo y cambiá lo que hace en Use().
///  3. Agregá ese script al kart. Listo: el pickup ya lo puede entregar.
/// </summary>
public enum PowerUpType
{
    None = 0,
    Cannon = 1,
    Mine = 2,
    Molotov = 3,
    Turret = 4
}


/// <summary>
/// Clase base de cada power-up. Cada arma hereda de esta y solo tiene que
/// decir qué tipo es y qué pasa cuando se usa. El inventario
/// (KartPowerUpHolder) se encarga de la munición y de la tecla.
/// </summary>
public abstract class KartPowerUp : MonoBehaviour
{
    [Header("Munición")]
    [Tooltip("Máximo de usos que puede acumular el jugador de este power-up.")]
    [SerializeField] private int maxAmmo = 5;


    public int MaxAmmo => maxAmmo;


    public abstract PowerUpType Type { get; }
    public abstract string DisplayName { get; }

    public virtual bool IsReady => isActiveAndEnabled;
    public virtual bool UsesCharge => false;
    public virtual int ShotsPerUse => 1;
    public virtual float ShotInterval => 0f;
    public virtual int PickupAmmoMultiplier => 1;

    public virtual bool BeginCharge(GameObject owner) => false;
    public virtual void UpdateCharge(GameObject owner, float dt) { }
    public virtual bool ReleaseCharge(GameObject owner) => false;
    public virtual void CancelCharge() { }


    /// <summary>
    /// Ejecuta el power-up. Devuelve true si se usó de verdad (entonces se
    /// descuenta 1 de munición) y false si no pudo (cooldown, falta algo).
    /// "owner" es el kart que lo usa.
    /// </summary>
    public abstract bool Use(GameObject owner);


    // --- Cooldown compartido ---
    private float nextUseTime;
    protected bool OnCooldown => Time.time < nextUseTime;
    protected void StartCooldown(float seconds) { nextUseTime = Time.time + seconds; }
}
