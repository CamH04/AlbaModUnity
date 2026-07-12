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

        // Use weapon holder if assigned, otherwise fall back to player root
        var holder = weaponHolder != null ? weaponHolder : transform;

        _currentWeapon = Instantiate(
            entry.weaponPrefab,
            holder.position,
            holder.rotation,
            holder              // parent to weapon holder so it moves with the player
        );

        var netObj = _currentWeapon.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.SpawnWithOwnership(OwnerClientId);

        Debug.Log($"Spawned {entry.weaponName} for client {OwnerClientId}");
    }
}