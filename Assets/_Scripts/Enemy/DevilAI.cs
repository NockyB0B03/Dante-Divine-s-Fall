using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DANTE: DIVINE'S FALL — DevilAI.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// State machine a 4 stati per i diavoli del Level 3.
/// Eredita da EnemyBase → ha già Health, EnemyAnimator, PlayerTransform,
/// IsPlayerInSight(), DistanceToPlayer(), DirectionToPlayer().
///
/// STATI:
///   Idle     → ruota su se stesso orario/antiorario alternati, cerca il player
///   Follow   → NavMeshAgent insegue il player guardandolo sempre
///   Hit      → OnCollisionEnter con Dante → danno singolo → entra in Fallback
///   Fallback → fermo per fallbackDuration secondi → torna a Idle o Follow
///
/// TRANSIZIONI:
///   Idle     → Follow    : IsPlayerInSight() == true
///   Follow   → Idle      : player fuori da detectionRange (con isteresi)
///   Follow   → Hit       : OnCollisionEnter con tag "Player"
///   Hit      → Fallback  : automatico dopo il danno
///   Fallback → Idle      : timer scaduto + player fuori sight
///   Fallback → Follow    : timer scaduto + player ancora in sight
///
/// ANIMATOR PARAMETERS:
///   Float   "Speed"   → 0 (fermo) / 1 (cammina)
///   Trigger "Attack"  → animazione colpo forcone
///   Trigger "Death"   → gestito da EnemyBase.DeathSequence()
///
/// INSPECTOR:
///   contactDamage     → danno inflitto a Dante al contatto (default 15)
///   fallbackDuration  → secondi di pausa dopo il colpo (default 2)
///   detectionRange    → distanza massima di rilevamento (default 10)
///   detectionAngle    → ampiezza del cono visivo in gradi (default 90)
///   loseRange         → distanza a cui il diavolo perde il player (default 15)
///   followSpeed       → velocità NavMeshAgent in Follow (default 4)
///   idleRotateSpeed   → gradi/secondo di rotazione in Idle (default 30)
///   idleRotateDuration→ secondi per ogni semi-rotazione orario/antiorario (default 2)
///
/// PREFAB SETUP:
///   NavMeshAgent:
///     Speed         → uguale a followSpeed
///     Angular Speed → 240
///     Stopping Distance → 0.5
///     Auto Braking  → ✓
///   Collider: CapsuleCollider (non trigger) sul root
///   Layer: "Enemy"
///   Tag:   "Enemy"
///
/// GERARCHIA:
///   Devil_Melee (DevilAI, NavMeshAgent, CapsuleCollider)
///   └── Devil_Visuals (Animator, Skinned Mesh)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DevilAI : EnemyBase
{
    // ─── Stato ────────────────────────────────────────────────────────────────
    public enum State { Idle, Follow, Hit, Fallback }

    [Header("State (read-only in Inspector)")]
    [SerializeField] private State _currentState = State.Idle;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Combat")]
    [Tooltip("Danno inflitto a Dante al contatto fisico.")]
    public float contactDamage = 15f;

    [Tooltip("Secondi di pausa dopo aver colpito Dante.")]
    public float fallbackDuration = 2f;

    [Header("Detection")]
    [Tooltip("Distanza massima entro cui il diavolo può rilevare il player.")]
    public float detectionRange = 10f;

    [Tooltip("Ampiezza del cono visivo in gradi (90 = ±45° dal forward).")]
    public float detectionAngle = 90f;

    [Tooltip("Distanza a cui il diavolo smette di seguire il player (isteresi).")]
    public float loseRange = 15f;

    [Header("Movement")]
    [Tooltip("Velocità di movimento durante il Follow.")]
    public float followSpeed = 4f;

    [Header("Idle Rotation")]
    [Tooltip("Velocità di rotazione in Idle in gradi/secondo.")]
    public float idleRotateSpeed = 30f;

    [Tooltip("Secondi per ogni semi-rotazione orario/antiorario.")]
    public float idleRotateDuration = 2f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private NavMeshAgent _agent;

    // Animator parameter IDs
    private static readonly int _animSpeed = Animator.StringToHash("Speed");
    private static readonly int _animAttack = Animator.StringToHash("Attack");

    // Idle rotation
    private float _idleRotateTimer;
    private int _idleRotateDirection = 1;   // 1 = orario, -1 = antiorario

    // Hit guard — evita danni multipli per lo stesso contatto
    private bool _hasHitThisContact = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = followSpeed;
        _agent.isStopped = true;
    }

    void Update()
    {
        if (IsDead) return;

        switch (_currentState)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Follow: UpdateFollow(); break;
            case State.Fallback: UpdateFallback(); break;
                // Hit è gestito da OnCollisionEnter + coroutine — non serve Update
        }

        UpdateAnimator();
    }

    // ─── Stati ────────────────────────────────────────────────────────────────

    private void UpdateIdle()
    {
        // Rotazione guardinga orario/antiorario alternata
        _idleRotateTimer += Time.deltaTime;
        transform.Rotate(0f, idleRotateSpeed * _idleRotateDirection * Time.deltaTime, 0f);

        if (_idleRotateTimer >= idleRotateDuration)
        {
            _idleRotateTimer = 0f;
            _idleRotateDirection = -_idleRotateDirection;   // inverti direzione
        }

        // Controlla se il player è entrato nel cono visivo
        if (IsPlayerInSight(detectionRange, detectionAngle))
            EnterFollow();
    }

    private void UpdateFollow()
    {
        if (PlayerTransform == null) return;

        // Aggiorna destinazione NavMesh ogni frame
        _agent.SetDestination(PlayerTransform.position);

        // Ruota verso il player sul piano XZ
        Vector3 dir = DirectionToPlayer();
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);

        // Perde il player se è troppo lontano (isteresi: loseRange > detectionRange)
        if (DistanceToPlayer() > loseRange)
            EnterIdle();
    }

    private void UpdateFallback()
    {
        // Il timer è gestito dalla coroutine FallbackRoutine()
        // Qui aggiorniamo solo la rotazione verso il player se ancora in vista
        if (PlayerTransform != null)
        {
            Vector3 dir = DirectionToPlayer();
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    3f * Time.deltaTime);
        }
    }

    // ─── Transizioni di Stato ─────────────────────────────────────────────────
    private void EnterIdle()
    {
        _currentState = State.Idle;
        _agent.isStopped = true;
        _agent.ResetPath();
        _idleRotateTimer = 0f;
    }

    private void EnterFollow()
    {
        _currentState = State.Follow;
        _agent.isStopped = false;
        _agent.speed = followSpeed;
    }

    private void EnterHit(Collider playerCollider)
    {
        if (_currentState == State.Hit || _currentState == State.Fallback) return;
        if (_hasHitThisContact) return;

        _hasHitThisContact = true;
        _currentState = State.Hit;

        // Ferma il NavMeshAgent durante l'attacco
        _agent.isStopped = true;
        _agent.ResetPath();

        // Trigger animazione attacco
        EnemyAnimator?.SetTrigger(_animAttack);

        // Applica danno tramite Health.cs del player
        Health playerHealth = playerCollider.GetComponent<Health>();
        if (playerHealth == null)
            playerHealth = playerCollider.GetComponentInParent<Health>();
        playerHealth?.TakeDamage(contactDamage);

        StartCoroutine(FallbackRoutine());
    }

    private void EnterFallback()
    {
        _currentState = State.Fallback;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    // ─── Coroutine Fallback ───────────────────────────────────────────────────
    private IEnumerator FallbackRoutine()
    {
        EnterFallback();

        yield return new WaitForSeconds(fallbackDuration);

        if (IsDead) yield break;

        // Dopo la pausa: torna a Follow se il player è ancora in vista, altrimenti Idle
        if (IsPlayerInSight(detectionRange, detectionAngle))
            EnterFollow();
        else
            EnterIdle();
    }

    // ─── Collisione con Dante ─────────────────────────────────────────────────
    void OnCollisionEnter(Collision collision)
    {
        if (IsDead) return;
        if (!collision.collider.CompareTag("Player")) return;

        EnterHit(collision.collider);
    }

    void OnCollisionExit(Collision collision)
    {
        // Reset del guard quando Dante non è più a contatto
        // permette un nuovo colpo al prossimo contatto
        if (collision.collider.CompareTag("Player"))
            _hasHitThisContact = false;
    }

    // ─── Animator ─────────────────────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (EnemyAnimator == null) return;

        // Speed: 1 se il NavMeshAgent si sta muovendo, 0 altrimenti
        float speed = (_agent.hasPath && !_agent.isStopped)
            ? _agent.velocity.magnitude / followSpeed
            : 0f;

        EnemyAnimator.SetFloat(_animSpeed, speed);
    }

    // ─── Override morte (da EnemyBase) ────────────────────────────────────────
    protected override void OnDeath()
    {
        // Ferma il NavMeshAgent alla morte
        _agent.isStopped = true;
        _agent.ResetPath();
        StopAllCoroutines();
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Detection range
        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.1f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, detectionRange);

        // Lose range
        UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.08f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, loseRange);

        // Cono visivo
        Vector3 leftDir = Quaternion.Euler(0, -detectionAngle * 0.5f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, detectionAngle * 0.5f, 0) * transform.forward;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, leftDir * detectionRange);
        Gizmos.DrawRay(transform.position, rightDir * detectionRange);

        // Stato corrente sopra il GameObject
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.2f,
            $"State: {_currentState}");
    }
#endif
}