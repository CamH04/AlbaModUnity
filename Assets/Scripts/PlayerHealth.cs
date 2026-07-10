using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class PlayerHealth : NetworkBehaviour {
    [Header("Settings")]
    public float maxHealth = 100f;
    public float respawnDelay = 3f;

    // Synced to all clients automatically
    private NetworkVariable<float> _health = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _isDead = false;

    // Hook into this from UI scripts to update health bar
    public event System.Action<float, float> OnHealthChanged; // current, max
    public event System.Action OnDied;
    public event System.Action OnRespawned;

    public float Health => _health.Value;
    public float MaxHealth => maxHealth;
    public bool IsDead => _isDead;

    public override void OnNetworkSpawn() {
        _health.OnValueChanged += HandleHealthChanged;

        // Trigger UI update on spawn so health bar initialises correctly
        if (IsOwner)
            OnHealthChanged?.Invoke(_health.Value, maxHealth);
    }

    public override void OnNetworkDespawn() {
        _health.OnValueChanged -= HandleHealthChanged;
    }

    void HandleHealthChanged(float previous, float current) {
        OnHealthChanged?.Invoke(current, maxHealth);

        if (current <= 0f && !_isDead)
            HandleDeath();
    }

    // Called on the server only — e.g. from a bullet hit
    public void TakeDamage(float amount) {
        if (!IsServer) return;
        if (_isDead) return;

        _health.Value = Mathf.Max(0f, _health.Value - amount);
    }

    // Called on the server only
    public void Heal(float amount) {
        if (!IsServer) return;
        _health.Value = Mathf.Min(maxHealth, _health.Value + amount);
    }

    void HandleDeath() {
        _isDead = true;
        OnDied?.Invoke();

        // Disable movement for owner
        if (IsOwner) {
            var pc = GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = false;
        }

        // Server handles respawn timer
        if (IsServer)
            StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay() {
        yield return new WaitForSeconds(respawnDelay);

        // Reset health on server (NetworkVariable syncs to all clients)
        _health.Value = maxHealth;

        // Teleport to a spawn point on server
        var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spawnPoints.Length > 0) {
            var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = point.transform.position;
            if (cc != null) cc.enabled = true;
        }

        RespawnClientRpc(transform.position);
    }

    [ClientRpc]
    void RespawnClientRpc(Vector3 spawnPosition) {
        _isDead = false;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = spawnPosition;
        if (cc != null) cc.enabled = true;

        if (IsOwner) {
            var pc = GetComponent<PlayerController>();
            if (pc != null) pc.enabled = true;
            var motor = GetComponent<PlayerMotor>();
            if (motor != null) motor.enabled = true;
            motor?.SetVelocity(Vector3.zero);

            // Re-lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        OnRespawned?.Invoke();
    }

    // Server resets health and teleports player to a spawn point
    [ServerRpc(RequireOwnership = false)]
    public void ResetHealthServerRpc() {
        _health.Value = maxHealth;
    }
}