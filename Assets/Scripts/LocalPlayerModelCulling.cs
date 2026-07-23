using Unity.Netcode;
using UnityEngine;

public class LocalPlayerModelCulling : NetworkBehaviour {
    [SerializeField] private GameObject model;

    public override void OnNetworkSpawn() {
        if (!IsOwner)
            return;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) {
            renderer.enabled = false;
        }
    }
}