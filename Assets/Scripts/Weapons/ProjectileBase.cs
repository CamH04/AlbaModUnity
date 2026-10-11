using Unity.Netcode;
using UnityEngine;

public abstract class ProjectileBase : NetworkBehaviour {
    [Header("Base Settings")]
    public float speed = 25f;
    public float lifetime = 8f;
    public LayerMask hitMask;
    [Tooltip("How far the projectile must travel before its own body can damage the shooter. The fire origin sits inside/against the shooter's collider at steep look angles, so this stops the rocket ever exploding on the shooter's own body at spawn, while still allowing explosion splash/knockback (rocket jumping).")]
    public float armDistance = 3f;

    protected ulong shooterClientId;
    protected float spawnTime;
    protected Vector3 spawnPosition;

    public virtual void Initialise(ulong shooterId) {
        shooterClientId = shooterId;
        spawnTime = Time.time;
        spawnPosition = transform.position;
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

        // Ignore the shooter's own body until the projectile has travelled a
        // safe distance. The fire origin is the camera, which ends up inside
        // or against the shooter's collider at steep look angles — the rocket
        // spawns there, so it must be armed before it can deal any damage to
        // the shooter's own body. Explosion splash/knockback (rocket jumping)
        // is handled separately in Explode() and is NOT affected by this.
        var otherHealth = other.GetComponentInParent<PlayerHealth>();
        if (otherHealth != null && otherHealth.OwnerClientId == shooterClientId) {
            if (Vector3.Distance(transform.position, spawnPosition) < armDistance)
                return;
        }

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