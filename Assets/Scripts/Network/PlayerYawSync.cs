using Unity.Netcode;
using UnityEngine;

public class PlayerYawSync : NetworkBehaviour {
    public NetworkVariable<float> Yaw = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private Transform _ori;

    void Start() {
        _ori = transform.Find("ori");
    }

    void LateUpdate() {
        if (!IsOwner) return;
        if (_ori == null) return;
        Yaw.Value = _ori.eulerAngles.y;
    }
}