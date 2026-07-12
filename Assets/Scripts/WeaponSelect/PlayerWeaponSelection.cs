using Unity.Netcode;
using UnityEngine;

public class PlayerWeaponSelection : NetworkBehaviour {
    public static PlayerWeaponSelection Instance;

    // Maps clientId -> selected weapon index
    private System.Collections.Generic.Dictionary<ulong, int> _selections
        = new System.Collections.Generic.Dictionary<ulong, int>();

    public int DefaultWeaponIndex = 0;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // Called by client when they pick a weapon in lobby
    [ServerRpc(RequireOwnership = false)]
    public void SelectWeaponServerRpc(int weaponIndex, ulong clientId) {
        _selections[clientId] = weaponIndex;
        Debug.Log($"Client {clientId} selected weapon index {weaponIndex}");
    }

    public int GetWeaponIndex(ulong clientId) {
        return _selections.TryGetValue(clientId, out int index) ? index : DefaultWeaponIndex;
    }
}