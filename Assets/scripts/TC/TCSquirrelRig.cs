using UnityEngine;

/// Fluid bounding scamper for the squirrel down the tree trunk:
/// - Cohesive quadruped scampering gait with synchronized front/hind strides and knee bends
/// - Dynamic spine flexion/extension (stretch and squash)
/// - Authentic body pitch & weight-shift roll
/// - Rhythmic scamper hop off bark
/// - Natural head stabilization looking downward along heading
/// - Lively springy bushy tail waving with follow-through
public class TCSquirrelRig : MonoBehaviour
{
    public Transform torso, head, tailBase, tailTip;
    public Transform[] legs;
    Transform[] knees;
    Transform[] ears;
    Transform bodyPivot;
    Transform modelRoot;
    float phase;
    Vector3 torsoScale = Vector3.one;
    float baseRadius = 0.54f;

    void Awake()
    {
        if (legs != null)
        {
            knees = new Transform[legs.Length];
            for (int i = 0; i < legs.Length; i++)
                knees[i] = legs[i] ? legs[i].Find("Knee") : null;
        }
        ears = head ? new[] { head.Find("EarL"), head.Find("EarR") } : new Transform[0];
        if (torso) torsoScale = torso.localScale;
        phase = Random.value * 6f;

        bodyPivot = transform;
        if (bodyPivot.parent)
        {
            modelRoot = bodyPivot.parent;
            baseRadius = modelRoot.localPosition.z;
            if (baseRadius <= 0.1f) baseRadius = 0.54f;
        }
    }

    void LateUpdate()
    {
        bool fast = player.Instance && player.Instance.boost;
        float runSpeedMul = fast ? 1.35f : 1.0f;
        float p = Time.time * (16f * runSpeedMul) + phase;

        // Quadruped Scamper Gait:
        // Front paws: fast scampering reach with slight left/right stagger (0.35 rad)
        // Hind paws: springy push-off from haunches half a cycle later
        float[] off = { 0f, 0.35f, Mathf.PI, Mathf.PI + 0.35f };
        for (int i = 0; i < 4 && legs != null && i < legs.Length; i++)
        {
            if (!legs[i]) continue;
            bool hind = i >= 2;
            float s = Mathf.Sin(p + off[i]);
            float c = Mathf.Cos(p + off[i]);

            // Swing angle: forward reach along +Z, push along -Z
            float swing = hind ? (-s * 46f + 8f) : (s * 52f - 4f);
            // Subtle lateral splay (left/right paw placement outwards for 3D bark grip)
            float splay = (i % 2 == 0 ? -1f : 1f) * (hind ? 8f : 6f);
            legs[i].localRotation = Quaternion.Euler(swing, splay, 0f);

            // Knee bend: hind knee flexes forward during swing recovery, front knee flexes during reach
            if (knees[i])
            {
                float kneeRot = hind ? Mathf.Max(0f, c) * -64f : Mathf.Max(0f, -c) * 62f;
                knees[i].localRotation = Quaternion.Euler(kneeRot, 0f, 0f);
            }
        }

        // Spine Stretch & Squash + Body Pitch:
        if (torso)
        {
            float st = Mathf.Sin(p + 0.6f);
            torso.localScale = new Vector3(
                torsoScale.x * (1f - 0.04f * st),
                torsoScale.y * (1f - 0.06f * st),
                torsoScale.z * (1f + 0.08f * st)
            );
            torso.localRotation = Quaternion.Euler(Mathf.Sin(p + 1.1f) * 6f, 0f, 0f);
        }

        // Body Pivot Pitch & Scamper Roll:
        if (bodyPivot)
        {
            float pitch = 90f + Mathf.Sin(p) * 5.5f; // head tilted down along trunk
            float roll = Mathf.Sin(p * 0.5f) * 4f;    // weight shift between left and right bounding steps
            bodyPivot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }

        // Rhythmic hop against the bark (scaled for attached paw scamper):
        if (modelRoot)
        {
            float hop = Mathf.Max(0f, Mathf.Sin(p)) * 0.020f;
            modelRoot.localPosition = new Vector3(0f, hop, baseRadius);
        }

        // Alert Head Looking Downward:
        if (head)
        {
            float headBob = Mathf.Sin(p - 0.8f) * 6f;
            float headYaw = Mathf.Sin(p * 0.5f) * 4f;
            head.localRotation = Quaternion.Euler(10f + headBob, headYaw, 0f);
        }

        // Ear Flicks in Wind & Motion:
        for (int i = 0; i < ears.Length; i++)
        {
            if (!ears[i]) continue;
            float sg = (i == 0) ? -1f : 1f;
            ears[i].localRotation = Quaternion.Euler(
                -20f + Mathf.Sin(p - 1.2f) * 8f,
                sg * 10f,
                sg * (-24f + Mathf.Sin(p * 0.7f + i) * 7f)
            );
        }

        // Bushy Tail Springy Waves & Follow-through (arching gracefully over back):
        if (tailBase)
        {
            float basePitch = Mathf.Sin(p - 0.9f) * 15f;
            float baseSway = Mathf.Sin(p * 0.5f) * 16f;
            float baseRoll = Mathf.Cos(p * 0.5f) * 8f;
            tailBase.localRotation = Quaternion.Euler(basePitch, baseSway, baseRoll);
        }
        if (tailTip)
        {
            float tipPitch = Mathf.Sin(p - 1.9f) * 22f;
            float tipSway = Mathf.Sin(p * 0.5f - 0.9f) * 26f;
            tailTip.localRotation = Quaternion.Euler(tipPitch, tipSway, 0f);
        }
    }
}
