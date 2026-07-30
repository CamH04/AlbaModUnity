using Unity.Netcode;
using UnityEngine;

public abstract class HitscanBase : WeaponBase {
    [Header("Hitscan")]
    public float damage = 50f;
    public float range = 200f;
    public LayerMask hitMask;

    [Header("Effects")]
    public GameObject impactEffect;

    [Header("Hit Sound")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitVolume = 1f;

    private AudioSource _audioSource;

    protected override void SpawnProjectile(Vector3 origin, Vector3 direction) {
        FireHitscan(origin, direction);
    }

    [ServerRpc(RequireOwnership = false)]
    protected override void FireServerRpc(Vector3 position, Vector3 direction) {
        SpawnProjectile(position, direction);
    }

    protected virtual void FireHitscan(Vector3 origin, Vector3 direction) {
        if (!IsServer) return;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
            return;

        PlayerHealth health = hit.collider.GetComponentInParent<PlayerHealth>();

        if (health != null && !health.IsDead) {
            float finalDamage = damage;
            if (GameModeSettings.Instance != null && GameModeSettings.Instance.IsTenXMode.Value)
                finalDamage *= 10f;

            health.TakeDamage(finalDamage, OwnerClientId, weaponName);
            OnPlayerHit(health, hit);

            // Send hit sound to the shooter only
            PlayHitSoundClientRpc(new ClientRpcParams {
                Send = new ClientRpcSendParams {
                    TargetClientIds = new[] { OwnerClientId }
                }
            });
        }

        SpawnImpactClientRpc(hit.point, hit.normal);
        OnHit(hit);
    }

    protected virtual void OnPlayerHit(PlayerHealth player, RaycastHit hit) {
        player.TakeDamage(damage);
    }

    protected virtual void OnHit(RaycastHit hit) { }

    [ClientRpc]
    void PlayHitSoundClientRpc(ClientRpcParams rpcParams = default) {
        if (hitSound == null) return;

        // Lazy init AudioSource
        if (_audioSource == null) {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.PlayOneShot(hitSound, hitVolume);
    }

    [ClientRpc]
    void SpawnImpactClientRpc(Vector3 position, Vector3 normal) {
        if (impactEffect == null) return;

        var fx = Instantiate(impactEffect, position, Quaternion.LookRotation(normal));
        Destroy(fx, 2f);
    }
}