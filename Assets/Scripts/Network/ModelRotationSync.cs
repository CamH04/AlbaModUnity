using Unity.Netcode;
using UnityEngine;

public class ModelRotationSync : NetworkBehaviour {
    private NetworkVariable<float> _yaw = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private WeaponSpawner _weaponSpawner;

    void Awake() {
        _weaponSpawner = GetComponent<WeaponSpawner>();
    }

    public override void OnNetworkSpawn() {
        _yaw.OnValueChanged += OnYawChanged;
    }

    public override void OnNetworkDespawn() {
        _yaw.OnValueChanged -= OnYawChanged;
    }

    void Update() {
        if (!IsOwner) return;

        // Owner writes yaw every frame
        _yaw.Value = transform.eulerAngles.y;
    }

    void OnYawChanged(float previous, float current) {
        if (IsOwner) return;

        // Rotate the modelHolder so the parented model follows
        var holder = _weaponSpawner?.modelHolder;
        if (holder != null)
            holder.rotation = Quaternion.Euler(0f, current, 0f);
    }
}