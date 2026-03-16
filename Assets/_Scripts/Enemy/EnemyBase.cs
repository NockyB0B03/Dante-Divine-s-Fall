using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — EnemyBase.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Classe base astratta condivisa da DevilAI.cs e LuciferBoss.cs.
/// Gestisce tutto ciò che ogni nemico ha in comune:
///   - Riferimento al player via GameManager
///   - Health con listener automatico alla morte
///   - Animator cached
///   - Sequenza di morte: trigger animazione → distruggi dopo X secondi
///
/// COME USARLA:
///   public class DevilAI : EnemyBase { ... }
///   public class LuciferBoss : EnemyBase { ... }
///
///   Le sottoclassi chiamano base.Awake() se fanno override di Awake().
///   Possono fare override di OnDeath() per aggiungere comportamenti
///   specifici prima che il GameObject venga distrutto.
///
/// INSPECTOR (su ogni sottoclasse):
///   maxHealth        → ereditato via Health.cs (assegnato in Awake)
///   destroyDelay     → secondi tra animazione morte e Destroy() (default 2)
///   deathAnimTrigger → nome del trigger nell'Animator (default "Death")
///
/// GERARCHIA CONSIGLIATA:
///   Devil_Melee (EnemyBase/DevilAI, Health, NavMeshAgent)
///   └── Devil_Visuals (Animator, Skinned Mesh)
///
/// NOTA: Health.cs NON va aggiunto manualmente — EnemyBase lo aggiunge
/// via RequireComponent e lo configura in Awake().
/// </summary>
[RequireComponent(typeof(Health))]
public abstract class EnemyBase : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Enemy Base Config")]
    [Tooltip("Secondi tra il trigger dell'animazione di morte e la distruzione del GameObject.")]
    public float destroyDelay = 2f;

    [Tooltip("Nome esatto del trigger di morte nell'Animator Controller.")]
    public string deathAnimTrigger = "Death";

    // ─── Proprietà protette — accessibili dalle sottoclassi ───────────────────
    protected Health EnemyHealth { get; private set; }
    protected Animator EnemyAnimator { get; private set; }
    protected Transform PlayerTransform { get; private set; }

    /// <summary>True dal momento in cui OnDeath() viene chiamato.</summary>
    protected bool IsDead { get; private set; } = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected virtual void Awake()
    {
        EnemyHealth = GetComponent<Health>();
        EnemyAnimator = GetComponentInChildren<Animator>();

        // Iscrive la morte — non usare destroyOnDeath di Health.cs,
        // gestiamo noi la sequenza per avere l'animazione
        EnemyHealth.OnDeath.AddListener(HandleDeath);
    }

    protected virtual void Start()
    {
        StartCoroutine(FindPlayerDelayed());
    }

    private IEnumerator FindPlayerDelayed()
    {
        // Aspetta un frame — garantisce che PlayerSpawnPoint.Awake()
        // e GameManager.RegisterPlayer() siano già stati eseguiti
        yield return null;
        FindPlayer();
    }

    /// <summary>
    /// Cerca il player tramite GameManager.
    /// Chiamabile anche dalle sottoclassi se PlayerTransform è null.
    /// </summary>
    protected void FindPlayer()
    {
        if (GameManager.Instance?.Player != null)
            PlayerTransform = GameManager.Instance.Player.transform;
        else
            Debug.LogWarning($"[{GetType().Name}] Player non trovato tramite GameManager.");
    }

    protected virtual void OnDestroy()
    {
        // Rimuove il listener per evitare chiamate su oggetti distrutti
        if (EnemyHealth != null)
            EnemyHealth.OnDeath.RemoveListener(HandleDeath);
    }

    // ─── Morte ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Listener su Health.OnDeath — avvia la sequenza di morte.
    /// Non fare override di questo metodo — usa OnDeath() invece.
    /// </summary>
    private void HandleDeath()
    {
        if (IsDead) return;
        IsDead = true;

        // Chiama il comportamento specifico della sottoclasse prima dell'animazione
        OnDeath();

        StartCoroutine(DeathSequence());
    }

    /// <summary>
    /// Override nelle sottoclassi per aggiungere comportamenti alla morte:
    /// es. DevilAI ferma il NavMeshAgent, LuciferBoss spawna il portale.
    /// Viene chiamato PRIMA dell'animazione di morte.
    /// </summary>
    protected virtual void OnDeath() { }

    /// <summary>
    /// Sequenza di morte:
    ///   1. Disabilita collider per evitare danni post-mortem
    ///   2. Trigger animazione morte
    ///   3. Attendi destroyDelay secondi
    ///   4. Distruggi il GameObject
    /// </summary>
    private IEnumerator DeathSequence()
    {
        // Disabilita tutti i collider per evitare interazioni post-mortem
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Trigger animazione morte
        if (EnemyAnimator != null && !string.IsNullOrEmpty(deathAnimTrigger))
            EnemyAnimator.SetTrigger(deathAnimTrigger);

        yield return new WaitForSeconds(destroyDelay);

        Destroy(gameObject);
    }

    // ─── Utility protette — usabili dalle sottoclassi ─────────────────────────

    /// <summary>
    /// Distanza dal player. Restituisce float.MaxValue se il player non è trovato.
    /// </summary>
    protected float DistanceToPlayer()
    {
        if (PlayerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, PlayerTransform.position);
    }

    /// <summary>
    /// Direzione normalizzata verso il player sul piano XZ (Y = 0).
    /// Restituisce Vector3.zero se il player non è trovato.
    /// </summary>
    protected Vector3 DirectionToPlayer()
    {
        if (PlayerTransform == null) return Vector3.zero;
        Vector3 dir = PlayerTransform.position - transform.position;
        dir.y = 0f;
        return dir.normalized;
    }

    /// <summary>
    /// True se il player è nel cono visivo del nemico.
    /// </summary>
    /// <param name="detectionRange">Distanza massima di rilevamento.</param>
    /// <param name="detectionAngle">Angolo del cono in gradi (es. 90 = ±45° dal forward).</param>
    protected bool IsPlayerInSight(float detectionRange, float detectionAngle)
    {
        if (PlayerTransform == null) return false;
        if (DistanceToPlayer() > detectionRange) return false;

        Vector3 dirToPlayer = (PlayerTransform.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        return angle <= detectionAngle * 0.5f;
    }
}