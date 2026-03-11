using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — FireData.cs
/// Crea asset: Assets → Create → Dante/Abilities/Fire Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/Abilities/Fire Data", fileName = "SO_FireAbility")]
public class FireData : AbilityData
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 18f;
}
