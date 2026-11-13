using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TCBakeSquirrelLeavesWarning
{
    const string PrefabPath = "Assets/prefabs/SquirrelLeavesWarning.prefab";
    const string ResourcesPrefabPath = "Assets/Resources/SquirrelLeavesWarning.prefab";

    static TCBakeSquirrelLeavesWarning()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(PrefabPath) || !File.Exists(ResourcesPrefabPath))
            {
                Bake();
            }
        };
    }

    [MenuItem("Tools/Tall Crawl/Bake Squirrel Leaves Warning Prefab")]
    public static void Bake()
    {
        // 1. Ensure Leaf Texture asset exists
        string texPath = "Assets/TC/Resources/FX/fx_leaf.png";
        if (!File.Exists(texPath))
        {
            var tex = TCSquirrel.GenerateLeafTexture();
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(texPath, png);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            if (ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
        }

        // 2. Ensure Leaf Material asset exists
        string matPath = "Assets/TC/Resources/FX/Materials/FX_SquirrelLeaves.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat)
        {
            Shader shader = Shader.Find("TallCrawl/FX Particle")
                ?? Shader.Find("Mobile/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Unlit/Transparent");
            mat = new Material(shader) { name = "FX_SquirrelLeaves" };
            var leafTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (leafTex)
            {
                mat.mainTexture = leafTex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", leafTex);
            }
            if (mat.HasProperty("_Additive")) mat.SetFloat("_Additive", 0f);
            if (mat.HasProperty("_DepthPull")) mat.SetFloat("_DepthPull", 0.32f);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // 3. Create the GameObject and ParticleSystem
        var go = new GameObject("SquirrelLeavesWarning");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1.0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.96f, 1.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(0.095f, 0.145f); // 30% smaller, proportional 1:1 fitting particle size
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.55f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var grad = new ParticleSystem.MinMaxGradient(
            new Color(0.42f, 0.86f, 0.22f, 0.98f),
            new Color(0.88f, 0.74f, 0.16f, 0.98f)
        );
        main.startColor = grad;

        var em = ps.emission;
        em.rateOverTime = 18f;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0.0f, 9, 13),
            new ParticleSystem.Burst(0.25f, 6, 9),
            new ParticleSystem.Burst(0.50f, 6, 9),
            new ParticleSystem.Burst(0.75f, 5, 7),
            new ParticleSystem.Burst(1.00f, 4, 6)
        });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.16f; // Tightly centered over squirrel run lane
        sh.position = new Vector3(-0.10f, 0f, 0f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f); // Broader horizontal drift across the lane
        vel.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.75f);
        vel.z = new ParticleSystem.MinMaxCurve(0.01f, 0.02f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
        rot.y = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);
        rot.z = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.12f, 1.0f);
        sizeCurve.AddKey(0.80f, 0.95f);
        sizeCurve.AddKey(1f, 0.2f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var alphaGrad = new Gradient();
        alphaGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.95f, 0.72f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = alphaGrad;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend)
        {
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.sortingOrder = 1;
            rend.sortingFudge = -2;
        }

        Directory.CreateDirectory("Assets/prefabs");
        Directory.CreateDirectory("Assets/Resources");

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, ResourcesPrefabPath);

        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TCBakeSquirrelLeavesWarning] Prefab baked at " + PrefabPath + " and " + ResourcesPrefabPath);
    }
}
