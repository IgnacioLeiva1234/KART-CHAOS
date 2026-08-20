using UnityEngine;

/// <summary>
/// Controlador básico de auto para Unity usando Rigidbody.
/// Permite acelerar, frenar, retroceder y girar.
/// Requiere un Rigidbody en el mismo GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    
    [Header("Wheels")]
    [SerializeField] private WheelCollider frontLeftCollider;
    [SerializeField] private WheelCollider frontRightCollider;
    [SerializeField] private WheelCollider rearLeftCollider;
    [SerializeField] private WheelCollider rearRightCollider;
    [SerializeField] private Transform frontLeftMesh;
    [SerializeField] private Transform frontRightMesh;
    [SerializeField] private Transform rearLeftMesh;
    [SerializeField] private Transform rearRightMesh;
    [Header("Performance")]
    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float reverseForce = 800f;
    [SerializeField] private float brakeForce = 3000f;
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private float maxSpeedKmh = 120f;
    [SerializeField] private float reverseEngageSpeedKmh = 5f;
    [Header("Stability")]
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private float downforce = 50f;
    private Rigidbody _rb;
    private float _steerInput;
    private float _throttleInput;
    private bool _handbrake;
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (centerOfMass != null)
        {
            _rb.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }
    }
    private void Update()
    {
        _steerInput = Input.GetAxis("Horizontal");
        _throttleInput = Input.GetAxis("Vertical");
        _handbrake = Input.GetButton("Jump");
    }
    private void FixedUpdate()
    {
        if (!HasWheels())
        {
            return;
        }
        float speedKmh = _rb.linearVelocity.magnitude * 3.6f;
        float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
        float steer = _steerInput * maxSteerAngle;
        frontLeftCollider.steerAngle = steer;
        frontRightCollider.steerAngle = steer;
        float motor = 0f;
        float brake = 0f;
        if (_handbrake)
        {
            brake = brakeForce;
        }
        else if (Mathf.Abs(_throttleInput) > 0.01f)
        {
            bool wantsForward = _throttleInput > 0f;
            bool movingForward = forwardSpeed > 0.5f;
            bool movingBackward = forwardSpeed < -0.5f;
            if (wantsForward && movingBackward)
            {
                brake = brakeForce;
            }
            else if (!wantsForward && movingForward)
            {
                brake = brakeForce;
            }
            else if (wantsForward)
            {
                if (speedKmh < maxSpeedKmh)
                {
                    motor = _throttleInput * motorForce;
                }
            }
            else
            {
                if (speedKmh < reverseEngageSpeedKmh || movingBackward)
                {
                    motor = _throttleInput * reverseForce;
                }
                else
                {
                    brake = brakeForce;
                }
            }
        }
        ApplyDrive(rearLeftCollider, motor, brake);
        ApplyDrive(rearRightCollider, motor, brake);
        ApplyDrive(frontLeftCollider, 0f, brake);
        ApplyDrive(frontRightCollider, 0f, brake);
        UpdateWheelVisual(frontLeftCollider, frontLeftMesh);
        UpdateWheelVisual(frontRightCollider, frontRightMesh);
        UpdateWheelVisual(rearLeftCollider, rearLeftMesh);
        UpdateWheelVisual(rearRightCollider, rearRightMesh);
        if (downforce > 0f)
        {
            _rb.AddForce(-transform.up * downforce * _rb.linearVelocity.magnitude);
        }
    }
    private static void ApplyDrive(WheelCollider wheel, float motor, float brake)
    {
        wheel.motorTorque = motor;
        wheel.brakeTorque = brake;
    }
    private static void UpdateWheelVisual(WheelCollider collider, Transform mesh)
    {
        if (mesh == null)
        {
            return;
        }
        collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.SetPositionAndRotation(pos, rot);
    }
    private bool HasWheels()
    {
        return frontLeftCollider != null
            && frontRightCollider != null
            && rearLeftCollider != null
            && rearRightCollider != null;
    }
}