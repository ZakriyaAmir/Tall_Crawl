using UnityEngine;

public class continuousRotation : MonoBehaviour
{
    public bool x;
    public bool y;
    public bool z;
    public float speed;

    Transform rotTarget;
    bool rotateTargetSeparately;

    void Awake()
    {
        // Performance optimization: If this object has a Collider (e.g. coin, pickup),
        // rotating it every frame forces PhysX to recalculate AABB bounds and rebuild
        // broadphase acceleration trees for dozens of items every frame.
        // If there is a child MeshRenderer without a collider, rotate that visual child
        // instead so the physics collider remains completely static in the PhysX engine.
        var col = GetComponent<Collider>();
        if (col != null && transform.childCount > 0)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null && mr.transform != transform && mr.GetComponent<Collider>() == null)
            {
                rotTarget = mr.transform;
                rotateTargetSeparately = true;
            }
        }
        if (!rotTarget) rotTarget = transform;
    }

    void Update()
    {
        if (speed == 0f || (!x && !y && !z)) return;
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        float angle = speed * dt;

        if (rotateTargetSeparately)
        {
            if (x) rotTarget.Rotate(transform.right, angle, Space.World);
            if (y) rotTarget.Rotate(transform.up, angle, Space.World);
            if (z) rotTarget.Rotate(transform.forward, -angle, Space.World);
        }
        else
        {
            if (x) transform.Rotate(angle, 0, 0, Space.Self);
            if (y) transform.Rotate(0, angle, 0, Space.Self);
            if (z) transform.Rotate(0, 0, -angle, Space.Self);
        }
    }
}