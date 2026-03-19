using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — TailAttack.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce il colpo di coda di Lucifero.
/// Vive su un GameObject figlio "Coda" del root Lucifer.
///
/// SEQUENZA:
///   1. Carica: ruota 30° in senso antiorario in windupDuration secondi
///   2. Colpo: ruota 360°+ in senso orario in sweepDuration secondi
///   3. Ritorno: ruota gradualmente verso il player in returnSpeed gradi/s
///
/// DANNO:
///   OnTriggerEnter sui CapsuleCollider figli — colpisce una sola volta per swing.
///   I collider devono essere sul layer Player per il layerMask.
///
/// SETUP:
///   Lucifer (root)
///   └── Coda                ← TailAttack.cs su questo GameObject
///       ├── CodaSegmento1   ← CapsuleCollider isTrigger ✓, layer Enemy
///       ├── CodaSegmento2
///       └── ...
///
/// INSPECTOR:
///   tailDamage      → danno per colpo (default 40)
///   windupAngle     → gradi di carica antioraria (default 30)
///   windupDuration  → durata carica in secondi (default 1.5)
///   sweepAngle      → gradi di rotazione del colpo (default 360)
///   sweepDuration   → durata colpo in secondi (default 0.5)
///   returnSpeed     → gradi/secondo per tornare verso il player (default 120)
///   playerLayer     → LayerMask "Player"
/// </summary>
public class TailAttack : MonoBehaviour
{
    [Header("Danno")]
    public float tailDamage = 40f;

    [Header("Carica (antioraria)")]
    [Tooltip("Gradi di rotazione antioraria prima del colpo.")]
    public float windupAngle = 30f;
    [Tooltip("Durata della carica in secondi.")]
    public float windupDuration = 1.5f;

    [Header("Colpo (orario)")]
    [Tooltip("Gradi di rotazione oraria del colpo.")]
    public float sweepAngle = 360f;
    [Tooltip("Durata del colpo in secondi.")]
    public float sweepDuration = 0.5f;

    [Header("Ritorno")]
    [Tooltip("Velocità di ritorno verso il player in gradi/secondo.")]
    public float returnSpeed = 120f;

    [Header("Layer")]
    public LayerMask playerLayer;

    [Header("Knockback")]
    [Tooltip("Forza del knockback in unità/secondo.")]
    public float knockbackForce = 8f;

    [Tooltip("Forza verticale del knockback — fa volare Dante in aria.")]
    public float knockbackUpForce = 4f;

    [Tooltip("Durata del knockback in secondi.")]
    public float knockbackDuration = 0.3f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private bool _isAttacking = false;
    private bool _hasHitThisSwing = false;

    // Riferimento al root di Lucifero per la rotazione
    private Transform _luciferRoot;

    // ─── Proprietà pubblica ───────────────────────────────────────────────────
    public bool IsAttacking => _isAttacking;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _luciferRoot = transform.parent;
        if (_luciferRoot == null)
            Debug.LogError("[TailAttack] TailAttack deve essere figlio del root Lucifer!");
    }

    // ─── API pubblica — chiamata da LuciferBoss ───────────────────────────────
    /// <summary>
    /// Esegue la sequenza completa del colpo di coda.
    /// Chiamato da LuciferBoss.TailLoop().
    /// Restituisce la coroutine — LuciferBoss la attende con yield.
    /// </summary>
    public IEnumerator PerformAttack(Transform playerTransform)
    {
        if (_isAttacking) yield break;
        _isAttacking = true;
        _hasHitThisSwing = false;

        // Abilita i collider durante l'attacco
        SetCollidersActive(false);   // disabilitati durante la carica

        // ── Fase 1: Carica antioraria ──────────────────────────────────────
        float elapsed = 0f;
        float startAngleW = _luciferRoot.eulerAngles.y;
        float endAngleW = startAngleW - windupAngle;   // antiorario = sottrai

        while (elapsed < windupDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / windupDuration);
            float y = startAngleW - windupAngle * t;   // antiorario = sottrai progressivamente
            _luciferRoot.eulerAngles = new Vector3(0f, y, 0f);
            yield return null;
        }

        _luciferRoot.eulerAngles = new Vector3(0f, startAngleW - windupAngle, 0f);

        // ── Fase 2: Colpo orario ───────────────────────────────────────────
        SetCollidersActive(true);    // abilita collider per il danno
        elapsed = 0f;
        float startAngleS = _luciferRoot.eulerAngles.y;

        while (elapsed < sweepDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sweepDuration);
            float y = startAngleS + sweepAngle * t;
            _luciferRoot.eulerAngles = new Vector3(0f, y, 0f);

            // Controlla danno ogni frame durante lo sweep
            CheckDamage();

            yield return null;
        }

        SetCollidersActive(false);
        _luciferRoot.eulerAngles = new Vector3(0f, startAngleS + sweepAngle, 0f);

        _isAttacking = false;
        _hasHitThisSwing = false;

        // ── Fase 3: Ritorno verso il player ───────────────────────────────
        // LuciferBoss.Update() gestisce la rotazione verso il player —
        // semplicemente lasciamo che riprenda il controllo
        // (LuciferBoss.RotateTowardPlayer() era già attivo ma bloccato durante l'attacco)
    }

    // ─── Collisione via OverlapCapsule ───────────────────────────────────────
    // Chiamato ogni frame durante lo sweep — compatibile con CharacterController
    private void CheckDamage()
    {
        if (_hasHitThisSwing) return;

        foreach (CapsuleCollider cap in GetComponentsInChildren<CapsuleCollider>())
        {
            if (!cap.enabled) continue;

            // Calcola i punti della capsula in world space
            Vector3 center = cap.transform.TransformPoint(cap.center);
            float halfH = Mathf.Max(0f, cap.height / 2f - cap.radius);
            Vector3 up = cap.transform.up * halfH;
            Vector3 p0 = center - up;
            Vector3 p1 = center + up;
            float r = cap.radius * Mathf.Max(
                                 cap.transform.lossyScale.x,
                                 cap.transform.lossyScale.z);

            Collider[] hits = Physics.OverlapCapsule(p0, p1, r, playerLayer,
                                  QueryTriggerInteraction.Ignore);

            if (hits.Length > 0)
            {
                _hasHitThisSwing = true;

                GameObject hitObj = hits[0].gameObject;

                // Danno
                Health health = hitObj.GetComponent<Health>();
                if (health == null) health = hitObj.GetComponentInParent<Health>();
                health?.TakeDamage(tailDamage);

                // Knockback — direzione dal root di Lucifero verso Dante
                Vector3 knockDir = (hitObj.transform.position - _luciferRoot.position).normalized;
                knockDir.y = 0f;
                knockDir = (knockDir + Vector3.up * (knockbackUpForce / knockbackForce)).normalized;

                PlayerController pc = hitObj.GetComponent<PlayerController>();
                if (pc == null) pc = hitObj.GetComponentInParent<PlayerController>();
                if (pc != null)
                    pc.ApplyKnockback(knockDir * knockbackForce, knockbackDuration);

                Debug.Log($"[TailAttack] Colpo di coda! Danno: {tailDamage} Knockback: {knockDir * knockbackForce}");
                return;
            }
        }
    }

    // ─── Utility ──────────────────────────────────────────────────────────────
    private void SetCollidersActive(bool active)
    {
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = active;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
        foreach (CapsuleCollider cap in GetComponentsInChildren<CapsuleCollider>())
        {
            Gizmos.DrawWireSphere(cap.transform.position, cap.radius);
        }
    }
#endif
}