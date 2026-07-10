using UnityEngine;

[RequireComponent(typeof(PlayerMotor))]
public class SlideController : MonoBehaviour {
    [Header("Slide Settings")]
    public float slideMaxSpeed = 18f;
    public float slideDuration = 0.8f;
    public float slideCooldown = 0.5f;

    private PlayerMotor _motor;
    private float _slideTimer;
    private float _cooldownTimer;
    private bool _sliding;
    private Vector3 _slideDir;

    public bool IsSliding => _sliding;

    void Awake() {
        _motor = GetComponent<PlayerMotor>();
    }

    public bool TrySlide(Vector3 currentVelocity) {
        if (_cooldownTimer > 0 || _sliding) return false;

        float hSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
        if (hSpeed < 4f) return false;

        _sliding = true;
        _slideTimer = slideDuration;
        _slideDir = new Vector3(currentVelocity.x, 0, currentVelocity.z).normalized;
        return true;
    }

    public Vector3 GetSlideVelocity(Vector3 currentVel) {
        _slideTimer -= Time.fixedDeltaTime;

        if (_slideTimer <= 0f) {
            _sliding = false;
            _cooldownTimer = slideCooldown;
            return currentVel;
        }

        float t = _slideTimer / slideDuration;
        float speed = Mathf.Lerp(4f, slideMaxSpeed, t);

        return _slideDir * speed + Vector3.up * currentVel.y;
    }

    public void CancelSlide() {
        _sliding = false;
        _cooldownTimer = slideCooldown;
    }

    void Update() {
        if (_cooldownTimer > 0) _cooldownTimer -= Time.deltaTime;
    }
}