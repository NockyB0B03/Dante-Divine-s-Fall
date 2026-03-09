using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — Arrow.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Freccia lanciata da ArrowTrap.cs.
/// Vola in linea retta a velocità costante nella direzione del suo forward.
/// NON usa Rigidbody — movimento via Transform per controllo totale.
///
/// COMPORTAMENTO:
///   - Vola per un massimo di lifetime secondi poi torna al pool
///   - Colpisce Dante → danno istantaneo → ritorna al pool immediatamente
///   - Ignora muri e terreno — ci pensa il lifetime
///
/// COLLIDER SETUP (sul prefab Arrow):
///   CapsuleCollider, isTrigger ✓
///   Direction: Z-Axis (asse di volo)
///   Layer: "Arrow"
///
/// INSPECTOR:
///   damage      → danno inflitto a Dante (default 10)
///   speed       → velocità in unità/secondo (default 15)
///   lifetime    → secondi di vita massima prima di tornare al pool (default 5)
///   playerLayer → LayerMask del layer "Player"
/// </summary>
[RequireComponent(typeof(Collider))]
public class Arrow : MonoBehaviour
{
    [Header("Stats")]
    public float damage = 10f;
    public float speed = 15f;
    public float lifetime = 5f;

    [Header("Layers")]
    [Tooltip("LayerMask del Player — riceve danno al contatto.")]
    public LayerMask playerLayer;

    [HideInInspector]
    public ArrowPool OwnerPool;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private bool _hasHit = false;
    private float _elapsed = 0f;
    private Collider _collider;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
    }

    void OnEnable()
    {
        _hasHit = false;
        _elapsed = 0f;
        _collider.enabled = true;
    }

    void Update()
    {
        if (_hasHit) return;

        // Lifetime — torna al pool dopo N secondi
        _elapsed += Time.deltaTime;
        if (_elapsed >= lifetime)
        {
            ReturnToPool();
            return;
        }

        // Volo in linea retta lungo il forward locale
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    // ─── Collisione ───────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (other.GetComponent<Arrow>() != null) return;   // ignora altre frecce

        bool isPlayer = ((1 << other.gameObject.layer) & playerLayer) != 0;
        if (!isPlayer) return;   // ignora muri e terreno — ci pensa il lifetime

        _hasHit = true;
        _collider.enabled = false;

        Health health = other.GetComponent<Health>();
        if (health == null) health = other.GetComponentInParent<Health>();
        health?.TakeDamage(damage);

        ReturnToPool();
    }

    // ─── Pool ─────────────────────────────────────────────────────────────────
    private void ReturnToPool()
    {
        _hasHit = true;
        if (OwnerPool != null)
            OwnerPool.Return(gameObject);
        else
        {
            Debug.LogWarning("[Arrow] OwnerPool non assegnato — distruzione fallback.");
            Destroy(gameObject);
        }
    }
}