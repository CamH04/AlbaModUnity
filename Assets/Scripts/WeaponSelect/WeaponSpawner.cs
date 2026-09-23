using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using System.Collections;

public class WeaponSpawner : NetworkBehaviour {
    [Header("Registries")]
    public WeaponRegistry weaponRegistry;
    public CharacterRegistry characterRegistry;

    [Header("References")]
    public Transform weaponHolder;
    public Transform modelHolder;

    private GameObject _currentWeapon;
    private GameObject _currentModel;

    public GameObject GetCurrentModel() => _currentModel;

    public override void OnNetworkSpawn() {
        // Driven by ClientSelectionSync.SyncSelectionsServerRpc
        // which calls SpawnWithSelections once selections are confirmed
        //if (!IsServer)
        //    StartCoroutine(SpawnModelWhenReady());
    }

    IEnumerator SpawnModelWhenReady() {
        yield return null;
        yield return null;

        int charIndex = PlayerCharacterSelection.Instance != null
            ? PlayerCharacterSelection.Instance.GetCharacterIndex(OwnerClientId)
            : 0;
        SpawnCharacterModel(charIndex);
    }

    // Called by ClientSelectionSync on the server once selections are confirmed
    public void SpawnWithSelections(int charIndex, int weaponIndex) {
        if (!IsServer) return;
        SpawnWeapon(weaponIndex);
        SpawnCharacterModelForIndex(charIndex);
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

        AttachWeaponClientRpc(netObj.NetworkObjectId);

        Debug.Log($"[AlbaMod] Spawned {entry.weaponName} for client {OwnerClientId}");
    }

    [ClientRpc]
    void AttachWeaponClientRpc(ulong weaponNetworkObjectId) {
        StartCoroutine(AttachWeaponWhenReady(weaponNetworkObjectId));
    }

    IEnumerator AttachWeaponWhenReady(ulong weaponNetworkObjectId) {
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
            Debug.LogError($"[AlbaMod] WeaponSpawner: timed out waiting for weapon {weaponNetworkObjectId}");
            yield break;
        }

        _currentWeapon = weaponNetObj.gameObject;
        Debug.Log($"[AlbaMod] Client attached weapon {_currentWeapon.name} to player {OwnerClientId}");
    }

    // ── Character model ───────────────────────────────────────────────────────

    // Called by SpawnWithSelections (server) with confirmed index
    public void SpawnCharacterModelForIndex(int index) {
        if (characterRegistry == null || index >= characterRegistry.characters.Length) {
            Debug.LogWarning($"[AlbaMod] WeaponSpawner: invalid character index {index}");
            return;
        }

        var def = characterRegistry.characters[index];
        if (def.characterModelPrefab == null) {
            Debug.LogWarning($"[AlbaMod] WeaponSpawner: {def.characterName} has no model prefab!");
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

        Debug.Log($"[AlbaMod] Spawned model {def.characterName} for client {OwnerClientId} | isOwner:{IsOwner}");
    }

    // Called by SpawnModelWhenReady (clients) using locally stored index
    void SpawnCharacterModel(int index) {
        SpawnCharacterModelForIndex(index);
    }

    void SetModelVisibility(bool visible) {
        if (_currentModel == null) return;
        foreach (var rend in _currentModel.GetComponentsInChildren<Renderer>())
            rend.enabled = visible;
    }

    // ── Weapon position tracking ──────────────────────────────────────────────

    void LateUpdate() {
        if (_currentWeapon != null && weaponHolder != null && IsOwner) {
            _currentWeapon.transform.position = weaponHolder.position;
            _currentWeapon.transform.rotation = weaponHolder.rotation;
        }

        // Rotate modelHolder to match player yaw locally
        if (modelHolder != null && IsOwner) {
            modelHolder.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

    }

    public override void OnNetworkDespawn() {
        if (_currentModel != null)
            Destroy(_currentModel);
    }
}