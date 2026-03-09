using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DANTE: DIVINE'S FALL — DashAbility.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Scatto rapido nella direzione del movimento corrente (WASD).
/// Se non c'è input di movimento, il dash non viene eseguito.
/// Durante il dash Dante è completamente invincibile (iframes via Health.IsInvincible).
///
/// FLUSSO:
///   1. Input Dash → verifica cooldown e input movimento → avvia DashRoutine()
///   2. Snapshot della direzione WASD corrente da PlayerController
///   3. IsDashing = true  →  PlayerController.HandleMovement() si disabilita
///   4. Trigger animazione Dash
///   5. CharacterController.Move() sposta Dante per dashDuration secondi
///      a walkSpeed * speedMultiplier
///   6. Fine dash → IsDashing = false, avvia cooldown
///
/// IFRAMES:
///   Health.IsInvincible = true per iFrameDuration secondi durante il dash.
///   Health.TakeDamage() controlla questo flag e ignora il danno se true.
///
/// NON usa IsAbilityCasting — il dash deve restare reattivo anche durante
/// animazioni di altre abilità (design choice da GDD).
///
/// INSPECTOR:
///   dashData        → SO_DashAbility ScriptableObject
///   speedMultiplier → moltiplicatore della walkSpeed base (default 3.5)
///   dashData.dashDuration   → durata dello scatto in secondi (default 0.2)
///   dashData.iFrameDuration → durata invincibilità in secondi (default 0.3)
///   dashData.cooldown       → secondi prima del prossimo dash (default 1.5)
///
/// DIPENDENZE:
///   PlayerController.cs — legge CurrentMoveDirection e walkSpeed
///   Health.cs           — setta IsInvincible durante gli iframes
///   DashData.cs         — ScriptableObject con i parametri
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Health))]
public class DashAbility : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Data")]
    [Tooltip("SO_DashAbility ScriptableObject.")]
    public DashData dashData;

    [Header("Speed")]
    [Tooltip("Moltiplicatore della walkSpeed di PlayerController durante il dash.")]
    public float speedMultiplier = 3.5f;

    // ─── Proprietà pubblica ───────────────────────────────────────────────────
    /// <summary>
    /// True durante il dash. PlayerController lo legge per disabilitare
    /// HandleMovement() e lasciare il controllo a DashAbility.
    /// </summary>
    public bool IsDashing { get; private set; } = false;

    // Evento per HUD cooldown fill
    public static event System.Action<float> OnCooldownChanged; // 0 = pronto, 1 = appena usato

    // ─── Privati ──────────────────────────────────────────────────────────────
    private PlayerController _playerController;
    private CharacterController _cc;
    private Health _health;
    private Animator _animator;
    private PlayerInputActions _input;

    private float _cooldownRemaining = 0f;

    private static readonly int _animDash = Animator.StringToHash("Dash");

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _cc = GetComponent<CharacterController>();
        _health = GetComponent<Health>();
        _animator = GetComponentInChildren<Animator>();
        _input = new PlayerInputActions();
    }

    void OnEnable()
    {
        _input.Enable();
        _input.Player.Dash.performed += OnDashInput;
    }

    void OnDisable()
    {
        _input.Player.Dash.performed -= OnDashInput;
        _input.Disable();
    }

    void Update()
    {
        if (_cooldownRemaining <= 0f) return;

        _cooldownRemaining -= Time.deltaTime;
        _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining);

        float normalised = dashData != null
            ? _cooldownRemaining / dashData.cooldown
            : 0f;
        OnCooldownChanged?.Invoke(normalised);
    }

    // ─── Input Callback ───────────────────────────────────────────────────────
    private void OnDashInput(InputAction.CallbackContext ctx)
    {
        // Gate 1: cooldown
        if (_cooldownRemaining > 0f) return;

        // Gate 2: non già in dash
        if (IsDashing) return;

        // Gate 3: deve esserci input di movimento — il dash va in direzione WASD
        if (_playerController.CurrentMoveDirection.sqrMagnitude < 0.01f) return;

        StartCoroutine(DashRoutine());
    }

    // ─── Dash Coroutine ───────────────────────────────────────────────────────
    private IEnumerator DashRoutine()
    {
        IsDashing = true;

        // Snapshot della direzione di movimento al momento della pressione
        Vector3 dashDirection = _playerController.CurrentMoveDirection.normalized;

        // Velocità del dash = walkSpeed * moltiplicatore
        float dashSpeed = _playerController.walkSpeed * speedMultiplier;

        float duration = dashData != null ? dashData.dashDuration : 0.2f;
        float iframes = dashData != null ? dashData.iFrameDuration : 0.3f;
        float cooldown = dashData != null ? dashData.cooldown : 1.5f;

        // ── Animazione ────────────────────────────────────────────────────
        _animator?.SetTrigger(_animDash);

        // ── Iframes ON ────────────────────────────────────────────────────
        _health.IsInvincible = true;
        StartCoroutine(IFrameRoutine(iframes));

        // ── Movimento dash per 'duration' secondi ─────────────────────────
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Applica solo la componente orizzontale — la gravità è gestita
            // da PlayerController separatamente tramite _verticalVelocity
            _cc.Move(dashDirection * dashSpeed * Time.deltaTime);

            yield return null;
        }

        // ── Fine dash ─────────────────────────────────────────────────────
        IsDashing = false;

        // ── Avvia cooldown ────────────────────────────────────────────────
        _cooldownRemaining = cooldown;
        OnCooldownChanged?.Invoke(1f);
    }

    // ─── IFrame Coroutine ─────────────────────────────────────────────────────

    /// <summary>
    /// Mantiene l'invincibilità per iframeDuration secondi,
    /// poi la rimuove indipendentemente dalla fine del dash.
    /// Gli iframes possono durare più a lungo del dash stesso.
    /// </summary>
    private IEnumerator IFrameRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        _health.IsInvincible = false;
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Forza Dash avanti")]
    private void Debug_ForceDash()
    {
        if (!IsDashing)
            StartCoroutine(DashRoutine());
    }
#endif
}