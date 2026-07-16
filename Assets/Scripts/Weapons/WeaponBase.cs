using Unity.Netcode;
using UnityEngine;

public abstract class WeaponBase : NetworkBehaviour {
    [Header("Base Weapon Settings")]
    public string weaponName = "Weapon";
    public float fireRate = 2f;      // shots per second
    public int maxAmmo = 200000;
    public int currentAmmo;
    public float reloadTime = 1.5f;

    [Header("References")]
    public Transform muzzle;         // where projectile spawns

    protected float _nextFireTime;
    protected bool _isReloading;
    protected PlayerController _playerController;

    protected Camera _playerCamera;

    public virtual void SetCamera(Camera cam) {
        _playerCamera = cam;
    }

    public bool CanFire => !_isReloading
                        && currentAmmo > 0
                        && Time.time >= _nextFireTime
                        && !_isReloading;

    // The transform we copy position/rotation from every frame. We deliberately
    // do NOT use transform.SetParent() to attach the weapon to this. Netcode
    // hooks Unity's OnTransformParentChanged callback on every spawned
    // NetworkObject and THROWS ("Invalid parenting, NetworkObject moved under
    // a non-NetworkObject parent") if you parent it under a transform that
    // doesn't itself have a NetworkObject component — which weaponHolder
    // (a hand/camera socket) never will. So real Unity parenting is off the
    // table here entirely; instead we just mirror the holder's world
    // position/rotation onto the weapon each frame, which achieves the same
    // visual result without touching transform.parent at all.
    private Transform _followTarget;

    public override void OnNetworkSpawn() {
        currentAmmo = maxAmmo;

        // We delay by one frame because WeaponSpawner spawns this weapon
        // from INSIDE the player object's own OnNetworkSpawn, which itself
        // runs INSIDE NetworkObject.SpawnAsPlayerObject — i.e. we are nested
        // in the same call stack as the player object's spawn. Netcode
        // hasn't finished registering "this object is client X's player
        // object" yet at that point, so looking it up immediately fails.
        // Waiting a frame guarantees that registration has completed.
        StartCoroutine(ResolveFollowTargetDelayed());

        // NOTE: we no longer disable this behaviour for non-owners.
        // LateUpdate() below needs to keep running on every client so the
        // weapon visually follows the holder everywhere, not just for the
        // owner. HandleInput() already guards on IsOwner itself.
    }

    System.Collections.IEnumerator ResolveFollowTargetDelayed() {
        yield return null; // wait one frame

        ResolveFollowTarget();
    }

    void ResolveFollowTarget() {
        // IMPORTANT: NetworkManager.ConnectedClients is only populated on the
        // server/host — it's empty on pure clients. GetPlayerNetworkObject()
        // reads from the locally-known spawned objects list instead, so it
        // works identically on server, host, and clients.
        var playerObject = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId)
            : null;

        if (playerObject == null) {
            Debug.LogError($"WeaponBase: could not find PlayerObject for owner {OwnerClientId}, weapon will not follow.");
            return;
        }

        _playerController = playerObject.GetComponent<PlayerController>();

        // Resolve the camera locally on THIS client too, rather than relying
        // solely on WeaponSpawner's SetCamera() call. That call only runs on
        // the server's own local instance (it's a plain method call, not an
        // RPC), so it silently never reaches a real remote client's copy of
        // the weapon — it only appeared to work when testing as host, since
        // host runs server + client 0 in the same process.
        if (_playerCamera == null)
            _playerCamera = playerObject.GetComponentInChildren<Camera>(true);

        var spawner = playerObject.GetComponentInChildren<WeaponSpawner>();
        if (spawner == null || spawner.weaponHolder == null) {
            Debug.LogError("WeaponBase: WeaponSpawner or weaponHolder missing on owner's player object.");
            return;
        }

        _followTarget = spawner.weaponHolder;

        // Snap immediately so there's no one-frame pop at the origin.
        transform.position = _followTarget.position;
        transform.rotation = _followTarget.rotation;
    }

    protected virtual void Update() {
        if (!IsOwner) return;
        HandleInput();
    }

    protected virtual void LateUpdate() {
        // Runs for every client (owner and non-owners) so everyone sees the
        // weapon tracking the holder. Plain world-space copy — cheap, and
        // safe since we removed NetworkTransform from the weapon prefab.
        if (_followTarget == null) return;

        transform.position = _followTarget.position;

        // Rotate with the CAMERA, not the holder. weaponHolder is typically
        // a hand/body socket that only inherits yaw (turning left/right) —
        // pitch (looking up/down) is applied separately to cameraHolder in
        // PlayerController.LateUpdate(). Using the camera's rotation here
        // means the weapon correctly follows full look direction, while
        // position still comes from the holder so it sits in-hand rather
        // than snapping exactly to the camera's origin.
        transform.rotation = _playerCamera != null
            ? _playerCamera.transform.rotation
            : _followTarget.rotation;
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