using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class NetworkBootstrapper : MonoBehaviour {
    public static NetworkBootstrapper Instance;

    [Header("Scenes")]
    public string lobbySceneName = "Lobby";
    public string gameSceneName = "Game";

    [Header("Prefabs")]
    public GameObject playerPrefab;

    // Holds pending approvals until game scene is ready
    private bool _gameSceneReady = false;

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

    public async void StartHost(string lobbyName) {
        await LobbyManager.Instance.CreateLobby(lobbyName);

        // Enable connection approval so we control when players spawn
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

        NetworkManager.Singleton.StartHost();
        Debug.Log("Host started — waiting in lobby");
    }

    public async void StartClient(string lobbyId) {
        bool joined = await LobbyManager.Instance.JoinLobbyById(lobbyId);
        if (!joined) return;

        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        NetworkManager.Singleton.StartClient();
        Debug.Log("Client started");
    }

    void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                   NetworkManager.ConnectionApprovalResponse response) {
        bool inGameScene = SceneManager.GetActiveScene().name == gameSceneName;

        response.Approved = true;
        response.CreatePlayerObject = inGameScene; // Only create player if in game scene
        response.PlayerPrefabHash = null;
        response.Pending = false;

        if (inGameScene) {
            response.Position = GetSpawnPosition();
            response.Rotation = Quaternion.identity;
        }

        Debug.Log($"Client {request.ClientNetworkId} approved | inGameScene: {inGameScene} | createPlayer: {inGameScene}");
    }

    Vector3 GetSpawnPosition() {
        // Find a SpawnPoint tagged object if one exists, otherwise default
        GameObject spawnPoint = GameObject.FindGameObjectWithTag("SpawnPoint");
        return spawnPoint != null ? spawnPoint.transform.position : new Vector3(0, 2, 0);
    }

    public void LoadGameScene() {
        if (!NetworkManager.Singleton.IsHost) {
            Debug.LogWarning("Only host can start the game");
            return;
        }

        // Subscribe to scene load so we can spawn players after scene is ready
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnGameSceneLoaded;
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public async void Disconnect() {
        await LobbyManager.Instance.LeaveLobby();
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(lobbySceneName);
    }


    private bool _spawningInProgress = false;

    void OnGameSceneLoaded(string sceneName, LoadSceneMode mode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut) {
        if (sceneName != gameSceneName) return;
        if (_spawningInProgress) return; // prevent double spawn
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnGameSceneLoaded;
        Debug.Log("Game scene loaded — scheduling player spawn next frame");
        _spawningInProgress = true;
        StartCoroutine(SpawnPlayersNextFrame());
    }

    IEnumerator SpawnPlayersNextFrame() {
        // Wait two frames for NGO to fully finish its internal scene sync
        yield return null;
        yield return null;

        Debug.Log($"Spawning players — connected clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds) {
            try {
                var playerInstance = Instantiate(playerPrefab);
                playerInstance.transform.position = GetSpawnPosition();
                playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
                Debug.Log($"Spawned player for client {clientId} at {playerInstance.transform.position}");
                StartCoroutine(CheckPlayerSurvived(playerInstance, clientId));
            }
            catch (System.Exception e) {
                Debug.LogError($"Exception spawning player for client {clientId}: {e}");
            }
        }
    }

    IEnumerator CheckPlayerSurvived(GameObject player, ulong clientId) {
        yield return null;
        yield return null;
        if (player == null)
            Debug.LogError($"Player for client {clientId} destroyed after spawn");
        else
            Debug.Log($"Player survived — pos: {player.transform.position} | active: {player.activeSelf}");
    }
}