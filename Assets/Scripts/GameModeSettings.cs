using Unity.Netcode;
using UnityEngine;

public class GameModeSettings : NetworkBehaviour {
    public static GameModeSettings Instance;

    // Synced to all clients so everyone knows the mode
    public NetworkVariable<bool> IsTenXMode = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public void SetTenXMode(bool enabled) {
        if (!IsServer) return;
        IsTenXMode.Value = enabled;
    }
}