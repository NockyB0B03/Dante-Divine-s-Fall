using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — FallingObjectTrigger.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Rileva Dante tramite Physics.OverlapBox ogni frame e chiama
/// FallingObject.Activate() — compatibile con CharacterController.
///
/// SETUP:
///   Oggetto che cade
///   └── TriggerZone  ← questo script + BoxCollider (isTrigger ✓)
///
/// INSPECTOR:
///   fallingObject → FallingObject.cs sul parent (assegnato automaticamente se null)
/// </summary>
public class FallingObjectTrigger : MonoBehaviour
{
    [Tooltip("FallingObject da attivare — se vuoto cerca sul GameObject parent.")]
    public FallingObject fallingObject;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private BoxCollider _col;
    private bool _hasTriggered = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Start()
    {
        _col = GetComponent<BoxCollider>();
        if (_col == null)
            Debug.LogError("[FallingObjectTrigger] BoxCollider non trovato!");

        // Auto-trova FallingObject sul parent se non assegnato
        if (fallingObject == null)
            fallingObject = GetComponentInParent<FallingObject>();

        if (fallingObject == null)
            Debug.LogError("[FallingObjectTrigger] FallingObject non trovato!");
    }

    void Update()
    {
        if (_hasTriggered) return;
        if (_col == null || fallingObject == null) return;

        Collider[] hits = Physics.OverlapBox(
            _col.bounds.center,
            _col.bounds.extents,
            transform.rotation,
            LayerMask.GetMask("Player"));

        if (hits.Length > 0)
        {
            _hasTriggered = true;
            fallingObject.Activate();
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, "Falling Trigger");
    }
#endif
}