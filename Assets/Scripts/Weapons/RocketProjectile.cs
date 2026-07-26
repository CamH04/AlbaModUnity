using Unity.Netcode;
using UnityEngine;

public class RocketProjectile : ProjectileBase {
    [Header("Rocket Settings")]
    public float directHitDamage = 75f;
    public float splashDamage = 45f;
    public float splashRadius = 6f;
    public float splashFalloff = 1f; // 1 = linear falloff, 0 = no falloff
    public float knockbackForce = 18f; // add this
    public float selfDamageMultiplier = 0.5f; // 50% self damage like TF2

    [Header("Effects")]
    public GameObject explosionEffectPrefab;

    private bool _hasExploded = false;

    public virtual void Initialise(ulong shooterId) {
        shooterClientId = shooterId;
        spawnTime = Time.time;

        // Apply *10 mode multipliers
        if (GameModeSettings.Instance != null && GameModeSettings.Instance.IsTenXMode.Value) {
            directHitDamage *= 10f;
            splashDamage *= 10f;
            knockbackForce *= 10f;
            speed *= 3f;   // 3x speed so rockets actually reach targets
            splashRadius *= 2f;   // bigger boom radius
        }
    }

    protected override void OnHit(Collider other, Vector3 hitPoint, Vector3 hitNormal) {
        if (_hasExploded) return;

        // Don't hit the shooter immediately after firing
        var hitHealth = other.GetComponentInParent<PlayerHealth>();
        if (hitHealth != null && hitHealth.OwnerClientId == shooterClientId &&
            Time.time - spawnTime < 0.15f) return;

        Explode(hitPoint, hitNormal, hitHealth);
    }

    void Explode(Vector3 point, Vector3 normal, PlayerHealth directHit) {
        _hasExploded = true;

        // Direct hit
        if (directHit != null && !directHit.IsDead) {
            directHit.TakeDamage(directHitDamage, shooterClientId, "Rocket Launcher");
            ApplyKnockback(directHit.gameObject, point, 1f);
        }

        // Splash — explicitly exclude shooter from direct hit AND from self-splash
        // unless you want self-damage (TF2 style does include self-splash)
        var colliders = Physics.OverlapSphere(point, splashRadius, hitMask);
        Debug.Log($"Overlap sphere found {colliders.Length} colliders");
        var alreadyHit = new System.Collections.Generic.HashSet<PlayerHealth>();

        foreach (var col in colliders) {
            var health = col.GetComponentInParent<PlayerHealth>();
            if (health == null || health.IsDead) continue;
            if (directHit != null && health == directHit) continue;
            if (!alreadyHit.Add(health)) continue; // skip if already processed this player

            float dist = Vector3.Distance(point, col.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / splashRadius);

            if (falloff > 0.01f) {
                float damage = splashDamage * falloff;
                if (health.OwnerClientId == shooterClientId)
                    damage *= selfDamageMultiplier;

                health.TakeDamage(damage * selfDamageMultiplier, shooterClientId, "Rocket Launcher");
                ApplyKnockback(health.gameObject, point, falloff);
            }
        }

        SpawnExplosionClientRpc(point, normal);
        NetworkObject.Despawn();
    }

    void ApplyKnockback(GameObject target, Vector3 explosionPoint, float falloff) {
        var motor = target.GetComponent<PlayerMotor>();
        if (motor == null) return;

        // Direction from explosion outward and slightly upward like TF2
        Vector3 dir = (target.transform.position - explosionPoint).normalized;
        dir.y = Mathf.Max(dir.y, 0.4f); // always has upward component
        dir.Normalize();

        Vector3 force = dir * knockbackForce * falloff;

        // Tell the target to apply knockback via ClientRpc
        var health = target.GetComponent<PlayerHealth>();
        if (health != null)
            ApplyKnockbackClientRpc(force, health.OwnerClientId);
    }

    [ClientRpc]
    void ApplyKnockbackClientRpc(Vector3 force, ulong targetClientId) {
        // Only apply on the machine that owns this player
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        // Find our local player
        foreach (var player in FindObjectsOfType<PlayerMotor>()) {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == targetClientId) {
                // Preserve momentum — add to existing velocity rather than replace it
                Vector3 newVel = player.Velocity + force;
                player.SetVelocity(newVel);
                break;
            }
        }
    }

    [ClientRpc]
    void SpawnExplosionClientRpc(Vector3 position, Vector3 normal) {
        if (explosionEffectPrefab != null) {
            var fx = Instantiate(explosionEffectPrefab, position,
                Quaternion.LookRotation(normal));
            Destroy(fx, 3f);
        }
    }
}