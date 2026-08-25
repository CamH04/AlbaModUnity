using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PlaylistMenuUI : MonoBehaviour {
    [Header("Panel")]
    public GameObject playlistPanel;

    [Header("Track List")]
    public Transform trackListContainer;
    public GameObject trackEntryPrefab;

    [Header("Now Playing")]
    public TextMeshProUGUI nowPlayingText;
    public TextMeshProUGUI nowPlayingArtist;
    public Button playStopButton;
    public TextMeshProUGUI playStopButtonText;
    public Button skipButton;

    [Header("Volume")]
    public Slider volumeSlider;

    [Header("Bulk Actions")]
    public Button enableAllButton;
    public Button disableAllButton;

    private List<GameObject> _trackEntries = new List<GameObject>();

    void Start() {
        if (playlistPanel != null)
            playlistPanel.SetActive(false);

        if (volumeSlider != null) {
            volumeSlider.value = MusicPlayer.Instance != null
                ? MusicPlayer.Instance.Volume : 0.5f;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (playStopButton != null)
            playStopButton.onClick.AddListener(OnPlayStopClicked);

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);

        if (enableAllButton != null)
            enableAllButton.onClick.AddListener(OnEnableAll);

        if (disableAllButton != null)
            disableAllButton.onClick.AddListener(OnDisableAll);
    }

    void Update() {
        if (playlistPanel != null && playlistPanel.activeSelf)
            RefreshNowPlaying();
    }

    public void OpenPlaylist() {
        if (playlistPanel != null)
            playlistPanel.SetActive(true);
        BuildTrackList();
        RefreshNowPlaying();
    }

    public void ClosePlaylist() {
        if (playlistPanel != null)
            playlistPanel.SetActive(false);
    }

    void BuildTrackList() {
        foreach (var entry in _trackEntries)
            if (entry != null) Destroy(entry);
        _trackEntries.Clear();

        if (MusicPlayer.Instance == null) return;

        var playlist = MusicPlayer.Instance.playlist;
        if (playlist == null) return;

        for (int i = 0; i < playlist.tracks.Length; i++) {
            var track = playlist.tracks[i];
            var entryGO = Instantiate(trackEntryPrefab, trackListContainer);

            // Track name
            var nameText = entryGO.transform.Find("TrackName") ?.GetComponent<TextMeshProUGUI>();

            if (nameText != null)
                nameText.text = track.trackName;

            // Toggle
            var toggle = entryGO.transform.Find("Toggle")
                ?.GetComponent<Toggle>();
            if (toggle != null) {
                int index = i;
                toggle.isOn = MusicPlayer.Instance.IsTrackEnabled(index);
                toggle.onValueChanged.AddListener(val =>
                    MusicPlayer.Instance.SetTrackEnabled(index, val));
            }

            _trackEntries.Add(entryGO);
        }
    }

    void RefreshNowPlaying() {
        if (MusicPlayer.Instance == null) return;

        var track = MusicPlayer.Instance.GetCurrentTrack();

        if (nowPlayingText != null)
            nowPlayingText.text = track != null ? track.trackName : "Nothing playing";

        if (nowPlayingArtist != null)
            nowPlayingArtist.text = track != null ? track.artistName : "";

        if (playStopButtonText != null)
            playStopButtonText.text = MusicPlayer.Instance.IsPlaying ? "Stop" : "Play";
    }

    void OnPlayStopClicked() {
        if (MusicPlayer.Instance == null) return;

        if (MusicPlayer.Instance.IsPlaying)
            MusicPlayer.Instance.StopPlayback();
        else
            MusicPlayer.Instance.StartPlayback();
    }

    void OnSkipClicked() => MusicPlayer.Instance?.SkipTrack();
    void OnEnableAll() { MusicPlayer.Instance?.EnableAll(); BuildTrackList(); }
    void OnDisableAll() { MusicPlayer.Instance?.DisableAll(); BuildTrackList(); }
    void OnVolumeChanged(float val) => MusicPlayer.Instance?.SetVolume(val);
}