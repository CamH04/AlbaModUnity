using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour {
    [Header("Ground Movement")]
    public float maxGroundSpeed = 12f;
    public float groundAccelerate = 14f;
    public float groundDecelerate = 10f;
    public float groundFriction = 6f;

    [Header("Air Movement")]
    public float maxAirSpeed = 0.8f;
    public float airAccelerate = 800f;
    public float gravity = 28f;
    public float fallGravityMult = 1.6f;
    public float jumpCutMultiplier = 0.4f;

    [Header("Jumping")]
    public float jumpForce = 10f;
    public bool autoBunnyHop = true;
    public bool allowDoubleJump = true;

    [Header("Speed Cap")]
    public float absoluteSpeedCap = 40f;

    [Header("References")]
    public Transform orientation;

    private CharacterController _cc;
    private Vector3 _velocity;
    private bool _jumpQueued;
    private bool _jumpHeld;
    private bool _hasDoubleJump;

    private float _momentumTimer;

    private bool _isGrappling;
    private Vector3 _grappleTarget;
    private float _grappleSpeed;
    private float _grappleStopDistance;

    // Forced, externally-driven movement (the AbductChain yank). While this is
    // active normal input, gravity and the swing-grapple are all bypassed.
    private bool _isAbducting;
    private Vector3 _abductDestination;
    private Vector3 _abductDirection = Vector3.forward;
    private float _abductTimer;
    private float _abductDuration;
    private float _abductExitSpeed;
    [Tooltip("Safety clamp on how far a single abduct step can move the controller.")]
    public float abductMaxStep = 6f;

    public Vector3 Velocity => _velocity;
    public bool IsGrounded => _cc.isGrounded;
    public bool IsGrappling => _isGrappling;
    public bool IsAbducting => _isAbducting;

    // When an external system (e.g. the Abilities swing-grapple) owns movement,
    // PlayerController must not apply normal input/gravity on top of it - that is
    // what let movement input (holding S) fight and cancel the grapple pull.
    private bool _externalControl;
    public bool ExternalControl => _externalControl;
    public void SetExternalControl(bool active) => _externalControl = active;

    void Awake() {
        _cc = GetComponent<CharacterController>();
    }

    void OnDisable() {
        // Never leave the controller stuck in an external-movement state if the
        // motor is disabled mid-abduct or mid-grapple (e.g. round reset).
        _isAbducting = false;
        _isGrappling = false;
        _externalControl = false;
    }

    public void QueueJump() => _jumpQueued = true;
    public void SetJumpHeld(bool held) => _jumpHeld = held;
    public void GrantDoubleJump() => _hasDoubleJump = true;
    public void SetVelocity(Vector3 vel) => _velocity = vel;
    public void SetHorizontalVelocity(Vector3 vel) {
        _velocity.x = vel.x;
        _velocity.z = vel.z;
    }

    public void PreserveMomentum(float duration) {
        _momentumTimer = duration;
    }

    public void StartGrapple(Vector3 target, float speed, float stopDistance) {
        _isAbducting = false;
        _grappleTarget = target;
        _grappleSpeed = speed;
        _grappleStopDistance = stopDistance;
        _isGrappling = true;

        Vector3 toTarget = _grappleTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.001f)
            _velocity = toTarget.normalized * _grappleSpeed;
        else if (orientation != null)
            _velocity = orientation.forward * _grappleSpeed;
        else
            _velocity = transform.forward * _grappleSpeed;

        _jumpQueued = false;
        _hasDoubleJump = false;
    }

    public void StopGrapple() {
        _isGrappling = false;
        _velocity = Vector3.zero;
    }

    public bool GrappleStep() {
        if (!_isGrappling) return false;

        Vector3 toTarget = _grappleTarget - transform.position;
        float dist = toTarget.magnitude;

        if (dist <= _grappleStopDistance) {
            StopGrapple();
            return false;
        }

        Vector3 dir = toTarget.normalized;
        _velocity = dir * _grappleSpeed;

        Vector3 step = _velocity * Time.fixedDeltaTime;
        if (step.magnitude > dist)
            step = toTarget;

        _cc.Move(step);
        return true;
    }

    // ── Abduct (Chain Lash) ──────────────────────────────────────────────────

    /// <summary>
    /// Forces this player to a destination over <paramref name="duration"/>.
    /// Used by both halves of the Abduct yank: the target is dragged toward the
    /// caster and the caster lunges toward the target.
    /// </summary>
    public void StartAbduct(Vector3 destination, float duration, float exitSpeed) {
        _isAbducting = true;
        _isGrappling = false;
        _externalControl = false;

        _abductDestination = destination;
        _abductDuration = Mathf.Max(0.01f, duration);
        _abductTimer = 0f;
        _abductExitSpeed = Mathf.Max(0f, exitSpeed);

        Vector3 toDest = destination - transform.position;
        _abductDirection = toDest.sqrMagnitude > 0.0001f
            ? toDest.normalized
            : (orientation != null ? orientation.forward : transform.forward);

        _velocity = Vector3.zero;
        _jumpQueued = false;
        _hasDoubleJump = false;
    }

    /// <summary>
    /// Advances the abduct. Returns true while still travelling.
    /// </summary>
    public bool AbductStep() {
        if (!_isAbducting) return false;

        _abductTimer += Time.fixedDeltaTime;

        Vector3 toDest = _abductDestination - transform.position;
        float dist = toDest.magnitude;

        if (dist <= 0.05f || _abductTimer >= _abductDuration) {
            _isAbducting = false;

            // The caster keeps a bit of forward momentum so the lunge flows
            // straight back into normal movement instead of stopping dead.
            if (_abductExitSpeed > 0f) {
                _velocity = _abductDirection * _abductExitSpeed;
                PreserveMomentum(0.35f);
            }
            else {
                _velocity = Vector3.zero;
            }
            return false;
        }

        // Spread what's left over the time that's left so the move lands
        // exactly on the destination instead of easing off short of it.
        float remainingTime = Mathf.Max(0.01f, _abductDuration - _abductTimer);
        float speed = dist / remainingTime;

        Vector3 step = toDest / dist * Mathf.Min(speed * Time.fixedDeltaTime, dist);
        if (step.magnitude > abductMaxStep)
            step = step.normalized * abductMaxStep;

        _velocity = step / Time.fixedDeltaTime;
        _cc.Move(step);
        return true;
    }

    public void Move(Vector2 inputDir, bool jumpHeld) {
        // While abducted, nothing else may drive the controller.
        if (_isAbducting) {
            AbductStep();
            return;
        }

        _jumpHeld = jumpHeld;

        Vector3 wishDir = orientation.right * inputDir.x + orientation.forward * inputDir.y;
        wishDir.y = 0;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        if (_cc.isGrounded) {
            _hasDoubleJump = false;

            if (_velocity.y < 0) _velocity.y = -4f;

            ApplyFriction(wishDir);
            GroundAccelerate(wishDir);

            if (_jumpQueued || (autoBunnyHop && jumpHeld)) {
                _velocity.y = jumpForce;
                _jumpQueued = false;
            }
        }
        else {
            if (_jumpQueued && _hasDoubleJump) {
                _velocity.y = jumpForce;
                _hasDoubleJump = false;
                _jumpQueued = false;
            }

            AirAccelerate(wishDir);
            ApplyGravity();
        }

        Vector3 flat = new Vector3(_velocity.x, 0, _velocity.z);
        if (flat.magnitude > absoluteSpeedCap) {
            flat = flat.normalized * absoluteSpeedCap;
            _velocity.x = flat.x;
            _velocity.z = flat.z;
        }

        _jumpQueued = false;
        _cc.Move(_velocity * Time.fixedDeltaTime);
    }

    void ApplyGravity() {
        float mult = _velocity.y < 0 ? fallGravityMult : 1f;

        if (!_jumpHeld && _velocity.y > 0)
            mult = jumpCutMultiplier * fallGravityMult;

        _velocity.y -= gravity * mult * Time.fixedDeltaTime;
    }

    void ApplyFriction(Vector3 wishDir) {
        if (_momentumTimer > 0f) {
            _momentumTimer -= Time.fixedDeltaTime;
            return;
        }

        float speed = new Vector3(_velocity.x, 0, _velocity.z).magnitude;
        if (speed < 0.1f) { _velocity.x = _velocity.z = 0; return; }

        float inputAlignment = Vector3.Dot(
            new Vector3(_velocity.x, 0, _velocity.z).normalized,
            wishDir
        );

        float frictionScale = wishDir.sqrMagnitude < 0.1f ? 1f
                            : Mathf.Lerp(0.05f, 1f, 1f - Mathf.Clamp01(inputAlignment));

        float drop = speed * groundFriction * frictionScale * Time.fixedDeltaTime;
        float newSpeed = Mathf.Max(speed - drop, 0f) / speed;

        _velocity.x *= newSpeed;
        _velocity.z *= newSpeed;
    }

    void GroundAccelerate(Vector3 wishDir) {
        if (wishDir.sqrMagnitude < 0.1f) return;

        float currentSpeed = Vector3.Dot(_velocity, wishDir);
        float addSpeed = Mathf.Clamp(maxGroundSpeed - currentSpeed, 0,
            groundAccelerate * Time.fixedDeltaTime);
        _velocity += wishDir * addSpeed;
    }

    void AirAccelerate(Vector3 wishDir) {
        if (wishDir.sqrMagnitude < 0.01f) return;

        if (_momentumTimer > 0f) {
            _momentumTimer -= Time.fixedDeltaTime;

            float currentSpeed = Vector3.Dot(_velocity, wishDir);
            float addSpeed = maxAirSpeed - currentSpeed;
            if (addSpeed <= 0) return;

            float accelSpeed = Mathf.Min(airAccelerate * maxAirSpeed * Time.fixedDeltaTime, addSpeed);
            _velocity.x += wishDir.x * accelSpeed;
            _velocity.z += wishDir.z * accelSpeed;
            return;
        }

        float wishSpeed2 = maxAirSpeed;
        float currentSpeed2 = Vector3.Dot(_velocity, wishDir);
        float addSpeed2 = wishSpeed2 - currentSpeed2;
        if (addSpeed2 <= 0) return;

        float accelSpeed2 = Mathf.Min(airAccelerate * wishSpeed2 * Time.fixedDeltaTime, addSpeed2);
        _velocity.x += wishDir.x * accelSpeed2;
        _velocity.z += wishDir.z * accelSpeed2;
    }
}