using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Netcode;

[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(WallRunController))]
[RequireComponent(typeof(SlideController))]
public class PlayerController : NetworkBehaviour{
    [Header("References")]
    public Transform orientation;
    public Transform cameraHolder;
    public Camera playerCamera;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.15f;
    public float maxLookAngle = 90f;

    [Header("Camera Effects")]
    public float wallRunTilt = 15f;
    public float slideTilt = 5f;
    public float tiltSpeed = 10f;
    public float baseFOV = 90f;
    public float sprintFOVMultiplier = 1.15f;
    public float fovSpeed = 6f;

    [Header("Character Height")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 10f;

    private PlayerMotor _motor;
    private WallRunController _wallRun;
    private SlideController _slide;
    private CharacterController _cc;

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpHeld;
    private bool _jumpPressed;

    private float _yaw;
    private float _pitch;
    private float _currentTilt;

    private bool _isOwned = false;
    void Awake() {
        _motor = GetComponent<PlayerMotor>();
        _wallRun = GetComponent<WallRunController>();
        _slide = GetComponent<SlideController>();
        _cc = GetComponent<CharacterController>();

        _wallRun.SetOrientation(orientation);

        if (playerCamera == null) playerCamera = Camera.main;
        if (cameraHolder == null && playerCamera != null)
            cameraHolder = playerCamera.transform.parent != null
                ? playerCamera.transform.parent
                : playerCamera.transform;
        if (orientation == null) orientation = transform;

        // Don't touch cursor here
    }
    public override void OnNetworkSpawn() {
        _isOwned = IsOwner;

        Debug.Log($"[{gameObject.name}] OnNetworkSpawn — IsOwner:{IsOwner} OwnerClientId:{OwnerClientId} LocalClient:{NetworkManager.Singleton.LocalClientId}");

        if (!IsOwner) {
            // Disable all cameras on remote players
            foreach (var cam in GetComponentsInChildren<Camera>(true)) {
                cam.gameObject.SetActive(false);
                Debug.Log($"Disabled camera {cam.gameObject.name} on non-owner player (owner={OwnerClientId})");
            }

            // Disable audio listeners on remote players
            foreach (var listener in GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
        }
        else {
            // Lock cursor for local owner
            if (SceneManager.GetActiveScene().name == "Game") {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Disable the FallbackCamera if it exists
            var fallback = GameObject.Find("FallbackCamera");
            if (fallback != null) fallback.SetActive(false);
        }
    }
    void OnEnable() {
    }

    void OnDisable() {
        if (_isOwned)
            UnlockCursor();
    }

    void LockCursor() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnSlidePressed() {
        if (_wallRun.IsWallRunning) _wallRun.StopWallRun();
        else _slide.TrySlide(_motor.Velocity);
    }

    void OnSlideReleased() {
        if (_slide.IsSliding) _slide.CancelSlide();
    }

    void Update() {
        if (!_isOwned) return;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        _moveInput = new Vector2(x, y);
        _lookInput = mouse.delta.ReadValue();

        if (kb.spaceKey.wasPressedThisFrame) { _jumpPressed = true; _jumpHeld = true; }
        if (kb.spaceKey.wasReleasedThisFrame) _jumpHeld = false;
        if (kb.leftCtrlKey.wasPressedThisFrame) OnSlidePressed();
        if (kb.leftCtrlKey.wasReleasedThisFrame) OnSlideReleased();

        _yaw += _lookInput.x * mouseSensitivity;
        _pitch -= _lookInput.y * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -maxLookAngle, maxLookAngle);

        orientation.rotation = Quaternion.Euler(0f, _yaw, 0f);

        UpdateCrouchHeight();
    }

    void FixedUpdate() {
        if (!_isOwned) return;
        float hSpeed = new Vector3(_motor.Velocity.x, 0, _motor.Velocity.z).magnitude;
        if (_wallRun.JustStoppedWallRun) {
            _motor.GrantDoubleJump();
            _wallRun.ConsumeStopFlag();
        }
        // Wall run
        if (!_motor.IsGrounded && _wallRun.OnAnyWall() && !_slide.IsSliding) {
            if (!_wallRun.IsWallRunning)
                _wallRun.TryStartWallRun(hSpeed);

            if (_wallRun.IsWallRunning) {
                Vector3 wallVel = _wallRun.GetWallRunVelocity(_motor.Velocity, orientation);
                _motor.SetVelocity(wallVel);
                _cc.Move(wallVel * Time.fixedDeltaTime);

                if (_jumpPressed) {
                    _motor.SetVelocity(_wallRun.WallJump(orientation));
                    _motor.GrantDoubleJump();
                    _jumpPressed = false;
                }
                return;
            }
        }
        else if (_motor.IsGrounded) {
            _wallRun.ResetWallTimer();
            if (_wallRun.IsWallRunning) _wallRun.StopWallRun();
        }

        // Slide
        if (_slide.IsSliding) {
            _motor.SetVelocity(_slide.GetSlideVelocity(_motor.Velocity));
            _motor.Move(Vector2.zero, false);
            _jumpPressed = false;
            return;
        }

        if (_jumpPressed) { _motor.QueueJump(); _jumpPressed = false; }

        _motor.Move(_moveInput, _jumpHeld);
    }
    void LateUpdate() {
        if (!_isOwned) return;
        float targetTilt = 0f;

        if (_wallRun.IsWallRunning) {
            bool onRight = Vector3.Dot(_wallRun.WallNormal, -orientation.right) > 0;
            targetTilt = onRight ? wallRunTilt : -wallRunTilt;
        }
        else if (_slide.IsSliding)
            targetTilt = slideTilt;

        _currentTilt = Mathf.LerpAngle(_currentTilt, targetTilt, Time.deltaTime * tiltSpeed);

        cameraHolder.rotation = Quaternion.Euler(_pitch, _yaw, _currentTilt);
    }

    void UpdateCrouchHeight() {
        float targetHeight = _slide.IsSliding ? crouchHeight : standHeight;
        _cc.height = Mathf.Lerp(_cc.height, targetHeight, Time.deltaTime * crouchSpeed);
        _cc.center = new Vector3(0, _cc.height / 2f, 0);
    }
}