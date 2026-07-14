using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class RocketLauncher : WeaponBase {
    [Header("Rocket Launcher Settings")]
    public GameObject rocketPrefab;
    public float rocketSpeed = 25f;

    private Camera _playerCamera;

    // Called by WeaponSpawner after instantiation
    public override void SetCamera(Camera cam) {
        _playerCamera = cam;
        if (_playerCamera == null)
            Debug.LogError("RocketLauncher.SetCamera: received null camera!");
        else
            Debug.Log($"RocketLauncher camera set: {_playerCamera.gameObject.name}");
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        // Camera is set via SetCamera() from WeaponSpawner, no search needed
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

        Debug.Log($"Firing rocket — direction: {fireDirection}");

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