using Unity.Netcode;
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
    public Vector3 GrapplePoint => _grapplePoint;

    [Header("Swing Settings")]
    public bool swingMode = true;
    public float swingRadius = 15f;

    [Tooltip("How strongly WASD steers the swing. The rope constraint always keeps the " +
             "pull dominant, so input can never cancel the grapple.")]
    [Range(0f, 1f)] public float swingAirControl = 0.6f;

    [Tooltip("Acceleration applied by WASD while swinging.")]
    public float swingAirAccel = 16f;

    [Tooltip("How fast the rope reels the player toward the anchor (m/s).")]
    public float reelSpeed = 14f;

    [Tooltip("The rope never reels in closer than this distance from the anchor.")]
    public float minRopeLength = 2.5f;

    [Tooltip("Extra gravity multiplier during the swing for a punchier pendulum.")]
    public float swingGravityMult = 1f;

    [Tooltip("Soft cap on the speed reached while swinging. 0 = uncapped.")]
    public float swingMaxSpeed = 60f;

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
    private PlayerController _playerController;
    private NetworkObject _netObj;
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

    // Only the owning client drives abilities. Remote copies just replay synced state,
    // otherwise every client's keyboard would move every player on screen.
    private bool IsLocalPlayer =>
        _netObj == null || !_netObj.IsSpawned || _netObj.IsOwner;

    void Awake() {
        _cc = GetComponent<CharacterController>();
        _camController = GetComponent<CameraController>();
        _playerController = GetComponent<PlayerController>();
        _netObj = GetComponent<NetworkObject>();

        // Store base values to restore after stim
        _baseMaxGroundSpeed = motor.maxGroundSpeed;
        _baseAirAccelerate = motor.airAccelerate;
        _baseJumpForce = motor.jumpForce;

        if (ropeRenderer != null)
            ropeRenderer.enabled = false;
    }

    void OnDisable() {
        // Never leave the motor locked if this component is disabled mid-grapple.
        _isGrappling = false;
        if (motor != null) motor.SetExternalControl(false);
        if (ropeRenderer != null) ropeRenderer.enabled = false;
    }

    void Update() {
        if (!IsLocalPlayer) return;

        // Cooldown timers
        if (_grappleCooldownTimer > 0f) _grappleCooldownTimer -= Time.deltaTime;
        if (_stimCooldownTimer > 0f) _stimCooldownTimer -= Time.deltaTime;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Q - grapple
        if (kb.qKey.wasPressedThisFrame) {
            if (_isGrappling) ReleaseGrapple();
            else TryGrapple();
        }

        // Space - release grapple early
        if (_isGrappling && kb.spaceKey.wasPressedThisFrame)
            ReleaseGrapple();

        // E - stim
        if (kb.eKey.wasPressedThisFrame)
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
        if (!IsLocalPlayer) return;
        if (_isGrappling)
            ApplyGrapple();
    }

    // -- Grapple -----------------------------------------------------------------

    void TryGrapple() {
        if (motor == null || cameraHolder == null) return;
        if (_grappleCooldownTimer > 0f) return;

        Ray ray = new Ray(cameraHolder.position, cameraHolder.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, grappleRange, grappleMask)) {
            _grapplePoint = hit.point;
            _isGrappling = true;
            _ropeLength = Vector3.Distance(transform.position, _grapplePoint);
            _grappleCooldownTimer = grappleCooldown;

            // Take over movement for the duration of the swing.
            motor.SetExternalControl(true);

            LocalStatsTracker.Instance?.OnGrappleStarted(transform);
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

        if (distance <= arrivalDistance) {
            ReleaseGrapple();
            return;
        }

        Vector3 direction = toPoint / distance;

        if (swingMode) ApplySwing(direction, distance);
        else ApplyDirectPull(direction, distance);
    }

    // Pathfinder-style swing: the rope pulls the player toward the anchor while the
    // rope length acts as a hard constraint, so the player swings like a pendulum.
    // Movement input can steer the swing but can never pull away from the anchor.
    void ApplySwing(Vector3 direction, float distance) {
        Vector3 vel = motor.Velocity;

        // Gravity - the pendulum falls and converts height into speed.
        vel.y -= motor.gravity * swingGravityMult * Time.fixedDeltaTime;

        // Inward pull toward the anchor. This is the dominant force and cannot be
        // cancelled by movement input.
        vel += direction * grapplePullStrength * Time.fixedDeltaTime;

        // Air control: steer the swing, but only on the plane perpendicular to the
        // rope so input can never push the player away from the anchor.
        Vector2 moveInput = _playerController != null ? _playerController.MoveInput : Vector2.zero;
        Transform orient = _playerController != null ? _playerController.orientation : transform;
        if (moveInput.sqrMagnitude > 0.0001f && orient != null) {
            Vector3 wish = orient.right * moveInput.x + orient.forward * moveInput.y;
            wish.y = 0f;
            wish -= direction * Vector3.Dot(wish, direction);
            if (wish.sqrMagnitude > 0.0001f)
                vel += wish.normalized * (swingAirAccel * swingAirControl) * Time.fixedDeltaTime;
        }

        // Reel in: shorten the rope over time so the player is drawn toward the anchor.
        _ropeLength = Mathf.Max(minRopeLength, _ropeLength - reelSpeed * Time.fixedDeltaTime);

        // Soft speed cap so long grapples don't fling the player into orbit.
        float speed = vel.magnitude;
        if (swingMaxSpeed > 0f && speed > swingMaxSpeed)
            vel *= swingMaxSpeed / speed;

        // Rope constraint: the rope cannot stretch. If this step would take the player
        // beyond the rope length, clamp onto the rope and strip any outward velocity.
        Vector3 step = vel * Time.fixedDeltaTime;
        Vector3 toNext = (transform.position + step) - _grapplePoint;
        if (toNext.magnitude > _ropeLength) {
            step = (_grapplePoint + toNext.normalized * _ropeLength) - transform.position;

            Vector3 radialOut = (transform.position - _grapplePoint).normalized;
            float outward = Vector3.Dot(vel, radialOut);
            if (outward > 0f) vel -= radialOut * outward;
        }

        motor.SetVelocity(vel);
        _cc.Move(step);
    }

    void ApplyDirectPull(Vector3 direction, float distance) {
        Vector3 vel = motor.Velocity;

        vel.y -= motor.gravity * Time.fixedDeltaTime;
        vel += direction * grapplePullStrength * Time.fixedDeltaTime;

        float speed = vel.magnitude;
        if (speed > maxGrappleSpeed)
            vel = vel.normalized * maxGrappleSpeed;

        motor.SetVelocity(vel);
        _cc.Move(vel * Time.fixedDeltaTime);
    }

    void ReleaseGrapple() {
        if (!_isGrappling) return;
        _isGrappling = false;

        if (motor != null) {
            motor.SetExternalControl(false);

            // Keep the momentum gained during the swing (slingshot release).
            Vector3 vel = motor.Velocity;
            vel.y = Mathf.Max(vel.y, launchUpward);
            motor.SetVelocity(vel);
            motor.PreserveMomentum(1.5f);
        }

        if (ropeRenderer != null)
            ropeRenderer.enabled = false;
        LocalStatsTracker.Instance?.OnGrappleEnded();
    }

    void UpdateRopeVisual() {
        if (ropeRenderer == null || !_isGrappling) return;
        ropeRenderer.SetPosition(0, cameraHolder.position);
        ropeRenderer.SetPosition(1, _grapplePoint);
    }

    // -- Stim --------------------------------------------------------------------

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
        LocalStatsTracker.Instance?.OnStimStarted();
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
        LocalStatsTracker.Instance?.OnStimEnded();
    }

    // -- Gizmos ------------------------------------------------------------------

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
