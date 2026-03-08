using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float jumpHeight = 2f;
    public float gravity = -20f;
    [Range(0.01f, 1f)]
    public float rotationSmoothTime = 0.12f;

    [Header("References")]
    public Transform cameraTransform;

    private CharacterController _cc;
    private PlayerInputActions _input;
    private Vector2 _moveInput;
    private Vector3 _velocity;
    private bool _isSprinting;
    private bool _jumpQueued;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _input = new PlayerInputActions();
    }

    void OnEnable() { _input.Enable(); BindActions(); }
    void OnDisable() { _input.Disable(); }

    void BindActions()
    {
        _input.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _input.Player.Move.canceled += ctx => _moveInput = Vector2.zero;
        _input.Player.Jump.performed += ctx => _jumpQueued = true;
        _input.Player.Sprint.performed += ctx => _isSprinting = true;
        _input.Player.Sprint.canceled += ctx => _isSprinting = false;
    }

    void Update()
    {
        HandleGravity();
        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        if (_moveInput.sqrMagnitude < 0.01f && _cc.isGrounded) return;

        // Camera-relative direction
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 dir = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
        float speed = _isSprinting ? sprintSpeed : walkSpeed;

        _cc.Move(dir * speed * Time.deltaTime);
    }

    void HandleRotation()
    {
        // Rotate toward camera-forward on input
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(forward);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, target,
            rotationSmoothTime > 0 ? Time.deltaTime / rotationSmoothTime : 1f);
    }

    void HandleGravity()
    {
        if (_cc.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (_jumpQueued && _cc.isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpQueued = false;
        }

        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }
}