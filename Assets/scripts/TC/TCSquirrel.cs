using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Animated Squirrel: scampers swiftly head-first down the trunk!
/// Features full quadruped running animation:
/// - Fast alternating front and hind leg scampering
/// - Rhythmic body pitch and spine flexion
/// - Dynamic springy bushy tail waving
/// - Alert head bobbing
/// </summary>
public class TCSquirrel : TCEnemy
{
    public Transform bodyPivot;
    public Transform head, tail;
    public Transform legFL, legFR, legBL, legBR;
    public float runSpeed = 1.6f;

    float delayTimer = 0f;
    bool hasPlayedSquirrelSound;
    ParticleSystem warningPS;
    GameObject warningGO;
    float warningAngleDeg;

    static player cachedPlayer;
    static playerInteract cachedInteract;

    static bool HasPlayerStartedAndCovered(float minDistanceMetres)
    {
        if (IsGameOverOrDead()) return false;
        // Tutorial mode scripts explicitly spawn and handle the tutorial squirrel
        if (TCGameUI.Instance != null && TCGameUI.Instance.IsTutorialRun) return true;

        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        if (cachedPlayer != null)
        {
            if (!cachedPlayer.GameOver && cachedPlayer.paused) return false;
            if (TCGameUI.Instance != null && !TCGameUI.Instance.Started) return false;
            return cachedPlayer.currentScore >= minDistanceMetres;
        }
        return false;
    }

    static bool IsPlayerInChilliMode()
    {
        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        if (cachedPlayer != null)
        {
            if (cachedPlayer.boost) return true;
            if (!cachedInteract) cachedInteract = cachedPlayer.GetComponentInChildren<playerInteract>();
            if (cachedInteract != null && cachedInteract.boostDuration > 0f) return true;
        }
        return false;
    }

    static float GetPlayerY()
    {
        if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
        return cachedPlayer != null ? cachedPlayer.transform.position.y : 0f;
    }

