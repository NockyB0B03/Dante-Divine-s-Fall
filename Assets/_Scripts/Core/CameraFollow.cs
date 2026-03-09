using UnityEngine;
using Cinemachine;

/// <summary>
/// DANTE: DIVINE'S FALL — CameraFollow.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Aggancia il CinemachineFreeLook al CameraTarget di Dante dopo che
/// PlayerSpawnPoint lo ha istanziato a runtime.
///
/// Metti questo script sul GameObject CM FreeLook1 in ogni scena.
/// Non serve assegnare nulla in Inspector — trova tutto automaticamente.
/// </summary>
[RequireComponent(typeof(CinemachineFreeLook))]
public class CameraFollow : MonoBehaviour
{
    [Tooltip("Nome del GameObject figlio di Dante da usare come target. Default: CameraTarget")]
    public string cameraTargetName = "CameraTarget";

    private CinemachineFreeLook _freeLook;

    void Awake()
    {
        _freeLook = GetComponent<CinemachineFreeLook>();
    }

    void Start()
    {
        // Start viene chiamato dopo tutti gli Awake — Dante è già in scena
        FindAndAssignTarget();
    }

    private void FindAndAssignTarget()
    {
        // Trova Dante tramite tag
        GameObject dante = GameObject.FindWithTag("Player");
        if (dante == null)
        {
            Debug.LogError("[CameraFollow] Player non trovato in scena — assicurati che PlayerSpawnPoint abbia istanziato Dante.");
            return;
        }

        // Trova CameraTarget tra i figli di Dante
        Transform target = dante.transform.Find(cameraTargetName);
        if (target == null)
        {
            // Fallback — usa il root di Dante
            Debug.LogWarning($"[CameraFollow] '{cameraTargetName}' non trovato come figlio di Dante — uso il root.");
            target = dante.transform;
        }

        _freeLook.Follow = target;
        _freeLook.LookAt = target;

        Debug.Log($"[CameraFollow] Camera agganciata a {target.name}");
    }
}