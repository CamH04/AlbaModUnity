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

    [Header("Status Image")]
    public Image statusImage;
    public Sprite defaultSprite;
    public Sprite damagedSprite;
    public Sprite grappleSprite;
    public Sprite stimSprite;
    public Sprite deadSprite;

    [Header("Colors")]
    public Color healthHighColor = new Color(0.2f, 0.85f, 0.3f);
    public Color healthMidColor = new Color(0.95f, 0.75f, 0.1f);
    public Color healthLowColor = new Color(0.9f, 0.15f, 0.15f);

    private PlayerHealth _health;
    private PlayerMotor _motor;
    private Abilities _abilities;
    private float _currentSpread;
    private float _respawnTimer;
    private bool _isDead;

    // Damage flash state
    private float _damagedTimer;
    private const float DamagedDuration = 0.3f;

    void Start() {
        StartCoroutine(FindLocalPlayer());
    }

    System.Collections.IEnumerator FindLocalPlayer() {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            yield return null;

        PlayerHealth found = null;
        while (found == null) {
            foreach (var ph in FindObjectsOfType<PlayerHealth>())
                if (ph.IsOwner) { found = ph; break; }
            yield return null;
        }

        Bind(found);
    }

    void Bind(PlayerHealth health) {
        _health = health;
        _motor = health.GetComponent<PlayerMotor>();
        _abilities = health.GetComponent<Abilities>();

        _health.OnHealthChanged += UpdateHealthBar;
        _health.OnHealthChanged += OnHealthChangedForStatus;
        _health.OnDied += HandleDeath;
        _health.OnRespawned += HandleRespawn;

        UpdateHealthBar(_health.Health, _health.MaxHealth);
        if (deathPanel != null) deathPanel.SetActive(false);
        UpdateStatusImage();
    }

    void OnDestroy() {
        if (_health == null) return;
        _health.OnHealthChanged -= UpdateHealthBar;
        _health.OnHealthChanged -= OnHealthChangedForStatus;
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

    void OnHealthChangedForStatus(float current, float max) {
        // Trigger damaged flash whenever health goes down
        _damagedTimer = DamagedDuration;
    }

    // ?? Status image ??????????????????????????????????????????????????????????

    void UpdateStatusImage() {
        if (statusImage == null) return;

        Sprite target = defaultSprite;

        if (_isDead)
            target = deadSprite;
        else if (_damagedTimer > 0f)
            target = damagedSprite;
        else if (_abilities != null && _abilities.IsGrappling)
            target = grappleSprite;
        else if (_abilities != null && _abilities.IsStimmed)
            target = stimSprite;

        if (target != null)
            statusImage.sprite = target;
    }

    // ?? Death / respawn ???????????????????????????????????????????????????????

    void HandleDeath() {
        _isDead = true;
        _respawnTimer = _health.respawnDelay;
        if (deathPanel != null) deathPanel.SetActive(true);
        SetCrosshairVisible(false);
        UpdateStatusImage();
    }

    void HandleRespawn() {
        _isDead = false;
        if (deathPanel != null) deathPanel.SetActive(false);
        SetCrosshairVisible(true);
        UpdateStatusImage();
    }

    // ?? Update ????????????????????????????????????????????????????????????????

    void Update() {
        if (_isDead) {
            _respawnTimer -= Time.deltaTime;
            if (respawnText != null)
                respawnText.text = $"Respawning in {Mathf.CeilToInt(Mathf.Max(0, _respawnTimer))}...";
            return;
        }

        if (_damagedTimer > 0f)
            _damagedTimer -= Time.deltaTime;

        UpdateCrosshair();
        UpdateStatusImage();
    }

    // ?? Crosshair ?????????????????????????????????????????????????????????????

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