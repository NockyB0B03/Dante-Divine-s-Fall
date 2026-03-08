using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — PortalController.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Attach to every portal GameObject in the scene.
/// Requires a Trigger Collider on the same GameObject.
///
/// INSPECTOR SETUP:
///   targetSceneIndex → Build Settings index of the destination scene.
///                      e.g. portal in Level 1 → targetSceneIndex = 2
///
/// NOTE: The portal in Level 5 (Boss) is spawned by LuciferBoss.cs OnDeath
///       UnityEvent — it instantiates this prefab at runtime after the boss dies.
///       Set targetSceneIndex = 6 on that prefab.
/// </summary>

[RequireComponent(typeof(Collider))]
public class PortalController : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Build Settings scene index this portal leads to.")]
    public int targetSceneIndex = -1;

    [Header("Visual Feedback (optional)")]
    [Tooltip("Particle system or VFX to play when player enters the portal.")]
    public ParticleSystem enterVFX;

    private bool _used = false;    // prevent double-trigger on the same frame

    void Awake()
    {
        // Ensure the collider is a trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[PortalController] Collider on {gameObject.name} " +
                             "was not a trigger — fixed automatically.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_used) return;
        if (!other.CompareTag("Player")) return;

        if (targetSceneIndex < 0)
        {
            Debug.LogError($"[PortalController] targetSceneIndex is not set on {gameObject.name}!");
            return;
        }

        _used = true;

        if (enterVFX != null)
            enterVFX.Play();

        LevelManager.Instance?.LoadLevel(targetSceneIndex);
    }
}