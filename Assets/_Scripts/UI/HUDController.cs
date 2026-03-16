using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DANTE: DIVINE'S FALL — HUDController.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Aggiorna tutti gli elementi dell'HUD iscrivendosi agli eventi statici.
/// Non contiene logica di gioco — solo lettura eventi → aggiornamento UI.
///
/// DIPENDENZE (eventi statici):
///   Health.OnHealthChangedStatic      → aggiorna hpFill
///   HealAbility.OnCooldownChanged     → aggiorna healCooldownFill
///   GameManager.OnTimerUpdated        → aggiorna timerText (usa levelTime)
/// </summary>
public class HUDController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("HP Bar")]
    [Tooltip("Image con Fill Method: Horizontal della barra HP.")]
    public Image hpFill;

    [Header("Heal Icon")]
    [Tooltip("Image con Fill Method: Radial 360 sopra l'icona Heal (overlay scuro).")]
    public Image healCooldownFill;

    [Header("Timer")]
    [Tooltip("TextMeshProUGUI del conto alla rovescia.")]
    public TMP_Text timerText;

    [Tooltip("Tempo massimo del livello in secondi (es. 300 = 5:00).")]
    public float levelDuration = 300f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Start()
    {
        // Inizializza il timer al valore massimo
        UpdateTimer(0f, 0f);
    }

    void OnEnable()
    {
        Health.OnHealthChangedStatic += UpdateHP;
        HealAbility.OnCooldownChanged += UpdateHealCooldown;
        GameManager.OnTimerUpdated += UpdateTimer;
    }

    void OnDisable()
    {
        Health.OnHealthChangedStatic -= UpdateHP;
        HealAbility.OnCooldownChanged -= UpdateHealCooldown;
        GameManager.OnTimerUpdated -= UpdateTimer;
    }

    // ─── Callback UI ──────────────────────────────────────────────────────────

    private void UpdateHP(float currentHP)
    {
        if (hpFill == null) return;

        // Legge maxHealth direttamente dal GameManager — zero dipendenze extra
        float max = GameManager.Instance?.PlayerHealth?.maxHealth ?? 100f;
        hpFill.fillAmount = currentHP / max;
    }

    private void UpdateHealCooldown(float normalised)
    {
        if (healCooldownFill == null) return;
        // 1 = appena usato (overlay pieno), 0 = pronto (overlay scompare)
        healCooldownFill.fillAmount = normalised;
    }

    private void UpdateTimer(float levelTime, float totalTime)
    {
        if (timerText == null) return;

        // Conto alla rovescia: sottraiamo il tempo trascorso dal massimo
        float remaining = Mathf.Max(0f, levelDuration - levelTime);

        int totalSeconds = Mathf.CeilToInt(remaining);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";
    }
}