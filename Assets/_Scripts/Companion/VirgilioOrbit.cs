using UnityEngine;

public class VirgilioOrbit : MonoBehaviour
{
    [Header("Orbit Target")]
    public Transform danteTransform;

    [Header("Orbit Parameters")]
    public float orbitRadius = 1.2f;
    public float orbitSpeed = 1.8f;   // radians / sec
    public float heightOffset = 1.5f;   // above Dante's origin
    public float bobAmplitude = 0.15f;
    public float bobFrequency = 2.0f;

    [Header("Smoothing")]
    public float followLerpSpeed = 8f;

    private float _angle;
    private float _bobTime;

    void Update()
    {
        if (danteTransform == null) return;

        _angle += orbitSpeed * Time.deltaTime;
        _bobTime += bobFrequency * Time.deltaTime;

        // Circular orbit on XZ plane, sine-wave Y bobbing
        float x = Mathf.Cos(_angle) * orbitRadius;
        float z = Mathf.Sin(_angle) * orbitRadius;
        float y = heightOffset + Mathf.Sin(_bobTime) * bobAmplitude;

        Vector3 targetPos = danteTransform.position + new Vector3(x, y, z);

        // Smooth follow — avoids rigid parenting artifact
        transform.position = Vector3.Lerp(
            transform.position, targetPos,
            followLerpSpeed * Time.deltaTime);

        // Always face Dante
        transform.LookAt(danteTransform.position + Vector3.up * heightOffset);
    }
}