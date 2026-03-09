using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — DamageDealer.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Applica il danno del pugno ai nemici colpiti dall'HitBox.
/// Vive sullo stesso GameObject di HitBox.cs (HitBox_Punch).
///
/// Il danno per colpo è configurabile in Inspector e può essere
/// sovrascritto da ComboStep[] per dare danni diversi per ogni
/// colpo della combo (es. il 5° colpo fa più danno).
///
/// INSPECTOR:
///   baseDamage   → danno base per ogni colpo (default 10)
///   comboSteps   → danno personalizzato per colpo 1…5
///                  Se vuoto, usa baseDamage per tutti.
///   targetLayers → LayerMask dei layer danneggiabili ("Enemy")
///
/// DIPENDENZE:
///   Health.cs sul nemico colpito (o sul suo parent)
///   HitBox.cs — chiama TryDealDamage() da OnTriggerEnter
/// </summary>
public class DamageDealer : MonoBehaviour
{
    [Header("Damage Config")]
    [Tooltip("Danno base applicato ad ogni colpo del combo.")]
    public float baseDamage = 10f;

    [Tooltip("Danno personalizzato per ogni colpo della combo (slot 0 = Punch1, …, slot 4 = Punch5). " +
             "Se vuoto, usa baseDamage per tutti i colpi.")]
    public float[] comboStepDamage;

    [Header("Layers")]
    [Tooltip("Layer dei nemici danneggiabili. Imposta 'Enemy'.")]
    public LayerMask targetLayers;

    // Indice del colpo corrente — settato da PlayerAnimationBridge
    [HideInInspector]
    public int CurrentComboIndex = 0;

    // ─── API pubblica ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tenta di applicare il danno al collider passato.
    /// Restituisce true se il danno è stato applicato (per il tracking in HitBox).
    /// </summary>
    public bool TryDealDamage(Collider other)
    {
        // Controlla se il layer del colpito è tra i target
        bool isTarget = ((1 << other.gameObject.layer) & targetLayers) != 0;
        if (!isTarget) return false;

        // Cerca Health sul collider o sul suo parent
        Health health = other.GetComponent<Health>();
        if (health == null)
            health = other.GetComponentInParent<Health>();

        if (health == null) return false;

        health.TakeDamage(GetCurrentDamage());
        return true;
    }

    // ─── Calcolo Danno ────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce il danno per il colpo corrente della combo.
    /// Se comboStepDamage è definito e l'indice è valido, usa quello.
    /// Altrimenti usa baseDamage.
    /// </summary>
    public float GetCurrentDamage()
    {
        if (comboStepDamage != null
            && comboStepDamage.Length > 0
            && CurrentComboIndex < comboStepDamage.Length)
        {
            return comboStepDamage[CurrentComboIndex];
        }
        return baseDamage;
    }
}