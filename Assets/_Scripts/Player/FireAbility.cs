using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DANTE: DIVINE'S FALL — FireAbility.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Handles Dante's Fire ability (left mouse button).
/// Locked behind GameManager.CombatUnlocked — only active in Level 5 (boss arena).
///
/// LAUNCH LOGIC:
///   1. Raycast from screen center through the camera into the world.
///   2. If the ray hits something → target point = hit.point.
///      If nothing is hit       → target point = ray.origin + ray.direction * fallbackRange.
///   3. Compute direction from muzzlePoint to target, apply parabolic launch velocity
///      via Rigidbody (gravity handles the arc — no manual angle needed).
///   4. Vertical velocity is boosted by an Inspector-tweakable 'arcBoost' to create
///      a natural upward arc toward distant targets.
///
/// MOVEMENT LOCK:
///   Sets PlayerController.IsAbilityCasting = true for the duration of the
///   fire animation, blocking move/jump/dash input inside PlayerController.
///
/// COOLDOWN:
///   Managed internally. HUDController subscribes to OnCooldownChanged to
///   update the Fire icon fill.
///
/// INSPECTOR SETUP:
///   muzzlePoint     → child Transform on Dante positioned at his hand/chest
///   fireData        → SO_FireAbility ScriptableObject asset
///   castAnimTrigger → "FireShot" (must match Animator parameter exactly)
///   castDuration    → duration in seconds of the fire animation clip
///   raycastMask     → layers the aim raycast should hit (Environment + Enemy)
///   fallbackRange   → how far the ray travels if it hits nothing (e.g. 50)
///   arcBoost        → extra upward velocity multiplier for the parabola (e.g. 0.4)
///
/// HIERARCHY:
///   FireAbility.cs lives on the Dante root GameObject alongside PlayerController.
/// </summary>

