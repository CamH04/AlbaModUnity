using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkBootstrapper : MonoBehaviour {
    public static NetworkBootstrapper Instance;

    [Header("Scenes")]
    public string lobbySceneName = "Lobby";
    public string gameSceneName = "Game";

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    async void Start() {
        await LobbyManager.Instance.Init();
    }

    // Just creates the lobby + starts NGO host — stays in Lobby scene
    public async void StartHost(string lobbyName) {
        await LobbyManager.Instance.CreateLobby(lobbyName);
        NetworkManager.Singleton.StartHost();
        Debug.Log("Host started — waiting in lobby");
        // NOTE: no scene load here anymore
    }

    public async void StartClient(string lobbyId) {
        bool joined = await LobbyManager.Instance.JoinLobbyById(lobbyId);
        if (!joined) return;
        NetworkManager.Singleton.StartClient();
        Debug.Log("Client started — waiting in lobby");
    }

    // Only the HOST should call this — it loads the scene for ALL connected clients
    public void LoadGameScene() {
        if (!NetworkManager.Singleton.IsHost) {
            Debug.LogWarning("Only the host can start the game");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public async void Disconnect() {
        await LobbyManager.Instance.LeaveLobby();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(lobbySceneName);
    }
}