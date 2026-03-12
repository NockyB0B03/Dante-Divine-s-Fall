using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — FallingObjectData.cs
/// Classe base astratta per tutti gli oggetti che cadono nel Level 4.
/// </summary>
public abstract class FallingObjectData : ScriptableObject
{
    [Header("Shake")]
    public float shakeDuration = 1.5f;
    public float shakeIntensity = 0.05f;
    public float shakeFrequency = 20f;

    [Header("Fall")]
    public float fallSpeed = 5f;

    [Header("Destroy")]
    public float stayDuration = 3f;
    public float fadeDuration = 0.5f;
}