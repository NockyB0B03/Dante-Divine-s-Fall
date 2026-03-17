using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DANTE: DIVINE'S FALL — HUDController.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Aggiorna tutti gli elementi dell'HUD iscrivendosi agli eventi statici.
/// Nessuna logica di gioco — solo eventi → UI.
///
/// ELEMENTI HUD:
///   hpFill            → barra HP (Image Filled Horizontal)
///   timerText         → conto alla rovescia MM:SS
///   healCooldownFill  → overlay radiale icona Heal
///   dashCooldownFill  → overlay radiale icona Dash
///   ultimateText      → testo numero usi Ultimate rimanenti
///
/// SETUP NEL PREFAB DANTE:
///   Dante
///   └── HUDCanvas        ← Canvas SO Overlay, Sort Order 10
///       └── HUDPanel
///           ├── HPBar
///           │   └── HPFill       ← Image Filled Horizontal
///           ├── TimerText        ← TMP_Text
///           ├── HealIcon
///           │   └── HealCooldownFill  ← Image Filled Radial360
///           ├── DashIcon
///           │   └── DashCooldownFill  ← Image Filled Radial360
///           └── UltimateText     ← TMP_Text (es. "x2")
///
/// EVENTI A CUI SI ISCRIVE:
///   Health.OnHealthChangedStatic(float current, float max)
///   GameTimer.OnTimerChanged(float remaining)
///   GameTimer.OnTimerExpired()
///   HealAbility.OnCooldownChanged(float normalised)
///   DashAbility.OnCooldownChanged(float normalised)
///   GameManager.OnUltimateUsesChanged(int uses)
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("HP Bar")]
    [Tooltip("Image Filled Horizontal — fillAmount = HP correnti / maxHP.")]
    public Image hpFill;

    [Header("Timer")]
    [Tooltip("TMP_Text del conto alla rovescia MM:SS.")]
    public TMP_Text timerText;

    [Tooltip("Colore normale del timer.")]
    public Color timerNormalColor = Color.white;

    [Tooltip("Colore quando il timer è sotto urgencyThreshold secondi.")]
    public Color timerUrgencyColor = Color.red;

    [Tooltip("Secondi sotto cui il timer diventa rosso (default 60).")]
    public float urgencyThreshold = 60f;

    [Header("Heal Cooldown")]
    [Tooltip("Image Filled Radial360 sopra icona Heal — 1=appena usato, 0=pronto.")]
    public Image healCooldownFill;

    [Header("Dash Cooldown")]
    [Tooltip("Image Filled Radial360 sopra icona Dash — 1=appena usato, 0=pronto.")]
    public Image dashCooldownFill;

    [Header("Ultimate")]
    [Tooltip("TMP_Text che mostra il numero di usi Ultimate rimanenti (es. 'x2').")]
    public TMP_Text ultimateText;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void OnEnable()
    {
        Health.OnHealthChangedStatic += UpdateHP;
        GameTimer.OnTimerChanged += UpdateTimer;
        GameTimer.OnTimerExpired += OnTimerExpired;
        HealAbility.OnCooldownChanged += UpdateHealCooldown;
        DashAbility.OnCooldownChanged += UpdateDashCooldown;
        GameManager.OnUltimateUsesChanged += UpdateUltimate;
    }

    void OnDisable()
    {
        Health.OnHealthChangedStatic -= UpdateHP;
        GameTimer.OnTimerChanged -= UpdateTimer;
        GameTimer.OnTimerExpired -= OnTimerExpired;
        HealAbility.OnCooldownChanged -= UpdateHealCooldown;
        DashAbility.OnCooldownChanged -= UpdateDashCooldown;
        GameManager.OnUltimateUsesChanged -= UpdateUltimate;
    }

    void Start()
    {
        // Inizializza valori di default
        if (hpFill != null) hpFill.fillAmount = 1f;
        if (healCooldownFill != null) healCooldownFill.fillAmount = 0f;
        if (dashCooldownFill != null) dashCooldownFill.fillAmount = 0f;
        if (ultimateText != null) ultimateText.text = "x0";
        if (timerText != null) timerText.color = timerNormalColor;

        // Inizializza timer dal GameTimer se già presente in scena
        GameTimer gt = FindObjectOfType<GameTimer>();
        if (gt != null) UpdateTimer(gt.GetRemaining());

        // Inizializza usi Ultimate
        if (GameManager.Instance != null)
            UpdateUltimate(GameManager.Instance.UltimateUsesRemaining);
    }

    // ─── Callback ─────────────────────────────────────────────────────────────

    private void UpdateHP(float current, float max)
    {
        if (hpFill == null) return;
        hpFill.fillAmount = max > 0f ? current / max : 0f;
    }

    // Overload per compatibilità con versioni di Health che sparano solo (float current)
    private void UpdateHP(float current)
    {
        float max = GameManager.Instance?.PlayerHealth?.maxHealth ?? 100f;
        UpdateHP(current, max);
    }

    private void UpdateTimer(float remaining)
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.CeilToInt(remaining);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Colore urgency
        timerText.color = remaining <= urgencyThreshold
            ? timerUrgencyColor
            : timerNormalColor;
    }

    private void OnTimerExpired()
    {
        if (timerText == null) return;
        timerText.text = "00:00";
        timerText.color = timerUrgencyColor;
    }

    private void UpdateHealCooldown(float normalised)
    {
        if (healCooldownFill == null) return;
        healCooldownFill.fillAmount = Mathf.Clamp01(normalised);
    }

    private void UpdateDashCooldown(float normalised)
    {
        if (dashCooldownFill == null) return;
        dashCooldownFill.fillAmount = Mathf.Clamp01(normalised);
    }

    private void UpdateUltimate(int usesRemaining)
    {
        if (ultimateText == null) return;
        ultimateText.text = $"x{usesRemaining}";
    }
}