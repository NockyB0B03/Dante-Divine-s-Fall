using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DANTE: DIVINE'S FALL — PlayerHPBar.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Script leggero da assegnare direttamente alla Image della barra HP.
/// Si sottoscrive a Health.OnHealthChangedStatic per aggiornare fillAmount e colore.
///
/// SETUP:
///   Seleziona il GameObject HPFill (Image tipo Filled) → Add Component → PlayerHPBar
///
/// INSPECTOR:
///   normalColor   → colore barra HP normale (default verde)
///   dangerColor   → colore sotto dangerThreshold (default rosso)
///   dangerThreshold → percentuale HP sotto cui cambia colore (default 0.3 = 30%)
/// </summary>
[RequireComponent(typeof(Image))]
public class PlayerHPBar : MonoBehaviour
{
    [Header("Colori")]
    [Tooltip("Colore normale della barra HP.")]
    public Color normalColor = Color.green;

    [Tooltip("Colore quando gli HP scendono sotto dangerThreshold.")]
    public Color dangerColor = Color.red;

    [Tooltip("Percentuale HP sotto cui la barra diventa rossa (default 0.3 = 30%).")]
    [Range(0f, 1f)]
    public float dangerThreshold = 0.3f;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private Image _image;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _image = GetComponent<Image>();
        _image.fillAmount = 1f;
        _image.color = normalColor;
    }

    void OnEnable()
    {
        Health.OnHealthChangedStatic += OnHealthChanged;
    }

    void OnDisable()
    {
        Health.OnHealthChangedStatic -= OnHealthChanged;
    }

    // ─── Callback ─────────────────────────────────────────────────────────────
    private void OnHealthChanged(float current, float max)
    {
        if (GameManager.Instance?.PlayerHealth == null) return;
        if (max <= 0f) return;

        float percent = Mathf.Clamp01(current / max);
        _image.fillAmount = percent;
        _image.color = percent <= dangerThreshold ? dangerColor : normalColor;
    }
}