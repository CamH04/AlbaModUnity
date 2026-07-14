using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class SniperRifle : HitscanBase {
    private Camera playerCamera;
    private AudioSource audioSource;

    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 1f;

    [Header("Zoom")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float zoomFOV = 20f;
    [SerializeField] private float zoomSpeed = 10f;

    private bool isZooming;

    private void Awake() {
        audioSource = GetComponent<AudioSource>();
    }

    public override void SetCamera(Camera cam) {
        playerCamera = cam;
        if (playerCamera != null)
            normalFOV = playerCamera.fieldOfView;
    }

    private void Update() {
        HandleZoom();
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

    private void HandleZoom() {
        if (!IsOwner || playerCamera == null)
            return;

        isZooming = Mouse.current != null && Mouse.current.rightButton.isPressed;

        float targetFOV = isZooming ? zoomFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Time.deltaTime * zoomSpeed);
    }

    protected override void Fire() {
        if (!CanFire)
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
        player.TakeDamage(150f);
    }
}