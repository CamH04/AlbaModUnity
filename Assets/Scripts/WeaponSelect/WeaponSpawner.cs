using Unity.Netcode;
using UnityEngine;

public class WeaponSpawner : NetworkBehaviour {
    public WeaponRegistry weaponRegistry;

    [Tooltip("Where the weapon attaches on the player — e.g. a hand or camera child")]
    public Transform weaponHolder;

    private GameObject _currentWeapon;

    public override void OnNetworkSpawn() {
        // Only server spawns weapons
        if (!IsServer) return;

        int index = PlayerWeaponSelection.Instance != null
            ? PlayerWeaponSelection.Instance.GetWeaponIndex(OwnerClientId)
            : 0;

        SpawnWeapon(index);
    }

    void SpawnWeapon(int index) {
        if (weaponRegistry == null || index >= weaponRegistry.weapons.Length) return;

        var entry = weaponRegistry.weapons[index];
        if (entry.weaponPrefab == null) return;

        if (_currentWeapon != null) {
            _currentWeapon.GetComponent<NetworkObject>()?.Despawn();
            Destroy(_currentWeapon);
        }

        // NOTE: We intentionally do NOT parent here and do NOT pass a parent
        // into Instantiate. Unity-level parenting done before Spawn() is not
        // replicated by Netcode, and TrySetParent() requires the parent
        // transform to have its own NetworkObject (weaponHolder usually
        // doesn't, since it's just a hand/camera socket). Instead, each
        // weapon attaches itself locally on every client in its own
        // OnNetworkSpawn (see WeaponBase/RocketLauncher).
        _currentWeapon = Instantiate(
            entry.weaponPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        var netObj = _currentWeapon.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.SpawnWithOwnership(OwnerClientId);

        // Pass the player's camera directly to the weapon
        var playerCamera = GetComponentInChildren<Camera>(true);
        var weapon = _currentWeapon.GetComponent<WeaponBase>();
        if (weapon != null)
            weapon.SetCamera(playerCamera);

        Debug.Log($"Spawned {entry.weaponName} for client {OwnerClientId} | camera: {(playerCamera != null ? playerCamera.name : "NULL")}");
    }
}