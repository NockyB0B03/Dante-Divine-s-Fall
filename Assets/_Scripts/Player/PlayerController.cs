using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DANTE: DIVINE'S FALL — PlayerController.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce tutta la locomotione di Dante:
///   - Movimento WASD camera-relativo
///   - Sprint (Shift)
///   - Salto con coyote time
///   - Rotazione Slerp che segue SEMPRE il forward della camera (stile Fortnite)
///   - Gravità custom
///   - IsAbilityCasting: flag settato da Fire/Heal/Ultimate per bloccare il movimento
///
/// DIPENDENZE:
///   - CharacterController sulla stessa GameObject
///   - PlayerInputActions (generato dall'Input Action Asset)
///   - GameManager.Instance per leggere lo stato di pausa
///
/// INSPECTOR SETUP:
///   walkSpeed           → 5
///   sprintSpeed         → 9
///   jumpHeight          → 2
///   gravity             → -20
///   rotationSmoothTime  → 0.12  (più alto = rotazione più lenta)
///   coyoteTimeDuration  → 0.15  (secondi di grazia dopo aver lasciato un bordo)
///   cameraTransform     → trascina il Transform della Main Camera (non il CM FreeLook)
///
/// ANIMATOR PARAMETERS (devono esistere nell'Animator Controller di Dante):
///   Float   "Speed"       → 0 (fermo) a 1 (sprint)
///   Bool    "IsGrounded"
///   Bool    "IsSprinting"
///   Trigger "Jump"
///   Trigger "Fall"
///   Trigger "Land"
/// </summary>

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 2f;
    public float gravity = -20f;
    [Tooltip("Secondi di grazia per saltare dopo aver lasciato un bordo.")]
    public float coyoteTimeDuration = 0.15f;

    [Header("Ground Check")]
    [Tooltip("Distanza del ground check SphereCast verso il basso.")]
    public float groundCheckDistance = 0.12f;

    [Tooltip("Raggio della sfera per il ground check — deve essere minore del Radius del CC.")]
    public float groundCheckRadius = 0.25f;

    [Tooltip("Layer su cui Dante può stare in piedi — spunta solo Default.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Layer Terrain — quando Dante è su questo layer non può morire per caduta.")]
    public LayerMask terrainLayer;

    [Tooltip("Distanza massima verso il basso per rilevare il Terrain (default 2).")]
    public float terrainCheckDistance = 2f;

    [Header("Rotation")]
    [Tooltip("Tempo di smoothing della rotazione Slerp. Valori bassi = più reattivo.")]
    [Range(0.01f, 1f)]
    public float rotationSmoothTime = 0.12f;

    [Header("References")]
    [Tooltip("Transform della Main Camera — NON il FreeLook di Cinemachine.")]
    public Transform cameraTransform;

    // ─── Proprietà pubblica per le abilità ────────────────────────────────────
    /// <summary>
    /// Settato a true da FireAbility, HealAbility, UltimateAbility durante il cast.
    /// Blocca movimento, salto e dash finché è true.
    /// Heal non ha animazione quindi lo setta e resetta nello stesso frame — comunque
    /// rispettato per coerenza architetturale.
    /// </summary>
    public bool IsAbilityCasting { get; set; } = false;

    /// <summary>True se Dante è a terra o nel coyote time window.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Velocità verticale corrente — negativa durante la caduta.</summary>
    public float VerticalVelocity => _verticalVelocity.y;

    /// <summary>
    /// True se il layer Terrain è rilevato entro terrainCheckDistance sotto Dante.
    /// Usato da DeathManager per disabilitare la morte per caduta sui livelli con Terrain.
    /// </summary>
    public bool IsOverTerrain { get; private set; } = false;

    /// <summary>
    /// True quando lo SphereCast rileva il layer Terrain sotto Dante.
    /// Usato da DeathManager per impedire la morte da caduta sul terreno.
    /// </summary>

    /// <summary>
    /// True se lo SphereCast rileva il layer Terrain sotto Dante.
    /// Quando true DeathManager non conta il fall timer.
    /// </summary>

    /// <summary>Velocità orizzontale normalizzata corrente (0–1). Usata dall'Animator.</summary>
    public float NormalisedSpeed { get; private set; }

    /// <summary>
    /// Direzione di movimento camera-relativa corrente nel mondo 3D.
    /// Letta da DashAbility per determinare la direzione dello scatto.
    /// Vector3.zero se nessun input di movimento.
    /// </summary>
    public Vector3 CurrentMoveDirection { get; private set; }

    // ─── Privati ──────────────────────────────────────────────────────────────
    private CharacterController _cc;
    private PlayerInputActions _input;
    private Animator _animator;

    // Input
    private Vector2 _moveInput;
    private bool _isSprinting;
    private bool _jumpQueued;

    // Fisica verticale
    private Vector3 _verticalVelocity;

    // Coyote time
    private float _coyoteTimeCounter;
    private bool _wasGrounded;

    // Stato animazione caduta
    private bool _isFalling;

    // ID parametri Animator (cache per evitare string lookup ogni frame)
    private static readonly int _animSpeed = Animator.StringToHash("Speed");
    private static readonly int _animIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int _animIsSprint = Animator.StringToHash("IsSprinting");
    private static readonly int _animJump = Animator.StringToHash("Jump");
    private static readonly int _animFall = Animator.StringToHash("Fall");
    private static readonly int _animLand = Animator.StringToHash("Land");

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>(true);
        _input = new PlayerInputActions();

        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;
    }

    void Start()
    {
        // Start() viene chiamato dopo TUTTI gli Awake() — GameManager è già inizializzato
        // RegisterPlayer qui garantisce che GameManager.Instance non sia null
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(gameObject);
            Debug.Log("[PlayerController] RegisterPlayer chiamato in Start.");
        }
        else
        {
            Debug.LogError("[PlayerController] GameManager.Instance è null in Start! " +
                           "Aggiungi GameManager nella scena.");
        }

        // Secondo tentativo camera
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        if (cameraTransform == null)
            Debug.LogError("[PlayerController] Camera non trovata! " +
                           "Assicurati che Main Camera sia in scena.");
    }

    void OnEnable()
    {
        _input.Enable();
        BindInputActions();
    }

    void OnDisable()
    {
        UnbindInputActions();
        _input.Disable();
    }

    void Update()
    {
        // Non processare input se il gioco è in pausa o in Game Over
        if (GameManager.Instance != null)
        {
            var state = GameManager.Instance.CurrentState;
            if (state == GameManager.GameState.Paused ||
                state == GameManager.GameState.GameOver ||
                state == GameManager.GameState.Victory)
                return;
        }

        UpdateGroundedState();
        HandleGravity();
        HandleMovement();
        HandleRotation();
        UpdateAnimator();
    }

    // ─── Input Binding ────────────────────────────────────────────────────────
    private void BindInputActions()
    {
        _input.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _input.Player.Move.canceled += ctx => _moveInput = Vector2.zero;
        _input.Player.Sprint.performed += ctx => _isSprinting = true;
        _input.Player.Sprint.canceled += ctx => _isSprinting = false;
        _input.Player.Jump.performed += OnJumpInput;
    }

    private void UnbindInputActions()
    {
        _input.Player.Move.performed -= ctx => _moveInput = ctx.ReadValue<Vector2>();
        _input.Player.Move.canceled -= ctx => _moveInput = Vector2.zero;
        _input.Player.Sprint.performed -= ctx => _isSprinting = true;
        _input.Player.Sprint.canceled -= ctx => _isSprinting = false;
        _input.Player.Jump.performed -= OnJumpInput;
    }

    private void OnJumpInput(InputAction.CallbackContext ctx)
    {
        // Salta se a terra O nel coyote time window, mai durante un cast
        if (IsAbilityCasting) return;

        // Controlla IsGrounded (che include il coyote time)
        // indipendentemente dall'input di movimento corrente
        if (IsGrounded)
            _jumpQueued = true;
    }

    // ─── Ground Detection ─────────────────────────────────────────────────────
    private void UpdateGroundedState()
    {
        // Doppio check: CC.isGrounded + SphereCast verso il basso
        bool ccGrounded = _cc.isGrounded;
        bool sphereGrounded = CheckGroundSphere();

        // Terrain check — se c'è un Terrain sotto entro terrainCheckDistance,
        // forza IsGrounded = true per evitare morti false sui livelli con Terrain
        IsOverTerrain = CheckTerrainBelow();
        bool groundedNow = ccGrounded || sphereGrounded || IsOverTerrain;

        // Coyote time
        if (groundedNow)
            _coyoteTimeCounter = coyoteTimeDuration;
        else
            _coyoteTimeCounter -= Time.deltaTime;

        _coyoteTimeCounter = Mathf.Max(0f, _coyoteTimeCounter);
        IsGrounded = groundedNow || _coyoteTimeCounter > 0f;

        // Animazione atterraggio
        if (!_wasGrounded && groundedNow && _isFalling)
        {
            _animator?.SetTrigger(_animLand);
            _isFalling = false;
        }

        _wasGrounded = groundedNow;
    }

    /// <summary>
    /// SphereCast verso il basso partendo dal centro del CharacterController.
    /// Più affidabile di CC.isGrounded sui bordi e superfici irregolari.
    /// </summary>
    private bool CheckGroundSphere()
    {
        if (!_cc.enabled) return false;

        Vector3 sphereOrigin = transform.position +
                               Vector3.up * (groundCheckRadius + 0.02f);

        float castDistance = groundCheckDistance + groundCheckRadius;

        // Check terreno normale
        bool grounded = Physics.SphereCast(
            sphereOrigin, groundCheckRadius, Vector3.down,
            out _, castDistance, groundLayers,
            QueryTriggerInteraction.Ignore);

        return grounded;
    }

    /// <summary>
    /// Controlla se sotto Dante c'è un layer Terrain entro terrainCheckDistance.
    /// </summary>
    private bool CheckTerrainBelow()
    {
        if (terrainLayer == 0) return false;
        if (!_cc.enabled) return false;

        return Physics.SphereCast(
            transform.position + Vector3.up * 0.3f,
            0.3f,
            Vector3.down,
            out _,
            terrainCheckDistance,
            terrainLayer,
            QueryTriggerInteraction.Ignore);
    }

    // ─── Gravità & Salto ──────────────────────────────────────────────────────
    private void HandleGravity()
    {
        if (!_cc.enabled) return;

        // Reset velocità Y quando a terra
        if (_cc.isGrounded && _verticalVelocity.y < 0f)
            _verticalVelocity.y = -2f;   // piccolo valore negativo mantiene il CC a terra

        // Processa salto accodato
        if (_jumpQueued)
        {
            _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _coyoteTimeCounter = 0f;    // consuma il coyote time
            _jumpQueued = false;
            _isFalling = false;
            _animator?.SetTrigger(_animJump);
        }

        // Applica gravità
        _verticalVelocity.y += gravity * Time.deltaTime;

        // Trigger animazione caduta (solo se in aria e in discesa)
        if (!_cc.isGrounded && _verticalVelocity.y < -1f && !_isFalling)
        {
            _isFalling = true;
            _animator?.SetTrigger(_animFall);
        }

        _cc.Move(_verticalVelocity * Time.deltaTime);
    }

    // ─── Movimento Orizzontale ────────────────────────────────────────────────
    private void HandleMovement()
    {
        // Blocca movimento durante cast abilità
        if (IsAbilityCasting)
        {
            NormalisedSpeed = 0f;
            return;
        }

        if (_moveInput.sqrMagnitude < 0.01f)
        {
            NormalisedSpeed = 0f;
            CurrentMoveDirection = Vector3.zero;
            return;
        }

        // Direzione relativa alla camera
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
        float speed = _isSprinting ? sprintSpeed : walkSpeed;

        // Aggiorna la direzione corrente — letta da DashAbility
        CurrentMoveDirection = moveDir;

        // Non muovere se DashAbility sta gestendo il movimento
        DashAbility dash = GetComponent<DashAbility>();
        if (dash != null && dash.IsDashing) return;

        _cc.Move(moveDir * speed * Time.deltaTime);

        // Normalizza velocità per Animator (0 = fermo, 0.5 = camminata, 1 = sprint)
        NormalisedSpeed = _isSprinting ? 1f : _moveInput.magnitude * 0.5f;
    }

    // ─── Rotazione ────────────────────────────────────────────────────────────
    private void HandleRotation()
    {
        if (cameraTransform == null) return;

        // Segue SEMPRE il forward della camera — anche da fermo (stile Fortnite)
        Vector3 targetForward = cameraTransform.forward;
        targetForward.y = 0f;

        if (targetForward.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetForward);

        // Slerp con rotationSmoothTime: più alto = più lento
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime / Mathf.Max(rotationSmoothTime, 0.001f));
    }

    // ─── Animator ─────────────────────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (_animator == null) return;

        _animator.SetFloat(_animSpeed, NormalisedSpeed);
        _animator.SetBool(_animIsGrounded, _cc.isGrounded);
        _animator.SetBool(_animIsSprint, _isSprinting && _moveInput.sqrMagnitude > 0.01f);
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Visualizza il forward di Dante in scena
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);

        // Visualizza il forward della camera
        if (cameraTransform != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 camFwd = cameraTransform.forward;
            camFwd.y = 0f;
            Gizmos.DrawRay(transform.position + Vector3.up, camFwd.normalized * 2f);
        }
    }
#endif
}