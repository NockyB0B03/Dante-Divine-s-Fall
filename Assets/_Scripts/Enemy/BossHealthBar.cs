using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DANTE: DIVINE'S FALL — BossHealthBar.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce la barra della vita di Lucifero divisa in due fasi.
/// Due Image Filled separate — una per ogni fase.
///
/// FASE 1 (100% → 50%):
///   phase1Fill si svuota da 1 a 0 mentre gli HP vanno da 100% a 50%
///
/// FASE 2 (50% → 0%):
///   phase2Fill si svuota da 1 a 0 mentre gli HP vanno da 50% a 0%
///   phase1Fill rimane vuota
///
/// SETUP CANVAS:
///   BossHPCanvas        ← Canvas SO Overlay, Sort Order 10
///   └── BossHPPanel
///       ├── Phase1Bar
///       │   └── Phase1Fill   ← Image Filled Horizontal
///       ├── Phase2Bar
///       │   └── Phase2Fill   ← Image Filled Horizontal
///       └── BossNameText     ← TMP_Text opzionale
///
/// INSPECTOR:
///   bossHealth    → Health.cs di Lucifero (assegnato automaticamente se null)
///   phase1Fill    → Image Fill della Fase 1
///   phase2Fill    → Image Fill della Fase 2
///   phase2Threshold → percentuale HP che divide le due fasi (default 0.5)
///   phase1Color   → colore barra Fase 1 (default arancione)
///   phase2Color   → colore barra Fase 2 (default rosso scuro)
///   transitionColor → colore durante la transizione di fase
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Health.cs di Lucifero — trovato automaticamente via tag 'Boss' se null.")]
    public Health bossHealth;

    [Header("Barre")]
    [Tooltip("Image Filled Horizontal — Fase 1 (100% → 50%).")]
    public Image phase1Fill;

    [Tooltip("Image Filled Horizontal — Fase 2 (50% → 0%).")]
    public Image phase2Fill;

    [Header("Config")]
    [Tooltip("Percentuale HP che divide le due fasi (default 0.5 = 50%).")]
    [Range(0f, 1f)]
    public float phase2Threshold = 0.5f;

    [Header("Colori")]
    public Color phase1Color = new Color(1f, 0.5f, 0f);    // arancione
    public Color phase2Color = new Color(0.7f, 0f, 0f);    // rosso scuro
    public Color transitionColor = Color.white;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private bool _inPhase2 = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Start()
    {
        // Trova Lucifero tramite tag se non assegnato
        if (bossHealth == null)
        {
            GameObject boss = GameObject.FindWithTag("Boss");
            if (boss != null)
                bossHealth = boss.GetComponent<Health>();
        }

        if (bossHealth == null)
        {
            Debug.LogError("[BossHealthBar] Health di Lucifero non trovato! " +
                           "Assegna bossHealth in Inspector o aggiungi tag 'Boss' a Lucifero.");
            return;
        }

        // Iscriviti all'evento HP
        bossHealth.OnHealthChanged.AddListener(UpdateBars);

        // Inizializza colori
        if (phase1Fill != null) phase1Fill.color = phase1Color;
        if (phase2Fill != null)
        {
            phase2Fill.color = phase2Color;
            phase2Fill.fillAmount = 1f;   // piena all'inizio (bloccata finché non si entra in Fase 2)
        }

        // Aggiorna subito con i valori correnti
        UpdateBars(bossHealth.Current);
    }

    void OnDestroy()
    {
        if (bossHealth != null)
            bossHealth.OnHealthChanged.RemoveListener(UpdateBars);
    }

    // ─── Update Barre ─────────────────────────────────────────────────────────
    private void UpdateBars(float currentHP)
    {
        float max = bossHealth.maxHealth;
        float percent = currentHP / max;

        if (percent > phase2Threshold)
        {
            // ── Fase 1 ────────────────────────────────────────────────────
            // phase1Fill va da 1 (100% HP) a 0 (50% HP)
            float phase1Percent = (percent - phase2Threshold) / (1f - phase2Threshold);
            if (phase1Fill != null)
            {
                phase1Fill.fillAmount = Mathf.Clamp01(phase1Percent);
                phase1Fill.color = phase1Color;
            }
            // phase2Fill rimane piena (mostra che la Fase 2 è ancora intatta)
            if (phase2Fill != null) phase2Fill.fillAmount = 1f;
        }
        else
        {
            // ── Fase 2 ────────────────────────────────────────────────────
            if (!_inPhase2)
            {
                _inPhase2 = true;
                // phase1Fill si svuota completamente
                if (phase1Fill != null)
                {
                    phase1Fill.fillAmount = 0f;
                    phase1Fill.color = transitionColor;
                }
            }

            // phase2Fill va da 1 (50% HP) a 0 (0% HP)
            float phase2Percent = percent / phase2Threshold;
            if (phase2Fill != null)
            {
                phase2Fill.fillAmount = Mathf.Clamp01(phase2Percent);
                phase2Fill.color = phase2Color;
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG — Simula 75% HP")]
    private void Debug_75() { if (bossHealth != null) UpdateBars(bossHealth.maxHealth * 0.75f); }

    [ContextMenu("DEBUG — Simula 25% HP")]
    private void Debug_25() { if (bossHealth != null) UpdateBars(bossHealth.maxHealth * 0.25f); }
#endif
}