using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Chain Lash — modelled on Sylas' second E cast (Abduct).
///
/// Fire hurls a chain in the aim direction. The chain is a real projectile on
/// the server: walls and props stop it, and the first enemy player it touches
/// takes damage. On a player hit the target is yanked toward the caster while
/// the caster lunges toward the target, so the two meet in the middle — then
/// the chain retracts back to the hand.
///
/// The visual chain is pooled (built once, repositioned every frame) and is
/// drawn from the weapon to the hook, so it stays attached to the hand while
/// the caster is moving instead of being left behind at the cast position.
/// </summary>
public class AbductChain : WeaponBase {
    [Header("Abduct Settings")]
    [Tooltip("Maximum chain travel distance.")]
    public float range = 50f;
    public float damage = 500f;
    [Tooltip("How fast the chain tip travels, in m/s.")]
    public float castSpeed = 55f;
    [Tooltip("Seconds between casts.")]
    public float cooldown = 6f;

    [Header("Yank")]
    [Tooltip("Reference speed used to derive how long the two-way pull takes.")]
    public float abductSpeed = 65f;
    [Tooltip("How much of the gap the caster covers. The target is yanked the rest.")]
    [Range(0.5f, 0.95f)] public float casterTravelShare = 0.72f;
    [Tooltip("How far apart the two players end up after the yank.")]
    public float meetDistance = 1.8f;
    [Tooltip("Clamp on the yank duration so point-blank hits do not snap.")]
    public float minAbductDuration = 0.12f;
    public float maxAbductDuration = 0.45f;
    [Tooltip("Momentum the caster keeps as the lunge ends.")]
    public float casterExitSpeed = 8f;

    [Header("Hit Detection")]
    [Tooltip("Radius of the chain tip. A little forgiveness makes the chain feel physical.")]
    public float chainHitRadius = 0.3f;

    [Header("Visual")]
    public GameObject chainSegmentPrefab;
    [Tooltip("Approximate world length of one visual chain link.")]
    public float segmentLength = 0.55f;
    [Tooltip("Hard cap on pooled chain links (raise if range / segmentLength needs more).")]
    public int maxChainSegments = 96;
    [Tooltip("Radius of one chain link, in metres.")]
    public float chainWidth = 0.06f;
    public Color chainColor = new Color(0.5f, 0.9f, 1f, 1f);
    [Tooltip("How fast the chain snaps back to the hand.")]
    public float retractSpeed = 70f;

    [Header("Hook")]
    public GameObject hookPrefab;

    [Header("Impact")]
    public GameObject impactEffectPrefab;

    // ── Cast state ───────────────────────────────────────────────────────────
    private bool _castLocked;
    private bool _castVisualStarted;
    private bool _predictedCastPending;
    private float _lastFireTime = -999f;
    private float _castUnlockTime;
    private float _serverLastCastTime = -999f;
    private bool _serverCasting;

    private PlayerMotor _ownerMotor;

    // ── Visual state (runs on every client) ──────────────────────────────────
    private enum VisualState { Idle, Extending, Hooked, Retracting }

    private VisualState _visualState = VisualState.Idle;
    private Vector3 _castRayOrigin;
    private Vector3 _castRayDir = Vector3.forward;
    private float _extendDistance;
    private Vector3 _hookPoint;
    private Transform _hookedTarget;
    private float _hookHoldTimer;

    private readonly List<GameObject> _segments = new List<GameObject>();
    private Material _chainMaterial;
    private GameObject _hookVisual;

    private void Awake() {
        BuildVisualPool();
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        _ownerMotor = ResolveLocalPlayerMotor();
    }

    // ── Input ────────────────────────────────────────────────────────────────

    protected override void Update() {
        base.Update(); // WeaponBase only runs HandleInput for the owner.

        // Unlock as soon as the chain is back in hand, with a time fallback so
        // a dropped RPC can never leave the weapon permanently locked.
        if (_castLocked &&
            ((_castVisualStarted && _visualState == VisualState.Idle) ||
             Time.time >= _castUnlockTime))
            _castLocked = false;
    }

    protected override void LateUpdate() {
        base.LateUpdate(); // mirrors the weapon to the holder/camera on every client
        TickVisual(Time.deltaTime);
    }

    protected override void HandleInput() {
        if (!IsOwner) return;
        if (_castLocked) return;
        if (Time.time - _lastFireTime < cooldown) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Fire();
    }

