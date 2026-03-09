using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LuciferBoss.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Boss finale stazionario con 2 fasi di vita.
///
/// FASE 1 (HP > 50%):
///   - Fireball: lancia sfere di fuoco verso l'ultima posizione nota del player.
///   - Spin: se il player è nel raggio di rotazione, applica danno.
///
/// FASE 2 (HP ≤ 50%):
///   - Stessi attacchi con intervalli ridotti.
///   - Ground Spikes: fa crescere spuntoni in linea retta davanti a sé.
///
/// DIPENDENZE:
///   - Health.cs sulla stessa GameObject
///   - LuciferFireballPool nella scena (singleton)
///   - GroundSpike prefab assegnato in Inspector
///   - GameManager.Instance per ResetUltimateUses() alla transizione di fase
///
/// INSPECTOR:
///   phase2Threshold       → 0.5 (50% HP)
///   fireballCooldown      → 3 secondi (Fase 1)
///   spinCooldown          → 5 secondi
///   spinRadius            → 4 unità
///   spinDamage            → 20
///   spikeCooldown         → 6 secondi (Fase 1)
///   spikeCount            → numero di spuntoni per lancio
///   spikeSpacing          → distanza tra uno spuntone e l'altro
///   groundSpikePrefab     → prefab dello spuntone
///   fireballSpawnPoints   → Transform[] — punti di spawn dei fireball (bocca)
///   playerTransform       → assegnato automaticamente via GameManager in Awake
///
/// GERARCHIA CONSIGLIATA:
///   Lucifer (LuciferBoss, Health, Animator)
///   └── FireballSpawnPoint  ← Transform bocca
///   └── BossPortal.prefab   ← disabilitato, abilitato da OnDeath UnityEvent
/// </summary>

public class LuciferBoss : EnemyBase
{
    public enum Phase { One, Two }

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Fasi")]
    [Range(0f, 1f)]
    [Tooltip("Percentuale HP sotto la quale scatta la Fase 2.")]
    public float phase2Threshold = 0.5f;

    [Header("Fireball")]
    public Transform[] fireballSpawnPoints;
    [Tooltip("Secondi tra un lancio e l'altro in Fase 1.")]
    public float fireballCooldown = 3f;

    [Header("Spin Attack")]
    [Tooltip("Secondi tra uno spin e l'altro.")]
    public float spinCooldown = 5f;
    [Tooltip("Raggio entro cui il player viene colpito dallo spin.")]
    public float spinRadius = 4f;
    [Tooltip("Danno inflitto dallo spin.")]
    public float spinDamage = 20f;

    [Header("Ground Spikes (Fase 2)")]
    public GameObject groundSpikePrefab;
    [Tooltip("Secondi tra un attacco spikes e l'altro in Fase 1 (ridotto in Fase 2).")]
    public float spikeCooldown = 6f;
    [Tooltip("Numero di spuntoni per lancio.")]
    public int spikeCount = 6;
    [Tooltip("Distanza tra uno spuntone e l'altro lungo la linea.")]
    public float spikeSpacing = 1.5f;

    [Header("Fase 2 — Moltiplicatore velocità attacchi")]
    public float phase2SpeedMultiplier = 1.6f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private Phase _currentPhase = Phase.One;
    private bool _phaseTransitioning = false;

