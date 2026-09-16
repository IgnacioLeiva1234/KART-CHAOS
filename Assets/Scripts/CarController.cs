using UnityEngine;

/// <summary>
/// Controlador de kart estilo arcade para Unity 6.
/// Portado del prototipo en canvas/JS: W acelera, S frena/retrocede,
/// A/D giran, Shift (mantenido mientras giras) activa el derrape con
/// carga de mini-turbo.
///
/// SETUP EN EL EDITOR:
/// 1. Creá un GameObject "Kart" con el modelo 3D del auto como hijo.
/// 2. Agregale un Rigidbody:
///      - Freeze Rotation en X y Z (para que no vuelque).
///      - Drag ~1, Angular Drag ~1, Use Gravity activado.
/// 3. Agregale un collider (BoxCollider o CapsuleCollider) que cubra el chasis.
/// 4. Arrastrá este script al GameObject "Kart".
/// 5. (Opcional) Asigná los transforms de las ruedas traseras en
///    "rearWheelTransforms" para dibujar marcas de derrape con TrailRenderer.
/// 6. (Opcional) Asigná un ParticleSystem en "boostVFX" para el efecto de
///    mini-turbo, y un AudioSource + clips si querés sonido de motor/derrape.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float maxSpeed = 32f;        // m/s
    [SerializeField] private float reverseMaxSpeed = 12f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float brakeForce = 36f;
    [SerializeField] private float naturalFriction = 12f;
    [SerializeField] private float turnRate = 140f;        // grados/seg a máxima velocidad

    [Header("Derrape")]
    [SerializeField] private float minDriftSpeed = 7f;     // velocidad mínima para poder derrapar
    [SerializeField] private float driftTurnMultiplier = 1.6f;
    [SerializeField] private float driftSlipLerpSpeed = 3.5f;  // qué tan lento "resbala" la velocidad hacia el morro
    [SerializeField] private float gripLerpSpeed = 14f;        // qué tan rápido agarra en agarre normal (fuera de drift)
    [SerializeField] private float driftRecoveryTime = 0.5f;   // segundos que tarda en recuperar el agarre total después de soltar el drift
    [SerializeField] private float driftChargeTime = 1.6f;     // segundos para llenar la barra al 100%
    [SerializeField] private float tier1Threshold = 0.35f;
    [SerializeField] private float tier2Threshold = 0.6f;
    [SerializeField] private float tier3Threshold = 0.85f;

    [Header("Mini-Turbo (boost al soltar el derrape)")]
    [Tooltip("Golpe INSTANTÁNEO de velocidad al activar el turbo, en m/s. Esto es lo que hace que se sienta como un turbo y no como acelerar de a poco.")]
    [SerializeField] private float boostInstantKick = 8f;
    [Tooltip("Velocidad extra por encima de maxSpeed que el turbo sostiene mientras dura. Se multiplica por el tier.")]
    [SerializeField] private float boostSpeedBonus = 6f;
    [Tooltip("Qué tan rápido empuja el turbo hacia su velocidad objetivo mientras está activo.")]
    [SerializeField] private float boostAcceleration = 90f;
    [Tooltip("Qué tan rápido cae la velocidad de vuelta a maxSpeed cuando el turbo termina. Bajo = la inercia se siente más.")]
    [SerializeField] private float boostDecayRate = 6f;
    [SerializeField] private float tier1Duration = 0.6f;
    [SerializeField] private float tier2Duration = 0.85f;
    [SerializeField] private float tier3Duration = 1.1f;
    [Tooltip("Multiplicador de fuerza por tier. Tier 3 pega mucho más fuerte que tier 1.")]
    [SerializeField] private float tier1Power = 1f;
    [SerializeField] private float tier2Power = 1.5f;
    [SerializeField] private float tier3Power = 2.2f;

    [Header("Referencias opcionales")]
    [SerializeField] private Transform[] rearWheelTransforms;
    [SerializeField] private ParticleSystem boostVFX;
    [SerializeField] private TrailRenderer[] skidTrails; // uno por rueda trasera, activar/desactivar según drift
    [SerializeField] private ParticleSystem[] driftSparksVFX; // chispas continuas mientras derrapa (una por rueda trasera)

    private Rigidbody rb;

    // Estado interno
    private float currentSpeed;          // escalar con signo (+ adelante, - atrás)
    private float facingYaw;             // hacia dónde apunta el kart (grados)
    private float velocityYaw;           // hacia dónde se mueve realmente (permite el slip del derrape)

    private bool isDrifting;
    private float driftDir;              // -1 izquierda, +1 derecha
    private float driftCharge;           // 0..1
    private float driftTimer;
    private float driftRecoveryTimer;    // cuenta regresiva post-drift: cuánto falta para recuperar agarre total

    private float boostTimer;
    private int boostTier;               // 0 = sin boost, 1/2/3 = nivel de mini-turbo
    private float overspeed;             // velocidad extra POR ENCIMA de maxSpeed; sube de golpe con el turbo y decae suave
    private float boostTotalDuration;    // duración total del boost actual (para calcular la intensidad 0..1)

    // Eventos para que la cámara / audio reaccionen al turbo sin acoplarse al script
    public System.Action<int> OnBoostStarted;   // recibe el tier (1, 2 o 3)
    public float BoostIntensity01 => boostTimer > 0f && boostTotalDuration > 0f
        ? Mathf.Clamp01(boostTimer / boostTotalDuration)
        : 0f;
    public bool IsBoosting => boostTimer > 0f;

    // Propiedades públicas por si un HUD quiere leerlas
    public float SpeedKmh => Mathf.Abs(currentSpeed) * 3.6f;
    public float DriftCharge01 => driftCharge;
    public bool IsDrifting => isDrifting;
    public float Tier1Threshold => tier1Threshold;
    public float Tier2Threshold => tier2Threshold;
    public float Tier3Threshold => tier3Threshold;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.4f, 0f); // más estable, menos vuelcos
        facingYaw = transform.eulerAngles.y;
        velocityYaw = facingYaw;
    }

    private void Update()
    {
        // El input se lee en Update (más responsivo) y se usa en FixedUpdate.
        // No hace falta guardarlo en variables intermedias porque Input.GetKey
        // es instantáneo y confiable en ambos loops, pero lo dejamos así por
        // claridad si luego migrás al nuevo Input System.
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        bool forward = Input.GetKey(KeyCode.W);
        bool back = Input.GetKey(KeyCode.S);
        bool left = Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.D);
        bool wantDrift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        HandleAcceleration(forward, back, dt);
        HandleSteeringAndDrift(left, right, wantDrift, dt);
        HandleBoost(dt);
        ApplyMovement(dt);
        UpdateVisuals();
    }

    private void HandleAcceleration(bool forward, bool back, float dt)
    {
        if (forward)
        {
            currentSpeed += acceleration * dt;
        }
        else if (back)
        {
            currentSpeed -= (currentSpeed > 0f ? brakeForce : acceleration) * dt;
        }
        else
        {
            // fricción natural cuando no se toca el acelerador
            float f = naturalFriction * dt;
            if (currentSpeed > 0f) currentSpeed = Mathf.Max(0f, currentSpeed - f);
            else if (currentSpeed < 0f) currentSpeed = Mathf.Min(0f, currentSpeed + f);
        }

        // El techo NO es fijo: incluye la sobrevelocidad del turbo, que decae
        // sola. Así el kart puede ir más rápido que maxSpeed un rato y volver
        // suavemente, en vez de cortarse en seco.
        float topSpeed = maxSpeed + overspeed;
        currentSpeed = Mathf.Clamp(currentSpeed, -reverseMaxSpeed, topSpeed);
    }

    private void HandleSteeringAndDrift(bool left, bool right, bool wantDrift, float dt)
    {
        float steer = (left ? -1f : 0f) + (right ? 1f : 0f);
        float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / 10f);
        bool canStartDrift = wantDrift && steer != 0f && Mathf.Abs(currentSpeed) > minDriftSpeed;

        if (canStartDrift && !isDrifting)
        {
            isDrifting = true;
            driftDir = steer;
            driftCharge = 0f;
            driftTimer = 0f;
            SetDriftEffects(true);
        }

        bool shouldStopDrift = isDrifting &&
            (!wantDrift || steer == 0f || Mathf.Abs(currentSpeed) < minDriftSpeed * 0.6f);

        if (shouldStopDrift)
        {
            ReleaseDrift();
        }

        if (isDrifting)
        {
            driftTimer += dt;
            driftCharge = Mathf.Clamp01(driftTimer / driftChargeTime);

            // el morro gira más brusco que el auto real
            facingYaw += driftDir * turnRate * driftTurnMultiplier * dt * (0.5f + speedFactor * 0.5f);
            facingYaw = NormalizeAngle(facingYaw);

            // la velocidad "resbala": se acerca lento al ángulo del morro -> patinada
            velocityYaw = Mathf.LerpAngle(velocityYaw, facingYaw, dt * driftSlipLerpSpeed);
        }
        else
        {
            facingYaw += steer * turnRate * dt * (0.35f + speedFactor * 0.65f) * (currentSpeed < 0f ? -1f : 1f);
            facingYaw = NormalizeAngle(facingYaw);

            // Justo después de soltar el drift, el agarre no vuelve de golpe:
            // arranca tan "resbaladizo" como durante el drift y se va
            // endureciendo hasta llegar al agarre normal (gripLerpSpeed) a
            // lo largo de driftRecoveryTime. Esto hace que el kart siga un
            // tramo la trayectoria que traía en vez de enderezarse en seco.
            float effectiveGrip;
            if (driftRecoveryTimer > 0f)
            {
                driftRecoveryTimer -= dt;
                float t = 1f - Mathf.Clamp01(driftRecoveryTimer / driftRecoveryTime); // 0 = recién soltado, 1 = agarre total
                effectiveGrip = Mathf.Lerp(driftSlipLerpSpeed, gripLerpSpeed, t);
            }
            else
            {
                effectiveGrip = gripLerpSpeed;
            }

            velocityYaw = Mathf.LerpAngle(velocityYaw, facingYaw, dt * effectiveGrip);
        }
    }

    private void ReleaseDrift()
    {
        float tierPower = 0f;

        if (driftCharge >= tier3Threshold)
        {
            boostTier = 3;
            boostTimer = tier3Duration;
            tierPower = tier3Power;
        }
        else if (driftCharge >= tier2Threshold)
        {
            boostTier = 2;
            boostTimer = tier2Duration;
            tierPower = tier2Power;
        }
        else if (driftCharge >= tier1Threshold)
        {
            boostTier = 1;
            boostTimer = tier1Duration;
            tierPower = tier1Power;
        }
        else
        {
            boostTier = 0;
        }

        if (boostTier > 0)
        {
            boostTotalDuration = boostTimer;

            // 1) Sobrevelocidad sostenida: el techo sube según el tier.
            overspeed = Mathf.Max(overspeed, boostSpeedBonus * tierPower);

            // 2) GOLPE INSTANTÁNEO: esto es lo que hace que se SIENTA un turbo.
            //    La velocidad salta de una, no espera a que la aceleración la alcance.
            currentSpeed = Mathf.Min(maxSpeed + overspeed,
                                     currentSpeed + boostInstantKick * tierPower);

            // 3) Avisar a la cámara / audio para el feedback visual.
            OnBoostStarted?.Invoke(boostTier);

            if (boostVFX != null)
            {
                var main = boostVFX.main;
                main.startColor = boostTier == 3 ? new Color(0.72f, 0.24f, 0.88f)
                                 : boostTier == 2 ? new Color(0.88f, 0.28f, 0.25f)
                                 : new Color(0.25f, 0.65f, 0.88f);
                boostVFX.Play();
            }
        }

        isDrifting = false;
        driftCharge = 0f;
        driftRecoveryTimer = driftRecoveryTime;
        SetDriftEffects(false);
    }

    private void HandleBoost(float dt)
    {
        if (boostTimer > 0f)
        {
            boostTimer -= dt;

            // Mientras el turbo está activo, empuja fuerte hacia el techo actual.
            float target = maxSpeed + overspeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, target, boostAcceleration * dt);

            if (boostTimer <= 0f)
            {
                boostTier = 0;
                boostTotalDuration = 0f;
            }
        }
        else if (overspeed > 0f)
        {
            // Turbo terminado: la sobrevelocidad se desinfla de a poco.
            // Esto es lo que da la sensación de inercia al final del turbo,
            // en vez de frenar de golpe al llegar a maxSpeed.
            overspeed = Mathf.MoveTowards(overspeed, 0f, boostDecayRate * dt);
        }
    }

    private void ApplyMovement(float dt)
    {
        // rotación visual del kart
        Quaternion targetRot = Quaternion.Euler(0f, facingYaw, 0f);
        rb.MoveRotation(targetRot);

        // dirección real del movimiento (puede diferir del morro durante el drift)
        Vector3 moveDir = Quaternion.Euler(0f, velocityYaw, 0f) * Vector3.forward;
        Vector3 targetVelocity = moveDir * currentSpeed;
        targetVelocity.y = rb.linearVelocity.y; // conservar gravedad/salto

        rb.linearVelocity = targetVelocity;
    }

    private void UpdateVisuals()
    {
        // Ejemplo simple: girar visualmente las ruedas delanteras según el steer,
        // si tenés transforms asignados podés extenderlo acá.
    }

    private void SetDriftEffects(bool active)
    {
        if (skidTrails != null)
        {
            foreach (var trail in skidTrails)
            {
                if (trail != null) trail.emitting = active;
            }
        }

        if (driftSparksVFX != null)
        {
            foreach (var sparks in driftSparksVFX)
            {
                if (sparks == null) continue;
                if (active) sparks.Play();
                else sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting); // deja de emitir pero las chispas ya lanzadas terminan su vida naturalmente
            }
        }
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }
}