    static float GetTopCameraViewY(float laneAngleDeg)
    {
        Camera cam = Camera.main;
        if (!cam)
        {
            if (!cachedPlayer) cachedPlayer = FindObjectOfType<player>();
            if (cachedPlayer && cachedPlayer.maincam) cam = cachedPlayer.maincam;
        }

        if (cam != null)
        {
            // Viewport point (0.5, 1.02) corresponds to the top center of the camera screen
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 1.02f, 0f));
            float dx = ray.direction.x;
            float dz = ray.direction.z;
            float ox = ray.origin.x;
            float oz = ray.origin.z;
            float a = dx * dx + dz * dz;
            float b = 2f * (ox * dx + oz * dz);
            float c = ox * ox + oz * oz - (0.603f * 0.603f);
            float disc = b * b - 4f * a * c;
            if (a > 0.0001f && disc >= 0f)
            {
                float t1 = (-b - Mathf.Sqrt(disc)) / (2f * a);
                float t2 = (-b + Mathf.Sqrt(disc)) / (2f * a);
                float t = (t1 > 0f) ? t1 : t2;
                if (t > 0f)
                {
                    return ray.origin.y + ray.direction.y * t;
                }
            }
        }
        return GetPlayerY() + 5.8f;
    }

    public override void Init(float startY, float startAngle, float sweep = 40f, float spd = 2.2f)
    {
        base.Init(startY, startAngle, sweep, spd);
        runSpeed = Random.Range(1.4f, 1.9f);
        hasPlayedSquirrelSound = false;

        // Clean up any previous warning particles if recycled from pool
        if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); warningPS = null; }
        if (warningGO) { Object.Destroy(warningGO); warningGO = null; }

        // Ensure model and hit colliders start hidden and disabled
        if (model) model.gameObject.SetActive(false);
        SetCollidersActive(false);

        // If player is already in chilli boost mode, start scampering immediately, play sound, and DO NOT spawn leaves
        if (IsPlayerInChilliMode() && HasPlayerStartedAndCovered(200f))
        {
            delayTimer = 0f;
            if (model) model.gameObject.SetActive(true);
            SetCollidersActive(true);
            hasPlayedSquirrelSound = true;
            TCSfx.Play("squirrel", 0.8f);
        }
        else
        {
            delayTimer = 2.0f; // Leaves fall from top of camera view 2.0s before squirrel arrives
            if (HasPlayerStartedAndCovered(200f))
            {
                SpawnFallingLeavesWarning(startAngle);
            }
        }
    }

    void SetCollidersActive(bool active)
    {
        var cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i]) cols[i].enabled = active;
        }
    }

    static Texture2D _leafTex;
    static Material _leafMat;

    public static Texture2D GenerateLeafTexture() => GetLeafTexture();

    static Texture2D GetLeafTexture()
    {
        if (_leafTex) return _leafTex;
        const int W = 64;
        const int H = 64;
        _leafTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            name = "TC_ProceduralLeaf",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float ny = (y - 4f) / (H - 8f); // 0 at stem base, 1 at leaf tip
            for (int x = 0; x < W; x++)
            {
                float nx = (x - W * 0.5f) / (W * 0.5f); // -1 to 1 across leaf width
                if (ny <= 0f || ny >= 1f)
                {
                    px[y * W + x] = Color.clear;
                    continue;
                }

                // Natural pointed oval leaf profile: rounded belly at ny ~ 0.38, tapering to tip
                float maxWidth = Mathf.Sin(ny * Mathf.PI) * (1.1f - ny * 0.45f);
                float absX = Mathf.Abs(nx);
                if (absX > maxWidth)
                {
                    px[y * W + x] = Color.clear;
                    continue;
                }

                float edgeDist = 1f - (absX / Mathf.Max(0.01f, maxWidth));
                float alpha = Mathf.Clamp01(edgeDist * 4f);

                // Leaf center vein line
                float vein = Mathf.Clamp01(Mathf.Abs(nx) * 12f);
                float shade = Mathf.Lerp(0.82f, 1.0f, vein);

                px[y * W + x] = new Color(shade, shade, shade, alpha);
            }
        }
        _leafTex.SetPixels(px);
        _leafTex.Apply(false, true);
        return _leafTex;
    }

    static Material GetLeafMaterial()
    {
        if (_leafMat) return _leafMat;
        Shader sh = Shader.Find("TallCrawl/FX Particle")
            ?? Shader.Find("Mobile/Particles/Alpha Blended")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Unlit/Transparent");
        _leafMat = new Material(sh) { name = "TC_SquirrelLeavesMat" };
        var tex = GetLeafTexture();
        if (tex)
        {
            _leafMat.mainTexture = tex;
            if (_leafMat.HasProperty("_BaseMap")) _leafMat.SetTexture("_BaseMap", tex);
        }
        if (_leafMat.HasProperty("_Additive")) _leafMat.SetFloat("_Additive", 0f);
        if (_leafMat.HasProperty("_DepthPull")) _leafMat.SetFloat("_DepthPull", 0.32f);
        return _leafMat;
    }

    public override void Smash(Vector3? hitSourcePos = null)
    {
        if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmitting); warningPS = null; }
        if (warningGO) { Object.Destroy(warningGO, 2.0f); warningGO = null; }
        base.Smash(hitSourcePos);
    }

    void OnDisable()
    {
        if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmitting); warningPS = null; }
        if (warningGO) { Object.Destroy(warningGO); warningGO = null; }
    }

    static GameObject warningPrefab;

    void SpawnFallingLeavesWarning(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float r = radius + 0.04f;
        float topY = GetTopCameraViewY(angleDeg);
        Vector3 spawnWorldPos = new Vector3(Mathf.Sin(rad) * r, topY, Mathf.Cos(rad) * r);
        Vector3 outward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        Quaternion spawnWorldRot = Quaternion.LookRotation(outward, Vector3.up);

        // Load prefab from Resources
        if (!warningPrefab)
        {
            warningPrefab = Resources.Load<GameObject>("SquirrelLeavesWarning");
#if UNITY_EDITOR
            if (!warningPrefab)
                warningPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/SquirrelLeavesWarning.prefab");
#endif
        }

        if (!warningPrefab) return;

        // Instantiate pure prefab directly with zero runtime property alterations
        GameObject go = Object.Instantiate(warningPrefab, spawnWorldPos, spawnWorldRot);
        go.name = "SquirrelLeavesWarning";
        go.transform.SetParent(null);
        warningGO = go;
        warningAngleDeg = angleDeg;

        var ps = go.GetComponent<ParticleSystem>();
        warningPS = ps;
        if (ps)
        {
            var sh = ps.shape;
            sh.position = new Vector3(-0.10f, 0f, 0f);
            ps.Play(true);
        }
        Object.Destroy(go, 12.0f);
    }

    void Update()
    {
        if (isSmashed || IsGameOverOrDead())
        {
            if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); warningPS = null; }
            if (warningGO) { Object.Destroy(warningGO); warningGO = null; }
            return;
        }
        // Squirrel only comes into action once the player has started moving and covered 200m
        if (!HasPlayerStartedAndCovered(200f)) return;

        float dt = Time.deltaTime;
        bool isChilli = IsPlayerInChilliMode();

        // Never show leaves during chilli mode: clear immediately if player enters chilli mode
        if (isChilli)
        {
            if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); warningPS = null; }
            if (warningGO) { Object.Destroy(warningGO); warningGO = null; }
            delayTimer = 0f;
            if (model && !model.gameObject.activeSelf) model.gameObject.SetActive(true);
            SetCollidersActive(true);
            if (!hasPlayedSquirrelSound)
            {
                hasPlayedSquirrelSound = true;
                TCSfx.Play("squirrel", 0.8f);
            }
        }

        if (delayTimer > 0f)
        {
            // If the player is in chilli mode or has climbed close to the squirrel, immediately wake up and scamper down
            float distToPlayer = y - GetPlayerY();
            if (isChilli || distToPlayer < 3.5f)
            {
                delayTimer = 0f;
            }
            else
            {
                delayTimer -= dt;
            }

            if (delayTimer <= 0f)
            {
                delayTimer = 0f;
                // Cease warning particle emission when the squirrel arrives from top of camera view
                if (warningPS) { warningPS.Stop(true, ParticleSystemStopBehavior.StopEmitting); warningPS = null; }
                if (warningGO) { Object.Destroy(warningGO, 2.5f); warningGO = null; }
                if (model) model.gameObject.SetActive(true);
                SetCollidersActive(true);
                if (!hasPlayedSquirrelSound)
                {
                    hasPlayedSquirrelSound = true;
                    TCSfx.Play("squirrel", 0.8f);
                }
            }
            else
            {
                // While waiting during advance warning, keep the falling leaves emitter positioned vertically at top of camera view
                if (warningGO)
                {
                    float rad = warningAngleDeg * Mathf.Deg2Rad;
                    float r = radius + 0.04f;
                    float topY = GetTopCameraViewY(warningAngleDeg);
                    warningGO.transform.position = new Vector3(Mathf.Sin(rad) * r, topY, Mathf.Cos(rad) * r);
                }

                // While waiting during advance warning, ensure model stays hidden so it never appears as a frozen statue
                if (model && model.gameObject.activeSelf) model.gameObject.SetActive(false);
                transform.position = new Vector3(0, y, 0);
                transform.rotation = Quaternion.Euler(0, baseAngle, 0);
                return;
            }
        }

        float t = Time.time;

        // 1. Scamper down the tree trunk (accelerated sprint in chilli mode)
        float moveSpeed = isChilli ? (runSpeed * 1.5f) : runSpeed;
        y -= moveSpeed * dt;

        // 2. Only run fallback procedural animation if TCSquirrelRig is not present on bodyPivot
        bool hasRig = bodyPivot && bodyPivot.GetComponent<TCSquirrelRig>() != null;
        if (!hasRig)
        {
            float runFreq = isChilli ? 26f : 18f;
            float legCycle = t * runFreq;
            float angleFL = Mathf.Sin(legCycle) * 38f;
            float angleFR = -Mathf.Sin(legCycle) * 38f;
            float angleBL = -Mathf.Sin(legCycle) * 42f;
            float angleBR = Mathf.Sin(legCycle) * 42f;

            if (legFL) legFL.localRotation = Quaternion.Euler(angleFL, 0, 0);
            if (legFR) legFR.localRotation = Quaternion.Euler(angleFR, 0, 0);
            if (legBL) legBL.localRotation = Quaternion.Euler(angleBL, 0, 0);
            if (legBR) legBR.localRotation = Quaternion.Euler(angleBR, 0, 0);

            float gallop = Mathf.Abs(Mathf.Sin(legCycle * 0.5f));
            if (bodyPivot)
            {
                float pitch = 90f + Mathf.Sin(legCycle) * 6f;
                float roll = Mathf.Sin(legCycle * 0.5f) * 4f;
                bodyPivot.localRotation = Quaternion.Euler(pitch, 0, roll);
            }

            if (tail)
            {
                float tailWhip = Mathf.Sin(legCycle - 1.2f) * 16f;
                float tailSway = Mathf.Cos(legCycle * 0.5f) * 10f;
                tail.localRotation = Quaternion.Euler(tailWhip, tailSway, 0);
            }

            if (head)
            {
                float headBob = Mathf.Sin(legCycle * 0.5f) * 5f;
                head.localRotation = Quaternion.Euler(headBob + 8f, 0, 0);
            }

            if (model)
            {
                if (!model.gameObject.activeSelf) model.gameObject.SetActive(true);
                float hop = gallop * 0.04f;
                model.localPosition = new Vector3(0, hop, radius);
            }
        }
        else
        {
            if (model && !model.gameObject.activeSelf) model.gameObject.SetActive(true);
        }

        transform.position = new Vector3(0, y, 0);
        transform.rotation = Quaternion.Euler(0, baseAngle, 0);
    }
}
