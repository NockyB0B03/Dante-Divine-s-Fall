using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — SandPlatformData.cs
/// Crea asset: Assets → Create → Dante/FallingObjects/Sand Platform Data
/// </summary>
[CreateAssetMenu(menuName = "Dante/FallingObjects/Sand Platform Data",
                 fileName = "SO_SandPlatform")]
public class SandPlatformData : FallingObjectData
{
    [Header("Sand Platform")]
    [Tooltip("Secondi di caduta prima di fermarsi — indipendentemente dalle collisioni.")]
    public float fallDuration = 2f;
}