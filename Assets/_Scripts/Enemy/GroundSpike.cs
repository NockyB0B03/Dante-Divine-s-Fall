using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — GroundSpike.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Spuntone spawned da LuciferBoss.SpawnGroundSpikes() in Fase 2.
/// 
/// CICLO DI VITA:
///   1. Emerge dal terreno — scala Y da 0 a 1 in emergeDuration secondi
///   2. Rimane fermo per stayDuration secondi — fa danno a Dante se lo tocca
///   3. Scompare — scala Y da 1 a 0 in emergeDuration secondi
///   4. Si autodistrugge
///
/// DANNO:
///   BoxCollider trigger attivo per tutta la durata in cui è visibile.
///   Usa OverlapBox ogni frame invece di OnTriggerEnter — compatibile
///   con CharacterController senza Rigidbody.
///   Cooldown interno per evitare danni multipli per frame.
///
/// PREFAB SETUP:
///   Mesh:        cilindro appuntito o cono, pivot al BASSO (Y=0 = base)
///   BoxCollider: isTrigger ✓, dimensionato attorno al mesh
///   Layer:       Default
///   Scale:       (1, 1, 1) — la scala Y viene animata dallo script
///
/// INSPECTOR:
///   damage          → danno per contatto (default 25)
///   emergeduration  → secondi per emergere/scomparire (default 0.3)
///   stayDuration    → secondi di permanenza visibile (default 2)
///   damageCooldown  → secondi tra un danno e il successivo (default 0.5)
///   playerLayer     → LayerMask "Player"
/// </summary>
public class GroundSpike : MonoBehaviour
{
    [Header("Stats")]
    public float damage = 25f;
    public float emergeDuration = 0.3f;
    public float stayDuration = 2f;
    public float damageCooldown = 0.5f;

    [Header("Layers")]
    public LayerMask playerLayer;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private BoxCollider _collider;
    private float _damageCooldownTimer = 0f;
    private bool _isVisible = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        if (_collider == null)
            Debug.LogError("[GroundSpike] BoxCollider non trovato sul prefab!");

        // Parte con scala Y = 0 — sotto il terreno
        transform.localScale = new Vector3(
            transform.localScale.x, 0f, transform.localScale.z);
    }

    void Start()
    {
        StartCoroutine(SpikeRoutine());
    }

    void Update()
    {
        if (!_isVisible) return;

        // Cooldown danno
        if (_damageCooldownTimer > 0f)
            _damageCooldownTimer -= Time.deltaTime;

        // OverlapBox — compatibile con CharacterController
        if (_collider == null) return;
        if (_damageCooldownTimer > 0f) return;

        Collider[] hits = Physics.OverlapBox(
            _collider.bounds.center,
            _collider.bounds.extents,
            transform.rotation,
            playerLayer);

        if (hits.Length > 0)
        {
            Health health = hits[0].GetComponent<Health>();
            if (health == null) health = hits[0].GetComponentInParent<Health>();
            health?.TakeDamage(damage);

            _damageCooldownTimer = damageCooldown;
        }
    }

    // ─── Spike Routine ────────────────────────────────────────────────────────
    private IEnumerator SpikeRoutine()
    {
        Vector3 baseScale = new Vector3(transform.localScale.x, 1f, transform.localScale.z);

        // ── Fase 1: Emerge ────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < emergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / emergeDuration);
            // Easing out — emerge rapidamente poi rallenta
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = new Vector3(
                baseScale.x, eased * baseScale.y, baseScale.z);
            yield return null;
        }

        // Scala Y esattamente a 1 per evitare imprecisioni floating point
        transform.localScale = baseScale;
        _isVisible = true;

        // ── Fase 2: Rimane fermo ──────────────────────────────────────────
        yield return new WaitForSeconds(stayDuration);

        // ── Fase 3: Scompare ──────────────────────────────────────────────
        _isVisible = false;
        elapsed = 0f;

        while (elapsed < emergeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / emergeDuration);
            float eased = Mathf.Pow(1f - t, 3f);   // easing in — accelera verso 0
            transform.localScale = new Vector3(
                baseScale.x, eased * baseScale.y, baseScale.z);
            yield return null;
        }

        // ── Fase 4: Autodistruzione ───────────────────────────────────────
        Destroy(gameObject);
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_collider == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
        Gizmos.DrawCube(_collider.bounds.center, _collider.bounds.size);
    }
#endif
}