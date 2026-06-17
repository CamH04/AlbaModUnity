using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class NetworkPlayerSpawner : MonoBehaviour {
    public GameObject playerPrefab;
    public string gameSceneName = "Game";

    IEnumerator Start() {
        while (NetworkManager.Singleton == null)
            yield return null;

        while (NetworkManager.Singleton.SceneManager == null)
            yield return null;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadComplete;
        Debug.Log("NetworkPlayerSpawner subscribed to scene load events");
    }

    void OnSceneLoadComplete(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut) {
        Debug.Log($"OnSceneLoadComplete fired: {sceneName} | IsServer: {NetworkManager.Singleton.IsServer}");

        if (!NetworkManager.Singleton.IsServer) return;
        if (sceneName != gameSceneName) return;

        Debug.Log($"Spawning players for scene {sceneName}");

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds) {
            GameObject playerInstance = Instantiate(playerPrefab);
            playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
            Debug.Log($"Spawned player for client {clientId}");
        }
    }

    void OnDestroy() {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadComplete;
    }
}