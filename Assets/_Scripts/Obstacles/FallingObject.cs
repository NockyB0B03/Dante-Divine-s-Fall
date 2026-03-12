using System.Collections;
using UnityEngine;

/// <summary>
/// DANTE: DIVINE'S FALL — FallingObject.cs
/// ─────────────────────────────────────────────────────────────────────────────
/// Gestisce il comportamento di un oggetto che cade nel Level 4.
/// Supporta due modalità tramite FallingObjectData SO:
///
///   SandPlatformData → cade per fallDuration secondi poi si ferma
///   LeadCapeData     → cade finché non colpisce una superficie (o maxFallDuration)
///                      + danno a Dante se colpito all'impatto
///
/// CICLO DI VITA:
///   1. Player entra nel trigger → ShakeRoutine()
///   2. Tremore per shakeDuration secondi
///   3. FallRoutine() — cade verso il basso
///   4. Si ferma (timer o collisione)
///   5. Rimane per stayDuration secondi
///   6. Fade out → Destroy
///
/// SETUP IN EDITOR:
///   Oggetto che cade (es. SandPlatform_01)
///   ├── FallingObject.cs
///   ├── Rigidbody → IsKinematic ✓ (mosso via transform, non fisica)
///   ├── Collider (MeshCollider o BoxCollider) — NON trigger
///   └── TriggerZone (figlio)
///       ├── BoxCollider → isTrigger ✓
///       └── FallingObjectTrigger.cs  ← script separato che chiama FallingObject.Activate()
///
/// INSPECTOR:
///   data         → ScriptableObject SandPlatformData o LeadCapeData
///   playerLayer  → LayerMask "Player" — usato da LeadCape per il danno impatto
/// </summary>
[RequireComponent(typeof(Renderer))]
public class FallingObject : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("ScriptableObject con i parametri — SandPlatformData o LeadCapeData.")]
    public FallingObjectData data;

    [Header("Layers")]
    [Tooltip("LayerMask del Player — usato da LeadCape per rilevare Dante all'impatto.")]
    public LayerMask playerLayer;

    // ─── Privati ──────────────────────────────────────────────────────────────
    private bool _hasActivated = false;
    private Renderer _renderer;
    private Vector3 _originPosition;

    // ─── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _originPosition = transform.position;
    }

    // ─── API pubblica — chiamata da FallingObjectTrigger ──────────────────────
    /// <summary>
    /// Avvia la sequenza shake → fall.
    /// Chiamato da FallingObjectTrigger.cs quando Dante entra nel trigger.
    /// </summary>
    public void Activate()
    {
        if (_hasActivated) return;
        _hasActivated = true;
        StartCoroutine(ShakeRoutine());
    }

    // ─── Shake ────────────────────────────────────────────────────────────────
    private IEnumerator ShakeRoutine()
    {
        if (data == null) yield break;

        float elapsed = 0f;

        while (elapsed < data.shakeDuration)
        {
            elapsed += Time.deltaTime;

            // Offset sinusoidale su X e Z — simula tremore
            float offsetX = Mathf.Sin(elapsed * data.shakeFrequency) * data.shakeIntensity;
            float offsetZ = Mathf.Sin(elapsed * data.shakeFrequency * 1.3f) * data.shakeIntensity;

            transform.position = _originPosition + new Vector3(offsetX, 0f, offsetZ);

            yield return null;
        }

        // Ripristina posizione esatta prima di cadere
        transform.position = _originPosition;

        // Avvia la caduta in base al tipo di data
        if (data is SandPlatformData sandData)
            yield return StartCoroutine(FallTimerRoutine(sandData));
        else if (data is LeadCapeData capeData)
            yield return StartCoroutine(FallUntilGroundRoutine(capeData));
    }

    // ─── Fall — Sand Platform (timer fisso) ───────────────────────────────────
    private IEnumerator FallTimerRoutine(SandPlatformData sandData)
    {
        float elapsed = 0f;

        while (elapsed < sandData.fallDuration)
        {
            elapsed += Time.deltaTime;
            transform.position += Vector3.down * sandData.fallSpeed * Time.deltaTime;
            yield return null;
        }

        yield return StartCoroutine(StayAndFadeRoutine());
    }

    // ─── Fall — Lead Cape (cade finché non colpisce una superficie) ───────────
    private IEnumerator FallUntilGroundRoutine(LeadCapeData capeData)
    {
        float elapsed = 0f;
        bool hasLanded = false;

        while (elapsed < capeData.maxFallDuration && !hasLanded)
        {
            elapsed += Time.deltaTime;

            float stepDistance = capeData.fallSpeed * Time.deltaTime;

            // Raycast verso il basso — rileva terreno e superfici inamovibili
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit,
                                stepDistance + 0.1f, capeData.groundLayers))
            {
                // Snappa alla superficie
                transform.position = new Vector3(
                    transform.position.x,
                    hit.point.y,
                    transform.position.z);

                hasLanded = true;

                // Controlla se Dante è nell'area di impatto
                CheckImpactDamage(capeData);
            }
            else
            {
                transform.position += Vector3.down * stepDistance;
            }

            yield return null;
        }

        yield return StartCoroutine(StayAndFadeRoutine());
    }

    // ─── Danno impatto — Lead Cape ────────────────────────────────────────────
    private void CheckImpactDamage(LeadCapeData capeData)
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, capeData.impactRadius, playerLayer);

        if (hits.Length > 0)
        {
            Health health = hits[0].GetComponent<Health>();
            if (health == null) health = hits[0].GetComponentInParent<Health>();
            health?.TakeDamage(capeData.impactDamage);
            Debug.Log($"[LeadCape] Dante colpito all'impatto — danno: {capeData.impactDamage}");
        }
    }

    // ─── Stay + Fade + Destroy ────────────────────────────────────────────────
    private IEnumerator StayAndFadeRoutine()
    {
        // Rimane visibile per stayDuration secondi
        yield return new WaitForSeconds(data.stayDuration);

        // Fade out via MaterialPropertyBlock — non modifica il materiale shared
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(mpb);

        Color startColor = _renderer.material.color;
        float elapsed = 0f;

        while (elapsed < data.fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / data.fadeDuration);
            mpb.SetColor("_Color", new Color(startColor.r, startColor.g, startColor.b, alpha));
            _renderer.SetPropertyBlock(mpb);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (data is LeadCapeData capeData)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, capeData.impactRadius);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"LeadCape\nDanno: {capeData.impactDamage} | Raggio: {capeData.impactRadius}");
        }
        else if (data is SandPlatformData sandData)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"SandPlatform\nCaduta: {sandData.fallDuration}s");
        }
    }
#endif
}