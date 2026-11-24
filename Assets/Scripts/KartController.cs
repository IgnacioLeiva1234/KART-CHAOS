using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class KartController : MonoBehaviour, IPunInstantiateMagicCallback
{
    [Header("Movimiento")]
    [SerializeField] private float maxSpeed = 16f;
    [SerializeField] private float reverseMaxSpeed = 6f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float brakeForce = 22f;
    [SerializeField] private float naturalFriction = 12f;
    [SerializeField] private float turnRate = 75f;

    [Header("Contacto con el piso")]
    [Min(0.02f)] [SerializeField] private float groundProbeDistance = 0.25f;
    [Range(0f, 80f)] [SerializeField] private float maxGroundAngle = 60f;
    [Min(1f)] [SerializeField] private float groundAlignmentSpeed = 12f;
    [Min(1f)] [SerializeField] private float groundNormalSmoothing = 8f;
    [Min(30f)] [SerializeField] private float maxGroundRotationSpeed = 110f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Derrape")]
    [SerializeField] private float minDriftSpeed = 7f;
    [SerializeField] private float driftTurnMultiplier = 1.2f;
    [SerializeField] private float driftSlipLerpSpeed = 3.5f;
    [SerializeField] private float gripLerpSpeed = 10f;
    [SerializeField] private float driftRecoveryTime = 0.25f;
    [SerializeField] private float driftChargeTime = 1.6f;
    [SerializeField] private float tier1Threshold = 0.35f;
    [SerializeField] private float tier2Threshold = 0.6f;
    [SerializeField] private float tier3Threshold = 0.85f;

    [Header("Mini-Turbo")]
    [Tooltip("Aumento instantáneo de velocidad al activar el turbo.")]
    [SerializeField] private float boostInstantKick = 3f;

    [Tooltip("Velocidad extra por encima de maxSpeed durante el turbo.")]
    [SerializeField] private float boostSpeedBonus = 2f;

    [Tooltip("Rapidez con la que el turbo alcanza su velocidad objetivo.")]
    [SerializeField] private float boostAcceleration = 25f;

    [Tooltip("Rapidez con la que desaparece la sobrevelocidad del turbo.")]
    [SerializeField] private float boostDecayRate = 6f;

    [SerializeField] private float tier1Duration = 0.6f;
    [SerializeField] private float tier2Duration = 0.85f;
    [SerializeField] private float tier3Duration = 1.1f;

    [SerializeField] private float tier1Power = 1f;
    [SerializeField] private float tier2Power = 1.3f;
    [SerializeField] private float tier3Power = 1.6f;

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
    private Collider[] groundProbeColliders;
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private readonly ContactPoint[] collisionContacts = new ContactPoint[24];
    private readonly Vector3[] wallNormals = new Vector3[24];
    private int wallNormalCount;
    private int groundContactCount;
    private Vector3 groundContactNormalSum;
    private Vector3 firstGroundContact;
    private bool groundContactsHaveSpan;
    private Vector3 lastAppliedVelocity;
    private Vector3 groundNormal = Vector3.up;
    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal => groundNormal;

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
    public SpawnPlayers spawnPlayers;
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
        // Calcula el centro de masa donde estan los colliders, aunque el modelo este desplazado.
        rb.ResetCenterOfMass();
        // Los impactos no pueden volcar el kart. MoveRotation controla su inclinación al terreno.
        rb.constraints |= RigidbodyConstraints.FreezeRotation;
        rb.maxDepenetrationVelocity = 3f;
        rb.collisionDetectionMode = rb.isKinematic
            ? CollisionDetectionMode.Discrete
            : CollisionDetectionMode.ContinuousDynamic;
        var ownColliders = GetComponentsInChildren<Collider>();
        groundProbeColliders = System.Array.FindAll(ownColliders,
            c => !c.isTrigger && c.attachedRigidbody == rb && c.name.StartsWith("Rueda"));
        if (groundProbeColliders.Length == 0)
            groundProbeColliders = System.Array.FindAll(ownColliders,
                c => !c.isTrigger && c.attachedRigidbody == rb);

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
        rb.angularVelocity = Vector3.zero;
        IsGrounded = FindGroundNormal(out Vector3 sampledNormal);
        groundContactCount = 0;
        groundContactNormalSum = Vector3.zero;
        groundContactsHaveSpan = false;
        // Un salto conserva su velocidad de salida aunque los rayos todavía alcancen el piso.
        if (IsGrounded && Vector3.Dot(rb.linearVelocity, sampledNormal) > 2f)
            IsGrounded = false;
        if (IsGrounded)
            groundNormal = Vector3.Slerp(groundNormal, sampledNormal,
                1f - Mathf.Exp(-groundNormalSmoothing * dt)).normalized;
        Vector3 moveDir = Quaternion.Euler(0f, velocityYaw, 0f) * Vector3.forward;
        Vector3 targetVelocity;
        if (IsGrounded)
        {
            // La dirección y la inclinación siguen la superficie real bajo las ruedas.
            Vector3 facing = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
            facing = Vector3.ProjectOnPlane(facing, groundNormal).normalized;
            Quaternion targetRot = Quaternion.LookRotation(facing, groundNormal);
            float alignment = 1f - Mathf.Exp(-groundAlignmentSpeed * dt);
            Quaternion smoothRotation = Quaternion.Slerp(rb.rotation, targetRot, alignment);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, smoothRotation, maxGroundRotationSpeed * dt));

            // Para la velocidad se usa la normal medida; el suavizado es solo visual/orientación.
            moveDir = Vector3.ProjectOnPlane(moveDir, sampledNormal).normalized;
            driveVelocityPreBump = moveDir * currentSpeed;
            // Conserva la velocidad perpendicular al terreno: la física resuelve el contacto.
            Vector3 normalVelocity = Vector3.Project(rb.linearVelocity, sampledNormal);
            targetVelocity = driveVelocityPreBump + Vector3.ProjectOnPlane(bumpVelocity, sampledNormal) + normalVelocity;
        }
        else
        {
            // En el aire no endereza el kart ni modifica su caída o salto.
            driveVelocityPreBump = moveDir * currentSpeed;
            targetVelocity = driveVelocityPreBump + bumpVelocity;
            targetVelocity.y = rb.linearVelocity.y;
        }
        bool pushingWall = RemoveWallVelocity(ref driveVelocityPreBump);
        RemoveWallVelocity(ref targetVelocity);
        RemoveWallVelocity(ref bumpVelocity);
        if (pushingWall) StopPushingWall();
        wallNormalCount = 0;
        lastAppliedVelocity = targetVelocity;
        rb.linearVelocity = targetVelocity;

        // Reduce el empujón para el próximo paso de física.
        bumpVelocity = Vector3.MoveTowards(
            bumpVelocity,
            Vector3.zero,
            bumpDecay * dt
        );
    }

    private bool FindGroundNormal(out Vector3 normal)
    {
        normal = Vector3.up;
        if (groundProbeColliders == null || groundProbeColliders.Length == 0) return false;
        Vector3 normalSum = Vector3.zero;
        int supports = 0;
        float minimumUp = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        foreach (var probe in groundProbeColliders)
        {
            if (probe == null || !probe.enabled || !probe.gameObject.activeInHierarchy) continue;
            Bounds bounds = probe.bounds;
            const float startHeight = 0.2f;
            Vector3 origin = bounds.center + Vector3.up * startHeight;
            float distance = bounds.extents.y + startHeight + groundProbeDistance;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance,
                groundLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Vector3 nearestNormal = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider.attachedRigidbody == rb || hit.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<KartController>() != null) continue;
                if (Vector3.Dot(hit.normal, Vector3.up) < minimumUp || hit.distance >= nearest) continue;
                nearest = hit.distance;
                nearestNormal = hit.normal;
            }
            if (nearest == float.MaxValue) continue;
            normalSum += nearestNormal;
            supports++;
        }
        // Una sola rueda en una esquina no debe orientar todo el kart como si fuera una rampa.
        int requiredSupports = Mathf.Min(2, groundProbeColliders.Length);
        if (supports < requiredSupports)
        {
            // Al entrar en una pendiente puede apoyar primero el chasis, antes que los rayos
            // de las ruedas. Dos contactos separados permiten inclinarlo sin usar un solo borde.
            if (groundContactCount < 2 || !groundContactsHaveSpan) return false;
            normal = groundContactNormalSum.normalized;
            return true;
        }
        normal = normalSum.normalized;
        return true;
    }

    private void OnCollisionEnter(Collision collision) { RecordWallContacts(collision); }
    private void OnCollisionStay(Collision collision) { RecordWallContacts(collision); }

    private void RecordWallContacts(Collision collision)
    {
        if (rb == null || rb.isKinematic || view == null || !view.IsMine) return;
        int contactCount = collision.GetContacts(collisionContacts);
        float minimumGroundUp = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        bool canSupport = collision.collider.GetComponentInParent<KartController>() == null;
        bool blocked = false;
        for (int i = 0; i < contactCount; i++)
        {
            Vector3 normal = collisionContacts[i].normal;
            if (Vector3.Dot(normal, Vector3.up) >= minimumGroundUp)
            {
                if (canSupport)
                {
                    Vector3 point = collisionContacts[i].point;
                    if (groundContactCount == 0) firstGroundContact = point;
                    else if ((point - firstGroundContact).sqrMagnitude > 0.01f)
                        groundContactsHaveSpan = true;
                    groundContactCount++;
                    groundContactNormalSum += normal;
                }
                continue;
            }
            Vector3 horizontalNormal = Vector3.ProjectOnPlane(normal, Vector3.up);
            if (horizontalNormal.sqrMagnitude < 0.1f) continue;
            horizontalNormal.Normalize();
            if (Vector3.Dot(lastAppliedVelocity, horizontalNormal) >= -0.05f) continue;
            blocked = true;
            bool alreadyRecorded = false;
            for (int j = 0; j < wallNormalCount; j++)
                if (Vector3.Dot(wallNormals[j], horizontalNormal) > 0.98f) { alreadyRecorded = true; break; }
            if (!alreadyRecorded && wallNormalCount < wallNormals.Length)
                wallNormals[wallNormalCount++] = horizontalNormal;
        }
        if (!blocked) return;

        // Un borde puede convertir el empuje del motor en un salto artificial. Conserva únicamente
        // la subida que ya tenía antes del contacto (por ejemplo al salir de una rampa).
        Vector3 velocity = rb.linearVelocity;
        float allowedRise = Mathf.Max(0f, lastAppliedVelocity.y);
        if (velocity.y > allowedRise) velocity.y = allowedRise;
        rb.linearVelocity = velocity;
    }

    private bool RemoveWallVelocity(ref Vector3 velocity)
    {
        bool blocked = false;
        // Dos pasadas también resuelven una esquina formada por dos paredes.
        for (int pass = 0; pass < 2; pass++)
        for (int i = 0; i < wallNormalCount; i++)
        {
            float intoWall = Vector3.Dot(velocity, wallNormals[i]);
            if (intoWall >= -0.01f) continue;
            velocity -= wallNormals[i] * intoWall;
            blocked = true;
        }
        return blocked;
    }

    private void StopPushingWall()
    {
        currentSpeed = 0f;
        overspeed = 0f;
        boostTimer = 0f;
        boostTier = 0;
        boostTotalDuration = 0f;
        if (!isDrifting) return;
        isDrifting = false;
        driftTimer = 0f;
        driftCharge = 0f;
        SetDriftEffects(false);
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
        bumpVelocity = Vector3.zero;
        groundNormal = Vector3.up;
        IsGrounded = false;
        wallNormalCount = 0;
        groundContactCount = 0;
        groundContactNormalSum = Vector3.zero;
        groundContactsHaveSpan = false;
        lastAppliedVelocity = Vector3.zero;
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
        playerCamera.SetActive(view.IsMine);
        rb.collisionDetectionMode = view.IsMine
            ? CollisionDetectionMode.ContinuousDynamic
            : CollisionDetectionMode.Discrete;
        rb.isKinematic = !view.IsMine;

        if (info.Sender != null)
            Debug.Log ("Objeto instanciado por: " + info.Sender.NickName);
    }
}
