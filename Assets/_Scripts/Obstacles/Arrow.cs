using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — Arrow.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Freccia lanciata da ArrowTrap.cs.
/// Vola in linea retta a velocità costante nella direzione del suo forward.
/// NON usa Rigidbody — movimento via Transform per controllo totale.
///
/// COMPORTAMENTO AL CONTATTO:
///   - Colpisce Dante → danno istantaneo → ritorna al pool immediatamente
///   - Colpisce muro/terreno → si ferma, rimane 1 secondo, poi ritorna al pool
///
/// COLLIDER SETUP (sul prefab Arrow):
///   CapsuleCollider, isTrigger ✓
///   Direction: Z-Axis (asse di volo)
///   Layer: "Arrow" (non collidere con altre frecce)
///
/// INSPECTOR:
///   damage        → danno inflitto a Dante (default 10)
///   speed         → velocità in unità/secondo (default 15)
///   wallStickTime → secondi di permanenza su muro prima del ritorno al pool (default 1)
///   playerLayer   → LayerMask del layer "Player"
/// </summary>
[RequireComponent(typeof(Collider))]
public class Arrow : MonoBehaviour
{
    [Header("Stats")]
    public float damage = 10f;
    public float speed = 15f;
    public float wallStickTime = 1f;

    [Header("Layers")]
    [Tooltip("LayerMask del Player — riceve danno al contatto.")]
    public LayerMask playerLayer;

    // Assegnato da ArrowPool.CreateInstance()
    [HideInInspector]
    public ArrowPool OwnerPool;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private bool _hasHit = false;
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
        _collider.enabled = true;
    }

    void Update()
    {
        if (_hasHit) return;

        // Volo in linea retta lungo il forward locale
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    // ─── Collisione ───────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        // Ignora altre frecce
        if (other.GetComponent<Arrow>() != null) return;

        _hasHit = true;
        _collider.enabled = false;   // disabilita per evitare collisioni multiple

        bool isPlayer = ((1 << other.gameObject.layer) & playerLayer) != 0;

        if (isPlayer)
        {
            // Colpisce Dante — danno e ritorno immediato al pool
            Health health = other.GetComponent<Health>();
            if (health == null) health = other.GetComponentInParent<Health>();
            health?.TakeDamage(damage);

            ReturnToPool();
        }
        else
        {
            // Colpisce muro/terreno — rimane stuck per wallStickTime poi ritorna
            StartCoroutine(WallStickRoutine());
        }
    }

    // ─── Wall Stick ───────────────────────────────────────────────────────────
    private IEnumerator WallStickRoutine()
    {
        // La freccia rimane nella posizione di contatto
        yield return new WaitForSeconds(wallStickTime);
        ReturnToPool();
    }

    // ─── Pool ─────────────────────────────────────────────────────────────────
    private void ReturnToPool()
    {
        if (OwnerPool != null)
            OwnerPool.Return(gameObject);
        else
        {
            Debug.LogWarning("[Arrow] OwnerPool non assegnato — distruzione fallback.");
            Destroy(gameObject);
        }
    }
}