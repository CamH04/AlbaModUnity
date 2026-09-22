using UnityEngine;

public class PlayerCharacterSelection : MonoBehaviour {
    public static PlayerCharacterSelection Instance;

    private System.Collections.Generic.Dictionary<ulong, int> _selections
        = new System.Collections.Generic.Dictionary<ulong, int>();

    public int DefaultCharacterIndex = 0;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public void StoreSelection(ulong clientId, int index) {
        _selections[clientId] = index;
        Debug.Log($"[AlbaMod] Stored character {index} for client {clientId}");
    }

    public int GetCharacterIndex(ulong clientId) {
        return _selections.TryGetValue(clientId, out int index)
            ? index : DefaultCharacterIndex;
    }
}