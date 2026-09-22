using Unity.Netcode;
using UnityEngine;

public class ModelRotationSync : NetworkBehaviour {
    // Synced yaw rotation for all clients to read
    private NetworkVariable<float> _yaw = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private WeaponSpawner _weaponSpawner;
    private Transform _modelTransform;

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
        // Owner writes their yaw every frame
        if (IsOwner) {
            _yaw.Value = transform.eulerAngles.y;
        }

        // Apply yaw to model on all clients every frame
        ApplyYawToModel(_yaw.Value);
    }

    void OnYawChanged(float previous, float current) {
        ApplyYawToModel(current);
    }

    void ApplyYawToModel(float yaw) {
        if (_weaponSpawner == null) return;

        // Get model from WeaponSpawner
        var model = _weaponSpawner.GetCurrentModel();
        if (model == null) return;

        model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}