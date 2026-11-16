using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class TCBakeBatPrefab
{
    public const string PrefabPath = "Assets/prefabs/Bat_Enemy.prefab";
    public const string ResourcesPrefabPath = "Assets/TC/Resources/Enemies/Bat_Enemy.prefab";
    public const string AnimPath = "Assets/TC/Resources/Enemies/Bat_Fly.anim";
    public const string ControllerPath = "Assets/TC/Resources/Enemies/Bat_Controller.controller";

    static TCBakeBatPrefab()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(PrefabPath) || !File.Exists(ResourcesPrefabPath))
            {
                Bake();
            }
        };
    }

    [MenuItem("Tools/Tall Crawl/Bake Bat Enemy Prefab")]
    public static void Bake()
    {
        Directory.CreateDirectory("Assets/prefabs");
        Directory.CreateDirectory("Assets/TC/Resources/Enemies");

        // 1. Ensure Bat Material with crisp White Outline
        var shader = Shader.Find("TallCrawl/SpiderPreBaked")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Mobile/Diffuse");

        string matPath = "Assets/TC/Resources/Enemies/Bat_BodyMat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = shader;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Resources/Enemies/Bat_Texture.png");
        if (tex) mat.mainTexture = tex;

        mat.color = Color.white;
        if (mat.HasProperty("_Brightness")) mat.SetFloat("_Brightness", 1.05f);
        if (mat.HasProperty("_ShadowDull")) mat.SetFloat("_ShadowDull", 0.75f);
        if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", Color.white);
        if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0.30f);
        if (mat.HasProperty("_EdgeThreshold")) mat.SetFloat("_EdgeThreshold", 0.0f);
        if (mat.HasProperty("_CelIntensity")) mat.SetFloat("_CelIntensity", 0.35f);
        EditorUtility.SetDirty(mat);

        // 2. Load 3D Meshes from the uploaded model
        var bodyMesh = TCEnemy.GetBatMesh("Body");
        var wingLMesh = TCEnemy.GetBatMesh("WingL");
        var wingRMesh = TCEnemy.GetBatMesh("WingR");

        // 3. Create Looping Flight Animation Clip
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath);
        if (!clip)
        {
            clip = new AnimationClip();
            clip.name = "Bat_Fly";
            AssetDatabase.CreateAsset(clip, AnimPath);
        }

        clip.frameRate = 30f;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Build animation curves for 0.28s cycle
        float dur = 0.28f;
        Keyframe[] kfWingL_Z = {
            new Keyframe(0.00f * dur,   0f, -400f, -400f),
            new Keyframe(0.25f * dur, -38f,    0f,    0f),
            new Keyframe(0.50f * dur,   0f,  420f,  420f),
            new Keyframe(0.75f * dur,  44f,    0f,    0f),
            new Keyframe(1.00f * dur,   0f, -400f, -400f)
        };
        Keyframe[] kfWingR_Z = {
            new Keyframe(0.00f * dur,   0f,  400f,  400f),
            new Keyframe(0.25f * dur,  38f,    0f,    0f),
            new Keyframe(0.50f * dur,   0f, -420f, -420f),
            new Keyframe(0.75f * dur, -44f,    0f,    0f),
            new Keyframe(1.00f * dur,   0f,  400f,  400f)
        };

        Keyframe[] kfWingL_X = {
            new Keyframe(0.00f * dur,   0f),
            new Keyframe(0.25f * dur,  12f),
            new Keyframe(0.50f * dur, -10f),
            new Keyframe(0.75f * dur, -16f),
            new Keyframe(1.00f * dur,   0f)
        };
        Keyframe[] kfWingR_X = {
            new Keyframe(0.00f * dur,   0f),
            new Keyframe(0.25f * dur,  12f),
            new Keyframe(0.50f * dur, -10f),
            new Keyframe(0.75f * dur, -16f),
            new Keyframe(1.00f * dur,   0f)
        };

        Keyframe[] kfBody_Y = {
            new Keyframe(0.00f * dur,  0.000f),
            new Keyframe(0.25f * dur,  0.022f), // Lift push
            new Keyframe(0.50f * dur,  0.005f),
            new Keyframe(0.75f * dur, -0.015f), // Upstroke sink
            new Keyframe(1.00f * dur,  0.000f)
        };

        clip.SetCurve("BatWingPivot_L", typeof(Transform), "localEulerAnglesRaw.z", new AnimationCurve(kfWingL_Z));
        clip.SetCurve("BatWingPivot_L", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(kfWingL_X));
        clip.SetCurve("BatWingPivot_R", typeof(Transform), "localEulerAnglesRaw.z", new AnimationCurve(kfWingR_Z));
        clip.SetCurve("BatWingPivot_R", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(kfWingR_X));
        clip.SetCurve("Body", typeof(Transform), "m_LocalPosition.y", new AnimationCurve(kfBody_Y));
        EditorUtility.SetDirty(clip);

        // 4. Create Animator Controller
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }
        var stateMachine = controller.layers[0].stateMachine;
        if (stateMachine.states.Length == 0)
        {
            var state = stateMachine.AddState("Bat_Fly");
            state.motion = clip;
            stateMachine.defaultState = state;
        }
        else
        {
            stateMachine.states[0].state.motion = clip;
        }
        EditorUtility.SetDirty(controller);

        // 5. Build Prefab Hierarchy
        var root = new GameObject("Bat_Visual");

        // Animator component
        var anim = root.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Central Body
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(root.transform, false);
        bodyGo.transform.localPosition = Vector3.zero;
        var bMf = bodyGo.AddComponent<MeshFilter>();
        bMf.sharedMesh = bodyMesh;
        var bMr = bodyGo.AddComponent<MeshRenderer>();
        bMr.sharedMaterial = mat;
        bMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bMr.receiveShadows = false;

        // Left Wing Pivot at anatomical shoulder joint
        var wingPivotL = new GameObject("BatWingPivot_L");
        wingPivotL.transform.SetParent(root.transform, false);
        wingPivotL.transform.localPosition = new Vector3(-0.065f, 0.055f, 0.002f);

        var wingGoL = new GameObject("WingMesh_L");
        wingGoL.transform.SetParent(wingPivotL.transform, false);
        var mfL = wingGoL.AddComponent<MeshFilter>();
        mfL.sharedMesh = wingLMesh;
        var mrL = wingGoL.AddComponent<MeshRenderer>();
        mrL.sharedMaterial = mat;
        mrL.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrL.receiveShadows = false;

        // Right Wing Pivot at anatomical shoulder joint
        var wingPivotR = new GameObject("BatWingPivot_R");
        wingPivotR.transform.SetParent(root.transform, false);
        wingPivotR.transform.localPosition = new Vector3(0.065f, 0.055f, 0.002f);

        var wingGoR = new GameObject("WingMesh_R");
        wingGoR.transform.SetParent(wingPivotR.transform, false);
        var mfR = wingGoR.AddComponent<MeshFilter>();
        mfR.sharedMesh = wingRMesh;
        var mrR = wingGoR.AddComponent<MeshRenderer>();
        mrR.sharedMaterial = mat;
        mrR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrR.receiveShadows = false;

        // 6. Save Prefabs
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(root, ResourcesPrefabPath);

        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TCBakeBatPrefab] Successfully baked bat enemy prefab with animations and white outline at " + PrefabPath);
    }
}
