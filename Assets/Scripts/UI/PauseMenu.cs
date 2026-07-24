using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour {
    [Header("References")]
    public GameObject pausePanel;

    private bool _isPaused = false;
    void Start() {
        SetPaused(false);
        Time.timeScale = 1.0f;
    }
    void Update() {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    void TogglePause() {
        _isPaused = !_isPaused;
        SetPaused(_isPaused);
    }

    void SetPaused(bool paused) {
        _isPaused = paused;
        pausePanel.SetActive(paused);

        // Lock/unlock cursor
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    public void OnResumeClicked() {
        SetPaused(false);
    }

    public void OnLeaveClicked() {
        NetworkBootstrapper.Instance.Disconnect();
    }
}