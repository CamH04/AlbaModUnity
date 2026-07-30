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

    // No longer an RPC — just store locally
    public void SelectCharacter(int characterIndex, ulong clientId) {
        _selections[clientId] = characterIndex;
        Debug.Log($"Client {clientId} selected character index {characterIndex}");
    }

    public int GetCharacterIndex(ulong clientId) {
        return _selections.TryGetValue(clientId, out int index)
            ? index : DefaultCharacterIndex;
    }
}