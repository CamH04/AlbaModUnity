using Unity.Netcode;
using UnityEngine;

public class WeaponSpawner : NetworkBehaviour {
    [Header("Registries")]
    public WeaponRegistry weaponRegistry;
    public CharacterRegistry characterRegistry;

    [Header("References")]
    [Tooltip("Where the weapon should appear — e.g. a camera child or hand socket")]
    public Transform weaponHolder;

    [Tooltip("Where the character model spawns — usually the player root or hip bone")]
    public Transform modelHolder;

    private GameObject _currentWeapon;
    private GameObject _currentModel;

    public override void OnNetworkSpawn() {
        // Server spawns the networked weapon
        if (IsServer) {
            int weaponIndex = PlayerWeaponSelection.Instance != null
                ? PlayerWeaponSelection.Instance.GetWeaponIndex(OwnerClientId)
                : 0;
            SpawnWeapon(weaponIndex);
        }

        // Every client spawns the character model locally (purely visual)
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
            Vector3.zero,
            Quaternion.identity
        );

        var netObj = _currentWeapon.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.SpawnWithOwnership(OwnerClientId);

        var playerCamera = GetComponentInChildren<Camera>(true);
        var weapon = _currentWeapon.GetComponent<WeaponBase>();
        if (weapon != null)
            weapon.SetCamera(playerCamera);

        Debug.Log($"Spawned {entry.weaponName} for client {OwnerClientId} | camera: {(playerCamera != null ? playerCamera.name : "NULL")}");
    }

    // ── Character model ───────────────────────────────────────────────────────

    void SpawnCharacterModel(int index) {
        if (characterRegistry == null
            || index >= characterRegistry.characters.Length) return;

        var def = characterRegistry.characters[index];
        if (def.characterModelPrefab == null) {
            Debug.LogWarning($"WeaponSpawner: character {def.characterName} has no model prefab assigned!");
            return;
        }

        if (_currentModel != null)
            Destroy(_currentModel);

        var holder = modelHolder != null ? modelHolder : transform;

        // Plain Instantiate — no NetworkObject, purely visual on each client
        _currentModel = Instantiate(
            def.characterModelPrefab,
            holder.position,
            holder.rotation,
            holder          // parented locally so it follows the player
        );

        // Hide the model for the local owner (first-person view)
        // but keep it visible for everyone else
        if (IsOwner)
            SetModelVisibility(false);

        Debug.Log($"Spawned model {def.characterName} for client {OwnerClientId} | isOwner: {IsOwner}");
    }

    void SetModelVisibility(bool visible) {
        if (_currentModel == null) return;
        foreach (var rend in _currentModel.GetComponentsInChildren<Renderer>())
            rend.enabled = visible;
    }

    // ── Weapon position tracking ──────────────────────────────────────────────

    void LateUpdate() {
        TrackWeaponToHolder();
    }

    void TrackWeaponToHolder() {
        if (_currentWeapon == null) return;
        if (weaponHolder == null) return;

        // Move and rotate the weapon to match the holder every frame
        // LateUpdate ensures this runs after all movement and camera updates
        _currentWeapon.transform.position = weaponHolder.position;
        _currentWeapon.transform.rotation = weaponHolder.rotation;
    }

    public override void OnNetworkDespawn() {
        if (_currentModel != null)
            Destroy(_currentModel);
    }
}