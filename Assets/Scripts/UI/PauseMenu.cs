using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour {
    [Header("Panel")]
    public GameObject pausePanel;

    [Header("Music Controls")]
    public Slider volumeSlider;
    public Button playStopButton;
    public TextMeshProUGUI playStopButtonText;
    public Button skipButton;
    public TextMeshProUGUI nowPlayingText;
    public TextMeshProUGUI nowPlayingArtist;

    private bool _isPaused = false;
    private PlayerHealth _localPlayerHealth;

    void Start() {
        StartCoroutine(FindLocalPlayer());
        InitMusicControls();

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    System.Collections.IEnumerator FindLocalPlayer() {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            yield return null;

        while (_localPlayerHealth == null) {
            foreach (var ph in FindObjectsOfType<PlayerHealth>())
                if (ph.IsOwner) { _localPlayerHealth = ph; break; }
            yield return null;
        }
    }

    void InitMusicControls() {
        if (volumeSlider != null) {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = MusicPlayer.Instance != null
                ? MusicPlayer.Instance.Volume : 0.5f;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (playStopButton != null)
            playStopButton.onClick.AddListener(OnPlayStopClicked);

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);
    }

    void Update() {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();

        if (Keyboard.current.nKey.wasPressedThisFrame)
            KillSelf();

        if (_isPaused)
            RefreshMusicControls();
    }

    // ── Music ─────────────────────────────────────────────────────────────────

    void RefreshMusicControls() {
        if (MusicPlayer.Instance == null) return;

        var track = MusicPlayer.Instance.GetCurrentTrack();

        if (nowPlayingText != null)
            nowPlayingText.text = track != null ? track.trackName : "Nothing playing";

        if (nowPlayingArtist != null)
            nowPlayingArtist.text = track != null ? track.artistName : "";

        if (playStopButtonText != null)
            playStopButtonText.text = MusicPlayer.Instance.IsPlaying ? "Stop" : "Play";

        // Keep slider in sync if volume changed elsewhere
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(MusicPlayer.Instance.Volume);
    }

    void OnVolumeChanged(float val) => MusicPlayer.Instance?.SetVolume(val);
    void OnPlayStopClicked() {
        if (MusicPlayer.Instance == null) return;
        if (MusicPlayer.Instance.IsPlaying)
            MusicPlayer.Instance.StopPlayback();
        else
            MusicPlayer.Instance.StartPlayback();
    }
    void OnSkipClicked() => MusicPlayer.Instance?.SkipTrack();

    // ── Pause ─────────────────────────────────────────────────────────────────

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