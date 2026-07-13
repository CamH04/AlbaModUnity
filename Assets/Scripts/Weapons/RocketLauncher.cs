using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class RocketLauncher : WeaponBase {
    [Header("Rocket Launcher Settings")]
    public GameObject rocketPrefab;
    public float rocketSpeed = 25f;

    private Camera _playerCamera;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if (!IsOwner) return;
        StartCoroutine(FindCamera());
    }

    System.Collections.IEnumerator FindCamera() {
        // Wait until camera is available on the parent player
        float timeout = 5f;
        float elapsed = 0f;

        while (_playerCamera == null && elapsed < timeout) {
            // Search up to root then back down including inactive objects
            _playerCamera = transform.root.GetComponentInChildren<Camera>(true);

            if (_playerCamera == null)
                yield return new WaitForSeconds(0.1f);

            elapsed += 0.1f;
        }

        if (_playerCamera == null)
            Debug.LogError("RocketLauncher: timed out looking for player camera!");
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