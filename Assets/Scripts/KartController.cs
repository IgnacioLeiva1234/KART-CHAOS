using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class KartController : MonoBehaviour, IPunInstantiateMagicCallback
{
    [Header("Movimiento")]
    [SerializeField] private float maxSpeed = 32f;
    [SerializeField] private float reverseMaxSpeed = 12f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float brakeForce = 36f;
    [SerializeField] private float naturalFriction = 12f;
    [SerializeField] private float turnRate = 140f;

    [Header("Derrape")]
    [SerializeField] private float minDriftSpeed = 7f;
    [SerializeField] private float driftTurnMultiplier = 1.6f;
    [SerializeField] private float driftSlipLerpSpeed = 3.5f;
    [SerializeField] private float gripLerpSpeed = 14f;
    [SerializeField] private float driftRecoveryTime = 0.5f;
    [SerializeField] private float driftChargeTime = 1.6f;
    [SerializeField] private float tier1Threshold = 0.35f;
    [SerializeField] private float tier2Threshold = 0.6f;
    [SerializeField] private float tier3Threshold = 0.85f;

    [Header("Mini-Turbo")]
    [Tooltip("Aumento instantáneo de velocidad al activar el turbo.")]
    [SerializeField] private float boostInstantKick = 8f;

    [Tooltip("Velocidad extra por encima de maxSpeed durante el turbo.")]
    [SerializeField] private float boostSpeedBonus = 6f;

    [Tooltip("Rapidez con la que el turbo alcanza su velocidad objetivo.")]
    [SerializeField] private float boostAcceleration = 90f;

    [Tooltip("Rapidez con la que desaparece la sobrevelocidad del turbo.")]
    [SerializeField] private float boostDecayRate = 6f;

    [SerializeField] private float tier1Duration = 0.6f;
    [SerializeField] private float tier2Duration = 0.85f;
    [SerializeField] private float tier3Duration = 1.1f;

    [SerializeField] private float tier1Power = 1f;
    [SerializeField] private float tier2Power = 1.5f;
    [SerializeField] private float tier3Power = 2.2f;

    [Header("Choque entre los karts")]
    [SerializeField] private float minBumpSpeed = 2f;
    [SerializeField] private float bumpMultiplier = 0.65f;
    [SerializeField] private float maxBumpSpeed = 8f;
    [SerializeField] private float bumpDecay = 12f;

    [Header("Referencias opcionales")]
    [SerializeField] private Transform[] rearWheelTransforms;
    [SerializeField] private ParticleSystem boostVFX;
    [SerializeField] private TrailRenderer[] skidTrails;
    [SerializeField] private ParticleSystem[] driftSparksVFX;

    // Componentes del kart.
    private Rigidbody rb;
    private PhotonView view;

    // Objetos hijos.
    private GameObject playerCamera;
    private GameObject skidPointIzq;
    private GameObject skidPointDer;
    private GameObject particleSystem;
    private GameObject firePoint;
    private GameObject mineDropPoint;
    private GameObject healthCanvas;

    // Estado del movimiento.
    private float currentSpeed;
    private float facingYaw;
    private float velocityYaw;

    // Estado del derrape.
    private bool isDrifting;
    private float driftDir;
    private float driftCharge;
    private float driftTimer;
    private float driftRecoveryTimer;

    // Estado del turbo.
    private float boostTimer;
    private int boostTier;
    private float overspeed;
    private float boostTotalDuration;

    // Estado de los empujones.
    private Vector3 driveVelocityPreBump;
    private Vector3 bumpVelocity;

    // Información para la cámara, el audio y la interfaz.
    public System.Action<int> OnBoostStarted;

    public float BoostIntensity01 =>
        boostTimer > 0f && boostTotalDuration > 0f
            ? Mathf.Clamp01(boostTimer / boostTotalDuration)
            : 0f;

    public bool IsBoosting => boostTimer > 0f;
    public float SpeedKmh => Mathf.Abs(currentSpeed) * 3.6f;
    public float DriftCharge01 => driftCharge;
    public bool IsDrifting => isDrifting;
    public float Tier1Threshold => tier1Threshold;
    public float Tier2Threshold => tier2Threshold;
    public float Tier3Threshold => tier3Threshold;

    private void Awake()
    {
        view = GetComponent<PhotonView>();

        Debug.Log(
            "Auto: " + gameObject.name +
            " | IsMine: " + view.IsMine +
            " | Owner: " + view.OwnerActorNr
        );

        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.4f, 0f);

        facingYaw = transform.eulerAngles.y;
        velocityYaw = facingYaw;

        playerCamera = transform.Find("Main Camera").gameObject;
        skidPointIzq = transform.Find("SkidPoint_Izq").gameObject;
        skidPointDer = transform.Find("SkidPoint_Der").gameObject;
        particleSystem = transform.Find("Particle System(TURBO)").gameObject;
        firePoint = transform.Find("FirePoint").gameObject;
        mineDropPoint = transform.Find("MineDropPoint").gameObject;
        healthCanvas = transform.Find("HealthCanvas").gameObject;
    }

    private void Update()
    {
    }

    private void FixedUpdate()
    {
        if (view.IsMine)
        {
            float dt = Time.fixedDeltaTime;

            bool forward = Input.GetKey(KeyCode.W);
            bool back = Input.GetKey(KeyCode.S);
            bool left = Input.GetKey(KeyCode.A);
            bool right = Input.GetKey(KeyCode.D);

            bool wantDrift =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);

            HandleAcceleration(forward, back, dt);
            HandleSteeringAndDrift(left, right, wantDrift, dt);
            HandleBoost(dt);
            ApplyMovement(dt);
            UpdateVisuals();
        }
    }

    private void HandleAcceleration(bool forward, bool back, float dt)
    {
        if (forward)
        {
            currentSpeed += acceleration * dt;
        }
        else if (back)
        {
            currentSpeed -=
                (currentSpeed > 0f ? brakeForce : acceleration) * dt;
        }
        else
        {
            float friction = naturalFriction * dt;

            if (currentSpeed > 0f)
            {
                currentSpeed = Mathf.Max(0f, currentSpeed - friction);
            }
            else if (currentSpeed < 0f)
            {
                currentSpeed = Mathf.Min(0f, currentSpeed + friction);
            }
        }

        float topSpeed = maxSpeed + overspeed;
        currentSpeed = Mathf.Clamp(currentSpeed, -reverseMaxSpeed, topSpeed);
    }

    private void HandleSteeringAndDrift(
        bool left,
        bool right,
        bool wantDrift,
        float dt)
    {
        float steer = (left ? -1f : 0f) + (right ? 1f : 0f);
        float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / 10f);

        bool canStartDrift =
            wantDrift &&
            steer != 0f &&
            Mathf.Abs(currentSpeed) > minDriftSpeed;

        if (canStartDrift && !isDrifting)
        {
            isDrifting = true;
            driftDir = steer;
            driftCharge = 0f;
            driftTimer = 0f;

            SetDriftEffects(true);
        }

        bool shouldStopDrift =
            isDrifting &&
            (
                !wantDrift ||
                steer == 0f ||
                Mathf.Abs(currentSpeed) < minDriftSpeed * 0.6f
            );

        if (shouldStopDrift)
        {
            ReleaseDrift();
        }

        if (isDrifting)
        {
            driftTimer += dt;
            driftCharge = Mathf.Clamp01(driftTimer / driftChargeTime);

            facingYaw +=
                driftDir *
                turnRate *
                driftTurnMultiplier *
                dt *
                (0.5f + speedFactor * 0.5f);

            facingYaw = NormalizeAngle(facingYaw);

            velocityYaw = Mathf.LerpAngle(
                velocityYaw,
                facingYaw,
                dt * driftSlipLerpSpeed
            );
        }
        else
        {
            facingYaw +=
                steer *
                turnRate *
                dt *
                (0.35f + speedFactor * 0.65f) *
                (currentSpeed < 0f ? -1f : 1f);

            facingYaw = NormalizeAngle(facingYaw);

            float effectiveGrip;

            if (driftRecoveryTimer > 0f)
            {
                driftRecoveryTimer -= dt;

                float t = 1f - Mathf.Clamp01(
                    driftRecoveryTimer / driftRecoveryTime
                );

                effectiveGrip = Mathf.Lerp(
                    driftSlipLerpSpeed,
                    gripLerpSpeed,
                    t
                );
            }
            else
            {
                effectiveGrip = gripLerpSpeed;
            }

            velocityYaw = Mathf.LerpAngle(
                velocityYaw,
                facingYaw,
                dt * effectiveGrip
            );
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

            overspeed = Mathf.Max(
                overspeed,
                boostSpeedBonus * tierPower
            );

            currentSpeed = Mathf.Min(
                maxSpeed + overspeed,
                currentSpeed + boostInstantKick * tierPower
            );

            OnBoostStarted?.Invoke(boostTier);

            if (boostVFX != null)
            {
                var main = boostVFX.main;

                main.startColor =
                    boostTier == 3
                        ? new Color(0.72f, 0.24f, 0.88f)
                        : boostTier == 2
                            ? new Color(0.88f, 0.28f, 0.25f)
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

            float target = maxSpeed + overspeed;

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                target,
                boostAcceleration * dt
            );

            if (boostTimer <= 0f)
            {
                boostTier = 0;
                boostTotalDuration = 0f;
            }
        }
        else if (overspeed > 0f)
        {
            overspeed = Mathf.MoveTowards(
                overspeed,
                0f,
                boostDecayRate * dt
            );
        }
    }

    private void ApplyMovement(float dt)
    {
        // Gira el kart hacia donde apunta el frente.
        Quaternion targetRot = Quaternion.Euler(0f, facingYaw, 0f);
        rb.MoveRotation(targetRot);

        // Calcula la velocidad de manejo, incluido el derrape.
        Vector3 moveDir =
            Quaternion.Euler(0f, velocityYaw, 0f) * Vector3.forward;

        driveVelocityPreBump = moveDir * currentSpeed;

        // Suma el empujón y conserva la velocidad vertical.
        Vector3 targetVelocityAfterBump =
            driveVelocityPreBump + bumpVelocity;

        targetVelocityAfterBump.y = rb.linearVelocity.y;
        rb.linearVelocity = targetVelocityAfterBump;

        // Reduce el empujón para el próximo paso de física.
        bumpVelocity = Vector3.MoveTowards(
            bumpVelocity,
            Vector3.zero,
            bumpDecay * dt
        );
    }

    private void UpdateVisuals()
    {
    }

    private void SetDriftEffects(bool active)
    {
        if (skidTrails != null)
        {
            foreach (var trail in skidTrails)
            {
                if (trail != null)
                {
                    trail.emitting = active;
                }
            }
        }

        if (driftSparksVFX != null)
        {
            foreach (var sparks in driftSparksVFX)
            {
                if (sparks == null)
                {
                    continue;
                }

                if (active)
                {
                    sparks.Play();
                }
                else
                {
                    sparks.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmitting
                    );
                }
            }
        }
    }

    // Lo llama DestructibleTarget al reaparecer el kart.
    private void OnRespawned()
    {
        currentSpeed = 0f;
        facingYaw = transform.eulerAngles.y;
        velocityYaw = facingYaw;

        isDrifting = false;
        driftCharge = 0f;
        driftTimer = 0f;
        driftRecoveryTimer = 0f;

        boostTimer = 0f;
        boostTier = 0;
        overspeed = 0f;
        boostTotalDuration = 0f;

        SetDriftEffects(false);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle;
    }

    public void OnPhotonInstantiate (PhotonMessageInfo info)
    {
        Debug.Log ("Objeto instanciado por: " + info.Sender.NickName);
    }
}