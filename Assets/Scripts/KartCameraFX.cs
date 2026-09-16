using UnityEngine;

/// <summary>
/// Feedback visual del turbo. Sin esto el turbo "no se siente", aunque la
/// física sea correcta: la mitad de la sensación de velocidad en los juegos
/// de karts viene de la cámara, no del movimiento real.
///
/// Hace tres cosas:
///  1. FOV kick: abre el campo de visión de golpe al activar el turbo y lo
///     cierra suave. Es el efecto que más "vende" la velocidad.
///  2. Shake: sacude la cámara con intensidad según el tier.
///  3. Pull-back: la cámara se atrasa un poco durante el turbo, como si el
///     kart se le escapara hacia adelante.
///
/// SETUP EN EL EDITOR:
/// 1. Poné este script en tu cámara (la Main Camera que sigue al kart).
/// 2. Arrastrá el GameObject "Kart" (el que tiene KartController) al campo "Kart".
/// 3. Si tu cámara es hija del Kart, dejá "Is Child Of Kart" tildado.
///    Si usás Cinemachine o un follow script aparte, destildalo y el script
///    solo aplicará FOV y shake (sin tocar la posición).
/// </summary>
[RequireComponent(typeof(Camera))]
public class KartCameraFX : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private KartController kart;

    [Header("FOV kick (lo que más vende la velocidad)")]
    [SerializeField] private float baseFOV = 60f;
    [Tooltip("Cuántos grados extra de FOV agrega el turbo. Se multiplica por el tier.")]
    [SerializeField] private float fovKickPerTier = 8f;
    [Tooltip("Qué tan rápido se abre el FOV al activar el turbo. Alto = golpe seco.")]
    [SerializeField] private float fovOpenSpeed = 12f;
    [Tooltip("Qué tan rápido vuelve el FOV a la normalidad. Bajo = baja lenta y suave.")]
    [SerializeField] private float fovCloseSpeed = 3f;

    [Header("Shake")]
    [SerializeField] private float shakeAmountPerTier = 0.06f;
    [SerializeField] private float shakeFrequency = 25f;

    [Header("Pull-back (la cámara se atrasa durante el turbo)")]
    [SerializeField] private bool isChildOfKart = true;
    [SerializeField] private float pullBackDistancePerTier = 0.35f;
    [SerializeField] private float pullBackSpeed = 8f;

    private Camera cam;
    private float targetFOVBonus;
    private float currentFOVBonus;
    private float currentShake;
    private float currentPullBack;
    private Vector3 basePosition;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (baseFOV <= 0f) baseFOV = cam.fieldOfView;
        basePosition = transform.localPosition;
    }

    private void OnEnable()
    {
        if (kart != null) kart.OnBoostStarted += HandleBoostStarted;
    }

    private void OnDisable()
    {
        if (kart != null) kart.OnBoostStarted -= HandleBoostStarted;
    }

    private void HandleBoostStarted(int tier)
    {
        // Golpe instantáneo: seteamos el bonus objetivo de una.
        targetFOVBonus = fovKickPerTier * tier;
        currentShake = shakeAmountPerTier * tier;
        currentPullBack = pullBackDistancePerTier * tier;
    }

    private void LateUpdate()
    {
        if (kart == null) return;

        float dt = Time.deltaTime;
        bool boosting = kart.IsBoosting;

        // Mientras dura el turbo mantenemos el kick; cuando termina, decae.
        if (!boosting)
        {
            targetFOVBonus = 0f;
            currentShake = Mathf.MoveTowards(currentShake, 0f, dt * 0.4f);
            currentPullBack = Mathf.MoveTowards(currentPullBack, 0f, dt * pullBackSpeed * 0.15f);
        }

        // El FOV sube rápido y baja lento: así el arranque pega y la salida es suave.
        float speed = targetFOVBonus > currentFOVBonus ? fovOpenSpeed : fovCloseSpeed;
        currentFOVBonus = Mathf.Lerp(currentFOVBonus, targetFOVBonus, dt * speed);
        cam.fieldOfView = baseFOV + currentFOVBonus;

        if (isChildOfKart)
        {
            Vector3 pos = basePosition;

            // atrasar la cámara durante el turbo
            pos -= Vector3.forward * currentPullBack;

            // shake con ruido, no aleatorio puro (se ve menos "sucio")
            if (currentShake > 0.001f)
            {
                float t = Time.time * shakeFrequency;
                float nx = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;
                pos += new Vector3(nx, ny, 0f) * currentShake;
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, pos, dt * pullBackSpeed);
        }
    }
}