using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class NetworkBootstrapper : MonoBehaviour {
    public static NetworkBootstrapper Instance;

    [Header("Scenes")]
    public string lobbySceneName = "Lobby";
    public string gameSceneName = "Game";

    [Header("Prefabs")]
    public GameObject playerPrefab;

    [Header("Spawn Points")]
    public string spawnPointTag = "SpawnPoint"; // Tag your spawn point objects with this

    private Transform[] _spawnPoints;

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
        response.CreatePlayerObject = inGameScene;
        response.PlayerPrefabHash = null;
        response.Pending = false;

        if (inGameScene) {
            response.Position = GetSpawnPosition(request.ClientNetworkId);
            response.Rotation = Quaternion.identity;
        }

        Debug.Log($"Client {request.ClientNetworkId} approved | inGameScene: {inGameScene} | createPlayer: {inGameScene}");
    }

    // Look up spawn points fresh from the currently loaded scene
    void RefreshSpawnPoints() {
        GameObject[] tagged = GameObject.FindGameObjectsWithTag(spawnPointTag);

        // Sort by name so SpawnPoint_1, SpawnPoint_2 etc come back in consistent order
        _spawnPoints = tagged
            .OrderBy(go => go.name)
            .Select(go => go.transform)
            .ToArray();

        Debug.Log($"Found {_spawnPoints.Length} spawn points in scene");
    }

    Vector3 GetSpawnPosition(ulong clientId) {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
            RefreshSpawnPoints();

        Debug.Log($"GetSpawnPosition called for clientId={clientId} | spawnPoints.Length={_spawnPoints?.Length ?? 0}");

        if (_spawnPoints != null && _spawnPoints.Length > 0) {
            int index = (int)(clientId % (ulong)_spawnPoints.Length);
            Debug.Log($"clientId={clientId} ? index={index} ? position={_spawnPoints[index].position}");
            return _spawnPoints[index].position;
        }

        Debug.LogWarning("No spawn points found in scene — using fallback position");
        return new Vector3(clientId * 3f, 4, 0);
    }

    public void LoadGameScene() {
        if (!NetworkManager.Singleton.IsHost) {
            Debug.LogWarning("Only host can start the game");
            return;
        }

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
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut) {
        if (sceneName != gameSceneName) return;
        if (_spawningInProgress) return;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnGameSceneLoaded;

        RefreshSpawnPoints(); // grab spawn points now that Game scene is active

        Debug.Log("Game scene loaded — scheduling player spawn next frame");
        _spawningInProgress = true;
        StartCoroutine(SpawnPlayersNextFrame());
    }

    IEnumerator SpawnPlayersNextFrame() {
        yield return null;
        yield return null;

        Debug.Log($"Spawning players — connected clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds) {
            try {
                var playerInstance = Instantiate(playerPrefab);
                playerInstance.transform.position = GetSpawnPosition(clientId);
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