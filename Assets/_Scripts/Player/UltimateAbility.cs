using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DANTE: DIVINE'S FALL — UltimateAbility.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce l'Ultimate di Dante: LegioniCelesti.
/// Spawna <spawnCount> entità celesti in cerchio attorno a Dante che accelerano
/// progressivamente verso la posizione fissa di Lucifero.
///
/// GATE:
///   - GameManager.CombatUnlocked == true  (solo Level 5)
///   - GameManager.TryConsumeUltimateUse() == true  (max 2 per fase boss)
///
/// FLUSSO:
///   1. Input Q → verifica gate → avvia UltimateRoutine()
///   2. Dante si ferma (IsAbilityCasting = true) per la durata del cast
///   3. Animazione Ultimate trigger
///   4. A metà animazione: spawna le entità in cerchio, ognuna con Initialize()
///   5. Fine animazione: Dante è libero (IsAbilityCasting = false)
///   6. Le entità volano autonomamente via LegionEntity.cs
///
/// POOL:
///   Le entità sono gestite da una lista interna (pool manuale leggero) —
///   non serve un ProjectilePool dedicato perché il numero è fisso e noto.
///   Il pool viene pre-warm in Awake() con <spawnCount> istanze disattivate.
///
/// INSPECTOR:
///   ultimateData    → SO_Ultimate ScriptableObject (spawnCount, spawnRadius, ecc.)
///   luciferoTarget  → Transform di Lucifero (assegnato in Inspector nella scena boss)
///   castAnimTrigger → "Ultimate" (deve esistere nell'Animator Controller)
///   castDuration    → durata animazione in secondi (default 1.2)
///
/// EVENTS STATICI:
///   OnUltimateCast(int usesRemaining) → HUDController aggiorna l'icona e i contatori
/// </summary>
public class UltimateAbility : MonoBehaviour
{
    // ─── Evento statico ───────────────────────────────────────────────────────
    /// <summary>
    /// Sparato dopo ogni uso. Passa gli usi rimanenti (0 o 1).
    /// HUDController aggiorna l'icona e il contatore usi.
    /// </summary>
    public static event System.Action<int> OnUltimateCast;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Data")]
    [Tooltip("UltimateData ScriptableObject — contiene spawnCount, spawnRadius, ecc.")]
    public UltimateData ultimateData;

    [Header("Target")]
    [Tooltip("Transform di Lucifero — assegnato in Inspector nella scena boss.")]
    public Transform luciferoTarget;

    [Header("Animation")]
    [Tooltip("Trigger esatto nell'Animator Controller.")]
    public string castAnimTrigger = "Ultimate";

    [Tooltip("Durata in secondi dell'animazione di cast.")]
    public float castDuration = 1.2f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private PlayerController _playerController;
    private Animator _animator;
    private PlayerInputActions _input;
    private bool _isCasting = false;

    // Pool manuale di LegionEntity
    private List<LegionEntity> _entityPool = new List<LegionEntity>();

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _animator = GetComponentInChildren<Animator>();
        _input = new PlayerInputActions();

        PrewarmPool();
    }

    void OnEnable()
    {
        _input.Enable();
        _input.Player.Ultimate.performed += OnUltimateInput;
    }

    void OnDisable()
    {
        _input.Player.Ultimate.performed -= OnUltimateInput;
        _input.Disable();
    }

    // ─── Pool Manuale ─────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-crea <spawnCount> istanze del prefab entità, disattivate,
    /// sotto un container figlio di questo GameObject.
    /// </summary>
    private void PrewarmPool()
    {
        if (ultimateData == null || ultimateData.legionePrefab == null)
        {
            Debug.LogError("[UltimateAbility] UltimateData o legionePrefab non assegnati!");
            return;
        }

        GameObject container = new GameObject("Pool_LegioniCelesti");
        container.transform.SetParent(transform);

        for (int i = 0; i < ultimateData.spawnCount; i++)
        {
            GameObject obj = Instantiate(
                ultimateData.legionePrefab, container.transform);
            obj.SetActive(false);

            LegionEntity entity = obj.GetComponent<LegionEntity>();
            if (entity != null)
                _entityPool.Add(entity);
            else
                Debug.LogWarning("[UltimateAbility] legionePrefab non ha LegionEntity.cs!");
        }
    }

    // ─── Input Callback ───────────────────────────────────────────────────────
    private void OnUltimateInput(InputAction.CallbackContext ctx)
    {
        // Gate 1: solo in Level 5
        if (!GameManager.Instance.CombatUnlocked) return;

        // Gate 2: non già in cast o altra abilità
        if (_isCasting || _playerController.IsAbilityCasting) return;

        // Gate 3: verifica e consuma un uso (max 2 per fase)
        if (!GameManager.Instance.TryConsumeUltimateUse()) return;

        // Gate 4: target Lucifero assegnato
        if (luciferoTarget == null)
        {
            Debug.LogError("[UltimateAbility] luciferoTarget non assegnato in Inspector!");
            return;
        }

        StartCoroutine(UltimateRoutine());
    }

    // ─── Ultimate Coroutine ───────────────────────────────────────────────────
    private IEnumerator UltimateRoutine()
    {
        _isCasting = true;
        _playerController.IsAbilityCasting = true;   // blocca movimento durante il cast

        // ── Trigger animazione ────────────────────────────────────────────
        _animator?.SetTrigger(castAnimTrigger);

        // ── Attendi metà animazione poi spawna le entità ──────────────────
        yield return new WaitForSeconds(castDuration * 0.5f);

        // Snapshot posizione Lucifero — fissa per tutta la durata del volo
        Vector3 luciferPosition = luciferoTarget.position;

        SpawnEntities(luciferPosition);

        // Notifica HUD con gli usi rimanenti aggiornati da GameManager
        OnUltimateCast?.Invoke(GameManager.Instance.UltimateUsesRemaining);

        // ── Attendi fine animazione poi libera Dante ──────────────────────
        yield return new WaitForSeconds(castDuration * 0.5f);

        _playerController.IsAbilityCasting = false;
        _isCasting = false;
        // Le entità continuano a volare autonomamente via LegionEntity.Update()
    }

    // ─── Spawn Entità ─────────────────────────────────────────────────────────

    /// <summary>
    /// Posiziona le entità in cerchio attorno a Dante a spawnRadius
    /// e chiama Initialize(luciferPosition) su ognuna.
    /// </summary>
    private void SpawnEntities(Vector3 luciferPosition)
    {
        int count = _entityPool.Count;
        float radius = ultimateData != null ? ultimateData.spawnRadius : 3f;

        for (int i = 0; i < count; i++)
        {
            // Angolo uniforme attorno al cerchio
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * radius,
                1.2f,                        // leggermente in aria
                Mathf.Sin(angle) * radius);

            Vector3 spawnPos = transform.position + offset;

            LegionEntity entity = _entityPool[i];
            entity.transform.position = spawnPos;
            entity.gameObject.SetActive(true);
            entity.Initialize(luciferPosition);
        }
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (ultimateData == null) return;

        // Visualizza il cerchio di spawn
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.2f, 0.25f);
        UnityEditor.Handles.DrawWireDisc(
            transform.position + Vector3.up * 1.2f,
            Vector3.up,
            ultimateData.spawnRadius);
    }

    [ContextMenu("DEBUG — Forza Ultimate (ignora gate)")]
    private void Debug_ForceUltimate()
    {
        if (luciferoTarget == null)
        {
            Debug.LogWarning("[UltimateAbility] Assegna luciferoTarget prima del debug.");
            return;
        }
        StartCoroutine(UltimateRoutine());
    }
#endif
}