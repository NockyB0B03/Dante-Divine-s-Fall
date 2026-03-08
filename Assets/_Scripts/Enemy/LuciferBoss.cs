using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class LuciferBoss : MonoBehaviour
{
    public enum Phase { One, Two }

    [Header("Phase Thresholds")]
    [Range(0, 1)] public float phase2Threshold = 0.5f;

    [Header("Phase 1 — Attacks")]
    public GameObject fireballPrefab;
    public Transform[] fireballSpawnPoints;
    public float fireballCooldown = 3f;
    public GameObject groundSpikePrefab;
    public float spikeCooldown = 5f;

    [Header("Phase 2 Modifiers")]
    public float phase2SpeedMultiplier = 1.6f;

    [Header("References")]
    public Transform playerTransform;

    private Health _health;
    private Phase _currentPhase = Phase.One;
    private bool _phaseTransitioning;

    void Awake() => _health = GetComponent<Health>();

    void OnEnable()
    {
        _health.OnHealthChanged.AddListener(CheckPhaseTransition);
        StartCoroutine(FireballLoop());
        StartCoroutine(SpikeLoop());
    }

    void CheckPhaseTransition(float current)
    {
        if (_currentPhase == Phase.One
            && _health.Percent <= phase2Threshold
            && !_phaseTransitioning)
        {
            StartCoroutine(EnterPhaseTwo());
        }
    }

    IEnumerator EnterPhaseTwo()
    {
        _phaseTransitioning = true;
        _currentPhase = Phase.Two;
        // Play roar anim, pause attacks briefly
        yield return new WaitForSeconds(2f);
        fireballCooldown /= phase2SpeedMultiplier;
        spikeCooldown /= phase2SpeedMultiplier;
        _phaseTransitioning = false;
    }

    IEnumerator FireballLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(fireballCooldown);
            if (_phaseTransitioning) continue;
            foreach (var spawnPt in fireballSpawnPoints)
            {
                var fb = Instantiate(fireballPrefab, spawnPt.position, spawnPt.rotation);
                // Projectile.cs handles homing/travel
                var proj = fb.GetComponent<Projectile>();
                if (proj) proj.SetTarget(playerTransform);
            }
        }
    }

    IEnumerator SpikeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spikeCooldown);
            if (_phaseTransitioning) continue;
            // Spawn spikes in a ring around player
            float angleStep = 360f / 8;
            for (int i = 0; i < 8; i++)
            {
                float a = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3f;
                Instantiate(groundSpikePrefab,
                    playerTransform.position + offset,
                    Quaternion.identity);
            }
        }
    }
}