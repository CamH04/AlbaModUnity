using Unity.Netcode;
using UnityEngine;

public abstract class WeaponBase : NetworkBehaviour {
    [Header("Base Weapon Settings")]
    public string weaponName = "Weapon";
    public float fireRate = 1f;      // shots per second
    public int maxAmmo = 20;
    public int currentAmmo;
    public float reloadTime = 1.5f;

    [Header("References")]
    public Transform muzzle;         // where projectile spawns

    protected float _nextFireTime;
    protected bool _isReloading;
    protected PlayerController _playerController;

    public virtual void SetCamera(Camera cam) { }

    public bool CanFire => !_isReloading
                        && currentAmmo > 0
                        && Time.time >= _nextFireTime
                        && !_isReloading;

    public override void OnNetworkSpawn() {
        currentAmmo = maxAmmo;
        _playerController = GetComponentInParent<PlayerController>();

        if (!IsOwner) enabled = false;
    }

    protected virtual void Update() {
        if (!IsOwner) return;
        HandleInput();
    }

    protected abstract void HandleInput();

    protected virtual void Fire() {
        if (!CanFire) return;

        _nextFireTime = Time.time + 1f / fireRate;
        currentAmmo--;

        FireServerRpc(muzzle.position, muzzle.forward);

        if (currentAmmo <= 0)
            StartCoroutine(Reload());
    }

    [ServerRpc]
    protected virtual void FireServerRpc(Vector3 position, Vector3 direction) {
        SpawnProjectile(position, direction);
    }

    protected abstract void SpawnProjectile(Vector3 position, Vector3 direction);

    protected System.Collections.IEnumerator Reload() {
        _isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = maxAmmo;
        _isReloading = false;
    }
}