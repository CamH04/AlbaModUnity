using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatsMenuUI : MonoBehaviour {
    [Header("Panel")]
    public GameObject statsPanel;

    [Header("Kill Stats")]
    public TextMeshProUGUI totalKillsText;
    public TextMeshProUGUI rocketKillsText;
    public TextMeshProUGUI sniperKillsText;
    public TextMeshProUGUI chainKillsText;

    [Header("Ability Stats")]
    public TextMeshProUGUI stimTimeText;
    public TextMeshProUGUI grappleDistanceText;

    void Start() {
        if (statsPanel != null)
            statsPanel.SetActive(false);
    }

    public void OpenStats() {
        if (statsPanel != null)
            statsPanel.SetActive(true);

        RefreshStats();
    }

    public void CloseStats() {
        if (statsPanel != null)
            statsPanel.SetActive(false);
    }

    void RefreshStats() {
        if (LocalStatsTracker.Instance == null) {
            Debug.LogWarning("StatsMenuUI: LocalStatsTracker not found!");
            return;
        }

        var s = LocalStatsTracker.Instance.GetStats();

        if (totalKillsText != null) totalKillsText.text = s.totalKills.ToString();
        if (rocketKillsText != null) rocketKillsText.text = s.rocketLauncherKills.ToString();
        if (sniperKillsText != null) sniperKillsText.text = s.sniperKills.ToString();
        if (chainKillsText != null) chainKillsText.text = s.chainKills.ToString();

        if (stimTimeText != null)
            stimTimeText.text = FormatTime(s.totalStimTimeSeconds);

        if (grappleDistanceText != null)
            grappleDistanceText.text = $"{s.totalGrappleDistanceMeters:F0}m";
    }

    string FormatTime(float seconds) {
        var t = System.TimeSpan.FromSeconds(seconds);
        return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
    }
}