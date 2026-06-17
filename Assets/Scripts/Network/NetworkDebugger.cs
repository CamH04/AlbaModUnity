using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class NetworkDebugger : MonoBehaviour {
    IEnumerator Start() {
        while (NetworkManager.Singleton == null)
            yield return null;

        NetworkManager.Singleton.OnServerStarted += () => Debug.Log("SERVER STARTED");
        NetworkManager.Singleton.OnClientConnectedCallback += (id) => Debug.Log($"CLIENT CONNECTED: {id}");
        NetworkManager.Singleton.OnClientDisconnectCallback += (id) => Debug.Log($"CLIENT DISCONNECTED: {id}");

        // Wait until SceneManager is actually initialized (only happens after StartHost/StartClient)
        while (NetworkManager.Singleton.SceneManager == null)
            yield return null;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += (sceneName, mode, completed, timedOut) => {
            Debug.Log($"SCENE LOAD COMPLETE: {sceneName} | Completed: {completed.Count} | TimedOut: {timedOut.Count}");
        };

        NetworkManager.Singleton.SceneManager.OnSceneEvent += (e) => {
            Debug.Log($"SCENE EVENT: {e.SceneEventType} | Scene: {e.SceneName} | Client: {e.ClientId}");
        };
    }
}