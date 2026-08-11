using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class WeaponSpawner : NetworkBehaviour {
    [Header("Registries")]
    public WeaponRegistry weaponRegistry;
    public CharacterRegistry characterRegistry;

    [Header("References")]
    public Transform weaponHolder;
    public Transform modelHolder;

    private GameObject _currentWeapon;
    private GameObject _currentModel;

    public override void OnNetworkSpawn() {
        if (IsServer) {
            int weaponIndex = PlayerWeaponSelection.Instance != null
                ? PlayerWeaponSelection.Instance.GetWeaponIndex(OwnerClientId)
                : 0;
            SpawnWeapon(weaponIndex);
        }

        int charIndex = PlayerCharacterSelection.Instance != null
            ? PlayerCharacterSelection.Instance.GetCharacterIndex(OwnerClientId)
            : 0;
        SpawnCharacterModel(charIndex);
    }

    // ── Weapon ────────────────────────────────────────────────────────────────

    void SpawnWeapon(int index) {
        if (weaponRegistry == null || index >= weaponRegistry.weapons.Length) return;

        var entry = weaponRegistry.weapons[index];
        if (entry.weaponPrefab == null) return;

        if (_currentWeapon != null) {
            _currentWeapon.GetComponent<NetworkObject>()?.Despawn();
            Destroy(_currentWeapon);
        }

        _currentWeapon = Instantiate(
            entry.weaponPrefab,
            weaponHolder != null ? weaponHolder.position : transform.position,
            weaponHolder != null ? weaponHolder.rotation : transform.rotation
        );

        var netObj = _currentWeapon.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.SpawnWithOwnership(OwnerClientId);

        var playerCamera = GetComponentInChildren<Camera>(true);
        var weapon = _currentWeapon.GetComponent<WeaponBase>();
        if (weapon != null)
            weapon.SetCamera(playerCamera);

        // Tell all clients to attach the weapon visually to this player
        AttachWeaponClientRpc(netObj.NetworkObjectId);

        Debug.Log($"Spawned {entry.weaponName} for client {OwnerClientId}");
    }

    [ClientRpc]
    void AttachWeaponClientRpc(ulong weaponNetworkObjectId) {
        StartCoroutine(AttachWeaponWhenReady(weaponNetworkObjectId));
    }

    System.Collections.IEnumerator AttachWeaponWhenReady(ulong weaponNetworkObjectId) {
        // Wait until the weapon NetworkObject is registered on this client
        NetworkObject weaponNetObj = null;
        float timeout = 5f;
        float elapsed = 0f;

        while (weaponNetObj == null && elapsed < timeout) {
            NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(weaponNetworkObjectId, out weaponNetObj);
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (weaponNetObj == null) {
            Debug.LogError($"WeaponSpawner: timed out waiting for weapon {weaponNetworkObjectId}");
            yield break;
        }

        // Store reference so LateUpdate can track it
        _currentWeapon = weaponNetObj.gameObject;
        Debug.Log($"Client attached weapon {_currentWeapon.name} to player {OwnerClientId}");
    }

    // ── Character model ───────────────────────────────────────────────────────

    void SpawnCharacterModel(int index) {
        if (characterRegistry == null
            || index >= characterRegistry.characters.Length) return;

        var def = characterRegistry.characters[index];
        if (def.characterModelPrefab == null) {
            Debug.LogWarning($"Character {def.characterName} has no model prefab!");
            return;
        }

        if (_currentModel != null)
            Destroy(_currentModel);

        var holder = modelHolder != null ? modelHolder : transform;

        _currentModel = Instantiate(
            def.characterModelPrefab,
            holder.position,
            holder.rotation,
            holder
        );

        if (IsOwner)
            SetModelVisibility(false);

        Debug.Log($"Spawned model {def.characterName} for client {OwnerClientId}");
    }

    void SetModelVisibility(bool visible) {
        if (_currentModel == null) return;
        foreach (var rend in _currentModel.GetComponentsInChildren<Renderer>())
            rend.enabled = visible;
    }

    // ── Weapon position tracking ──────────────────────────────────────────────

    void LateUpdate() {
        if (_currentWeapon == null) return;
        if (weaponHolder == null) return;

        // Only the owner drives the weapon position
        // NetworkTransform on the weapon replicates it to everyone else
        if (IsOwner) {
            _currentWeapon.transform.position = weaponHolder.position;
            _currentWeapon.transform.rotation = weaponHolder.rotation;
        }
    }

    public override void OnNetworkDespawn() {
        if (_currentModel != null)
            Destroy(_currentModel);
    }
}