using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — AbilityData.cs
/// Classe base astratta per tutti i dati delle abilità di Dante.
/// </summary>
public abstract class AbilityData : ScriptableObject
{
    [Header("Identity")]
    public string abilityName = "Ability";
    public Sprite icon;

    [Header("Timing")]
    public float cooldown = 2f;

    [Header("Stats")]
    public float damage = 10f;
}
