using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class HealthDebug : NetworkBehaviour {
    void Update() {
        if (!IsOwner) return;
        if (Keyboard.current.hKey.wasPressedThisFrame)
            TakeDamageServerRpc(25f);
    }

    [ServerRpc]
    void TakeDamageServerRpc(float amount) {
        GetComponent<PlayerHealth>()?.TakeDamage(amount);
    }
}