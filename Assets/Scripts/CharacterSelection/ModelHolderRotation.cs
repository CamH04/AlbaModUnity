using UnityEngine;

public class ModelHolderRotation : MonoBehaviour {
    private PlayerYawSync _yawSync;

    void Start() {
        _yawSync = GetComponentInParent<PlayerYawSync>();

        if (_yawSync == null)
            Debug.LogError("ModelHolderRotation: could not find PlayerYawSync in parent!");
    }

    void LateUpdate() {
        if (_yawSync == null) return;
        transform.rotation = Quaternion.Euler(0f, _yawSync.Yaw.Value, 0f);
    }
}