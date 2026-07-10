using UnityEngine;

public class CameraController : MonoBehaviour {
    [Header("References")]
    public Transform cameraHolder;
    public Camera playerCamera;
    public PlayerMotor motor;

    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public float bobFrequency = 5f;
    public float bobAmplitude = 0.05f;
    public float bobSmoothSpeed = 10f;

    [Header("Landing Punch")]
    public float landingPunchAmount = 0.08f;
    public float landingPunchSpeed = 10f;

    [Header("FOV")]
    public float baseFOV = 90f;
    public float sprintFOVMultiplier = 1.15f;
    public float wallRunFOVMultiplier = 1.1f;
    public float fovSpeed = 6f;

    [Header("Camera Smoothing")]
    public float positionSmoothSpeed = 20f;

    private WallRunController _wallRun;
    private Vector3 _initialLocalPos;
    private float _bobTimer;
    private float _bobTargetY;
    private float _currentBobY;
    private bool _wasGrounded;
    private float _landingPunch;

    void Awake() {
        _wallRun = motor.GetComponent<WallRunController>();
        _initialLocalPos = cameraHolder.localPosition;
    }

    void LateUpdate() {
        HandleHeadBob();
        HandleLandingPunch();
        HandleFOV();
    }

    void HandleHeadBob() {
        if (!enableHeadBob) return;

        float hSpeed = new Vector3(motor.Velocity.x, 0, motor.Velocity.z).magnitude;
        bool isMoving = hSpeed > 0.5f && motor.IsGrounded;

        if (isMoving) {
            _bobTimer += Time.deltaTime * bobFrequency;
            _bobTargetY = Mathf.Sin(_bobTimer) * bobAmplitude;
        }
        else {
            _bobTimer = 0f;
            _bobTargetY = 0f;
        }

        _currentBobY = Mathf.Lerp(_currentBobY, _bobTargetY, Time.deltaTime * bobSmoothSpeed);

        Vector3 targetPos = _initialLocalPos + new Vector3(0f, _currentBobY + _landingPunch, 0f);
        cameraHolder.localPosition = Vector3.Lerp(
            cameraHolder.localPosition,
            targetPos,
            Time.deltaTime * positionSmoothSpeed
        );
    }

    void HandleLandingPunch() {
        bool grounded = motor.IsGrounded;

        // Just landed
        if (grounded && !_wasGrounded) {
            float fallSpeed = Mathf.Abs(motor.Velocity.y);
            _landingPunch = -landingPunchAmount * Mathf.Clamp01(fallSpeed / 20f);
        }

        _landingPunch = Mathf.Lerp(_landingPunch, 0f, Time.deltaTime * landingPunchSpeed);
        _wasGrounded = grounded;
    }

    void HandleFOV() {
        if (playerCamera == null) return;

        float hSpeed = new Vector3(motor.Velocity.x, 0, motor.Velocity.z).magnitude;
        float speedT = Mathf.Clamp01(hSpeed / (motor.maxGroundSpeed * 1.5f));
        float targetFOV = baseFOV;

        if (_wallRun != null && _wallRun.IsWallRunning)
            targetFOV = baseFOV * wallRunFOVMultiplier;
        else
            targetFOV = Mathf.Lerp(baseFOV, baseFOV * sprintFOVMultiplier, speedT);

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Time.deltaTime * fovSpeed
        );
    }
}