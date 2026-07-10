using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUI : MonoBehaviour {
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject lobbyPanel;

    [Header("Main Panel")]
    public TMP_InputField lobbyNameInput;
    public TMP_InputField lobbyIdInput;
    public Button hostButton;
    public Button joinButton;
    public Button refreshButton;
    public Transform lobbyListContainer;
    public GameObject lobbyListItemPrefab;

    [Header("Lobby Panel")]
    public TMP_Text lobbyIdText;
    public TMP_Text playerCountText;
    public Button startButton;
    public Button leaveButton;

    void Start() {
        hostButton.onClick.AddListener(OnHost);
        joinButton.onClick.AddListener(OnJoin);
        refreshButton.onClick.AddListener(OnRefresh);
        leaveButton.onClick.AddListener(OnLeave);
        startButton.onClick.AddListener(OnStart);

        ShowMain();
    }

    async void OnHost() {
        string name = string.IsNullOrEmpty(lobbyNameInput.text)
            ? "My Lobby" : lobbyNameInput.text;

        NetworkBootstrapper.Instance.StartHost(name);
        ShowLobby();
    }

    async void OnJoin() {
        if (string.IsNullOrEmpty(lobbyIdInput.text)) return;
        NetworkBootstrapper.Instance.StartClient(lobbyIdInput.text);
        ShowLobby();
    }

    async void OnRefresh() {
        List<Lobby> lobbies = await LobbyManager.Instance.FetchLobbies();

        foreach (Transform child in lobbyListContainer)
            Destroy(child.gameObject);

        foreach (Lobby lobby in lobbies) {
            var item = Instantiate(lobbyListItemPrefab, lobbyListContainer);
            var label = item.GetComponentInChildren<TMP_Text>();
            var btn = item.GetComponentInChildren<Button>();

            label.text = $"{lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers})";
            string id = lobby.Id;
            btn.onClick.AddListener(() => {
                lobbyIdInput.text = id;
            });
        }
    }

    void OnLeave() {
        NetworkBootstrapper.Instance.Disconnect();
        ShowMain();
    }

    void OnStart() {
        NetworkBootstrapper.Instance.LoadGameScene();
    }


    void Update() {
        if (LobbyManager.Instance.CurrentLobby != null && lobbyPanel.activeSelf) {
            var lobby = LobbyManager.Instance.CurrentLobby;
            lobbyIdText.text = $"Lobby ID: {lobby.Id}";
            playerCountText.text = $"Players: {lobby.Players.Count}/2";

            // Only show Start button to the host
            bool isHost = Unity.Netcode.NetworkManager.Singleton != null
                       && Unity.Netcode.NetworkManager.Singleton.IsHost;
            startButton.gameObject.SetActive(isHost);
        }
    }

    void ShowMain() { mainPanel.SetActive(true); lobbyPanel.SetActive(false); }
    void ShowLobby() { mainPanel.SetActive(false); lobbyPanel.SetActive(true); }
}