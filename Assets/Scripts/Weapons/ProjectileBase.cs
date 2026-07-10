using Unity.Netcode;
using UnityEngine;

public abstract class ProjectileBase : NetworkBehaviour {
    [Header("Base Settings")]
    public float speed = 25f;
    public float lifetime = 8f;
    public LayerMask hitMask;

    protected ulong shooterClientId;
    protected float spawnTime;

    public virtual void Initialise(ulong shooterId) {
        shooterClientId = shooterId;
        spawnTime = Time.time;
    }

    protected virtual void Update() {
        if (IsServer) {
            if (Time.time - spawnTime > lifetime) {
                NetworkObject.Despawn();
                return;
            }
        }
        MoveProjectile();
    }

    protected virtual void MoveProjectile() {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    protected virtual void OnTriggerEnter(Collider other) {
        if (!IsServer) return;

        // Ignore triggers and the rocket itself
        if (other.isTrigger) return;

        // Ignore shooter for a short window after firing
        var otherHealth = other.GetComponentInParent<PlayerHealth>();
        if (otherHealth != null && otherHealth.OwnerClientId == shooterClientId
            && Time.time - spawnTime < 0.3f) return;

        OnHit(other, transform.position, -transform.forward);
    }

    protected abstract void OnHit(Collider other, Vector3 hitPoint, Vector3 hitNormal);

    protected PlayerHealth GetShooterHealth() {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
            if (client.ClientId == shooterClientId)
                return client.PlayerObject?.GetComponent<PlayerHealth>();
        }
        return null;
    }
}