using System;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — GameTimer.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Conto alla rovescia configurabile da Inspector.
/// Comunica con HUDController tramite evento statico — nessuna dipendenza diretta.
///
/// EVENTI STATICI:
///   OnTimerChanged(float remaining)  → HUDController aggiorna il testo MM:SS
///   OnTimerExpired()                 → GameManager gestisce il game over
///
/// INSPECTOR:
///   totalTime  → secondi totali (default 300 = 5 minuti)
/// </summary>
public class GameTimer : MonoBehaviour
{
    // ─── Eventi statici ───────────────────────────────────────────────────────
    /// <summary>Sparato ogni frame con i secondi rimanenti.</summary>
    public static event Action<float> OnTimerChanged;

    /// <summary>Sparato una sola volta quando il timer raggiunge 0.</summary>
    public static event Action OnTimerExpired;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Config")]
    [Tooltip("Tempo totale in secondi (es. 300 = 5:00).")]
    public float totalTime = 300f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private float _remaining;
    private bool _running = true;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _remaining = totalTime;
    }

    void Update()
    {
        if (!_running) return;

        _remaining -= Time.deltaTime;

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _running = false;

            OnTimerChanged?.Invoke(0f);
            OnTimerExpired?.Invoke();
            return;
        }

        OnTimerChanged?.Invoke(_remaining);
    }

    // ─── Utility pubblica ─────────────────────────────────────────────────────
    /// <summary>Usato da HUDController per inizializzare il testo al caricamento.</summary>
    public float GetRemaining() => _remaining;

    /// <summary>Pausa/riprende il timer — chiamalo da PauseMenu.</summary>
    public void SetPaused(bool paused) => _running = !paused;
}