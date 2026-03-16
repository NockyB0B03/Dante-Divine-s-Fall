using UnityEngine;
using Cinemachine;

/// <summary>
/// DANTE: DIVINE'S FALL — CameraFollow.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Aggancia il CinemachineFreeLook al CameraTarget di Dante a runtime.
/// Gestisce il camera collision avoidance — quando un muro o soffitto
/// si interpone tra Dante e la camera, il raggio dei rig viene ridotto
/// progressivamente finché la camera non è più ostruita.
///
/// SETUP: aggiungi su CM FreeLook1 in ogni scena di gioco.
/// Non serve assegnare nulla — trova Dante e la camera automaticamente.
///
/// INSPECTOR:
///   cameraTargetName    → nome del figlio di Dante (default "CameraTarget")
///   collisionLayers     → layer con cui la camera collide (spunta Default)
///   collisionRadius     → raggio della sfera del cast (default 0.3)
///   minRadiusMultiplier → moltiplicatore minimo del raggio rig (default 0.1)
///   recoverSpeed        → velocità di recupero del raggio originale (default 2)
///   shrinkSpeed         → velocità di riduzione del raggio (default 8)
/// </summary>
[RequireComponent(typeof(CinemachineFreeLook))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public string cameraTargetName = "CameraTarget";

    [Header("Collision")]
    public LayerMask collisionLayers = ~0;

    [Range(0.01f, 1f)]
    public float collisionRadius = 0.3f;

    [Range(0.01f, 0.5f)]
    public float minRadiusMultiplier = 0.1f;

    public float recoverSpeed = 2f;
    public float shrinkSpeed = 8f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private CinemachineFreeLook _freeLook;
    private Transform _cameraTarget;

    private float _topRadiusOriginal;
    private float _midRadiusOriginal;
    private float _botRadiusOriginal;

    private float _topMultiplier = 1f;
    private float _midMultiplier = 1f;
    private float _botMultiplier = 1f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _freeLook = GetComponent<CinemachineFreeLook>();
    }

    void Start()
    {
        FindAndAssignTarget();

        if (_freeLook != null)
        {
            _topRadiusOriginal = _freeLook.m_Orbits[0].m_Radius;
            _midRadiusOriginal = _freeLook.m_Orbits[1].m_Radius;
            _botRadiusOriginal = _freeLook.m_Orbits[2].m_Radius;
        }
    }

    void LateUpdate()
    {
        if (_freeLook == null || _cameraTarget == null) return;
        UpdateCollision();
    }

    // ─── Target ───────────────────────────────────────────────────────────────
    private void FindAndAssignTarget()
    {
        GameObject dante = GameObject.FindWithTag("Player");
        if (dante == null)
        {
            Debug.LogError("[CameraFollow] Player non trovato!");
            return;
        }

        Transform target = dante.transform.Find(cameraTargetName);
        if (target == null)
        {
            Debug.LogWarning($"[CameraFollow] '{cameraTargetName}' non trovato — uso root Dante.");
            target = dante.transform;
        }

        _cameraTarget = target;
        _freeLook.Follow = target;
        _freeLook.LookAt = target;

        Debug.Log($"[CameraFollow] Camera agganciata a {target.name}");
    }

    // ─── Collision Avoidance ──────────────────────────────────────────────────
    private void UpdateCollision()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 targetPos = _cameraTarget.position;
        Vector3 camPos = mainCam.transform.position;
        Vector3 direction = camPos - targetPos;
        float distance = direction.magnitude;

        bool blocked = Physics.SphereCast(
            targetPos,
            collisionRadius,
            direction.normalized,
            out RaycastHit hit,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore);

        if (blocked)
        {
            float hitFraction = hit.distance / distance;
            float targetMult = Mathf.Max(minRadiusMultiplier, hitFraction);

            _topMultiplier = Mathf.Lerp(_topMultiplier, targetMult, shrinkSpeed * Time.deltaTime);
            _midMultiplier = Mathf.Lerp(_midMultiplier, targetMult, shrinkSpeed * Time.deltaTime);
            _botMultiplier = Mathf.Lerp(_botMultiplier, targetMult, shrinkSpeed * Time.deltaTime);
        }
        else
        {
            _topMultiplier = Mathf.Lerp(_topMultiplier, 1f, recoverSpeed * Time.deltaTime);
            _midMultiplier = Mathf.Lerp(_midMultiplier, 1f, recoverSpeed * Time.deltaTime);
            _botMultiplier = Mathf.Lerp(_botMultiplier, 1f, recoverSpeed * Time.deltaTime);
        }

        _freeLook.m_Orbits[0].m_Radius = _topRadiusOriginal * _topMultiplier;
        _freeLook.m_Orbits[1].m_Radius = _midRadiusOriginal * _midMultiplier;
        _freeLook.m_Orbits[2].m_Radius = _botRadiusOriginal * _botMultiplier;
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_cameraTarget == null || Camera.main == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_cameraTarget.position, Camera.main.transform.position);
        Gizmos.DrawWireSphere(Camera.main.transform.position, collisionRadius);
    }
#endif
}