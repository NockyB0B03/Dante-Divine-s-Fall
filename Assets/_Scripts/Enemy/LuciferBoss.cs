using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LuciferBoss.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Boss finale con FSM a 6 stati.
///
/// STATI:
///   Idle            → aspetta il cooldown del prossimo attacco disponibile
///   Fireball        → lancia sfere di fuoco verso il player
///   TailAttack      → colpo di coda (carica + sweep 360°)
///   SpikeAttack     → spawna spuntoni (solo Fase 2)
///   PhaseTransition → ruggito + potenziamento (HP ≤ 50%)
///   Death           → morte
///
/// TRANSIZIONI:
///   Idle → Fireball    : cooldown fireball scaduto
///   Idle → TailAttack  : cooldown tail scaduto
///   Idle → SpikeAttack : cooldown spike scaduto (solo Fase 2)
///   Idle → PhaseTransition : HP ≤ 50% e fase == One
///   Qualsiasi → Death  : HP == 0
///   Qualsiasi → Idle   : attacco completato
///
/// REGOLA FONDAMENTALE:
///   Un solo attacco alla volta — lo stato cambia solo quando l'attacco corrente
///   è completato. I cooldown scorrono sempre, anche durante un attacco.
/// </summary>
public class LuciferBoss : EnemyBase
{
    // ─── FSM ──────────────────────────────────────────────────────────────────
    public enum BossState { Idle, Fireball, TailAttack, SpikeAttack, PhaseTransition, Death }

    [Header("Stato (read-only)")]
    [SerializeField] private BossState _currentState = BossState.Idle;

    public enum Phase { One, Two }
    private Phase _currentPhase = Phase.One;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Fasi")]
    [Range(0f, 1f)]
    public float phase2Threshold = 0.5f;
    public float phase2SpeedMultiplier = 1.6f;

    [Header("Fireball")]
    public Transform[] fireballSpawnPoints;
    public float fireballCooldown = 3f;

    [Header("Tail Attack")]
    public float tailCooldown = 5f;
    public TailAttack tailAttack;

    [Header("Ground Spikes (Fase 2)")]
    public GameObject groundSpikePrefab;
    public float spikeCooldown = 6f;
    public int spikeCount = 6;
    public float spikeSpacing = 1.5f;

    [Header("Rotazione verso player")]
    public float rotationSpeed = 90f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private float _fireballTimer = 0f;
    private float _tailTimer = 0f;
    private float _spikeTimer = 0f;

    private bool _phaseTransitionDone = false;

    private static readonly int _animRoar = Animator.StringToHash("Roar");

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();

        if (tailAttack == null)
            tailAttack = GetComponentInChildren<TailAttack>();

