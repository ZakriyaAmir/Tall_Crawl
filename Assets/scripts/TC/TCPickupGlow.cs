using UnityEngine;

/// <summary>
/// Controls the circular aura glow behind power-up pickups.
/// Synchronizes visibility with the pickup target when collected and pooled,
/// and adds a gentle breathing pulse.
/// </summary>
[ExecuteAlways]
public class TCPickupGlow : MonoBehaviour
{
    [Tooltip("The pickup item sibling to follow/sync visibility with.")]
    public Transform targetPickup;

    [Tooltip("Speed of the gentle breathing pulse.")]
    public float pulseSpeed = 2.8f;

    [Tooltip("Magnitude of the gentle breathing pulse (0.05 = 5%).")]
    public float pulseMagnitude = 0.06f;

    Vector3 baseScale;
    bool initializedBaseScale;

    void Awake()
    {
        InitBaseScale();
        FindTargetPickup();
    }

    void OnEnable()
    {
        InitBaseScale();
        FindTargetPickup();
    }

    void InitBaseScale()
    {
        if (!initializedBaseScale && transform.localScale.sqrMagnitude > 0.0001f)
        {
            baseScale = transform.localScale;
            initializedBaseScale = true;
        }
        else if (initializedBaseScale && (pulseSpeed <= 0f || pulseMagnitude <= 0f))
        {
            transform.localScale = baseScale;
        }
    }

    void FindTargetPickup()
    {
        if (targetPickup == null && transform.parent != null)
        {
            for (int i = 0; i < transform.parent.childCount; i++)
            {
                var ch = transform.parent.GetChild(i);
                if (ch != transform && (ch.CompareTag("health") || ch.CompareTag("boost") || ch.CompareTag("cooldown") || ch.CompareTag("magnet")))
                {
                    targetPickup = ch;
                    break;
                }
            }
        }
    }

    void Update()
    {
        // 1. Sync active state with the pickup object (hides glow on collection)
        if (targetPickup != null && !targetPickup.gameObject.activeSelf && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
            return;
        }

        // 2. Gentle breathing pulse during gameplay
        if (Application.isPlaying && initializedBaseScale && pulseSpeed > 0f && pulseMagnitude > 0f)
        {
            float s = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = baseScale * s;
        }
    }
}
