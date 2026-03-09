using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — PlayerSpawnPoint.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Istanzia il prefab di Dante alla posizione di questo GameObject in Awake().
/// Metti un'istanza di questo script in ogni scena invece di trascinare
/// il prefab Dante direttamente nella gerarchia.
///
/// VANTAGGI:
///   - Sposti lo spawn point senza toccare il prefab
///   - Facile da estendere con un sistema di checkpoint
///   - Consistente in tutte le scene
///
/// INSPECTOR:
///   playerPrefab → trascina il prefab Dante
///
/// GERARCHIA CONSIGLIATA:
///   --- SPAWN ---
///   └── PlayerSpawnPoint  (PlayerSpawnPoint.cs)
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Prefab di Dante — Assets/_Project/Prefabs/Player/Dante.prefab")]
    public GameObject playerPrefab;

    void Awake()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawnPoint] playerPrefab non assegnato in Inspector!");
            return;
        }

        Instantiate(playerPrefab, transform.position, transform.rotation);
    }

#if UNITY_EDITOR
    // Gizmo — mostra la posizione e direzione dello spawn in scena
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.8f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, "Player Spawn");
    }
#endif
}