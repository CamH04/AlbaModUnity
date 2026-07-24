using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour {
    [Header("References")]
    public GameObject pausePanel;

    private bool _isPaused = false;
    private PlayerHealth _localPlayerHealth;

    void Start() {
        SetPaused(false);
        Time.timeScale = 1.0f;
        StartCoroutine(FindLocalPlayer());
    }

    System.Collections.IEnumerator FindLocalPlayer() {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            yield return null;

        while (_localPlayerHealth == null) {
            foreach (var ph in FindObjectsOfType<PlayerHealth>()) {
                if (ph.IsOwner) { _localPlayerHealth = ph; break; }
            }
            yield return null;
        }
    }

    void Update() {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();

        if (Keyboard.current.nKey.wasPressedThisFrame)
            KillSelf();
    }

    void KillSelf() {
        if (_localPlayerHealth == null || _localPlayerHealth.IsDead) return;
        _localPlayerHealth.KillBindServerRpc();
    }

    void TogglePause() {
        _isPaused = !_isPaused;
        SetPaused(_isPaused);
    }

    void SetPaused(bool paused) {
        _isPaused = paused;
        pausePanel.SetActive(paused);
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    public void OnResumeClicked() => SetPaused(false);
    public void OnLeaveClicked() => NetworkBootstrapper.Instance.Disconnect();
}