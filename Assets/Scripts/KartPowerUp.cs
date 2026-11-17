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
    Mine = 2
}

/// <summary>
/// Clase base de cada power-up. Cada arma hereda de esta y solo tiene que
/// decir qué tipo es y qué pasa cuando se usa. El inventario
/// (KartPowerUpHolder) se encarga de la munición y de la tecla.
/// </summary>

public abstract class KartPowerUp : MonoBehaviour
{
    
}
