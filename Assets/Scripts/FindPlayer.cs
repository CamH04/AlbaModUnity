using UnityEngine;
using Unity.Netcode;

public class PlayerFinder : MonoBehaviour {
    void Start() {
        InvokeRepeating("FindPlayers", 1f, 2f);
    }

    void FindPlayers() {
        var networkObjects = FindObjectsOfType<NetworkObject>();
        var allObjects = FindObjectsOfType<GameObject>();

        Debug.Log($"Found {networkObjects.Length} NetworkObjects | Total GameObjects in scene: {allObjects.Length}");

        foreach (var obj in networkObjects) {
            Debug.Log($"- {obj.name} | NetworkID: {obj.NetworkObjectId} | Owner: {obj.OwnerClientId} | IsPlayer: {obj.IsPlayerObject} | IsSpawned: {obj.IsSpawned}");
        }
    }
}