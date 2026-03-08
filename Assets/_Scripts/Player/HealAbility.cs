using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DANTE: DIVINE'S FALL — HealAbility.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce la cura istantanea di Dante al tasto F.
/// Comunica con VirgilioOrbit.cs esclusivamente tramite eventi statici —
/// i due script non si referenziano mai direttamente.
///
/// FLUSSO:
///   1. Awake()  → cooldown già azzerato, abilità subito disponibile.
///   2. Update() → ticka il cooldown; quando arriva a 0 spara OnHealReady.
///   3. Tasto F  → se cooldown == 0: cura Dante, spara OnHealUsed, avvia cooldown.
///   4. Il cooldown riparte immediatamente alla pressione (non a fine animazione).
///
/// EVENTI STATICI (VirgilioOrbit.cs si iscrive in OnEnable/OnDisable):
///   OnHealReady  → Virgilio cambia colore e fa il saltino.
///   OnHealUsed   → Virgilio torna al colore/comportamento normale.
///   OnCooldownChanged(float 0→1) → HUDController aggiorna la fill dell'icona.
///
/// INSPECTOR:
///   cooldown    → secondi di attesa tra una cura e l'altra  (default: 15)
///   healAmount  → HP ripristinati alla pressione di F       (default: 30)
/// </summary>
public class HealAbility : MonoBehaviour
{
    // ─── Evento statici ───────────────────────────────────────────────────────
    /// <summary>Sparato quando il cooldown raggiunge 0 — Virgilio reagisce.</summary>
    public static event Action OnHealReady;

    /// <summary>Sparato quando il player usa la cura — Virgilio torna normale.</summary>
    public static event Action OnHealUsed;

    /// <summary>
    /// Sparato ogni frame durante il cooldown e una volta a 0.
    /// Valore normalizzato: 1 = appena usato, 0 = pronto.
    /// HUDController lo usa per aggiornare la fill dell'icona.
    /// </summary>
    public static event Action<float> OnCooldownChanged;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Config")]
    [Tooltip("Secondi di attesa tra una cura e la successiva.")]
    public float cooldown = 15f;

    [Tooltip("Quantità di HP ripristinati alla pressione di F.")]
    public float healAmount = 30f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private PlayerInputActions _input;
    private Health _playerHealth;
    private float _cooldownRemaining = 0f;
    private bool _isReady = true;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _input = new PlayerInputActions();
        _playerHealth = GetComponent<Health>();

        if (_playerHealth == null)
            Debug.LogError("[HealAbility] Nessun componente Health trovato su Dante!");
    }

    void OnEnable()
    {
        _input.Enable();
        _input.Player.Heal.performed += OnHealInput;
    }

    void OnDisable()
    {
        _input.Player.Heal.performed -= OnHealInput;
        _input.Disable();
    }

    void Update()
    {
        if (_cooldownRemaining <= 0f) return;

        _cooldownRemaining -= Time.deltaTime;

        if (_cooldownRemaining <= 0f)
        {
            _cooldownRemaining = 0f;
            _isReady = true;

            OnCooldownChanged?.Invoke(0f);   // icona HUD: pronto
            OnHealReady?.Invoke();           // Virgilio: saltino + cambio colore
        }
        else
        {
            // Broadcast normalizzato per la fill HUD (1 = appena usato, 0 = pronto)
            OnCooldownChanged?.Invoke(_cooldownRemaining / cooldown);
        }
    }

    // ─── Input Callback ───────────────────────────────────────────────────────
    private void OnHealInput(InputAction.CallbackContext ctx)
    {
        if (!_isReady) return;

        // ── Cura istantanea ───────────────────────────────────────────────
        _playerHealth.Heal(healAmount);

        // ── Avvia cooldown immediatamente ─────────────────────────────────
        _isReady = false;
        _cooldownRemaining = cooldown;

        OnCooldownChanged?.Invoke(1f);   // icona HUD: cooldown pieno
        OnHealUsed?.Invoke();            // Virgilio: torna normale
    }

    // ─── Utility pubblica ─────────────────────────────────────────────────────
    /// <summary>
    /// Restituisce il cooldown normalizzato corrente (0 = pronto, 1 = appena usato).
    /// Usato da HUDController per inizializzare la UI al caricamento della scena.
    /// </summary>
    public float GetNormalisedCooldown() =>
        cooldown > 0f ? _cooldownRemaining / cooldown : 0f;
}