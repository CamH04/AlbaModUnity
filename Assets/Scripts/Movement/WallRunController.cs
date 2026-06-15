using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public class WallRunController : MonoBehaviour {
    [Header("Detection")]
    public float wallCheckDistance = 3f;
    public LayerMask wallMask;
    public float minWallRunSpeed = 2f;

    [Header("Wall Run Feel")]
    public float wallRunGravity = 4f;
    public float wallRunSpeed = 12f;
    public float wallJumpForce = 8f;
    public float wallJumpSideForce = 6f;
    public float wallRunDuration = 2f;
    public float wallAttractionForce = 15f;

    private PlayerMotor _motor;
    private CharacterController _cc;

    // Detection
    private RaycastHit _rightHit, _leftHit;
    private bool _onRightWall, _onLeftWall;

    // State
    private bool _isWallRunning;
    private Vector3 _wallNormal;
    private bool _isOnRightSide;
    private float _wallRunTimer;

    // Cooldown — prevents immediately re-sticking to the same wall
    private float _wallCooldown;
    private const float WallCooldownDuration = 0.5f;

    public bool IsWallRunning => _isWallRunning;
    public Vector3 WallNormal => _wallNormal;

    public bool JustStoppedWallRun { get; private set; }

    void Awake() {
        _motor = GetComponent<PlayerMotor>();
        _cc = GetComponent<CharacterController>();
    }

    void Update() {
        DetectWalls();

        if (_wallCooldown > 0f)
            _wallCooldown -= Time.deltaTime;
    }

    private Transform _orientation;

    // Add this public method to set it
    public void SetOrientation(Transform orientation) {
        _orientation = orientation;
    }

    void DetectWalls() {
        // Fall back to transform if orientation not set
        Transform dir = _orientation != null ? _orientation : transform;
        Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.4f);

        _onRightWall = Physics.Raycast(origin, dir.right, out _rightHit, wallCheckDistance, wallMask);
        _onLeftWall = Physics.Raycast(origin, -dir.right, out _leftHit, wallCheckDistance, wallMask);

        // Debug
        RaycastHit debugHit;
        bool hitRight = Physics.Raycast(origin, dir.right, out debugHit, wallCheckDistance);
        if (hitRight) Debug.Log($"RIGHT hit: {debugHit.collider.name} layer={LayerMask.LayerToName(debugHit.collider.gameObject.layer)}");
        bool hitLeft = Physics.Raycast(origin, -dir.right, out debugHit, wallCheckDistance);
        if (hitLeft) Debug.Log($"LEFT hit: {debugHit.collider.name} layer={LayerMask.LayerToName(debugHit.collider.gameObject.layer)}");
    }

    public bool TryStartWallRun(float horizontalSpeed) {
        Debug.Log($"TryStartWallRun | grounded={_motor.IsGrounded} | cooldown={_wallCooldown:F2} | speed={horizontalSpeed:F2} | onRight={_onRightWall} | onLeft={_onLeftWall}");

        if (_motor.IsGrounded) { Debug.Log("BLOCKED: grounded"); return false; }
        if (_wallCooldown > 0f) { Debug.Log("BLOCKED: cooldown"); return false; }
        if (horizontalSpeed < minWallRunSpeed) { Debug.Log($"BLOCKED: speed {horizontalSpeed:F2} < {minWallRunSpeed}"); return false; }
        if (!_onRightWall && !_onLeftWall) { Debug.Log("BLOCKED: no wall detected"); return false; }

        _isOnRightSide = _onRightWall;
        _wallNormal = _isOnRightSide ? _rightHit.normal : _leftHit.normal;
        _isWallRunning = true;
        _wallRunTimer = wallRunDuration;

        Vector3 v = _motor.Velocity;
        v.y = 0f;
        _motor.SetVelocity(v);

        Debug.Log($"WALL RUN STARTED | side={(_isOnRightSide ? "right" : "left")} | normal={_wallNormal}");
        return true;
    }

    public Vector3 GetWallRunVelocity(Vector3 currentVelocity, Transform orientation) {
        _orientation = orientation; 
        Transform dir = _orientation != null ? _orientation : transform;
        Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.4f);
        Vector3 checkDir = _isOnRightSide ? dir.right : -dir.right;
        bool stillOnWall = Physics.Raycast(origin, checkDir, wallCheckDistance * 1.5f, wallMask);

        Debug.Log($"WallRun | stillOnWall={stillOnWall} | timer={_wallRunTimer:F2} | onRight={_onRightWall} | onLeft={_onLeftWall} | isRunning={_isWallRunning} | vel={currentVelocity}");

        if (!stillOnWall) {
            Debug.Log("STOPPED: lost wall contact");
            StopWallRun();
            return currentVelocity;
        }

        _wallRunTimer -= Time.fixedDeltaTime;
        if (_wallRunTimer <= 0f) {
            Debug.Log("STOPPED: timer ran out");
            StopWallRun();
            return currentVelocity;
        }

        Vector3 wallForward = Vector3.Cross(_wallNormal, Vector3.up);
        if (Vector3.Dot(wallForward, orientation.forward) < 0f)
            wallForward = -wallForward;

        float newY = Mathf.MoveTowards(currentVelocity.y, -1f, wallRunGravity * Time.fixedDeltaTime);
        Vector3 intoWall = -_wallNormal * wallAttractionForce;

        return wallForward * wallRunSpeed + Vector3.up * newY + intoWall;
    }

    public Vector3 WallJump(Transform orientation) {
        StopWallRun();
        _wallCooldown = WallCooldownDuration;

        return _wallNormal * wallJumpSideForce
             + Vector3.up * wallJumpForce
             + orientation.forward * 3f;
    }

    public void StopWallRun() {
        if (_isWallRunning) JustStoppedWallRun = true;
        _isWallRunning = false;
    }
    public void ConsumeStopFlag() => JustStoppedWallRun = false;

    // Call this on landing so player can wall run again
    public void ResetWallTimer() {
        _wallCooldown = 0f;
    }

    public bool OnAnyWall() => _onRightWall || _onLeftWall;

    void OnDrawGizmos() {
        if (_cc == null) return;
        Transform dir = _orientation != null ? _orientation : transform;
        Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.4f);

        Gizmos.color = _isWallRunning ? Color.green : Color.red;
        Gizmos.DrawRay(origin, dir.right * wallCheckDistance);
        Gizmos.DrawRay(origin, -dir.right * wallCheckDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, 0.1f);
    }
}