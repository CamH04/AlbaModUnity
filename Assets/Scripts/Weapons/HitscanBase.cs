using Unity.Netcode;
using UnityEngine;

public abstract class HitscanBase : WeaponBase {
    [Header("Hitscan")]
    public float damage = 50f;
    public float range = 200f;
    public LayerMask hitMask;

    [Header("Effects")]
    public GameObject impactEffect;

    protected override void SpawnProjectile(Vector3 origin, Vector3 direction) {
        FireHitscan(origin, direction);
    }

    protected virtual void FireHitscan(Vector3 origin, Vector3 direction) {
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
            return;

        PlayerHealth health = hit.collider.GetComponentInParent<PlayerHealth>();

        if (health != null && !health.IsDead) {
            OnPlayerHit(health, hit);
        }

        SpawnImpactClientRpc(hit.point, hit.normal);

        OnHit(hit);
    }

    protected virtual void OnPlayerHit(PlayerHealth player, RaycastHit hit) {
        player.TakeDamage(damage);
    }

    protected virtual void OnHit(RaycastHit hit) {

    }

    [ClientRpc]
    void SpawnImpactClientRpc(Vector3 position, Vector3 normal) {
        if (impactEffect == null)
            return;

        GameObject fx = Instantiate(
            impactEffect,
            position,
            Quaternion.LookRotation(normal));

        Destroy(fx, 2f);
    }
}