using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ConfigureBeeSwarm
{
    [MenuItem("Tall Crawl/Configure Bee Swarm")]
    public static void Run()
    {
        // 1. Create or load the Ring Collider Mesh asset
        string meshDir = "Assets/resources";
        if (!AssetDatabase.IsValidFolder(meshDir))
        {
            AssetDatabase.CreateFolder("Assets", "resources");
        }
        string meshPath = "Assets/resources/BeeRingColliderMesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = CreateRingColliderMesh(0.72f, 0.16f, 24);
            AssetDatabase.CreateAsset(mesh, meshPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ConfigureBeeSwarm] Created new BeeRingColliderMesh asset.");
        }
        else
        {
            Mesh newMesh = CreateRingColliderMesh(0.72f, 0.16f, 24);
            mesh.Clear();
            mesh.vertices = newMesh.vertices;
            mesh.triangles = newMesh.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            Debug.Log("[ConfigureBeeSwarm] Updated existing BeeRingColliderMesh asset.");
        }

        // 2. Load beesParent prefab
        string prefabPath = "Assets/prefabs/beesParent.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (!root)
        {
            Debug.LogError("[ConfigureBeeSwarm] Could not load beesParent prefab!");
            return;
        }

        root.tag = "bees";
        root.transform.localScale = Vector3.one;

        // Find child Particle System
        Transform psChild = root.transform.Find("Particle System");
        if (psChild)
        {
            psChild.localPosition = Vector3.zero;
            psChild.localRotation = Quaternion.Euler(90f, 0f, 0f);
            psChild.localScale = Vector3.one;
        }
        GameObject psObj = psChild ? psChild.gameObject : root;
        psObj.tag = "bees";

        // 3. Configure Collider on psObj: Use ONE Ring MeshCollider
        // Remove old BoxCollider or CapsuleCollider
        var oldBox = psObj.GetComponent<BoxCollider>();
        if (oldBox) Object.DestroyImmediate(oldBox, true);
        var oldCap = psObj.GetComponent<CapsuleCollider>();
        if (oldCap) Object.DestroyImmediate(oldCap, true);
        var rootBox = root.GetComponent<BoxCollider>();
        if (rootBox) Object.DestroyImmediate(rootBox, true);
        var rootCap = root.GetComponent<CapsuleCollider>();
        if (rootCap) Object.DestroyImmediate(rootCap, true);

        // Put the ring MeshCollider on root (which is aligned with world coordinates: Y = up along trunk)
        var meshCol = root.GetComponent<MeshCollider>();
        if (!meshCol) meshCol = root.AddComponent<MeshCollider>();
        meshCol.sharedMesh = mesh;
        meshCol.convex = true;
        meshCol.isTrigger = true;

        // Also remove any MeshCollider on psObj to ensure exactly ONE collider
        var psMeshCol = psObj.GetComponent<MeshCollider>();
        if (psMeshCol && psObj != root) Object.DestroyImmediate(psMeshCol, true);

        // 4. Configure Rigidbody on root (Kinematic, not interpolated so rotation doesn't stutter)
        var oldRb = psObj.GetComponent<Rigidbody>();
        if (oldRb && psObj != root) Object.DestroyImmediate(oldRb, true);
        var rb = root.GetComponent<Rigidbody>();
        if (!rb) rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // 5. Configure bees script on root for constant rotation and audio
        var oldBees = psObj.GetComponent<bees>();
        if (oldBees && psObj != root) Object.DestroyImmediate(oldBees, true);
        var b = root.GetComponent<bees>();
        if (!b) b = root.AddComponent<bees>();
        b.spinSpeed = 240f; // Constant speed: 240 deg/sec

        // Remove old continuousRotation
        var rot = psObj.GetComponent<continuousRotation>();
        if (rot) Object.DestroyImmediate(rot, true);
        var rootRot = root.GetComponent<continuousRotation>();
        if (rootRot) Object.DestroyImmediate(rootRot, true);

        // AudioSource on root
        var psAudio = psObj.GetComponent<AudioSource>();
        if (psAudio && psObj != root)
        {
            var rootAudio = root.GetComponent<AudioSource>();
            if (!rootAudio) rootAudio = root.AddComponent<AudioSource>();
            EditorUtility.CopySerialized(psAudio, rootAudio);
            Object.DestroyImmediate(psAudio, true);
        }

        // 6. Configure ParticleSystem for a tight, beautiful ring of bees circling the tree
        var ps = psObj.GetComponent<ParticleSystem>();
        if (ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startLifetime = 4.0f;
            main.startSize = 0.28f;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = 60;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle; // Single flat 2D circle (type 10)
            shape.radius = 0.67f;                            // Exactly one circle hugging the trunk
            shape.radiusThickness = 0f;                      // 0 = purely on the single circle perimeter (no double ring or donut thickness)
            shape.donutRadius = 0f;
            shape.arc = 360f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 6f; // ~24 bees in the single circle

            main.maxParticles = 30;

            Debug.Log("[ConfigureBeeSwarm] ParticleSystem configured successfully.");
        }

        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[ConfigureBeeSwarm] Prefab beesParent saved successfully with Ring MeshCollider and constant circling rotation!");
    }

    static Mesh CreateRingColliderMesh(float radius, float height, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "BeeRingColliderMesh";

        Vector3[] vertices = new Vector3[segments * 2];
        float halfH = height * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // Top vertex (i)
            vertices[i] = new Vector3(x, halfH, z);
            // Bottom vertex (segments + i)
            vertices[segments + i] = new Vector3(x, -halfH, z);
        }

        List<int> triangles = new List<int>();

        // Side faces (connecting top and bottom)
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int t1 = i;
            int t2 = next;
            int b1 = segments + i;
            int b2 = segments + next;

            // Quad split into 2 triangles (outward facing)
            triangles.Add(t1);
            triangles.Add(t2);
            triangles.Add(b2);

            triangles.Add(t1);
            triangles.Add(b2);
            triangles.Add(b1);
        }

        // Top cap (fan)
        for (int i = 1; i < segments - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i);
        }

        // Bottom cap (fan)
        for (int i = 1; i < segments - 1; i++)
        {
            triangles.Add(segments);
            triangles.Add(segments + i);
            triangles.Add(segments + i + 1);
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
