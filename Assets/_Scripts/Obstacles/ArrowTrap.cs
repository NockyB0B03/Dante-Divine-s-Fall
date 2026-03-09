using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — ArrowTrap.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Trappola del Level 2. Quando Dante entra nel trigger:
///   1. Parte un timer (spawnDelay)
///   2. Spawnano arrowCount frecce da posizioni casuali sul piano del muro
///   3. Stop — la trappola non si riattiva
///
/// SETUP IN EDITOR — due GameObject separati:
///
///   [TriggerZone]  ← BoxCollider trigger, ArrowTrap.cs
///   [WallPlane]    ← assegnato in Inspector come spawnWall Transform
///       Il WallPlane è un quad o un piano posizionato nel muro.
///       Le frecce partono da punti casuali sulla sua superficie
///       e volano nella direzione del suo -forward (dentro la stanza).
///
/// COME CALCOLA I PUNTI DI SPAWN:
///   Genera (u, v) casuali in [-0.5, 0.5] e li trasforma in coordinate
///   world usando i vettori right e up del WallPlane scalati per
///   wallWidth e wallHeight. La freccia viene orientata con -wallPlane.forward
///   così punta sempre verso l'interno della stanza indipendentemente
///   dalla rotazione del muro in scena.
///
/// INSPECTOR:
///   spawnWall     → Transform del piano muro da cui escono le frecce
///   wallWidth     → larghezza del muro in unità (default 5)
///   wallHeight    → altezza del muro in unità (default 3)
///   arrowCount    → numero di frecce per attivazione (default 6)
///   spawnDelay    → secondi di attesa prima dello spawn (default 1.5)
///   spawnInterval → secondi tra una freccia e la successiva (default 0.05)
///
/// GERARCHIA CONSIGLIATA in Level 2:
///   --- TRAPS ---
///   └── ArrowTrap_01
///       ├── TriggerZone  (ArrowTrap.cs + BoxCollider trigger)
///       └── WallPlane    (Transform di riferimento, opzionale MeshRenderer visivo)
/// </summary>
public class ArrowTrap : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Wall Reference")]
    [Tooltip("Transform del piano muro — le frecce escono da punti casuali sulla sua superficie.")]
    public Transform spawnWall;

    [Tooltip("Larghezza del muro in unità Unity — usata per distribuire le frecce.")]
    public float wallWidth = 5f;

    [Tooltip("Altezza del muro in unità Unity.")]
    public float wallHeight = 3f;

    [Header("Spawn Config")]
    [Tooltip("Numero di frecce spawnate all'attivazione.")]
    public int arrowCount = 6;

    [Tooltip("Secondi di attesa dal momento in cui Dante entra nel trigger allo spawn.")]
    public float spawnDelay = 1.5f;

    [Tooltip("Secondi tra il lancio di una freccia e la successiva — evita spawn istantaneo.")]
    public float spawnInterval = 0.05f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private bool _hasTriggered = false;

    // ─── Trigger ──────────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        _hasTriggered = true;
        StartCoroutine(SpawnRoutine());
    }

    // ─── Spawn Coroutine ──────────────────────────────────────────────────────
    private IEnumerator SpawnRoutine()
    {
        if (ArrowPool.Instance == null)
        {
            Debug.LogError("[ArrowTrap] ArrowPool.Instance non trovato nella scena!");
            yield break;
        }

        if (spawnWall == null)
        {
            Debug.LogError("[ArrowTrap] spawnWall non assegnato in Inspector!");
            yield break;
        }

        // Attendi il timer prima dello spawn
        yield return new WaitForSeconds(spawnDelay);

        // Spawna le frecce una alla volta con spawnInterval tra ognuna
        for (int i = 0; i < arrowCount; i++)
        {
            Vector3 spawnPos = GetRandomWallPosition();
            Quaternion spawnRot = GetArrowRotation();

            ArrowPool.Instance.Get(spawnPos, spawnRot);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // ─── Calcolo Posizione e Rotazione ────────────────────────────────────────

    /// <summary>
    /// Genera una posizione casuale sulla superficie del muro.
    /// Usa i vettori right e up del WallPlane scalati per wallWidth e wallHeight.
    /// </summary>
    private Vector3 GetRandomWallPosition()
    {
        // Coordinate locali casuali sulla superficie del piano [-0.5, 0.5]
        float u = Random.Range(-0.5f, 0.5f);
        float v = Random.Range(-0.5f, 0.5f);

        // Trasforma in coordinate world usando gli assi del muro
        Vector3 position = spawnWall.position
                         + spawnWall.right * (u * wallWidth)
                         + spawnWall.up * (v * wallHeight);

        return position;
    }

    /// <summary>
    /// La freccia vola nella direzione del -forward del muro
    /// (verso l'interno della stanza).
    /// Quaternion.LookRotation orienta il forward della freccia
    /// nella direzione di volo corretta.
    /// </summary>
    private Quaternion GetArrowRotation()
    {
        // -forward del muro = direzione verso l'interno della stanza
        Vector3 flyDirection = -spawnWall.forward;
        return Quaternion.LookRotation(flyDirection);
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (spawnWall == null) return;

        // Disegna il rettangolo del muro
        Vector3 center = spawnWall.position;
        Vector3 right = spawnWall.right * wallWidth;
        Vector3 up = spawnWall.up * wallHeight;

        Vector3 tl = center - right * 0.5f + up * 0.5f;
        Vector3 tr = center + right * 0.5f + up * 0.5f;
        Vector3 bl = center - right * 0.5f - up * 0.5f;
        Vector3 br = center + right * 0.5f - up * 0.5f;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);
        Gizmos.DrawLine(bl, tl);

        // Freccia che indica la direzione di volo
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(center, -spawnWall.forward * 2f);

        // Label
        UnityEditor.Handles.Label(center + Vector3.up * 0.3f,
            $"ArrowTrap\n{arrowCount} frecce | delay: {spawnDelay}s");
    }

    [ContextMenu("DEBUG — Forza Spawn (Play Mode)")]
    private void Debug_ForceSpawn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ArrowTrap] Funziona solo in Play Mode.");
            return;
        }
        _hasTriggered = false;
        StartCoroutine(SpawnRoutine());
    }
#endif
}