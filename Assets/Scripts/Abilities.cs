using UnityEngine;
using UnityEngine.InputSystem;

public class Abilities : MonoBehaviour {
    [Header("References")]
    public Transform cameraHolder;
    public PlayerMotor motor;

    [Header("Grapple Settings")]
    public float grappleRange = 30f;
    public float grappleSpeed = 20f;
    public float grappleCooldown = 5f;
    public float arrivalDistance = 2f;
    public float grapplePullStrength = 25f;
    public float maxGrappleSpeed = 35f;
    public float launchUpward = 4f;
    public LayerMask grappleMask;

    [Header("Swing Settings")]
    public bool swingMode = true;
    public float swingRadius = 15f;

    [Header("Grapple Visual")]
    public LineRenderer ropeRenderer;

    [Header("Stim Settings")]
    public float stimDuration = 4f;     // How long stim lasts
    public float stimCooldown = 10f;    // Cooldown after stim ends
    public float stimSpeedMult = 1.6f;   // Ground speed multiplier
    public float stimAirAccelMult = 2f;     // Air acceleration multiplier
    public float stimJumpMult = 1.2f;   // Jump force multiplier
    public float stimFOVAdd = 15f;    // Extra FOV during stim

    // Grapple state
    private CharacterController _cc;
    private Vector3 _grapplePoint;
    private bool _isGrappling;
    private float _grappleCooldownTimer;
    private float _ropeLength;

    // Stim state
    private bool _isStimmed;
    private float _stimTimer;
    private float _stimCooldownTimer;

    // Stim original values
    private float _baseMaxGroundSpeed;
    private float _baseAirAccelerate;
    private float _baseJumpForce;

    // Camera ref for FOV
    private CameraController _camController;

    public bool IsGrappling => _isGrappling;
    public bool IsStimmed => _isStimmed;
    public float GrappleCooldown => _grappleCooldownTimer;
    public float StimCooldown => _stimCooldownTimer;
    public float StimTimeLeft => _stimTimer;

    void Awake() {
        _cc = GetComponent<CharacterController>();
        _camController = GetComponent<CameraController>();

        // Store base values to restore after stim
        _baseMaxGroundSpeed = motor.maxGroundSpeed;
        _baseAirAccelerate = motor.airAccelerate;
        _baseJumpForce = motor.jumpForce;

        if (ropeRenderer != null)
            ropeRenderer.enabled = false;
    }

    void Update() {
        // Cooldown timers
        if (_grappleCooldownTimer > 0f) _grappleCooldownTimer -= Time.deltaTime;
        if (_stimCooldownTimer > 0f) _stimCooldownTimer -= Time.deltaTime;

        // Q — grapple
        if (Keyboard.current.qKey.wasPressedThisFrame) {
            if (_isGrappling) ReleaseGrapple();
            else TryGrapple();
        }

        // Space — release grapple early
        if (_isGrappling && Keyboard.current.spaceKey.wasPressedThisFrame)
            ReleaseGrapple();

        // E — stim
        if (Keyboard.current.eKey.wasPressedThisFrame)
            TryStim();

        // Stim countdown
        if (_isStimmed) {
            _stimTimer -= Time.deltaTime;
            if (_stimTimer <= 0f)
                EndStim();
        }

        UpdateRopeVisual();
    }

    void FixedUpdate() {
        if (_isGrappling)
            ApplyGrapple();
    }

    // ?? Grapple ????????????????????????????????????????????????

    void TryGrapple() {
        if (_grappleCooldownTimer > 0f) return;

        Ray ray = new Ray(cameraHolder.position, cameraHolder.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, grappleRange, grappleMask)) {
            _grapplePoint = hit.point;
            _isGrappling = true;
            _ropeLength = Vector3.Distance(transform.position, _grapplePoint);
            _grappleCooldownTimer = grappleCooldown;

            if (ropeRenderer != null)
                ropeRenderer.enabled = true;
        }
        else {
            Debug.Log("Grapple missed");
        }
    }

    void ApplyGrapple() {
        Vector3 toPoint = _grapplePoint - transform.position;
        float distance = toPoint.magnitude;
        Vector3 direction = toPoint.normalized;

        if (swingMode) ApplySwing(direction, distance);
        else ApplyDirectPull(direction, distance);

        if (distance < arrivalDistance)
            ReleaseGrapple();
    }

    void ApplySwing(Vector3 direction, float distance) {
        Vector3 vel = motor.Velocity;

        vel += direction * grapplePullStrength * Time.fixedDeltaTime;

        if (distance > _ropeLength) {
            Vector3 awayFromPoint = -direction;
            float outwardSpeed = Mathf.Max(0f, Vector3.Dot(vel, awayFromPoint));
            vel -= awayFromPoint * outwardSpeed;
        }

        motor.SetVelocity(vel);
        _cc.Move(vel * Time.fixedDeltaTime);
    }

    void ApplyDirectPull(Vector3 direction, float distance) {
        Vector3 vel = motor.Velocity;
        vel += direction * grapplePullStrength * Time.fixedDeltaTime;

        float speed = vel.magnitude;
        if (speed > maxGrappleSpeed)
            vel = vel.normalized * maxGrappleSpeed;

        motor.SetVelocity(vel);
        _cc.Move(vel * Time.fixedDeltaTime);
    }

    void ReleaseGrapple() {
        _isGrappling = false;

        Vector3 vel = motor.Velocity;
        vel.y = Mathf.Max(vel.y, launchUpward);
        motor.SetVelocity(vel);
        motor.PreserveMomentum(1.5f);

        if (ropeRenderer != null)
            ropeRenderer.enabled = false;
    }

    void UpdateRopeVisual() {
        if (ropeRenderer == null || !_isGrappling) return;
        ropeRenderer.SetPosition(0, cameraHolder.position);
        ropeRenderer.SetPosition(1, _grapplePoint);
    }

    // ?? Stim ???????????????????????????????????????????????????

    void TryStim() {
        if (_isStimmed) return; // Already stimmed
        if (_stimCooldownTimer > 0f) return; // On cooldown

        _isStimmed = true;
        _stimTimer = stimDuration;

        // Boost motor values directly
        motor.maxGroundSpeed = _baseMaxGroundSpeed * stimSpeedMult;
        motor.airAccelerate = _baseAirAccelerate * stimAirAccelMult;
        motor.jumpForce = _baseJumpForce * stimJumpMult;

        // FOV boost
        if (_camController != null)
            _camController.baseFOV += stimFOVAdd;

        Debug.Log("Stim activated");
    }

    void EndStim() {
        _isStimmed = false;
        _stimCooldownTimer = stimCooldown;

        // Restore base values
        motor.maxGroundSpeed = _baseMaxGroundSpeed;
        motor.airAccelerate = _baseAirAccelerate;
        motor.jumpForce = _baseJumpForce;

        // Restore FOV
        if (_camController != null)
            _camController.baseFOV -= stimFOVAdd;

        Debug.Log("Stim ended");
    }

    // ?? Gizmos ?????????????????????????????????????????????????

    void OnDrawGizmos() {
        if (cameraHolder == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(cameraHolder.position, grappleRange);

        if (_isGrappling) {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_grapplePoint, 0.3f);
            Gizmos.DrawLine(cameraHolder.position, _grapplePoint);
        }
    }
}