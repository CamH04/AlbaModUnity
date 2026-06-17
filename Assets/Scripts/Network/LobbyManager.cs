using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using ParrelSync;

public class LobbyManager : MonoBehaviour {
    public static LobbyManager Instance;

    private Lobby _currentLobby;
    private float _heartbeatTimer;
    private float _pollTimer;
    private const float HeartbeatInterval = 15f;
    private const float PollInterval = 1.5f;

    public Lobby CurrentLobby => _currentLobby;
    public bool IsHost => _currentLobby != null &&
                                  _currentLobby.HostId == AuthenticationService.Instance.PlayerId;

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Call once at startup

    public async Task Init() {
        await UnityServices.InitializeAsync();

        string profile = "Host";

        if (!AuthenticationService.Instance.IsSignedIn) {
#if UNITY_EDITOR
        if (ClonesManager.IsClone())
            profile = "Client";
#endif

            AuthenticationService.Instance.SwitchProfile(profile);
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Debug.Log($"Signed in as {AuthenticationService.Instance.PlayerId} (profile: {profile})");
    }

    public async Task<string> CreateLobby(string lobbyName) {
        try {
            string relayCode = await RelayManager.Instance.CreateRelay();

            var options = new CreateLobbyOptions {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Public, relayCode) }
                }
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2, options);
            Debug.Log($"Lobby created: {_currentLobby.Id}");
            return _currentLobby.Id;
        }
        catch (LobbyServiceException e) {
            Debug.LogError($"Lobby creation failed: {e}");
            return null;
        }
    }

    public async Task<bool> JoinLobbyById(string lobbyId) {
        try {
            _currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            string relayCode = _currentLobby.Data["RelayCode"].Value;
            await RelayManager.Instance.JoinRelay(relayCode);
            Debug.Log($"Joined lobby: {lobbyId}");
            return true;
        }
        catch (LobbyServiceException e) {
            Debug.LogError($"Lobby join failed: {e}");
            return false;
        }
    }

    public async Task<List<Lobby>> FetchLobbies() {
        try {
            var options = new QueryLobbiesOptions { Count = 10 };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }
        catch (LobbyServiceException e) {
            Debug.LogError($"Lobby fetch failed: {e}");
            return new List<Lobby>();
        }
    }

    public async Task LeaveLobby() {
        if (_currentLobby == null) return;
        try {
            await LobbyService.Instance.RemovePlayerAsync(
                _currentLobby.Id,
                AuthenticationService.Instance.PlayerId
            );
            _currentLobby = null;
        }
        catch (LobbyServiceException e) {
            Debug.LogError($"Leave lobby failed: {e}");
        }
    }

    void Update() {
        HandleHeartbeat();
        HandlePoll();
    }

    // Host must ping lobby every 15s or it expires
    async void HandleHeartbeat() {
        if (_currentLobby == null || !IsHost) return;
        _heartbeatTimer += Time.deltaTime;
        if (_heartbeatTimer >= HeartbeatInterval) {
            _heartbeatTimer = 0f;
            await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
        }
    }

    // Poll for lobby updates (client waiting for host)
    async void HandlePoll() {
        if (_currentLobby == null) return;
        _pollTimer += Time.deltaTime;
        if (_pollTimer >= PollInterval) {
            _pollTimer = 0f;
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
        }
    }
}