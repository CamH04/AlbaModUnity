using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour {
    [Header("Health Bar")]
    public Image healthBarFill;
    public TextMeshProUGUI healthText;

    [Header("Crosshair")]
    public Image crosshairDot;
    public Image crosshairTop;
    public Image crosshairBottom;
    public Image crosshairLeft;
    public Image crosshairRight;

    [Header("Crosshair Settings")]
    public float crosshairBaseSpread = 10f;
    public float crosshairSpreadPerSpeed = 0.05f;
    public float crosshairLerpSpeed = 8f;

    [Header("Death Screen")]
    public GameObject deathPanel;
    public TextMeshProUGUI respawnText;

    [Header("Colors")]
    public Color healthHighColor = new Color(0.2f, 0.85f, 0.3f);
    public Color healthMidColor = new Color(0.95f, 0.75f, 0.1f);
    public Color healthLowColor = new Color(0.9f, 0.15f, 0.15f);

    private PlayerHealth _health;
    private PlayerMotor _motor;
    private float _currentSpread;
    private float _respawnTimer;
    private bool _isDead;

    void Start() {
        // HUD only activates for the local owner — find our own player
        StartCoroutine(FindLocalPlayer());
    }

    System.Collections.IEnumerator FindLocalPlayer() {
        // Wait until NetworkManager is ready and our player is spawned
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            yield return null;

        PlayerHealth found = null;
        while (found == null) {
            foreach (var ph in FindObjectsOfType<PlayerHealth>()) {
                if (ph.IsOwner) { found = ph; break; }
            }
            yield return null;
        }

        Bind(found);
    }

    void Bind(PlayerHealth health) {
        _health = health;
        _motor = health.GetComponent<PlayerMotor>();

        _health.OnHealthChanged += UpdateHealthBar;
        _health.OnDied += HandleDeath;
        _health.OnRespawned += HandleRespawn;

        // Initialise bar
        UpdateHealthBar(_health.Health, _health.MaxHealth);
        if (deathPanel != null) deathPanel.SetActive(false);
    }

    void OnDestroy() {
        if (_health == null) return;
        _health.OnHealthChanged -= UpdateHealthBar;
        _health.OnDied -= HandleDeath;
        _health.OnRespawned -= HandleRespawn;
    }

    // ?? Health bar ????????????????????????????????????????????????????????????

    void UpdateHealthBar(float current, float max) {
        float pct = Mathf.Clamp01(current / max);

        if (healthBarFill != null) {
            healthBarFill.fillAmount = pct;
            healthBarFill.color = pct > 0.6f ? healthHighColor
                                : pct > 0.3f ? Color.Lerp(healthMidColor, healthHighColor, (pct - 0.3f) / 0.3f)
                                             : Color.Lerp(healthLowColor, healthMidColor, pct / 0.3f);
        }

        if (healthText != null)
            healthText.text = Mathf.CeilToInt(current).ToString();
    }

    // ?? Death / respawn ???????????????????????????????????????????????????????

    void HandleDeath() {
        _isDead = true;
        _respawnTimer = _health.GetComponent<PlayerHealth>() != null
            ? FindObjectOfType<PlayerHealth>().respawnDelay
            : 3f;

        if (deathPanel != null) deathPanel.SetActive(true);
        SetCrosshairVisible(false);
    }

    void HandleRespawn() {
        _isDead = false;
        if (deathPanel != null) deathPanel.SetActive(false);
        SetCrosshairVisible(true);
    }

    // ?? Crosshair ?????????????????????????????????????????????????????????????

    void Update() {
        if (_isDead) {
            // Count down respawn text
            _respawnTimer -= Time.deltaTime;
            if (respawnText != null)
                respawnText.text = $"Respawning in {Mathf.CeilToInt(Mathf.Max(0, _respawnTimer))}...";
            return;
        }

        UpdateCrosshair();
    }

    void UpdateCrosshair() {
        if (_motor == null) return;

        float speed = new Vector3(_motor.Velocity.x, 0f, _motor.Velocity.z).magnitude;
        float targetSpread = crosshairBaseSpread + speed * crosshairSpreadPerSpeed;

        _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, Time.deltaTime * crosshairLerpSpeed);

        if (crosshairTop != null) crosshairTop.rectTransform.anchoredPosition = new Vector2(0, _currentSpread);
        if (crosshairBottom != null) crosshairBottom.rectTransform.anchoredPosition = new Vector2(0, -_currentSpread);
        if (crosshairLeft != null) crosshairLeft.rectTransform.anchoredPosition = new Vector2(-_currentSpread, 0);
        if (crosshairRight != null) crosshairRight.rectTransform.anchoredPosition = new Vector2(_currentSpread, 0);
    }

    void SetCrosshairVisible(bool visible) {
        if (crosshairDot != null) crosshairDot.gameObject.SetActive(visible);
        if (crosshairTop != null) crosshairTop.gameObject.SetActive(visible);
        if (crosshairBottom != null) crosshairBottom.gameObject.SetActive(visible);
        if (crosshairLeft != null) crosshairLeft.gameObject.SetActive(visible);
        if (crosshairRight != null) crosshairRight.gameObject.SetActive(visible);
    }
}