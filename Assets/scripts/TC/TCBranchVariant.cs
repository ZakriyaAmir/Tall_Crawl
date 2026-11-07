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
        Pink = 3,
        Blue = 4
    }

    static Material matNone, matRed, matYellow, matPink, matBlue;
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
        if (!matBlue) matBlue = Resources.Load<Material>("Buisson_Atlas_Blue") ?? LoadMatFromPath("Assets/TC/Resources/Buisson_Atlas_Blue.mat");

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

                // Subtly tone down prefab bush scale so foliage does not block player sightlines
                r.transform.localScale = Vector3.Scale(r.transform.localScale, new Vector3(0.75f, 0.75f, 0.75f));
            }
        }

        // Keep branches clean and open for clear visibility (no extra cluttering bush clusters)

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
    /// Applies one of the 5 flower/leaf variants to this branch
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
            case FlowerType.Blue:
                targetMat = matBlue ? matBlue : matNone;
                break;
            default:
                targetMat = matNone;
                break;
        }

        foreach (var mr in foliageRenderers)
        {
            if (mr && targetMat) mr.sharedMaterial = targetMat;
        }

        var thornSpikes = transform.Find("thorn_spikes");
        if (thornSpikes) thornSpikes.gameObject.SetActive(false);
    }
}
