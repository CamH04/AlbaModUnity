using Unity.Netcode;
using UnityEngine;

public class PlayerCharacterSelection : NetworkBehaviour {
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

    // Client calls this to register their selection on the server
    [ServerRpc(RequireOwnership = false)]
    public void SelectCharacterServerRpc(int characterIndex, ulong clientId) {
        _selections[clientId] = characterIndex;
        Debug.Log($"[AlbaMod] Server stored character {characterIndex} for client {clientId}");
    }

    // Also store locally for immediate access before network is ready
    public void SelectCharacterLocal(int characterIndex, ulong clientId) {
        _selections[clientId] = characterIndex;
    }

    public int GetCharacterIndex(ulong clientId) {
        return _selections.TryGetValue(clientId, out int index)
            ? index : DefaultCharacterIndex;
    }
}