using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DANTE: DIVINE'S FALL — PlayerAnimationBridge.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Vive su Dante_Visuals (figlio del root Dante) — lo stesso GameObject
/// che ha l'Animator. Questo è OBBLIGATORIO perché gli Animation Events
/// chiamano metodi sugli script dello stesso GameObject dell'Animator.
///
/// RESPONSABILITÀ:
///   1. Logica combo punch (ex ComboManager.cs — rimosso)
///      - Queue-based: fino a 1 colpo accodato mentre uno è in corso
///      - 5 trigger Animator: Punch1…Punch5 in sequenza ciclica
///      - Punch sempre disponibile (no CombatUnlocked gate)
///   2. Relay di tutti gli Animation Events al sistema giusto:
///      - EnableHitBox()  / DisableHitBox()  → HitBox.cs sul root
///      - OnAttackAnimationEnd()             → drain della queue
///      - OnLandAnimationEnd()               → notifica PlayerController (futuro)
///
/// ANIMATION EVENTS DA CONFIGURARE IN UNITY:
///   Su ogni clip Punch1…Punch5:
///     Frame ~33%  → Function: EnableHitBox
///     Frame ~66%  → Function: DisableHitBox
///     Frame 100%  → Function: OnAttackAnimationEnd
///
/// GERARCHIA:
///   Dante (root)
///   └── Dante_Visuals  ← PlayerAnimationBridge qui
///       └── Armature/…
///
/// INSPECTOR:
///   punchTriggers      → array di 5 stringhe: Punch1…Punch5
///   comboWindowDuration→ secondi di finestra dopo fine animazione (default 0.5)
///   hitBox             → riferimento a HitBox.cs sul root Dante (drag in Inspector)
/// </summary>
public class PlayerAnimationBridge : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Combo Config")]
    [Tooltip("Nomi esatti dei trigger nell'Animator Controller, in ordine.")]
    public string[] punchTriggers = { "Punch1", "Punch2", "Punch3", "Punch4", "Punch5" };

    [Tooltip("Secondi di finestra dopo la fine dell'animazione per accodare il prossimo colpo.")]
    public float comboWindowDuration = 0.5f;

    [Tooltip("Durata totale di un colpo in secondi — usata SOLO se non c'è Animator (demo mode).")]
    public float punchDuration = 0.4f;

    [Tooltip("Frazione della durata del colpo in cui la hitbox è attiva (es. 0.33 = 33%-66%).")]
    public float hitBoxStartFraction = 0.33f;

    [Header("References")]
    [Tooltip("HitBox.cs sul root Dante — abilita/disabilita il collider punch.")]
    public HitBox hitBox;

    [Tooltip("DamageDealer.cs su HitBox_Punch — aggiornato con l'indice del colpo corrente.")]
    public DamageDealer damageDealer;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private Animator _animator;
    private PlayerInputActions _input;

    private Queue<int> _comboQueue = new Queue<int>();
    private int _currentIndex = -1;
    private bool _isAttacking = false;
    private Coroutine _windowRoutine;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _animator = GetComponent<Animator>();

        if (_animator == null)
            Debug.LogError("[PlayerAnimationBridge] Animator non trovato su Dante_Visuals!");

        if (hitBox == null)
            Debug.LogWarning("[PlayerAnimationBridge] HitBox non assegnato in Inspector.");

        _input = new PlayerInputActions();
    }

    void OnEnable()
    {
        _input.Enable();
        _input.Player.Punch.performed += OnPunchInput;
    }

    void OnDisable()
    {
        _input.Player.Punch.performed -= OnPunchInput;
        _input.Disable();
    }

    // ─── Input Punch ──────────────────────────────────────────────────────────
    private void OnPunchInput(InputAction.CallbackContext ctx)
    {
        Debug.Log($"[PAB] OnPunchInput ricevuto — isAttacking={_isAttacking} hitBox={hitBox}");

        int next = (_currentIndex + 1) % punchTriggers.Length;

        if (!_isAttacking)
        {
            // Nessun attacco in corso — esegui subito
            ExecuteAttack(next);
        }
        else
        {
            // Attacco in corso — accoda al massimo 1 colpo
            if (_comboQueue.Count < 1)
                _comboQueue.Enqueue(next);
        }
    }

    // ─── Esecuzione Attacco ───────────────────────────────────────────────────
    private void ExecuteAttack(int index)
    {
        // Interrompi la finestra combo se era aperta
        if (_windowRoutine != null)
        {
            StopCoroutine(_windowRoutine);
            _windowRoutine = null;
        }

        _currentIndex = index;
        _isAttacking = true;

        if (damageDealer != null)
            damageDealer.CurrentComboIndex = index;

        if (_animator != null)
        {
            // Modalità normale — Animator presente, usa i trigger
            _animator.SetTrigger(punchTriggers[index]);
        }
        else
        {
            // Demo mode — nessun Animator, usa timer per hitbox e fine attacco
            StartCoroutine(PunchTimerRoutine());
        }
    }

    /// <summary>
    /// Demo mode — simula gli Animation Events con un timer.
    /// Abilita la hitbox al 33% della durata, la disabilita al 66%,
    /// poi chiama OnAttackAnimationEnd() alla fine.
    /// </summary>
    private IEnumerator PunchTimerRoutine()
    {
        float hitStart = punchDuration * hitBoxStartFraction;
        float hitEnd = punchDuration * (1f - hitBoxStartFraction);

        // Attendi inizio hitbox
        yield return new WaitForSeconds(hitStart);
        EnableHitBox();

        // Attendi fine hitbox
        yield return new WaitForSeconds(hitEnd - hitStart);
        DisableHitBox();

        // Attendi fine colpo
        yield return new WaitForSeconds(punchDuration - hitEnd);
        OnAttackAnimationEnd();
    }

    // ─── Animation Events ─────────────────────────────────────────────────────
    // Questi metodi vengono chiamati direttamente dai clip Mixamo tramite
    // Animation Events configurati nel Animation window di Unity.

    /// <summary>
    /// Animation Event — frame ~33% di ogni clip Punch.
    /// Abilita il collider trigger del pugno.
    /// </summary>
    public void EnableHitBox()
    {
        hitBox?.SetActive(true);
    }

    /// <summary>
    /// Animation Event — frame ~66% di ogni clip Punch.
    /// Disabilita il collider trigger del pugno.
    /// </summary>
    public void DisableHitBox()
    {
        hitBox?.SetActive(false);
    }

    /// <summary>
    /// Animation Event — ultimo frame di ogni clip Punch.
    /// Controlla se c'è un colpo accodato nella queue.
    /// Se sì → eseguilo subito.
    /// Se no → apri la finestra combo, poi resetta.
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        if (_comboQueue.Count > 0)
        {
            // C'è un colpo accodato — eseguilo immediatamente
            ExecuteAttack(_comboQueue.Dequeue());
        }
        else
        {
            // Nessun colpo accodato — apri finestra combo
            _windowRoutine = StartCoroutine(ComboWindowRoutine());
        }
    }

    /// <summary>
    /// Animation Event — ultimo frame del clip Land (atterraggio da caduta).
    /// Placeholder per futuri effetti sull'atterraggio (camera shake, VFX, ecc.)
    /// </summary>
    public void OnLandAnimationEnd()
    {
        // Estendibile — es: CameraShake.Instance?.Shake(0.1f, 0.2f);
    }

    // ─── Combo Window ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tiene aperta la finestra combo per comboWindowDuration secondi.
    /// Se non arriva nessun input, resetta il combo.
    /// </summary>
    private IEnumerator ComboWindowRoutine()
    {
        // La finestra è aperta — _isAttacking rimane true
        // così un eventuale input in questo periodo esegue ExecuteAttack()
        // invece di entrare nella queue
        _isAttacking = true;

        yield return new WaitForSeconds(comboWindowDuration);

        // Finestra scaduta senza input — reset completo
        ResetCombo();
        _windowRoutine = null;
    }

    // ─── Reset ────────────────────────────────────────────────────────────────
    private void ResetCombo()
    {
        _isAttacking = false;
        _currentIndex = -1;
        _comboQueue.Clear();
        hitBox?.SetActive(false);   // safety: assicura che l'hitbox sia off
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("DEBUG — Simula Punch1")]
    private void Debug_Punch1()
    {
        OnPunchInput(default);
    }

    [ContextMenu("DEBUG — Reset Combo")]
    private void Debug_ResetCombo() => ResetCombo();
#endif
}