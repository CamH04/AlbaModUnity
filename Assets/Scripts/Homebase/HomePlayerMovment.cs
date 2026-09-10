using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour {
    [Header("Movement")]
    public float speed = 5f;
    public float gravity = -25f;

    [Header("Jump")]
    public float jumpHeight = 2f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Mouse Look")]
    public Transform cameraTransform;
    public float mouseSensitivity = 0.1f;
    public float maxLookAngle = 80f;

    private CharacterController controller;
    private Vector3 velocity;

    private float cameraPitch = 0f;

    private float coyoteTimer;
    private float jumpBufferTimer;

    private bool inputEnabled = true;

    private void Awake() {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update() {
        if (!inputEnabled)
            return;

        Move();
        Look();
    }

    public void SetInputEnabled(bool enabled) {
        inputEnabled = enabled;
    }

    private void Move() {
        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            input.y += 1;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 movement =
            transform.right * input.x +
            transform.forward * input.y;

        controller.Move(movement * speed * Time.deltaTime);

        if (controller.isGrounded) {
            coyoteTimer = coyoteTime;

            if (velocity.y < 0) {
                velocity.y = -2f;
            }
        }
        else {
            coyoteTimer -= Time.deltaTime;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame) {
            jumpBufferTimer = jumpBufferTime;
        }
        else {
            jumpBufferTimer -= Time.deltaTime;
        }

        if (jumpBufferTimer > 0f && coyoteTimer > 0f) {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    private void Look() {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;

        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}