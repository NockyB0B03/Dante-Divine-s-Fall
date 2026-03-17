using System;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — GameTimer.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Conto alla rovescia da totalTime secondi.
/// Alla scadenza chiama DeathManager.OnHealthDeath() — stessa sequenza
/// di morte che si vede quando Dante perde tutti gli HP.
///
/// Si pausa automaticamente quando GameManager è in stato Paused.
///
/// INSPECTOR:
///   totalTime → secondi totali (default 300 = 5:00)
///
/// EVENTI STATICI:
///   OnTimerChanged(float remaining) → HUDController aggiorna il testo MM:SS
///   OnTimerExpired()                → HUDController può colorare il testo di rosso
/// </summary>
public class GameTimer : MonoBehaviour
{
    // ─── Eventi statici ───────────────────────────────────────────────────────
    public static event Action<float> OnTimerChanged;
    public static event Action OnTimerExpired;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Config")]
    [Tooltip("Tempo totale in secondi (default 300 = 5:00).")]
    public float totalTime = 300f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private float _remaining;
    private bool _expired = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _remaining = totalTime;
    }

    void Start()
    {
        // Invia subito il valore iniziale all'HUD
        OnTimerChanged?.Invoke(_remaining);
    }

    void Update()
    {
        if (_expired) return;

        // Non scorrere durante la pausa
        if (GameManager.Instance?.CurrentState == GameManager.GameState.Paused) return;

        _remaining -= Time.deltaTime;

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _expired = true;
            OnTimerChanged?.Invoke(0f);
            OnTimerExpired?.Invoke();

            // Attiva la sequenza di morte normale
            DeathManager dm = FindObjectOfType<DeathManager>();
            if (dm != null)
                dm.OnHealthDeath();
            else
                Debug.LogError("[GameTimer] DeathManager non trovato — impossibile triggerare la morte.");

            return;
        }

        OnTimerChanged?.Invoke(_remaining);
    }

    // ─── Utility ─────────────────────────────────────────────────────────────
    public float GetRemaining() => _remaining;
    public float GetNormalised() => _remaining / totalTime;
}