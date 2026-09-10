using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeBasePauseMenu : MonoBehaviour {
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private PlayerMovement playerMovement;

    private void Start() {
        pausePanel.SetActive(false);
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        playerMovement.SetInputEnabled(true);
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            TogglePauseMenu();
        }
    }

    private void TogglePauseMenu() {
        bool paused = !pausePanel.activeSelf;

        pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;

        playerMovement.SetInputEnabled(!paused);

        if (paused) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ResumeGame() {
        Debug.Log("RESUME BUTTON CLICKED");

        pausePanel.SetActive(false);
        Time.timeScale = 1f;

        playerMovement.SetInputEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void LeaveGame() {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene("Lobby");
    }

    private void OnDestroy() {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}