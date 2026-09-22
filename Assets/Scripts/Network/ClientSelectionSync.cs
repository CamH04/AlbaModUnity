using Unity.Netcode;
using UnityEngine;

public class ClientSelectionSync : NetworkBehaviour {
    private NetworkVariable<int> _characterIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> _weaponIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn() {
        _characterIndex.OnValueChanged += OnCharacterIndexReceived;

        if (IsOwner) {
            ulong localId = NetworkManager.Singleton.LocalClientId;

            int charIndex = PlayerCharacterSelection.Instance != null
                ? PlayerCharacterSelection.Instance.GetCharacterIndex(localId)
                : 0;

            int weaponIndex = PlayerWeaponSelection.Instance != null
                ? PlayerWeaponSelection.Instance.GetWeaponIndex(localId)
                : 0;

            Debug.Log($"[AlbaMod] ClientSelectionSync — char:{charIndex} weapon:{weaponIndex}");
            SyncSelectionsServerRpc(charIndex, weaponIndex);
        }
        else {
            // Client joining after host already set their index —
            // NetworkVariable has the value but OnValueChanged won't fire
            // so read and apply it directly
            StartCoroutine(SpawnModelFromCurrentValue());
        }
    }

    System.Collections.IEnumerator SpawnModelFromCurrentValue() {
        // Wait a couple frames for the NetworkVariable to be fully received
        yield return null;
        yield return null;

        int current = _characterIndex.Value;
        Debug.Log($"[AlbaMod] Non-owner reading existing character index: {current} for client {OwnerClientId}");

        var spawner = GetComponent<WeaponSpawner>();
        if (spawner != null)
            spawner.SpawnCharacterModelForIndex(current);
    }

    public override void OnNetworkDespawn() {
        _characterIndex.OnValueChanged -= OnCharacterIndexReceived;
    }

    [ServerRpc]
    void SyncSelectionsServerRpc(int charIndex, int weaponIndex) {
        if (PlayerCharacterSelection.Instance != null)
            PlayerCharacterSelection.Instance.StoreSelection(OwnerClientId, charIndex);

        if (PlayerWeaponSelection.Instance != null)
            PlayerWeaponSelection.Instance.StoreSelection(OwnerClientId, weaponIndex);

        _characterIndex.Value = charIndex;
        _weaponIndex.Value = weaponIndex;

        Debug.Log($"[AlbaMod] Server stored — client:{OwnerClientId} char:{charIndex} weapon:{weaponIndex}");

        var spawner = GetComponent<WeaponSpawner>();
        if (spawner != null)
            spawner.SpawnWithSelections(charIndex, weaponIndex);
        else
            Debug.LogError("[AlbaMod] ClientSelectionSync: WeaponSpawner not found!");
    }

    void OnCharacterIndexReceived(int previous, int current) {
        Debug.Log($"[AlbaMod] Character index changed — client:{OwnerClientId} index:{current}");

        var spawner = GetComponent<WeaponSpawner>();
        if (spawner != null)
            spawner.SpawnCharacterModelForIndex(current);
    }
}