[RequireComponent(typeof(PlayerController))]
public class FireAbility : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Spawn point for the projectile — position at Dante's hand or chest.")]
    public Transform muzzlePoint;

    [Tooltip("FireData ScriptableObject — holds projectileSpeed and damage.")]
    public FireData fireData;

    public Camera mainCamera;

    [Header("Animation")]
    [Tooltip("Exact Animator trigger name for the fire cast animation.")]
    public string castAnimTrigger = "FireShot";

    [Tooltip("Duration of the fire animation in seconds. Movement is locked for this long.")]
    public float castDuration = 0.8f;

    [Header("Aim")]
    [Tooltip("Layers the aim raycast collides with. Include Environment and Enemy.")]
    public LayerMask raycastMask;

    [Tooltip("Distance used as target when the aim ray hits nothing.")]
    public float fallbackRange = 50f;

    [Tooltip("Extra upward push applied to launch velocity to create a visible arc.")]
    [Range(0f, 5f)]
    public float arcBoost = 0.4f;

    // Events — HUDController subscribes to update the cooldown fill UI
    public static event System.Action<float> OnCooldownChanged;  // passes 0→1 normalised remaining

    // ─── Private State ────────────────────────────────────────────────────────
    private PlayerController _playerController;
    private Animator _animator;
    private PlayerInputActions _input;

    private float _cooldownRemaining = 0f;
    private bool _isCasting = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _animator = GetComponentInChildren<Animator>(true);
        // true = include inactive GameObjects nella ricerca
        _input = new PlayerInputActions();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void OnEnable()
    {
        _input.Enable();
        _input.Player.Fire.performed += OnFireInput;
    }

    void OnDisable()
    {
        _input.Player.Fire.performed -= OnFireInput;
        _input.Disable();
    }

    void Update()
    {
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= Time.deltaTime;
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining);

            // Broadcast normalised remaining (1 = just used, 0 = ready)
            float normalised = fireData != null
                ? _cooldownRemaining / fireData.cooldown
                : 0f;
            OnCooldownChanged?.Invoke(normalised);
        }
    }

    // ─── Input Callback ───────────────────────────────────────────────────────
    private void OnFireInput(InputAction.CallbackContext ctx)
    {
        // Gate 1: combat must be unlocked (boss level only)
        if (!GameManager.Instance.CombatUnlocked) return;

        // Gate 2: cooldown must be finished
        if (_cooldownRemaining > 0f) return;

        // Gate 3: not already casting, not in another ability
        if (_isCasting) return;
        if (_playerController.IsAbilityCasting) return;

        StartCoroutine(FireRoutine());
    }

    // ─── Fire Coroutine ───────────────────────────────────────────────────────
    private IEnumerator FireRoutine()
    {
        _isCasting = true;
        _playerController.IsAbilityCasting = true;   // locks move/jump/dash in PlayerController

        // ── Play animation ────────────────────────────────────────────────
        // Secondo tentativo — cerca l'Animator se non trovato in Awake
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        _animator?.SetTrigger(castAnimTrigger);

        // ── Wait for half the animation before launching
        // (feels more natural — orb leaves hand mid-swing)
        yield return new WaitForSeconds(castDuration * 0.5f);

        // ── Calculate aim target via screen-center raycast ────────────────
        Vector3 targetPoint = GetAimTargetPoint();

        // ── Calculate launch velocity ─────────────────────────────────────
        Vector3 launchVelocity = CalculateLaunchVelocity(targetPoint);

        // ── Get orb from pool and launch ──────────────────────────────────
        if (FireOrbPool.Instance != null)
        {
            GameObject orb = FireOrbPool.Instance.Get(
                muzzlePoint.position,
                Quaternion.LookRotation(launchVelocity.normalized));

            Projectile proj = orb.GetComponent<Projectile>();
            proj?.Launch(launchVelocity);
        }
        else
        {
            Debug.LogError("[FireAbility] FireOrbPool.Instance is null — " +
                           "make sure FireOrbPool is present in the boss level scene.");
        }

        // ── Wait for remainder of animation ───────────────────────────────
        yield return new WaitForSeconds(castDuration * 0.5f);

        // ── Start cooldown ────────────────────────────────────────────────
        _cooldownRemaining = fireData != null ? fireData.cooldown : 3f;
        OnCooldownChanged?.Invoke(1f);  // immediately show cooldown as full

        _playerController.IsAbilityCasting = false;
        _isCasting = false;
    }

    // ─── Aim Raycast ──────────────────────────────────────────────────────────

    /// <summary>
    /// Casts a ray from the center of the screen (where the crosshair sits).
    /// Returns the world-space point the player is aiming at.
    /// </summary>
    private Vector3 GetAimTargetPoint()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, fallbackRange, raycastMask))
            return hit.point;

        // Nothing hit — project to fallback distance along ray
        return ray.origin + ray.direction * fallbackRange;
    }

    // ─── Launch Velocity Calculation ──────────────────────────────────────────

    /// <summary>
    /// Computes the initial velocity needed to send the orb from muzzlePoint
    /// toward targetPoint, with an upward arc boost.
    ///
    /// Because Rigidbody gravity handles the parabola, we only need to give
    /// the orb an initial velocity vector. The arcBoost raises the vertical
    /// component proportionally to the horizontal distance — closer targets
    /// get a gentle lob, farther ones get a flatter but still arced shot.
    /// </summary>
    private Vector3 CalculateLaunchVelocity(Vector3 targetPoint)
    {
        Vector3 toTarget = targetPoint - muzzlePoint.position;
        Vector3 toTargetFlat = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDist = toTargetFlat.magnitude;

        float speed = fireData != null ? fireData.projectileSpeed : 18f;

        // Base direction toward target
        Vector3 direction = toTarget.normalized;

        // Add upward boost proportional to horizontal distance (arc shape)
        direction.y += arcBoost * (horizontalDist / fallbackRange);
        direction = direction.normalized;

        return direction * speed;
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (muzzlePoint == null || mainCamera == null) return;

        // Draw aim ray
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(ray.origin, ray.direction * fallbackRange);

        // Draw muzzle point
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(muzzlePoint.position, 0.1f);
    }
#endif
}