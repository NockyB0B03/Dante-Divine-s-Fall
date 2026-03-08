using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LegionEntity.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Comportamento della singola entità celeste spawned dall'Ultimate.
/// Vola verso la posizione fissa di Lucifero accelerando progressivamente.
/// Si auto-distrugge al contatto con Lucifero o allo scadere del lifetime.
///
/// MOVIMENTO:
///   Accelerazione continua verso targetPosition (fissa al momento dello spawn).
///   velocità = initialSpeed + acceleration * tempoTrascorso
///   Lucifero non si muove mai — targetPosition non viene aggiornata dopo lo spawn.
///
/// COLLISIONE:
///   OnTriggerEnter → chiama TakeDamage su Health di Lucifero + si disattiva.
///   Ignora collider del Player (layer "Player") e altri proiettili.
///
/// PREFAB SETUP:
///   - Rigidbody: Is Kinematic ✓ (movimento gestito via transform, non fisica)
///   - Collider: isTrigger ✓
///   - Layer: "PlayerProjectile" (non collidere con Dante)
///
/// INSPECTOR:
///   damage        → danno inflitto a Lucifero al contatto (default 15)
///   initialSpeed  → velocità di partenza (default 3)
///   acceleration  → incremento velocità per secondo (default 6)
///   lifetime      → secondi prima di auto-distruggersi se non colpisce (default 6)
///   luciferoLayer → LayerMask del layer "Enemy" per riconoscere Lucifero
/// </summary>
public class LegionEntity : MonoBehaviour
{
    [Header("Stats")]
    public float damage = 15f;
    public float initialSpeed = 3f;
    public float acceleration = 6f;
    public float lifetime = 6f;

    [Header("Layer")]
    [Tooltip("LayerMask del layer 'Enemy' — identifica Lucifero al contatto.")]
    public LayerMask luciferoLayer;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private Vector3 _targetPosition;   // posizione fissa di Lucifero — mai aggiornata
    private float _currentSpeed;
    private float _elapsedTime;
    private bool _hasHit;

    // ─── API pubblica — chiamata da UltimateAbility.cs ────────────────────────

    /// <summary>
    /// Inizializza l'entità con la posizione fissa del bersaglio.
    /// Chiamato da UltimateAbility.cs subito dopo lo spawn.
    /// </summary>
    public void Initialize(Vector3 targetPosition)
    {
        _targetPosition = targetPosition;
        _currentSpeed = initialSpeed;
        _elapsedTime = 0f;
        _hasHit = false;

        // Guarda subito verso il target
        Vector3 dir = (_targetPosition - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void OnEnable()
    {
        _hasHit = false;
        _elapsedTime = 0f;
        _currentSpeed = initialSpeed;
    }

    void Update()
    {
        if (_hasHit) return;

        _elapsedTime += Time.deltaTime;

        // Auto-distruzione per lifetime
        if (_elapsedTime >= lifetime)
        {
            Deactivate();
            return;
        }

        // Accelerazione progressiva: v = v0 + a*t
        _currentSpeed = initialSpeed + acceleration * _elapsedTime;

        // Movimento verso la posizione fissa di Lucifero
        Vector3 direction = (_targetPosition - transform.position).normalized;
        transform.position += direction * _currentSpeed * Time.deltaTime;

        // Ruota verso la direzione di movimento (effetto visivo)
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    // ─── Collisione ───────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        // Colpisce solo il layer Enemy (Lucifero)
        bool isEnemy = ((1 << other.gameObject.layer) & luciferoLayer) != 0;
        if (!isEnemy) return;

        _hasHit = true;

        Health health = other.GetComponent<Health>();
        if (health == null) health = other.GetComponentInParent<Health>();
        health?.TakeDamage(damage);

        Deactivate();
    }

    // ─── Disattivazione ───────────────────────────────────────────────────────
    private void Deactivate()
    {
        _hasHit = true;
        gameObject.SetActive(false);
    }
}