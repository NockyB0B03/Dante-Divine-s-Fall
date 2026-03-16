using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — HitBox.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce il collider trigger del pugno di Dante.
/// Vive su un GameObject figlio del root Dante: "HitBox_Punch".
///
/// Viene abilitato/disabilitato da PlayerAnimationBridge tramite
/// gli Animation Events (EnableHitBox / DisableHitBox).
///
/// Quando attivo, usa OnTriggerEnter per rilevare i nemici colpiti
/// e delega il danno a DamageDealer.cs sullo stesso GameObject.
///
/// PREFAB SETUP — HitBox_Punch:
///   Position  → (0, 0.9, 0.6)  davanti e all'altezza del busto di Dante
///   BoxCollider:
///     Size      → (0.6, 0.6, 0.5)
///     isTrigger → ✓
///   Layer     → "PlayerHitBox" (non collidere con Dante stesso)
///   Starts    → DISABLED (SetActive false di default)
///
/// GERARCHIA:
///   Dante (root)
///   ├── Dante_Visuals (PlayerAnimationBridge)
///   └── HitBox_Punch  ← HitBox.cs + DamageDealer.cs + BoxCollider (trigger)
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(DamageDealer))]
public class HitBox : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Se true, mostra il BoxCollider in scena anche quando disabilitato.")]
    public bool showGizmoAlways = true;

    private DamageDealer _damageDealer;
    private Collider _collider;

    // Traccia i nemici già colpiti in questo swing
    // per evitare danni multipli allo stesso nemico per frame
    private System.Collections.Generic.HashSet<GameObject> _hitThisSwing
        = new System.Collections.Generic.HashSet<GameObject>();

    void Awake()
    {
        _damageDealer = GetComponent<DamageDealer>();
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;

        // Parte disabilitato — PlayerAnimationBridge lo abilita via EnableHitBox()
        gameObject.SetActive(false);
    }

    // ─── API pubblica — chiamata da PlayerAnimationBridge ─────────────────────

    /// <summary>
    /// Abilita o disabilita l'hitbox.
    /// SetActive(true)  → EnableHitBox()  Animation Event
    /// SetActive(false) → DisableHitBox() Animation Event
    /// </summary>
    public void SetActive(bool active)
    {
        if (active)
        {
            _hitThisSwing.Clear();
            Debug.Log($"[HitBox] ABILITATA — pos={transform.position} worldPos={transform.TransformPoint(GetComponent<BoxCollider>()?.center ?? Vector3.zero)}");
        }
        else
        {
            Debug.Log("[HitBox] disabilitata");
        }

        gameObject.SetActive(active);
    }

    // ─── Collision ────────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[HitBox] OnTriggerEnter: {other.gameObject.name} layer={LayerMask.LayerToName(other.gameObject.layer)}");

        if (_hitThisSwing.Contains(other.gameObject)) return;

        bool hit = _damageDealer.TryDealDamage(other);
        Debug.Log($"[HitBox] TryDealDamage su {other.gameObject.name}: {(hit ? "COLPITO" : "mancato — layer o Health mancante")}");

        if (hit)
            _hitThisSwing.Add(other.gameObject);
    }

    // ─── OverlapBox manuale ogni frame quando attiva ───────────────────────────
    // Fallback per CharacterController che non genera OnTriggerEnter con altri trigger
    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Collider[] hits = Physics.OverlapBox(
            transform.TransformPoint(box.center),
            box.size * 0.5f,
            transform.rotation);

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (_hitThisSwing.Contains(hit.gameObject)) continue;

            Debug.Log($"[HitBox] OverlapBox trova: {hit.gameObject.name} layer={LayerMask.LayerToName(hit.gameObject.layer)}");

            bool dealt = _damageDealer.TryDealDamage(hit);
            if (dealt)
            {
                Debug.Log($"[HitBox] DANNO applicato a {hit.gameObject.name}");
                _hitThisSwing.Add(hit.gameObject);
            }
        }
    }

    // ─── Gizmo ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmoAlways) return;
        DrawHitBoxGizmo(new Color(1f, 0.2f, 0.2f, 0.3f));
    }

    void OnDrawGizmosSelected()
    {
        DrawHitBoxGizmo(new Color(1f, 0.2f, 0.2f, 0.6f));
    }

    private void DrawHitBoxGizmo(Color color)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = color;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.TransformPoint(box.center),
            transform.rotation,
            transform.lossyScale);
        Gizmos.DrawCube(Vector3.zero, box.size);
        Gizmos.matrix = oldMatrix;
    }
#endif
}