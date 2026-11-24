using UnityEngine;

public class MolotovPowerUp : KartPowerUp
{
    [Header("Lanzamiento")]
    [SerializeField] private Transform launchPoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 22f;
    [SerializeField] private float chargeTime = 1.5f;
    [SerializeField] private float arcHeight = 4f;
    [SerializeField] private float cooldown = .5f;
    [Header("Vista previa")]
    [SerializeField] private Material trajectoryMaterial;
    [SerializeField] private Material landingMaterial;
    [SerializeField] private float collisionRadius = .18f;
    [SerializeField] private float fireRadius = 2.5f;
    [SerializeField] private int segments = 64;
    public override PowerUpType Type => PowerUpType.Molotov;
    public override string DisplayName => "Molotov";
    public override bool UsesCharge => true;
    public override bool IsReady => launchPoint != null && projectilePrefab != null;
    public float Charge01 => charge;
    public float AimDistance => Mathf.Lerp(minDistance, maxDistance, charge);
    public Vector3 LandingPoint { get; private set; }
    public Vector3 LaunchVelocity => velocity;
    public Vector3 LaunchOrigin => origin;
    public float FlightTime => flightTime;
    public bool IsAiming => aiming;
    private bool aiming, validAim;
    private float charge, flightTime;
    private Vector3 origin, velocity, gravity, landingNormal;
    private LineRenderer arc, ring;

    public override bool BeginCharge(GameObject owner)
    {
        if (!IsReady || OnCooldown || aiming) return false;
        charge = 0f;
        aiming = true;
        EnsurePreview();
        UpdateCharge(owner, 0f);
        return true;
    }
    public override void UpdateCharge(GameObject owner, float dt)
    {
        if (!aiming) return;
        charge = Mathf.Clamp01(charge + Mathf.Max(0f, dt) / Mathf.Max(.01f, chargeTime));
        validAim = Calculate(owner);
        arc.enabled = validAim;
        ring.enabled = validAim;
        if (validAim) DrawPreview(owner);
    }
    public override bool ReleaseCharge(GameObject owner)
    {
        if (!aiming) return false;
        // Usar la última trayectoria visible: al soltar no se recalcula otra distancia.
        bool launch = validAim && IsReady && !OnCooldown;
        CancelCharge();
        if (!launch) return false;
        var shot = PowerUpNetwork.Spawn(projectilePrefab, origin, Quaternion.LookRotation(velocity), owner,
            velocity, gravity, flightTime, collisionRadius, fireRadius);
        if (shot == null) return false;
        StartCooldown(cooldown);
        return true;
    }
    public override bool Use(GameObject owner)
    {
        return BeginCharge(owner) && ReleaseCharge(owner);
    }
    public override void CancelCharge()
    {
        aiming = false;
        if (arc != null) arc.enabled = false;
        if (ring != null) ring.enabled = false;
    }
    private void OnDisable() => CancelCharge();
    private void OnDestroy()
    {
        if (arc != null) Destroy(arc.gameObject);
        if (ring != null) Destroy(ring.gameObject);
    }

    private bool Calculate(GameObject owner)
    {
        origin = launchPoint.position + Vector3.up * .35f;
        Vector3 forward = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
        Vector3 desired = owner.transform.position + forward * AimDistance;
        float best = float.MaxValue;
        RaycastHit ground = default;
        foreach (var hit in Physics.RaycastAll(desired + Vector3.up * 25f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(owner.transform) || hit.collider.GetComponentInParent<NetworkHazard>() != null) continue;
            if (hit.normal.y < .25f || hit.distance >= best) continue;
            best = hit.distance;
            ground = hit;
        }
        if (best == float.MaxValue) return false;
        Vector3 end = ground.point + ground.normal * collisionRadius;
        gravity = new Vector3(0f, Mathf.Min(-.1f, Physics.gravity.y), 0f);
        float g = -gravity.y;
        float apex = Mathf.Max(origin.y, end.y) + Mathf.Max(.5f, arcHeight);
        float upTime = Mathf.Sqrt(2f * (apex - origin.y) / g);
        float downTime = Mathf.Sqrt(2f * (apex - end.y) / g);
        flightTime = upTime + downTime;
        velocity = (end - origin) / flightTime;
        velocity.y = g * upTime;
        LandingPoint = ground.point;
        landingNormal = ground.normal;
        return true;
    }

    private void EnsurePreview()
    {
        if (arc == null) arc = MakeLine("MolotovTrajectory", trajectoryMaterial, .065f, false);
        if (ring == null) ring = MakeLine("MolotovLandingArea", landingMaterial, .075f, true);
    }
    private static LineRenderer MakeLine(string name, Material material, float width, bool loop)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = material;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.loop = loop;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }
    private void DrawPreview(GameObject owner)
    {
        int count = Mathf.Clamp(segments, 16, 128);
        arc.positionCount = count + 1;
        Vector3 previous = origin;
        arc.SetPosition(0, origin);
        for (int i = 1; i <= count; i++)
        {
            Vector3 point = MolotovTrajectory.Position(origin, velocity, gravity, flightTime * i / count);
            if (MolotovTrajectory.FirstHit(previous, point, collisionRadius, owner, null, out var hit))
            {
                LandingPoint = hit.point;
                landingNormal = hit.normal;
                arc.positionCount = i + 1;
                arc.SetPosition(i, hit.point + hit.normal * collisionRadius);
                break;
            }
            arc.SetPosition(i, point);
            previous = point;
        }
        ring.positionCount = 64;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, landingNormal);
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64;
            ring.SetPosition(i, LandingPoint + landingNormal * .04f + rotation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * fireRadius);
        }
    }
}
