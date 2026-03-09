using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — Projectile.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Componente base per tutti i proiettili del gioco.
/// Attach su ogni prefab proiettile (FireOrb, LuciferFireball, ecc.).
/// Richiede Rigidbody (parabola fisica) e un Trigger Collider.
///
/// ESTENSIONE:
///   LuciferFireball.cs estende questa classe e fa override di OnContactHit()
///   per implementare il danno ad area invece del danno diretto.
///
/// RIGIDBODY SETUP (sul prefab):
///   Mass                → 0.1
///   Use Gravity         → ✓
///   Is Kinematic        → ✗
///   Collision Detection → Continuous
///   Constraints         → Freeze Rotation X Y Z
///
/// COLLIDER SETUP:
///   SphereCollider, isTrigger = ✓, radius = 0.2
///
/// INSPECTOR:
///   damage           → danno applicato al bersaglio (default 20)
///   damageableLayers → layer che ricevono danno (solo "Enemy" per FireOrb)
///   impactVFXPrefab  → particella spawned all'impatto (opzionale)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Danno applicato al bersaglio al contatto.")]
    public float damage = 20f;

    [Header("Layer Mask")]
    [Tooltip("Layer che ricevono danno. FireOrb → 'Enemy'. LuciferFireball usa playerLayer separato.")]
    public LayerMask damageableLayers;

    [Header("VFX (opzionale)")]
    [Tooltip("Prefab particella spawned al punto di impatto.")]
    public GameObject impactVFXPrefab;

    // Assegnato automaticamente da ProjectilePool.CreateInstance()
    [HideInInspector]
    public ProjectilePool OwnerPool;

    protected Rigidbody _rb;
    private bool _hasHit = false;
    private float _spawnTimer = 0f;          // ignora collisioni nei primi frame
    private const float SpawnGrace = 0.15f;   // secondi di grazia dopo lo spawn

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Resetta lo stato ogni volta che il pool riattiva questo oggetto.
    /// </summary>
    protected virtual void OnEnable()
    {
        _hasHit = false;
        _spawnTimer = 0f;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    protected virtual void Update()
    {
        if (_spawnTimer < SpawnGrace)
            _spawnTimer += Time.deltaTime;
    }

    // ─── Launch ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Applica la velocità iniziale al Rigidbody.
    /// Chiamato da FireAbility.cs e LuciferFireball.LaunchToward().
    /// </summary>
    public void Launch(Vector3 velocity)
    {
        _rb.velocity = velocity;
    }

    // ─── Collision ────────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (_spawnTimer < SpawnGrace) return;   // grazia spawn — ignora collisioni iniziali
        _hasHit = true;

        // Chiama il metodo virtuale — LuciferFireball lo sovrascrive per splash
        OnContactHit(transform.position);

        ReturnToPool();
    }

    /// <summary>
    /// Comportamento base: danno diretto tramite OverlapSphere minima sul punto di contatto.
    /// LuciferFireball.cs fa override per danno ad area con raggio maggiore.
    /// </summary>
    protected virtual void OnContactHit(Vector3 contactPoint)
    {
        Collider[] hits = Physics.OverlapSphere(contactPoint, 0.3f, damageableLayers);
        foreach (var col in hits)
        {
            Health health = col.GetComponent<Health>();
            if (health == null)
                health = col.GetComponentInParent<Health>();

            if (health != null)
            {
                health.TakeDamage(damage);
                break;   // danno diretto: solo il primo bersaglio
            }
        }

        SpawnImpactVFX(contactPoint);
    }

    // ─── VFX ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawna il prefab VFX al punto di contatto.
    /// Chiamabile anche dalle sottoclassi.
    /// </summary>
    protected void SpawnImpactVFX(Vector3 position)
    {
        if (impactVFXPrefab != null)
            Instantiate(impactVFXPrefab, position, Quaternion.identity);
    }

    // ─── Return to Pool ───────────────────────────────────────────────────────
    protected void ReturnToPool()
    {
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        if (OwnerPool != null)
            OwnerPool.Return(gameObject);
        else
        {
            Debug.LogWarning("[Projectile] OwnerPool non assegnato — distruzione fallback.");
            Destroy(gameObject);
        }
    }
}