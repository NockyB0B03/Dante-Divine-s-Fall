using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — UltimateData.cs
/// Crea asset: Assets → Create → Dante/Abilities/Ultimate Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/Abilities/Ultimate Data", fileName = "SO_Ultimate")]
public class UltimateData : AbilityData
{
    [Header("Legioni Celesti")]
    public GameObject legionePrefab;
    public int   spawnCount   = 8;
    public float spawnRadius  = 5f;
    public float legionSpeed  = 12f;
    public float legionLifetime = 6f;
}
