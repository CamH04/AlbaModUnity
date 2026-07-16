using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class RocketLauncher : WeaponBase {
    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 1f;
    private AudioSource audioSource;

    [Header("Rocket Launcher Settings")]
    public GameObject rocketPrefab;
    public float rocketSpeed = 25f;

    private void Awake() {
        audioSource = GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        // Camera and weaponHolder are both resolved locally per-client in
        // WeaponBase.ResolveFollowTarget(), no extra work needed here.
    }

    protected override void HandleInput() {
        if (!IsOwner) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            Fire();

        if (Keyboard.current.rKey.wasPressedThisFrame && !_isReloading)
            StartCoroutine(Reload());
    }

    protected override void Fire() {
        if (!CanFire) return;
        if (_playerCamera == null) {
            Debug.LogError("RocketLauncher: no camera assigned, cannot fire!");
            return;
        }

        _nextFireTime = Time.time + 1f / fireRate;
        currentAmmo--;

        Vector3 fireDirection = _playerCamera.transform.forward;
        Vector3 firePosition = _playerCamera.transform.position;

        Debug.Log($"Firing rocket � direction: {fireDirection}");

        if (fireSound != null && audioSource != null)
            audioSource.PlayOneShot(fireSound, fireVolume);

        FireServerRpc(firePosition, fireDirection);

        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }

    protected override void SpawnProjectile(Vector3 position, Vector3 direction) {
        if (rocketPrefab == null) {
            Debug.LogError("RocketLauncher: rocket prefab is null!");
            return;
        }

        var rocketObj = Instantiate(rocketPrefab, position,
            Quaternion.LookRotation(direction));

        var netObj = rocketObj.GetComponent<NetworkObject>();
        netObj.Spawn();

        var rocket = rocketObj.GetComponent<RocketProjectile>();
        if (rocket != null) {
            rocket.speed = rocketSpeed;
            rocket.Initialise(OwnerClientId);
        }
    }
}