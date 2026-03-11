using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — DashData.cs
/// Crea asset: Assets → Create → Dante/Abilities/Dash Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/Abilities/Dash Data", fileName = "SO_DashAbility")]
public class DashData : AbilityData
{
    [Header("Dash")]
    public float dashDistance  = 6f;
    public float iFrameDuration = 0.3f;
    public float dashDuration  = 0.2f;
}