    private static readonly int _animSpin = Animator.StringToHash("Spin");
    private static readonly int _animRoar = Animator.StringToHash("Roar");

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();   // inizializza EnemyHealth, EnemyAnimator, PlayerTransform
    }

    protected override void Start()
    {
        base.Start();   // chiama FindPlayer() — PlayerTransform è disponibile qui

        EnemyHealth.OnHealthChanged.AddListener(CheckPhaseTransition);
        StartCoroutine(FireballLoop());
        StartCoroutine(SpinLoop());
        // Gli spikes partono solo in Fase 2 — avviati in EnterPhaseTwo()
    }

    void OnDisable()
    {
        EnemyHealth.OnHealthChanged.RemoveListener(CheckPhaseTransition);
    }

    // ─── Override morte ──────────────────────────────────────────────────────
    protected override void OnDeath()
    {
        // Il portale verso Level 6 viene spawnato dalla UnityEvent OnDeath
        // di Health.cs — configurata in Inspector su Lucifero.
        // Qui fermiamo le coroutine degli attacchi.
        StopAllCoroutines();
    }

    // ─── Transizione di Fase ──────────────────────────────────────────────────
    private void CheckPhaseTransition(float currentHP)
    {
        if (_currentPhase == Phase.One
            && EnemyHealth.Percent <= phase2Threshold
            && !_phaseTransitioning)
        {
            StartCoroutine(EnterPhaseTwo());
        }
    }

    private IEnumerator EnterPhaseTwo()
    {
        _phaseTransitioning = true;
        _currentPhase = Phase.Two;

        // Notifica GameManager → resetta i 2 usi dell'Ultimate
        GameManager.Instance?.ResetUltimateUses();

        // Animazione ruggito — pausa attacchi durante la transizione
        EnemyAnimator?.SetTrigger(_animRoar);
        yield return new WaitForSeconds(2f);

        // Aumenta frequenza attacchi
        fireballCooldown /= phase2SpeedMultiplier;
        spinCooldown /= phase2SpeedMultiplier;
        spikeCooldown /= phase2SpeedMultiplier;

        // Avvia attacco spikes esclusivo della Fase 2
        StartCoroutine(SpikeLoop());

        _phaseTransitioning = false;
    }

    // ─── Fireball Loop ────────────────────────────────────────────────────────
    private IEnumerator FireballLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireballCooldown);
            if (_phaseTransitioning || PlayerTransform == null) continue;
            if (LuciferFireballPool.Instance == null)
            {
                Debug.LogError("[LuciferBoss] LuciferFireballPool non trovato nella scena!");
                continue;
            }

            // Snapshot della posizione del player al momento del lancio
            Vector3 lastKnownPlayerPos = PlayerTransform.position;

            foreach (var spawnPt in fireballSpawnPoints)
            {
                GameObject fb = LuciferFireballPool.Instance.Get(
                    spawnPt.position, spawnPt.rotation);

                LuciferFireball fireball = fb.GetComponent<LuciferFireball>();
                fireball?.LaunchToward(lastKnownPlayerPos);
            }
        }
    }

    // ─── Spin Loop ────────────────────────────────────────────────────────────
    private IEnumerator SpinLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spinCooldown);
            if (_phaseTransitioning || PlayerTransform == null) continue;

            EnemyAnimator?.SetTrigger(_animSpin);

            // Controlla se il player è nel raggio
            float dist = Vector3.Distance(transform.position, PlayerTransform.position);
            if (dist <= spinRadius)
            {
                Health playerHealth = PlayerTransform?.GetComponent<Health>();
                playerHealth?.TakeDamage(spinDamage);
            }
        }
    }

    // ─── Spike Loop (solo Fase 2) ─────────────────────────────────────────────
    private IEnumerator SpikeLoop()
    {
        while (_currentPhase == Phase.Two)
        {
            yield return new WaitForSeconds(spikeCooldown);
            if (_phaseTransitioning) continue;

            SpawnGroundSpikes();
        }
    }

    private void SpawnGroundSpikes()
    {
        if (groundSpikePrefab == null) return;

        // Direzione casuale nell'arco frontale di Lucifero (180°)
        float randomAngle = Random.Range(-90f, 90f);
        Vector3 spikeDir = Quaternion.Euler(0f, randomAngle, 0f) * transform.forward;
        spikeDir.y = 0f;
        spikeDir.Normalize();

        // Spawna gli spuntoni in linea retta partendo da Lucifero verso l'esterno
        for (int i = 0; i < spikeCount; i++)
        {
            Vector3 spawnPos = transform.position + spikeDir * (spikeSpacing * (i + 1));
            spawnPos.y = 0f;   // a livello del terreno
            Instantiate(groundSpikePrefab, spawnPos, Quaternion.LookRotation(spikeDir));
        }
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Spin radius
        UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.2f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, spinRadius);
    }
#endif
}