    protected override void Fire() {
        if (_castLocked) return;
        if (_playerCamera == null) return;
        if (Time.time - _lastFireTime < cooldown) return;

        _lastFireTime = Time.time;
        _castLocked = true;
        _castVisualStarted = false;
        _predictedCastPending = true;

        // Longest possible life of one cast: out, yank, back.
        _castUnlockTime = Time.time
            + range / Mathf.Max(1f, castSpeed)
            + maxAbductDuration
            + range / Mathf.Max(1f, retractSpeed)
            + 0.35f;

        Vector3 origin = _playerCamera.transform.position;
        Vector3 direction = _playerCamera.transform.forward.normalized;

        // Start the chain immediately on this client so it leaves the hand on
        // the same frame as the click. The server's cast RPC will skip its own
        // echo for this one cast (see _predictedCastPending).
        BeginVisual(origin, direction);

        CastChainServerRpc(origin, direction);
    }

    // ── Server: chain projectile ─────────────────────────────────────────────

    [ServerRpc]
    private void CastChainServerRpc(Vector3 origin, Vector3 direction) {
        if (_serverCasting) return;
        if (Time.time - _serverLastCastTime < cooldown) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();

        _serverLastCastTime = Time.time;
        _serverCasting = true;

        // Starts the chain on every client at the authoritative cast moment,
        // so the owner's chain never overshoots the target waiting for the hit.
        CastVisualClientRpc(origin, direction);

        StartCoroutine(ServerCastRoutine(origin, direction));
    }

    private IEnumerator ServerCastRoutine(Vector3 origin, Vector3 direction) {
        Vector3 pos = origin;
        float travelled = 0f;

        while (travelled < range) {
            float step = Mathf.Min(castSpeed * Time.fixedDeltaTime, range - travelled);
            if (step <= 0f) break;

            if (TryHitAlong(pos, direction, step, out RaycastHit hit, out bool blocked)) {
                if (blocked) {
                    MissClientRpc(hit.point);
                    _serverCasting = false;
                    yield break;
                }

                var health = hit.collider.GetComponentInParent<PlayerHealth>();
                if (health != null) {
                    ResolveAbduct(health, hit);
                    yield break;
                }
            }

            pos += direction * step;
            travelled += step;
            yield return new WaitForFixedUpdate();
        }

        MissClientRpc(origin + direction * range);
        _serverCasting = false;
    }

    /// <summary>
    /// Sweeps the chain tip through this step. Returns true when it hit
    /// anything. <paramref name="blocked"/> is true for non-player geometry.
    /// </summary>
    private bool TryHitAlong(Vector3 pos, Vector3 dir, float dist,
                             out RaycastHit hit, out bool blocked) {
        hit = default;
        blocked = false;

        RaycastHit[] hits = Physics.SphereCastAll(
            pos, chainHitRadius, dir, dist,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits) {
            var health = h.collider.GetComponentInParent<PlayerHealth>();

            if (health != null) {
                // The camera can start inside the caster's own capsule.
                if (health.OwnerClientId == OwnerClientId) continue;
                if (health.IsDead) continue;

                hit = h;
                return true;
            }

            // Level geometry / props stop the chain. Abduct only pulls players.
            hit = h;
            blocked = true;
            return true;
        }

        return false;
    }

    private void ResolveAbduct(PlayerHealth target, RaycastHit hit) {
        var targetNet = target.GetComponent<NetworkObject>();

        Vector3 casterPos = GetPlayerPosition(OwnerClientId);
        Vector3 targetPos = target.transform.position;

        Vector3 delta = targetPos - casterPos;
        float gap = delta.magnitude;
        Vector3 dir = gap > 0.001f ? delta / gap : transform.forward;

        // The caster covers most of the gap and the target gets yanked the
        // rest of the way. Both stop short of the meet point so they end up
        // beside each other instead of inside each other.
        Vector3 meetPoint = Vector3.Lerp(casterPos, targetPos, casterTravelShare);
        Vector3 casterDest = meetPoint - dir * (meetDistance * 0.5f);
        Vector3 targetDest = meetPoint + dir * (meetDistance * 0.5f);

        float moveDist = Mathf.Max(
            Vector3.Distance(casterPos, casterDest),
            Vector3.Distance(targetPos, targetDest));

        float duration = Mathf.Clamp(
            moveDist / Mathf.Max(1f, abductSpeed),
            minAbductDuration, maxAbductDuration);

        // Point-blank: don't shove either player around.
        if (gap <= meetDistance + 0.05f) {
            duration = minAbductDuration;
            casterDest = casterPos;
            targetDest = targetPos;
        }

        if (targetNet != null)
            HitClientRpc(hit.point, targetNet.NetworkObjectId, duration);

        PullTargetClientRpc(targetDest, duration, TargetParams(target.OwnerClientId));
        PullCasterClientRpc(casterDest, duration, casterExitSpeed, TargetParams(OwnerClientId));

        // Damage lands when the yank completes. A lethal hit at the hook point
        // would disable the target's movement controllers and cancel the pull,
        // which made the abduct invisible for a 500-damage weapon.
        StartCoroutine(DealDamageDelayed(target, duration));

        StartCoroutine(FinishCastRoutine(duration + 0.05f));
    }

