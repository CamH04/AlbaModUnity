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

    private void Update() {
        HandleZoomOverlay();
        HandleInput();
    }

    protected override void HandleInput() {
        if (!IsOwner)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            Fire();

        if (Keyboard.current.rKey.wasPressedThisFrame && !_isReloading)
            StartCoroutine(Reload());
    }

    private void HandleZoomOverlay() {
        if (!IsOwner || playerCamera == null)
            return;

        isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (scopeOverlay != null)
            scopeOverlay.SetActive(isAiming);

        if (!fovInitialized) {
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
        if (!CanFire || playerCamera == null)
            return;

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
        PlayerHealth health = hit.collider.GetComponentInParent<PlayerHealth>();
        health.TakeDamage(150f, OwnerClientId, "HAMR");
    }
}