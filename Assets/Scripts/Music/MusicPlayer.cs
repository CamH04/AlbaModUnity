using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MusicPlayer : MonoBehaviour {
    public static MusicPlayer Instance;

    [Header("References")]
    public MusicPlaylist playlist;

    [Header("Settings")]
    public float volume = 0.5f;

    private AudioSource _audioSource;
    private List<int> _queue = new List<int>();
    private int _currentTrackIndex = -1;
    private bool _isPlaying = false;

    // Persisted selection — track indices the player has enabled
    private HashSet<int> _enabledTracks = new HashSet<int>();
    private const string SaveKey = "AlbaMod_EnabledTracks";

    // Secret "Lighthouse" playlist — tracks moved out of the main playlist
    private HashSet<int> _lighthouseTracks = new HashSet<int>();
    private const string LighthouseKey = "AlbaMod_LighthouseTracks";

    public int CurrentTrackIndex => _currentTrackIndex;
    public bool IsPlaying => _isPlaying;

    public AudioSource AudioSource => _audioSource;

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

        if (_enabledTracks.Count > 0)
            StartPlayback();
    }

    void Update() {
        // Advance to next track when current finishes
        if (_isPlaying && _audioSource != null && !_audioSource.isPlaying)
            PlayNext();
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    public void StartPlayback() {
        if (_enabledTracks.Count == 0) return;
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
        if (_queue.Count == 0)
            BuildShuffledQueue();

        if (_queue.Count == 0) return;

        _currentTrackIndex = _queue[0];
        _queue.RemoveAt(0);

        var track = playlist.tracks[_currentTrackIndex];
        if (track.clip == null) return;

        _audioSource.clip = track.clip;
        _audioSource.volume = volume;
        _audioSource.Play();

        Debug.Log($"[AlbaMod Music] Now playing: {track.trackName} — {track.artistName}");
    }

    void BuildShuffledQueue() {
        _queue = _enabledTracks.ToList();

        // Fisher-Yates shuffle
        for (int i = _queue.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
        }
    }

    // ── Track selection ───────────────────────────────────────────────────────

    public bool IsTrackEnabled(int index) => _enabledTracks.Contains(index);

    public void SetTrackEnabled(int index, bool enabled) {
        if (enabled)
            _enabledTracks.Add(index);
        else
            _enabledTracks.Remove(index);

        SaveSelection();

        // Rebuild queue with new selection
        if (_isPlaying)
            BuildShuffledQueue();
    }

    public void EnableAllMain() {
        if (playlist == null) return;

        for (int i = 0; i < playlist.tracks.Length; i++)
            if (!IsLighthouseTrack(i))
                _enabledTracks.Add(i);

        SaveSelection();
        BuildShuffledQueue();
    }

    public void DisableAllMain() {
        if (playlist == null) return;

        for (int i = 0; i < playlist.tracks.Length; i++)
            if (!IsLighthouseTrack(i))
                _enabledTracks.Remove(i);

        SaveSelection();

        if (_enabledTracks.Count == 0)
            StopPlayback();
        else if (_isPlaying)
            BuildShuffledQueue();
    }

    // ── Lighthouse playlist ───────────────────────────────────────────────────

    public bool IsLighthouseTrack(int index) => _lighthouseTracks.Contains(index);

    public void SetLighthouseTrack(int index, bool inLighthouse) {
        if (playlist == null || index < 0 || index >= playlist.tracks.Length) return;

        bool changed = inLighthouse
            ? _lighthouseTracks.Add(index)
            : _lighthouseTracks.Remove(index);

        if (!changed) return;

        SaveLighthouse();

        if (_isPlaying)
            BuildShuffledQueue();
    }

    public int[] GetLighthouseTrackIndices() {
        var indices = _lighthouseTracks.ToList();
        indices.Sort();
        return indices.ToArray();
    }

    public void EnableAllLighthouse() {
        foreach (int index in _lighthouseTracks)
            _enabledTracks.Add(index);

        SaveSelection();

        if (_enabledTracks.Count > 0 && !_isPlaying)
            StartPlayback();
        else if (_isPlaying)
            BuildShuffledQueue();
    }

    public void DisableAllLighthouse() {
        foreach (int index in _lighthouseTracks)
            _enabledTracks.Remove(index);

        SaveSelection();

        if (_enabledTracks.Count == 0)
            StopPlayback();
        else if (_isPlaying)
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
                if (playlist != null && index >= 0 && index < playlist.tracks.Length)
                    _enabledTracks.Add(index);
            }
        }

        Debug.Log($"[AlbaMod Music] Loaded {_enabledTracks.Count} enabled tracks");
    }

    void SaveLighthouse() {
        string saved = string.Join(",", _lighthouseTracks);
        PlayerPrefs.SetString(LighthouseKey, saved);
        PlayerPrefs.Save();
        Debug.Log($"[AlbaMod Music] Saved lighthouse tracks: {saved}");
    }

    void LoadLighthouse() {
        _lighthouseTracks.Clear();

        string saved = PlayerPrefs.GetString(LighthouseKey, "");
        if (string.IsNullOrEmpty(saved)) return;

        foreach (var part in saved.Split(',')) {
            if (int.TryParse(part.Trim(), out int index)) {
                if (playlist != null && index >= 0 && index < playlist.tracks.Length)
                    _lighthouseTracks.Add(index);
            }
        }

        Debug.Log($"[AlbaMod Music] Loaded {_lighthouseTracks.Count} lighthouse tracks");
    }

    // ── Info ──────────────────────────────────────────────────────────────────

    public MusicTrack GetCurrentTrack() {
        if (_currentTrackIndex < 0 || playlist == null) return null;
        if (_currentTrackIndex >= playlist.tracks.Length) return null;
        return playlist.tracks[_currentTrackIndex];
    }

    public void SetVolume(float val) {
        Volume = val;
    }
}