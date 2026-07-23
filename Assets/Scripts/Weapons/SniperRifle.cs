using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class SniperRifle : HitscanBase {
    private Camera playerCamera;

    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 1f;

    [Header("Scope")]
    [SerializeField] private GameObject scopeOverlay;

    [Header("Scope Zoom")]
    [SerializeField] private float zoomFOV = 20f;
    [SerializeField] private float zoomSpeed = 12f;

    private float defaultFOV;
    private bool fovInitialized;
    private AudioSource audioSource;
    private bool isAiming;

    private void Awake() {
        audioSource = GetComponent<AudioSource>();
        if (scopeOverlay != null)
            scopeOverlay.SetActive(false);
    }

    public override void SetCamera(Camera cam) {
        playerCamera = cam;
        if (playerCamera != null) {
            defaultFOV = playerCamera.fieldOfView;
            fovInitialized = true;
        }
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        // Disable scope overlay on non-owners immediately
        if (!IsOwner && scopeOverlay != null)
            scopeOverlay.SetActive(false);
    }

    // WeaponBase.Update calls HandleInput — we handle zoom here too
    protected override void HandleInput() {
        if (!IsOwner) return;
        if (playerCamera == null) return;

        HandleZoom();

        if (Mouse.current.leftButton.wasPressedThisFrame)
            Fire();

        if (Keyboard.current.rKey.wasPressedThisFrame && !_isReloading)
            StartCoroutine(Reload());
    }

    private void HandleZoom() {
        // Only runs inside HandleInput which already guards IsOwner
        isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (scopeOverlay != null)
            scopeOverlay.SetActive(isAiming);

        if (!fovInitialized && playerCamera != null) {
            defaultFOV = playerCamera.fieldOfView;
            fovInitialized = true;
        }

        float targetFOV = isAiming ? zoomFOV : defaultFOV;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Time.deltaTime * zoomSpeed
        );
    }

    protected override void Fire() {
        if (!CanFire || playerCamera == null) return;

        _nextFireTime = Time.time + 1f / fireRate;
        currentAmmo--;

        if (fireSound != null && audioSource != null)
            audioSource.PlayOneShot(fireSound, fireVolume);

        FireServerRpc(
            playerCamera.transform.position,
            playerCamera.transform.forward);

        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }

    protected override void OnPlayerHit(PlayerHealth player, RaycastHit hit) {
        var health = hit.collider.GetComponentInParent<PlayerHealth>();
        if (health != null)
            health.TakeDamage(150f, OwnerClientId, "HAMR");
    }
}