        EnemyHealth.OnHealthChanged.AddListener(OnHealthChanged);
    }

    void OnDisable()
    {
        EnemyHealth.OnHealthChanged.RemoveListener(OnHealthChanged);
    }

    void Update()
    {
        if (IsDead || PlayerTransform == null) return;

        // Cooldown sempre attivi — anche durante un attacco
        _fireballTimer += Time.deltaTime;
        _tailTimer += Time.deltaTime;
        _spikeTimer += Time.deltaTime;

        // Rotazione verso il player solo in Idle
        if (_currentState == BossState.Idle)
            RotateTowardPlayer();

        // Transizioni dalla FSM
        UpdateFSM();
    }

    // ─── FSM ──────────────────────────────────────────────────────────────────
    private void UpdateFSM()
    {
        // Solo da Idle si può entrare in un nuovo stato
        if (_currentState != BossState.Idle) return;

        // Controlla phase transition
        if (!_phaseTransitionDone &&
            EnemyHealth.Percent <= phase2Threshold &&
            _currentPhase == Phase.One)
        {
            EnterState(BossState.PhaseTransition);
            return;
        }

        // Priorità attacchi:
        // FASE 1: Tail → Fireball
        // FASE 2: Spike → Fireball → Tail
        // Le spike hanno priorità massima in Fase 2 perché hanno cooldown maggiore
        if (_currentPhase == Phase.Two && _spikeTimer >= spikeCooldown)
        {
            EnterState(BossState.SpikeAttack);
            return;
        }

        if (_fireballTimer >= fireballCooldown)
        {
            EnterState(BossState.Fireball);
            return;
        }

        if (_tailTimer >= tailCooldown)
        {
            EnterState(BossState.TailAttack);
            return;
        }
    }

    private void EnterState(BossState newState)
    {
        _currentState = newState;

        switch (newState)
        {
            case BossState.Fireball:
                _fireballTimer = 0f;
                StartCoroutine(FireballRoutine());
                break;

            case BossState.TailAttack:
                _tailTimer = 0f;
                StartCoroutine(TailRoutine());
                break;

            case BossState.SpikeAttack:
                _spikeTimer = 0f;
                StartCoroutine(SpikeRoutine());
                break;

            case BossState.PhaseTransition:
                StartCoroutine(PhaseTransitionRoutine());
                break;

            case BossState.Death:
                OnDeath();
                break;
        }
    }

    private void ExitToIdle()
    {
        _currentState = BossState.Idle;
    }

    // ─── Routines ─────────────────────────────────────────────────────────────
    private IEnumerator FireballRoutine()
    {
        if (PlayerTransform == null) { ExitToIdle(); yield break; }
        if (LuciferFireballPool.Instance == null)
        {
            Debug.LogError("[LuciferBoss] LuciferFireballPool non trovato!");
            ExitToIdle();
            yield break;
        }

        Vector3 lastKnownPos = PlayerTransform.position;

        foreach (var spawnPt in fireballSpawnPoints)
        {
            if (spawnPt == null) continue;
            GameObject fb = LuciferFireballPool.Instance.Get(spawnPt.position, spawnPt.rotation);
            LuciferFireball fireball = fb.GetComponent<LuciferFireball>();
            fireball?.LaunchToward(lastKnownPos);
        }

        // Piccola pausa dopo il lancio prima di tornare Idle
        yield return new WaitForSeconds(0.5f);
        ExitToIdle();
    }

    private IEnumerator TailRoutine()
    {
        if (tailAttack == null) { ExitToIdle(); yield break; }

        yield return StartCoroutine(tailAttack.PerformAttack(PlayerTransform));

        // Ritorno graduale verso il player dopo il colpo
        float returnDuration = 1f;
        float elapsed = 0f;
        while (elapsed < returnDuration && PlayerTransform != null)
        {
            elapsed += Time.deltaTime;
            RotateTowardPlayer();
            yield return null;
        }

        ExitToIdle();
    }

    private IEnumerator SpikeRoutine()
    {
        SpawnGroundSpikes();
        yield return new WaitForSeconds(0.5f);
        ExitToIdle();
    }

    private IEnumerator PhaseTransitionRoutine()
    {
        _phaseTransitionDone = true;
        _currentPhase = Phase.Two;

        GameManager.Instance?.ResetUltimateUses();
        EnemyAnimator?.SetTrigger(_animRoar);
        yield return new WaitForSeconds(2f);

        // Aumenta frequenza attacchi
        fireballCooldown /= phase2SpeedMultiplier;
        tailCooldown /= phase2SpeedMultiplier;
        spikeCooldown /= phase2SpeedMultiplier;

        ExitToIdle();
    }

    // ─── Health Changed ───────────────────────────────────────────────────────
    private void OnHealthChanged(float currentHP)
    {
        // La phase transition è gestita dalla FSM in UpdateFSM
        // Questo listener non fa nulla — teniamo per compatibilità
    }

    // ─── Override morte ───────────────────────────────────────────────────────
    protected override void OnDeath()
    {
        _currentState = BossState.Death;
        StopAllCoroutines();
    }

    // ─── Rotazione ────────────────────────────────────────────────────────────
    private void RotateTowardPlayer()
    {
        if (PlayerTransform == null) return;

        Vector3 dir = PlayerTransform.position - transform.position;
        dir.y = 0f;
        if (dir == Vector3.zero) return;

        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, rotationSpeed * Time.deltaTime);
    }

    // ─── Ground Spikes ────────────────────────────────────────────────────────
    private void SpawnGroundSpikes()
    {
        if (groundSpikePrefab == null) return;

        float randomAngle = Random.Range(-90f, 90f);
        Vector3 spikeDir = Quaternion.Euler(0f, randomAngle, 0f) * transform.forward;
        spikeDir.y = 0f;
        spikeDir.Normalize();

        for (int i = 0; i < spikeCount; i++)
        {
            Vector3 candidatePos = transform.position + spikeDir * (spikeSpacing * (i + 1));

            RaycastHit hit;
            Vector3 spawnPos;
            if (Physics.Raycast(candidatePos + Vector3.up * 10f, Vector3.down, out hit, 20f))
                spawnPos = hit.point;
            else
                spawnPos = new Vector3(candidatePos.x, transform.position.y, candidatePos.z);

            Instantiate(groundSpikePrefab, spawnPos, Quaternion.LookRotation(spikeDir));
        }
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Forza Fase 2")]
    private void Debug_ForcePhase2()
    {
        if (!Application.isPlaying) return;
        EnemyHealth.TakeDamage(EnemyHealth.Current * 0.6f);
    }

    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.2f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, 4f);

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.5f,
            $"State: {_currentState} | Phase: {_currentPhase}");
    }
#endif
}