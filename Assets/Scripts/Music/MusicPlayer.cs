using UnityEngine;
using System.Collections.Generic;

public class MusicPlayer : MonoBehaviour {
    public static MusicPlayer Instance;

    [Header("References")]
    public MusicPlaylist playlist;

    [Header("Settings")]
    public float volume = 0.5f;

    private AudioSource _audioSource;
    private readonly List<MusicTrack> _queue = new List<MusicTrack>();
    private MusicTrack _currentTrack;
    private bool _isPlaying = false;

    // Persisted selection — standard track indices the player has enabled
    private readonly HashSet<int> _enabledTracks = new HashSet<int>();
    private const string SaveKey = "AlbaMod_EnabledTracks";

    // Lighthouse-exclusive tracks are selected separately, but their selected
    // songs share the same live queue as the standard playlist.
    private readonly HashSet<int> _enabledLighthouseTracks = new HashSet<int>();
    private const string LighthouseSaveKey = "AlbaMod_EnabledLighthouseTracks";

    public bool IsPlaying => _isPlaying;

    public AudioSource AudioSource => _audioSource;

    public MusicTrack CurrentTrack => _currentTrack;

    public int CurrentTrackIndex {
        get {
            if (_currentTrack == null || playlist == null || playlist.tracks == null)
                return -1;

            return System.Array.IndexOf(playlist.tracks, _currentTrack);
        }
    }

