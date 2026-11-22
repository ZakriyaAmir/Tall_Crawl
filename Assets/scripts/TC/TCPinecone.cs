using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Spiky Pinecone: tumbles down a specific lane on the trunk bark!
/// Highly convenient and predictable (stays in its lane).
/// Seamlessly engages all 3 controls:
/// 1) Steer around into another lane
/// 2) Jump over it as it rolls underneath
/// 3) Roll Attack into it to smash it to pieces for bonus coins!
/// </summary>
public class TCPinecone : TCEnemy
{
    public Transform roller;
    public float rollDownSpeed = 0.85f;
    float currentRollAngle;

    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        base.Init(startY, startAngle, sweep, spd);
        rollDownSpeed = Random.Range(0.70f, 1.05f);
        currentRollAngle = 0f;
    }

    void Update()
    {
        if (isSmashed || IsGameOverOrDead()) return;
        float dt = Time.deltaTime;

        // 1. Tumble smoothly down the trunk in its designated lane
        y -= rollDownSpeed * dt;

        // 2. End-over-end rolling rotation as it rolls along the rough bark
        currentRollAngle -= rollDownSpeed * 380f * dt;
        if (roller)
        {
            roller.localRotation = Quaternion.Euler(currentRollAngle, 0, 0);
        }

        // 3. Subtle rhythmic hop bounce on the textured tree bark
        if (model)
        {
            float bounce = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * 0.035f;
            model.localPosition = new Vector3(0, bounce, radius);
        }

        transform.position = new Vector3(0, y, 0);
        transform.rotation = Quaternion.Euler(0, baseAngle, 0);
    }
}
