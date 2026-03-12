using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — LeadCapeData.cs
/// Crea asset: Assets → Create → Dante/FallingObjects/Lead Cape Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/FallingObjects/Lead Cape Data",
                 fileName = "SO_LeadCape")]
public class LeadCapeData : FallingObjectData
{
    [Header("Lead Cape")]
    [Tooltip("Danno inflitto a Dante se colpito al momento dell'impatto.")]
    public float impactDamage = 30f;

    [Tooltip("Raggio della sfera di overlap al momento dell'impatto per rilevare Dante.")]
    public float impactRadius = 1f;

    [Tooltip("Timer di sicurezza — la cappa si ferma dopo questi secondi anche senza colpire nulla.")]
    public float maxFallDuration = 5f;

    [Tooltip("Layer delle superfici su cui la cappa si ferma.")]
    public LayerMask groundLayers;
}