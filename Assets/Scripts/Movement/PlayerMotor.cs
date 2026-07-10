using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour {
    [Header("Ground Movement")]
    public float maxGroundSpeed = 12f;
    public float groundAccelerate = 14f;   // Low — you build speed, not instant max
    public float groundDecelerate = 10f;   // Separate decel for stopping feel
    public float groundFriction = 6f;

    [Header("Air Movement")]
    public float maxAirSpeed = 0.8f;  // Cap per-frame air add
    public float airAccelerate = 800f;  // High — strafing works
    public float gravity = 28f;   // Heavy gravity — no floaty arc
    public float fallGravityMult = 1.6f;  // Extra gravity on the way down
    public float jumpCutMultiplier = 0.4f;  // Tap jump = short hop

    [Header("Jumping")]
    public float jumpForce = 10f;
    public bool autoBunnyHop = true;
    public bool allowDoubleJump = true;

    [Header("Speed Cap")]
    public float absoluteSpeedCap = 40f;    // Prevents infinite bhop speed

    [Header("References")]
    public Transform orientation;

    private CharacterController _cc;
    private Vector3 _velocity;
    private bool _jumpQueued;
    private bool _jumpHeld;
    private bool _hasDoubleJump;

    public Vector3 Velocity => _velocity;
    public bool IsGrounded => _cc.isGrounded;

    private float _momentumTimer;

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

    public void Move(Vector2 inputDir, bool jumpHeld) {
        _jumpHeld = jumpHeld;

        Vector3 wishDir = orientation.right * inputDir.x + orientation.forward * inputDir.y;
        wishDir.y = 0;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        if (_cc.isGrounded) {
            _hasDoubleJump = false;

            if (_velocity.y < 0) _velocity.y = -4f; // Snappy landing, not floaty

            ApplyFriction(wishDir);
            GroundAccelerate(wishDir);

            if (_jumpQueued || (autoBunnyHop && jumpHeld)) {
                _velocity.y = jumpForce;
                _jumpQueued = false;
            }
        }
        else {
            // Double jump
            if (_jumpQueued && _hasDoubleJump) {
                _velocity.y = jumpForce;
                _hasDoubleJump = false;
                _jumpQueued = false;
            }

            AirAccelerate(wishDir);
            ApplyGravity();
        }

        // Hard speed cap — lets momentum build but prevents runaway speed
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
        // Heavier gravity on the way down — snappy arc, not floaty
        float mult = _velocity.y < 0 ? fallGravityMult : 1f;

        // Jump cut — tap space for a short hop
        if (!_jumpHeld && _velocity.y > 0)
            mult = jumpCutMultiplier * fallGravityMult;

        _velocity.y -= gravity * mult * Time.fixedDeltaTime;
    }

    void ApplyFriction(Vector3 wishDir) {
        // Don't bleed speed during momentum window
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

    // Quake/Source air strafing — high accel, low per-frame cap
    // Pressing A/D curves your trajectory without killing speed
    void AirAccelerate(Vector3 wishDir) {
        if (wishDir.sqrMagnitude < 0.01f) return;

        // During momentum window, only allow strafing — don't cap existing speed
        if (_momentumTimer > 0f) {
            _momentumTimer -= Time.fixedDeltaTime;

            // Still allow air strafing to steer but don't reduce velocity
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

    public void PreserveMomentum(float duration) {
        _momentumTimer = duration;
    }

}