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
    [SerializeField] private float boostAcceleration = 60f;
    [SerializeField] private float boostSpeedBonus = 10f;
    [SerializeField] private float tier1Duration = 0.6f;
    [SerializeField] private float tier2Duration = 0.85f;
    [SerializeField] private float tier3Duration = 1.1f;

    [Header("Referencias opcionales")]
    [SerializeField] private Transform[] rearWheelTransforms;
    [SerializeField] private ParticleSystem boostVFX;
    [SerializeField] private TrailRenderer[] skidTrails; // uno por rueda trasera, activar/desactivar según drift

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

    // Propiedades públicas por si un HUD quiere leerlas
    public float SpeedKmh => Mathf.Abs(currentSpeed) * 3.6f;
    public float DriftCharge01 => driftCharge;
    public bool IsDrifting => isDrifting;

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

        float topSpeed = maxSpeed + (boostTimer > 0f ? boostSpeedBonus : 0f);
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
            SetSkidTrails(true);
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
        if (driftCharge >= tier3Threshold)
        {
            boostTier = 3;
            boostTimer = tier3Duration;
        }
        else if (driftCharge >= tier2Threshold)
        {
            boostTier = 2;
            boostTimer = tier2Duration;
        }
        else if (driftCharge >= tier1Threshold)
        {
            boostTier = 1;
            boostTimer = tier1Duration;
        }
        else
        {
            boostTier = 0;
        }

        if (boostTier > 0 && boostVFX != null)
        {
            var main = boostVFX.main;
            main.startColor = boostTier == 3 ? new Color(0.72f, 0.24f, 0.88f)
                             : boostTier == 2 ? new Color(0.88f, 0.28f, 0.25f)
                             : new Color(0.25f, 0.65f, 0.88f);
            boostVFX.Play();
        }

        isDrifting = false;
        driftCharge = 0f;
        driftRecoveryTimer = driftRecoveryTime;
        SetSkidTrails(false);
    }

    private void HandleBoost(float dt)
    {
        if (boostTimer > 0f)
        {
            boostTimer -= dt;
            currentSpeed = Mathf.Min(maxSpeed + boostSpeedBonus, currentSpeed + boostAcceleration * dt);
            if (boostTimer <= 0f) boostTier = 0;
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

    private void SetSkidTrails(bool active)
    {
        if (skidTrails == null) return;
        foreach (var trail in skidTrails)
        {
            if (trail != null) trail.emitting = active;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }
}