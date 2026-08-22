using UnityEngine;
using System;
using System.IO;
using Unity.Netcode;

[Serializable]
public class PlayerStats {
    // Kills
    public int totalKills;
    public int rocketLauncherKills;
    public int sniperKills;
    public int chainKills;

    // Abilities
    public float totalStimTimeSeconds;
    public float totalGrappleDistanceMeters;

    // Session tracking (not saved)
    [NonSerialized] public float sessionStimTime;
    [NonSerialized] public float sessionGrappleDistance;
}

public class LocalStatsTracker : MonoBehaviour {
    public static LocalStatsTracker Instance;

    private PlayerStats _stats = new PlayerStats();
    private string _savePath;

    // Stim tracking
    private bool _isStimActive = false;
    private float _stimStartTime;

    // Grapple tracking
    private bool _isGrappling = false;
    private Vector3 _lastGrapplePosition;
    private Transform _playerTransform;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _savePath = Path.Combine(Application.persistentDataPath, "albaMod_stats.json");
            LoadStats();
        }
        else Destroy(gameObject);
    }

    void Update() {
        TrackStimTime();
        TrackGrappleDistance();
    }

    // ── Kill tracking ─────────────────────────────────────────────────────────

    public void RegisterKill(string weaponName) {
        _stats.totalKills++;

        switch (weaponName) {
            case "Rocket Launcher": _stats.rocketLauncherKills++; break;
            case "HAMR": _stats.sniperKills++; break;
            case "Abduct Chain": _stats.chainKills++; break;
        }

        SaveStats();
        Debug.Log($"[AlbaMod Stats] Kill registered with {weaponName} | Total: {_stats.totalKills}");
    }

    // ── Stim tracking ─────────────────────────────────────────────────────────

    public void OnStimStarted() {
        if (_isStimActive) return;
        _isStimActive = true;
        _stimStartTime = Time.time;
    }

    public void OnStimEnded() {
        if (!_isStimActive) return;
        _isStimActive = false;

        float duration = Time.time - _stimStartTime;
        _stats.totalStimTimeSeconds += duration;
        _stats.sessionStimTime += duration;

        SaveStats();
        Debug.Log($"[AlbaMod Stats] Stim ended — session: {_stats.sessionStimTime:F1}s | total: {_stats.totalStimTimeSeconds:F1}s");
    }

    void TrackStimTime() {
        // Accumulates live time while stim is active for display purposes
        // Actual save happens in OnStimEnded
    }

    // ── Grapple tracking ──────────────────────────────────────────────────────

    public void OnGrappleStarted(Transform playerTransform) {
        if (_isGrappling) return;
        _isGrappling = true;
        _playerTransform = playerTransform;
        _lastGrapplePosition = playerTransform.position;
    }

    public void OnGrappleEnded() {
        if (!_isGrappling) return;
        _isGrappling = false;
        _playerTransform = null;
    }

    void TrackGrappleDistance() {
        if (!_isGrappling || _playerTransform == null) return;

        float delta = Vector3.Distance(_playerTransform.position, _lastGrapplePosition);
        _stats.totalGrappleDistanceMeters += delta;
        _stats.sessionGrappleDistance += delta;
        _lastGrapplePosition = _playerTransform.position;
    }

    // ── Save / Load ───────────────────────────────────────────────────────────

    public void SaveStats() {
        try {
            string json = JsonUtility.ToJson(_stats, true);
            File.WriteAllText(_savePath, json);
        }
        catch (Exception e) {
            Debug.LogError($"[AlbaMod Stats] Failed to save: {e.Message}");
        }
    }

    public void LoadStats() {
        try {
            if (File.Exists(_savePath)) {
                string json = File.ReadAllText(_savePath);
                _stats = JsonUtility.FromJson<PlayerStats>(json);
                Debug.Log($"[AlbaMod Stats] Loaded stats from {_savePath}");
            }
            else {
                _stats = new PlayerStats();
                Debug.Log("[AlbaMod Stats] No save file found — starting fresh");
            }
        }
        catch (Exception e) {
            Debug.LogError($"[AlbaMod Stats] Failed to load: {e.Message}");
            _stats = new PlayerStats();
        }
    }

    public PlayerStats GetStats() => _stats;

    // ── Formatted summary for debug or UI ────────────────────────────────────

    public string GetStatsSummary() {
        var s = _stats;
        return
            $"=== AlbaMod Stats ===\n" +
            $"Total Kills:          {s.totalKills}\n" +
            $"  Rocket Launcher:    {s.rocketLauncherKills}\n" +
            $"  HAMR (Sniper):      {s.sniperKills}\n" +
            $"  Abduct Chain:       {s.chainKills}\n" +
            $"Stim Time:            {FormatTime(s.totalStimTimeSeconds)}\n" +
            $"Grapple Distance:     {s.totalGrappleDistanceMeters:F0}m\n" +
            $"=====================";
    }

    string FormatTime(float seconds) {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
    }
}