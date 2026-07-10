using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class RocketLauncher : WeaponBase {
    [Header("Rocket Launcher Settings")]
    public GameObject rocketPrefab;
    public float rocketSpeed = 25f;

    [Header("Visual")]
    public GameObject weaponModel;

    private Camera _playerCamera;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if (!IsOwner) return;

        // Find the camera that belongs to THIS player specifically
        // by searching children of this player object
        _playerCamera = GetComponentInChildren<Camera>(true);

        if (_playerCamera == null)
            Debug.LogError("RocketLauncher could not find a camera in children!");
        else
            Debug.Log($"RocketLauncher found camera: {_playerCamera.gameObject.name}");
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
        if (_playerCamera == null) return;

        _nextFireTime = Time.time + 1f / fireRate;
        currentAmmo--;

        // Read direction directly from our own camera transform
        Vector3 fireDirection = _playerCamera.transform.forward;
        Vector3 firePosition = _playerCamera.transform.position;

        Debug.Log($"Firing rocket — direction: {fireDirection} | position: {firePosition}");

        FireServerRpc(firePosition, fireDirection);

        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }

    protected override void SpawnProjectile(Vector3 position, Vector3 direction) {
        if (rocketPrefab == null) {
            Debug.LogError("Rocket prefab is null!");
            return;
        }

        Debug.Log($"Spawning rocket at {position} facing {direction}");

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