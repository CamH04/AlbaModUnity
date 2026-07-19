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

    public Vector3 Velocity => _velocity;
    public bool IsGrounded => _cc.isGrounded;
    public bool IsGrappling => _isGrappling;

    void Awake() {
        _cc = GetComponent<CharacterController>();
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

    public void Move(Vector2 inputDir, bool jumpHeld) {
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