    private IEnumerator DealDamageDelayed(PlayerHealth target, float delay) {
        yield return new WaitForSeconds(delay);

        if (target == null || target.IsDead) yield break;
        target.TakeDamage(damage, OwnerClientId, "Chain Lash");
    }

    private IEnumerator FinishCastRoutine(float delay) {
        yield return new WaitForSeconds(delay);
        _serverCasting = false;
    }

    private Vector3 GetPlayerPosition(ulong clientId) {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject != null)
            return client.PlayerObject.transform.position;

        return transform.position;
    }

    private static ClientRpcParams TargetParams(ulong clientId) {
        return new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
    }

    // ── Client RPCs ──────────────────────────────────────────────────────────

    [ClientRpc]
    private void CastVisualClientRpc(Vector3 origin, Vector3 direction) {
        // This client already predicted this cast in Fire(); don't rewind it.
        if (_predictedCastPending) {
            _predictedCastPending = false;
            return;
        }

        BeginVisual(origin, direction);
    }

    [ClientRpc]
    private void HitClientRpc(Vector3 hitPoint, ulong targetNetObjectId, float holdDuration) {
        _hookPoint = hitPoint;
        _hookedTarget = ResolveTransform(targetNetObjectId);
        _hookHoldTimer = Mathf.Max(0.05f, holdDuration);
        _visualState = VisualState.Hooked;

        if (impactEffectPrefab != null) {
            GameObject fx = Instantiate(impactEffectPrefab, hitPoint, Quaternion.identity);
            Destroy(fx, 1f);
        }
    }

    [ClientRpc]
    private void MissClientRpc(Vector3 endPoint) {
        if (_visualState == VisualState.Idle) return;

        _hookPoint = endPoint;
        _hookedTarget = null;
        _visualState = VisualState.Retracting;
    }

    [ClientRpc]
    private void PullCasterClientRpc(Vector3 destination, float duration, float exitSpeed,
                                     ClientRpcParams rpcParams = default) {
        if (!IsOwner) return;

        if (_ownerMotor == null)
            _ownerMotor = ResolveLocalPlayerMotor();

        _ownerMotor?.StartAbduct(destination, duration, exitSpeed);
    }

    [ClientRpc]
    private void PullTargetClientRpc(Vector3 destination, float duration,
                                     ClientRpcParams rpcParams = default) {
        var motor = ResolveLocalPlayerMotor();
        motor?.StartAbduct(destination, duration, 0f);
    }

    // ── Visuals ──────────────────────────────────────────────────────────────

    private void BeginVisual(Vector3 origin, Vector3 direction) {
        _castVisualStarted = true;
        _castRayOrigin = origin;
        _castRayDir = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.forward;

        _extendDistance = 0f;
        _hookPoint = transform.position;
        _hookedTarget = null;
        _hookHoldTimer = 0f;
        _visualState = VisualState.Extending;

        EnsurePool();

        if (_chainMaterial != null)
            ApplyChainColor(_chainMaterial);
    }

    private void TickVisual(float dt) {
        if (_visualState == VisualState.Idle) {
            SetVisible(false);
            return;
        }

        switch (_visualState) {
            case VisualState.Extending:
                _extendDistance += castSpeed * dt;
                _hookPoint = _castRayOrigin + _castRayDir * _extendDistance;

                if (_extendDistance >= range)
                    _visualState = VisualState.Retracting;
                break;

            case VisualState.Hooked:
                // Stay latched to the target while the abduct plays out.
                if (_hookedTarget != null)
                    _hookPoint = _hookedTarget.position + Vector3.up * 1f;

                _hookHoldTimer -= dt;
                if (_hookHoldTimer <= 0f) {
                    _hookedTarget = null;
                    _visualState = VisualState.Retracting;
                }
                break;

            case VisualState.Retracting:
                Vector3 hand = transform.position;
                _hookPoint = Vector3.MoveTowards(_hookPoint, hand, retractSpeed * dt);

                if ((_hookPoint - hand).sqrMagnitude <= 0.0025f)
                    _visualState = VisualState.Idle;
                break;
        }

        if (_visualState == VisualState.Idle) {
            SetVisible(false);
            return;
        }

        DrawChain(transform.position, _hookPoint);
    }

    private void DrawChain(Vector3 start, Vector3 end) {
        Vector3 delta = end - start;
        float length = delta.magnitude;

        if (length < 0.01f) {
            SetVisible(false);
            return;
        }

        Vector3 dir = delta / length;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir);

        if (_segments.Count > 0) {
            int count = Mathf.Clamp(
                Mathf.CeilToInt(length / Mathf.Max(0.05f, segmentLength)),
                1, _segments.Count);

            float linkLength = length / count;

            for (int i = 0; i < _segments.Count; i++) {
                GameObject seg = _segments[i];
                bool use = i < count;

                if (seg.activeSelf != use)
                    seg.SetActive(use);

                if (!use) continue;

                seg.transform.SetPositionAndRotation(
                    start + dir * (linkLength * (i + 0.5f)), rot);

                // Capsule primitive: 2 units tall, radius 0.5 on local Y, so
                // y-scale is half the link length and x/z is the diameter.
                seg.transform.localScale = new Vector3(
                    chainWidth * 2f, linkLength * 0.5f * 1.06f, chainWidth * 2f);
            }
        }

        if (_hookVisual != null) {
            if (!_hookVisual.activeSelf)
                _hookVisual.SetActive(true);

            _hookVisual.transform.SetPositionAndRotation(
                end, Quaternion.LookRotation(dir, Vector3.up));
        }
    }

    private void SetVisible(bool visible) {
        for (int i = 0; i < _segments.Count; i++) {
            GameObject seg = _segments[i];
            if (seg != null && seg.activeSelf != visible)
                seg.SetActive(visible);
        }

        if (_hookVisual != null && _hookVisual.activeSelf != visible)
            _hookVisual.SetActive(visible);
    }

    private void BuildVisualPool() {
        if (chainSegmentPrefab != null) {
            if (_chainMaterial == null) {
                Renderer src = chainSegmentPrefab.GetComponentInChildren<Renderer>(true);
                if (src != null && src.sharedMaterial != null) {
                    _chainMaterial = new Material(src.sharedMaterial);
                    ApplyChainColor(_chainMaterial);
                }
            }

            int target = Mathf.Max(1, maxChainSegments);

            while (_segments.Count < target) {
                GameObject seg = Instantiate(chainSegmentPrefab);
                seg.name = "ChainSegment";
                StripPhysics(seg);

                Renderer rend = seg.GetComponentInChildren<Renderer>(true);
                if (rend != null && _chainMaterial != null)
                    rend.sharedMaterial = _chainMaterial;

                seg.SetActive(false);
                _segments.Add(seg);
            }
        }

        if (hookPrefab != null && _hookVisual == null) {
            _hookVisual = Instantiate(hookPrefab);
            _hookVisual.name = "ChainHook";
            StripPhysics(_hookVisual);
            _hookVisual.SetActive(false);
        }
    }

    private void EnsurePool() {
        if (chainSegmentPrefab != null &&
            _segments.Count < Mathf.Max(1, maxChainSegments))
            BuildVisualPool();
    }

    private static void StripPhysics(GameObject obj) {
        foreach (Collider c in obj.GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        foreach (Rigidbody rb in obj.GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = true;
    }

    private void ApplyChainColor(Material mat) {
        // URP uses _BaseColor, built-in uses _Color; support both.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", chainColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", chainColor);
    }

    // ── Resolution helpers ───────────────────────────────────────────────────

    // NetworkManager.ConnectedClients only exists on the server. The local
    // client's PlayerObject works on host and pure clients alike, which is
    // what makes the pull work for non-host players.
    private static PlayerMotor ResolveLocalPlayerMotor() {
        if (NetworkManager.Singleton == null) return null;

        var local = NetworkManager.Singleton.LocalClient;
        if (local == null || local.PlayerObject == null) return null;

        return local.PlayerObject.GetComponent<PlayerMotor>();
    }

    private static Transform ResolveTransform(ulong networkObjectId) {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SpawnManager == null) return null;

        return nm.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var obj) && obj != null
            ? obj.transform
            : null;
    }

    protected override void SpawnProjectile(Vector3 position, Vector3 direction) {
        // Chain travel is handled by ServerCastRoutine, not the base projectile path.
    }

    public override void OnDestroy() {
        base.OnDestroy();

        foreach (GameObject seg in _segments) {
            if (seg != null)
                Destroy(seg);
        }
        _segments.Clear();

        if (_hookVisual != null)
            Destroy(_hookVisual);
        _hookVisual = null;

        if (_chainMaterial != null)
            Destroy(_chainMaterial);
        _chainMaterial = null;
    }
}
