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
        // Only runs on server — called by FireServerRpc in WeaponBase
        FireHitscan(origin, direction);
    }

    // Override WeaponBase's ServerRpc to remove ownership requirement
    // so clients can call it on a server-owned weapon object
    [ServerRpc(RequireOwnership = false)]
    protected override void FireServerRpc(Vector3 position, Vector3 direction) {
        SpawnProjectile(position, direction);
    }

    protected virtual void FireHitscan(Vector3 origin, Vector3 direction) {
        if (!IsServer) return;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
            return;

        PlayerHealth health = hit.collider.GetComponentInParent<PlayerHealth>();

        if (health != null && !health.IsDead)
            OnPlayerHit(health, hit);

        SpawnImpactClientRpc(hit.point, hit.normal);
        OnHit(hit);
    }

    protected virtual void OnPlayerHit(PlayerHealth player, RaycastHit hit) {
        player.TakeDamage(damage);
    }

    protected virtual void OnHit(RaycastHit hit) { }

    [ClientRpc]
    void SpawnImpactClientRpc(Vector3 position, Vector3 normal) {
        if (impactEffect == null) return;

        var fx = Instantiate(impactEffect, position,
            Quaternion.LookRotation(normal));
        Destroy(fx, 2f);
    }
}