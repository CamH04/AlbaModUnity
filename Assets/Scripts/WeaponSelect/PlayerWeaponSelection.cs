using UnityEngine;

public class PlayerWeaponSelection : MonoBehaviour {
    public static PlayerWeaponSelection Instance;

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

    public void StoreSelection(ulong clientId, int index) {
        _selections[clientId] = index;
        Debug.Log($"[AlbaMod] Stored weapon {index} for client {clientId}");
    }

    public int GetWeaponIndex(ulong clientId) {
        return _selections.TryGetValue(clientId, out int index)
            ? index : DefaultWeaponIndex;
    }
}