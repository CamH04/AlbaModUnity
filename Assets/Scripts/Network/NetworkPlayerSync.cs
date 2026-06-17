using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSync : NetworkBehaviour {
    [Header("Sync Settings")]
    public float positionLerpSpeed = 20f;
    public float rotationLerpSpeed = 20f;

    // Synced vars — automatically replicated to all clients
    private NetworkVariable<Vector3> _netPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>();
    private NetworkVariable<bool> _netWallRun = new NetworkVariable<bool>();
    private NetworkVariable<bool> _netSliding = new NetworkVariable<bool>();
    private NetworkVariable<bool> _netStimmed = new NetworkVariable<bool>();
    private NetworkVariable<bool> _netGrappling = new NetworkVariable<bool>();
    private NetworkVariable<Vector3> _netGrapplePoint = new NetworkVariable<Vector3>();

    private PlayerMotor _motor;
    private WallRunController _wallRun;
    private SlideController _slide;
    private Abilities _abilities;
    private CharacterController _cc;

    // For remote player interpolation
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;

    // Remote rope visual
    private LineRenderer _ropeRenderer;

    void Awake() {
        _motor = GetComponent<PlayerMotor>();
        _wallRun = GetComponent<WallRunController>();
        _slide = GetComponent<SlideController>();
        _abilities = GetComponent<Abilities>();
        _cc = GetComponent<CharacterController>();
        _ropeRenderer = GetComponent<LineRenderer>();
    }

    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            // Disable all local input/physics on remote players
            GetComponent<PlayerController>().enabled = false;
            GetComponent<PlayerMotor>().enabled = false;
            _cc.enabled = false;

            // Disable camera on remote player
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cam.gameObject.SetActive(false);

            // Listen for state changes to drive visuals
            _netWallRun.OnValueChanged += (_, val) => { /* drive animator here */ };
            _netGrappling.OnValueChanged += OnGrappleChanged;
        }
    }

    void Update() {
        if (IsOwner)
            SendState();
        else
            InterpolateRemote();
    }

    // Owner sends its state to the server every frame
    void SendState() {
        if (!IsSpawned) return;

        // Only send if changed to save bandwidth
        if (Vector3.Distance(_netPosition.Value, transform.position) > 0.01f)
            UpdatePositionServerRpc(transform.position, transform.rotation);

        UpdateStateServerRpc(
            _wallRun.IsWallRunning,
            _slide.IsSliding,
            _abilities.IsStimmed,
            _abilities.IsGrappling,
            _abilities.IsGrappling ? _abilities.GrapplePoint : Vector3.zero
        );
    }

    // Smoothly move remote player to synced position
    void InterpolateRemote() {
        transform.position = Vector3.Lerp(
            transform.position,
            _netPosition.Value,
            Time.deltaTime * positionLerpSpeed
        );

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            _netRotation.Value,
            Time.deltaTime * rotationLerpSpeed
        );

        // Drive remote rope visual
        if (_ropeRenderer != null && _netGrappling.Value) {
            _ropeRenderer.enabled = true;
            _ropeRenderer.SetPosition(0, transform.position + Vector3.up);
            _ropeRenderer.SetPosition(1, _netGrapplePoint.Value);
        }
        else if (_ropeRenderer != null) {
            _ropeRenderer.enabled = false;
        }
    }

    void OnGrappleChanged(bool prev, bool current) {
        if (_ropeRenderer != null)
            _ropeRenderer.enabled = current;
    }

    [ServerRpc]
    void UpdatePositionServerRpc(Vector3 position, Quaternion rotation) {
        _netPosition.Value = position;
        _netRotation.Value = rotation;
    }

    [ServerRpc]
    void UpdateStateServerRpc(bool wallRun, bool sliding, bool stimmed,
                               bool grappling, Vector3 grapplePoint) {
        _netWallRun.Value = wallRun;
        _netSliding.Value = sliding;
        _netStimmed.Value = stimmed;
        _netGrappling.Value = grappling;
        _netGrapplePoint.Value = grapplePoint;
    }
}