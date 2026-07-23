using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class AbductChain : WeaponBase {
    [Header("Abduct Settings")]
    public float range = 20f;
    public float damage = 500f;
    public float grappleSpeed = 45f;
    public float grappleStopDistance = 0.75f;
    public float castSpeed = 40f;
    public float cooldown = 8f;

    [Header("Layers")]
    public LayerMask playerLayerMask;
    public LayerMask pullableLayerMask;

    [Header("Visual")]
    public GameObject chainSegmentPrefab;
    public int chainSegments = 12;
    public float chainWidth = 0.06f;
    public Color chainColor = new Color(0.5f, 0.9f, 1f, 1f);

    [Header("Hook")]
    public GameObject hookPrefab;

    [Header("Impact")]
    public GameObject impactEffectPrefab;

    private bool _isCasting = false;
    private float _lastFireTime = -999f;

    private readonly List<GameObject> _chainVisuals = new List<GameObject>();
    private GameObject _hookVisual;

    private PlayerMotor _ownerMotor;

    public override void SetCamera(Camera cam) => _playerCamera = cam;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if (IsOwner)
            _ownerMotor = ResolveOwnerMotor();
    }

    // Weapons aren't parented under the player, so GetComponent/GetComponentInParent
    // can't find the motor. Resolve it through the owning client's PlayerObject instead,
    // which Netcode tracks for us regardless of where the weapon is spawned.
    private PlayerMotor ResolveOwnerMotor() {
        if (NetworkManager.Singleton == null) return null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerClientId, out var client))
            return null;

        if (client.PlayerObject == null) return null;

        return client.PlayerObject.GetComponent<PlayerMotor>();
    }

    protected override void HandleInput() {
        if (!IsOwner) return;
        if (Time.time - _lastFireTime < cooldown) return;

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            !_isCasting) {
            Fire();
        }
    }

    protected override void Fire() {
        if (_isCasting) return;
        if (_playerCamera == null) return;

        _lastFireTime = Time.time;

        Vector3 origin = _playerCamera.transform.position;
        Vector3 direction = _playerCamera.transform.forward;

        FireServerRpc(origin, direction);
        StartCoroutine(CastChainVisual(origin, direction));
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 origin, Vector3 direction) {
        direction.Normalize();

        LayerMask combined = playerLayerMask | pullableLayerMask;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, combined);

        if (hits == null || hits.Length == 0) {
            MissClientRpc(origin, origin + direction * range);
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits) {
            PlayerHealth health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null) {
                if (health.OwnerClientId == OwnerClientId)
                    continue;

                if (health.IsDead)
                    continue;

                health.TakeDamage(damage, OwnerClientId, "Chain Lash");
                GrappleOwnerClientRpc(hit.point);
                HitTargetClientRpc(origin, hit.point, health.OwnerClientId);
                return;
            }

            if (((1 << hit.collider.gameObject.layer) & pullableLayerMask) != 0) {
                GrappleOwnerClientRpc(hit.point);
                HitObjectClientRpc(origin, hit.point);
                return;
            }
        }

        MissClientRpc(origin, origin + direction * range);
    }

    [ClientRpc]
    private void GrappleOwnerClientRpc(Vector3 hookPoint) {
        if (!IsOwner) return;

        if (_ownerMotor == null)
            _ownerMotor = ResolveOwnerMotor();

        if (_ownerMotor != null) {
            _ownerMotor.StartGrapple(hookPoint, grappleSpeed, grappleStopDistance);
            _ownerMotor.PreserveMomentum(0.15f);
        }
    }

    [ClientRpc]
    private void HitTargetClientRpc(Vector3 start, Vector3 hitPoint, ulong hitClientId) {
        StartCoroutine(ChainHitVisual(start, hitPoint));
        SpawnImpact(hitPoint);
    }

    [ClientRpc]
    private void HitObjectClientRpc(Vector3 start, Vector3 hitPoint) {
        StartCoroutine(ChainHitVisual(start, hitPoint));
        SpawnImpact(hitPoint);
    }

    [ClientRpc]
    private void MissClientRpc(Vector3 start, Vector3 end) {
        StartCoroutine(ChainMissVisual(start, end));
    }

    private void SpawnImpact(Vector3 point) {
        if (impactEffectPrefab == null) return;

        GameObject fx = Instantiate(impactEffectPrefab, point, Quaternion.identity);
        Destroy(fx, 1f);
    }

    private IEnumerator CastChainVisual(Vector3 origin, Vector3 direction) {
        _isCasting = true;
        yield return new WaitForSeconds(0.05f);
        _isCasting = false;
    }

    private IEnumerator ChainHitVisual(Vector3 start, Vector3 hitPoint) {
        float dist = Vector3.Distance(start, hitPoint);
        float extTime = Mathf.Max(0.01f, dist / castSpeed);

        float elapsed = 0f;
        while (elapsed < extTime) {
            elapsed += Time.deltaTime;
            DrawChain(start, Vector3.Lerp(start, hitPoint, Mathf.Clamp01(elapsed / extTime)));
            yield return null;
        }

        DrawChain(start, hitPoint);
        yield return new WaitForSeconds(0.05f);

        float retractTime = 0.2f;
        elapsed = 0f;

        while (elapsed < retractTime) {
            elapsed += Time.deltaTime;
            DrawChain(Vector3.Lerp(start, hitPoint, Mathf.Clamp01(elapsed / retractTime)), hitPoint);
            yield return null;
        }

        ClearChain();
    }

    private IEnumerator ChainMissVisual(Vector3 start, Vector3 end) {
        float dist = Vector3.Distance(start, end);
        float extTime = Mathf.Max(0.01f, dist / castSpeed);

        float elapsed = 0f;
        while (elapsed < extTime) {
            elapsed += Time.deltaTime;
            DrawChain(start, Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / extTime)));
            yield return null;
        }

        DrawChain(start, end);
        yield return new WaitForSeconds(0.05f);

        elapsed = 0f;
        float retractTime = Mathf.Max(0.05f, extTime * 0.5f);

        while (elapsed < retractTime) {
            elapsed += Time.deltaTime;
            DrawChain(Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / retractTime)), end);
            yield return null;
        }

        ClearChain();
    }

    private void DrawChain(Vector3 start, Vector3 end) {
        ClearChain();

        if (chainSegmentPrefab == null) return;

        float totalLen = Vector3.Distance(start, end);
        if (totalLen < 0.01f) return;

        Vector3 direction = (end - start).normalized;

        for (int i = 0; i < chainSegments; i++) {
            float t0 = (float)i / chainSegments;
            float t1 = (float)(i + 1) / chainSegments;

            Vector3 segStart = Vector3.Lerp(start, end, t0);
            Vector3 segEnd = Vector3.Lerp(start, end, t1);
            float segLen = Vector3.Distance(segStart, segEnd);

            GameObject seg = Instantiate(
                chainSegmentPrefab,
                (segStart + segEnd) * 0.5f,
                Quaternion.LookRotation(direction)
            );

            seg.transform.localScale = new Vector3(chainWidth, chainWidth, segLen);

            // Chain segments are purely visual - strip any physics collision so they
            // can never push/clip the player (or anything else) as they extend from
            // a point that starts out inside the shooter's own collider.
            Collider[] segColliders = seg.GetComponentsInChildren<Collider>();
            foreach (Collider c in segColliders)
                c.enabled = false;

            Rigidbody segRb = seg.GetComponent<Rigidbody>();
            if (segRb != null)
                segRb.isKinematic = true;

            Renderer rend = seg.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = chainColor;

            _chainVisuals.Add(seg);
        }

        if (hookPrefab != null) {
            if (_hookVisual == null) {
                _hookVisual = Instantiate(hookPrefab);

                Collider[] hookColliders = _hookVisual.GetComponentsInChildren<Collider>();
                foreach (Collider c in hookColliders)
                    c.enabled = false;

                Rigidbody hookRb = _hookVisual.GetComponent<Rigidbody>();
                if (hookRb != null)
                    hookRb.isKinematic = true;
            }

            _hookVisual.transform.position = end;
            _hookVisual.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void ClearChain() {
        foreach (GameObject seg in _chainVisuals) {
            if (seg != null)
                Destroy(seg);
        }

        _chainVisuals.Clear();

        if (_hookVisual != null) {
            Destroy(_hookVisual);
            _hookVisual = null;
        }
    }

    protected override void SpawnProjectile(Vector3 position, Vector3 direction) {
    }

    private void OnDestroy() {
        ClearChain();
    }
}