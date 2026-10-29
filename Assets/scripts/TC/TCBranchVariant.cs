using System.Collections.Generic;
using UnityEngine;

/// Manages branch visual variants:
/// 4 variants:
///  0: No flowers (lush foliage and leaves only)
///  1: Red flowers (with lush foliage and leaves)
///  2: Yellow flowers (with lush foliage and leaves)
///  3: Pink flowers (with lush foliage and leaves)
/// Also enriches every branch with extra lush foliage clusters for a full, natural cartoon canopy.
public class TCBranchVariant : MonoBehaviour
{
    public enum FlowerType
    {
        None = 0,
        Red = 1,
        Yellow = 2,
        Pink = 3
    }

    static Material matNone, matRed, matYellow, matPink;
    static Mesh bushMesh;
    static Material woodMat;

    readonly List<MeshRenderer> foliageRenderers = new List<MeshRenderer>(8);
    bool initialized;

    static void EnsureResources()
    {
        if (!matNone) matNone = Resources.Load<Material>("Buisson_Atlas_None") ?? LoadMatFromPath("Assets/objects/models/bush/source/Buisson_Atlas_None.mat");
        if (!matRed) matRed = Resources.Load<Material>("Buisson_Atlas_Red") ?? LoadMatFromPath("Assets/objects/models/bush/source/Buisson_Atlas_Red.mat");
        if (!matYellow) matYellow = Resources.Load<Material>("Buisson_Atlas_Yellow") ?? LoadMatFromPath("Assets/objects/models/bush/source/Buisson_Atlas_Yellow.mat");
        if (!matPink) matPink = Resources.Load<Material>("Buisson_Atlas_Pink") ?? LoadMatFromPath("Assets/objects/models/bush/source/Buisson_Atlas_Pink.mat");

        if (!woodMat) woodMat = LoadMatFromPath("Assets/objects/materials/log.mat");
    }

    static Material LoadMatFromPath(string path)
    {
#if UNITY_EDITOR
        var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
#endif
        return null;
    }

    void Awake()
    {
        EnsureResources();
        InitFoliage();
    }

    void InitFoliage()
    {
        if (initialized) return;
        initialized = true;

        foliageRenderers.Clear();

        // Find existing bush renderers on the branch (searches in all children and prefab instances)
        var allRenderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in allRenderers)
        {
            string n = r.name.ToLower();
            string matName = (r.sharedMaterial != null) ? r.sharedMaterial.name : "";
            if (n.Contains("bush") || n.Contains("buisson") || matName.Contains("Buisson") || matName.Contains("bush"))
            {
                foliageRenderers.Add(r);
                if (!bushMesh)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf) bushMesh = mf.sharedMesh;
                }
            }
        }

        // Add extra leaf clusters along the branch for lots of lush foliage
        if (bushMesh && foliageRenderers.Count > 0)
        {
            AddLeafCluster(new Vector3(-0.04f, 0.06f, 0.45f), new Vector3(0.55f, 0.55f, 0.55f), Quaternion.Euler(-15f, 35f, 10f));
            AddLeafCluster(new Vector3(0.04f, 0.04f, 0.85f), new Vector3(0.65f, 0.65f, 0.65f), Quaternion.Euler(20f, -45f, -15f));
            AddLeafCluster(new Vector3(0.00f, 0.08f, 1.25f), new Vector3(0.70f, 0.70f, 0.70f), Quaternion.Euler(-10f, 75f, 20f));
        }

        // Ensure log arm uses the cartoon wood material
        var logObj = transform.Find("log");
        if (logObj && woodMat)
        {
            var mr = logObj.GetComponent<MeshRenderer>();
            if (mr) mr.sharedMaterial = woodMat;
        }
    }

    void AddLeafCluster(Vector3 localPos, Vector3 scale, Quaternion localRot)
    {
        var leafObj = new GameObject("bush_extra");
        leafObj.transform.SetParent(transform, false);
        leafObj.transform.localPosition = localPos;
        leafObj.transform.localScale = scale;
        leafObj.transform.localRotation = localRot;

        var mf = leafObj.AddComponent<MeshFilter>();
        mf.sharedMesh = bushMesh;

        var mr = leafObj.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        foliageRenderers.Add(mr);
    }

    /// <summary>
    /// Applies one of the 4 flower/leaf variants to this branch
    /// </summary>
    public void SetVariant(FlowerType type)
    {
        EnsureResources();
        if (!initialized) InitFoliage();

        Material targetMat = matNone;
        switch (type)
        {
            case FlowerType.Red:
                targetMat = matRed ? matRed : matNone;
                break;
            case FlowerType.Yellow:
                targetMat = matYellow ? matYellow : matNone;
                break;
            case FlowerType.Pink:
                targetMat = matPink ? matPink : matNone;
                break;
            default:
                targetMat = matNone;
                break;
        }

        foreach (var mr in foliageRenderers)
        {
            if (mr && targetMat) mr.sharedMaterial = targetMat;
        }
    }
}