    public float Volume {
        get => volume;
        set {
            volume = Mathf.Clamp01(value);
            if (_audioSource != null) _audioSource.volume = volume;
            PlayerPrefs.SetFloat("AlbaMod_MusicVolume", volume);
        }
    }

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = false;
            _audioSource.volume = volume;
            LoadSelection();
            LoadLighthouse();
        }
        else Destroy(gameObject);
    }

    void Start() {
        volume = PlayerPrefs.GetFloat("AlbaMod_MusicVolume", 0.5f);
        _audioSource.volume = volume;

        if (HasSelection)
            StartPlayback();
    }

    void Update() {
        // Advance to next track when current finishes
        if (_isPlaying && _audioSource != null && !_audioSource.isPlaying)
            PlayNext();
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    bool HasSelection =>
        _enabledTracks.Count > 0 || _enabledLighthouseTracks.Count > 0;

    public void StartPlayback() {
        if (!HasSelection) return;
        BuildShuffledQueue();
        _isPlaying = true;
        PlayNext();
    }

    public void StopPlayback() {
        _isPlaying = false;
        _audioSource.Stop();
    }

    public void SkipTrack() {
        if (!_isPlaying) return;
        PlayNext();
    }

    void PlayNext() {
        // Skip over any entries that are missing or have no clip.
        while (true) {
            if (_queue.Count == 0)
                BuildShuffledQueue();

            if (_queue.Count == 0) {
                StopPlayback();
                return;
            }

            _currentTrack = _queue[0];
            _queue.RemoveAt(0);

            if (_currentTrack != null && _currentTrack.clip != null)
                break;
        }

        _audioSource.clip = _currentTrack.clip;
        _audioSource.volume = volume;
        _audioSource.Play();

        Debug.Log($"[AlbaMod Music] Now playing: {_currentTrack.trackName} — {_currentTrack.artistName}");
    }

    void BuildShuffledQueue() {
        _queue.Clear();

        if (playlist != null) {
            // Standard playlist selection
            if (playlist.tracks != null) {
                foreach (int index in _enabledTracks) {
                    if (index >= 0 && index < playlist.tracks.Length &&
                        playlist.tracks[index] != null &&
                        playlist.tracks[index].clip != null)
                        _queue.Add(playlist.tracks[index]);
                }
            }

            // Lighthouse selection joins the same live queue
            if (playlist.lighthouseTracks != null) {
                foreach (int index in _enabledLighthouseTracks) {
                    if (index >= 0 && index < playlist.lighthouseTracks.Length &&
                        playlist.lighthouseTracks[index] != null &&
                        playlist.lighthouseTracks[index].clip != null)
                        _queue.Add(playlist.lighthouseTracks[index]);
                }
            }
        }

        // Fisher-Yates shuffle
        for (int i = _queue.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
        }
    }

    // ── Standard track selection ──────────────────────────────────────────────

    public bool IsTrackEnabled(int index) => _enabledTracks.Contains(index);

    public void SetTrackEnabled(int index, bool enabled) {
        if (playlist == null || playlist.tracks == null ||
            index < 0 || index >= playlist.tracks.Length)
            return;

        bool changed = enabled
            ? _enabledTracks.Add(index)
            : _enabledTracks.Remove(index);

        if (!changed) return;

        SaveSelection();
        RefreshPlaybackAfterSelectionChange();
    }

    public void EnableAllMain() {
        if (playlist == null || playlist.tracks == null) return;

        for (int i = 0; i < playlist.tracks.Length; i++)
            _enabledTracks.Add(i);

        SaveSelection();

        if (_isPlaying)
            BuildShuffledQueue();
    }

    public void DisableAllMain() {
        if (playlist == null || playlist.tracks == null) return;

        _enabledTracks.Clear();
        SaveSelection();
        RefreshPlaybackAfterSelectionChange();
    }

    // ── Lighthouse track selection ────────────────────────────────────────────

    public int LighthouseTrackCount =>
        playlist != null && playlist.lighthouseTracks != null
            ? playlist.lighthouseTracks.Length
            : 0;

    public bool IsLighthouseTrackEnabled(int index) =>
        _enabledLighthouseTracks.Contains(index);

    public void SetLighthouseTrackEnabled(int index, bool enabled) {
        if (playlist == null || playlist.lighthouseTracks == null ||
            index < 0 || index >= playlist.lighthouseTracks.Length)
            return;

        bool changed = enabled
            ? _enabledLighthouseTracks.Add(index)
            : _enabledLighthouseTracks.Remove(index);

        if (!changed) return;

        SaveLighthouse();
        RefreshPlaybackAfterSelectionChange();
    }

    public void EnableAllLighthouse() {
        if (playlist == null || playlist.lighthouseTracks == null) return;

        for (int i = 0; i < playlist.lighthouseTracks.Length; i++)
            _enabledLighthouseTracks.Add(i);

        SaveLighthouse();

        if (_isPlaying)
            BuildShuffledQueue();
        else if (HasSelection)
            StartPlayback();
    }

    public void DisableAllLighthouse() {
        if (playlist == null || playlist.lighthouseTracks == null) return;

        _enabledLighthouseTracks.Clear();
        SaveLighthouse();
        RefreshPlaybackAfterSelectionChange();
    }

    void RefreshPlaybackAfterSelectionChange() {
        if (!HasSelection) {
            if (_isPlaying)
                StopPlayback();
            return;
        }

        if (_isPlaying)
            BuildShuffledQueue();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    void SaveSelection() {
        // Save as comma-separated indices
        string saved = string.Join(",", _enabledTracks);
        PlayerPrefs.SetString(SaveKey, saved);
        PlayerPrefs.Save();
        Debug.Log($"[AlbaMod Music] Saved selection: {saved}");
    }

    void LoadSelection() {
        _enabledTracks.Clear();

        string saved = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var part in saved.Split(',')) {
            if (int.TryParse(part.Trim(), out int index)) {
                if (playlist != null && playlist.tracks != null &&
                    index >= 0 && index < playlist.tracks.Length)
                    _enabledTracks.Add(index);
            }
        }

        Debug.Log($"[AlbaMod Music] Loaded {_enabledTracks.Count} enabled tracks");
    }

    void SaveLighthouse() {
        string saved = string.Join(",", _enabledLighthouseTracks);
        PlayerPrefs.SetString(LighthouseSaveKey, saved);
        PlayerPrefs.Save();
        Debug.Log($"[AlbaMod Music] Saved lighthouse selection: {saved}");
    }

    void LoadLighthouse() {
        _enabledLighthouseTracks.Clear();

        string saved = PlayerPrefs.GetString(LighthouseSaveKey, "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var part in saved.Split(',')) {
            if (int.TryParse(part.Trim(), out int index)) {
                if (playlist != null && playlist.lighthouseTracks != null &&
                    index >= 0 && index < playlist.lighthouseTracks.Length)
                    _enabledLighthouseTracks.Add(index);
            }
        }

        Debug.Log($"[AlbaMod Music] Loaded {_enabledLighthouseTracks.Count} lighthouse tracks");
    }

    // ── Info ──────────────────────────────────────────────────────────────────

    public MusicTrack GetCurrentTrack() => _currentTrack;

    public void SetVolume(float val) {
        Volume = val;
    }
}
