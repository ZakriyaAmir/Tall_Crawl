using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Game-feel helpers: pooled one-shot particles and camera shake (applied after the camera Animator).
public class TCFx : MonoBehaviour
{
    static TCFx inst;
    readonly Dictionary<GameObject, TCPool> pools = new Dictionary<GameObject, TCPool>();
    float shake; Vector3 lastOffset;
    Transform cam;

    static TCFx I { get { if (!inst) inst = new GameObject("TCFx").AddComponent<TCFx>(); return inst; } }

    public static void Spawn(GameObject prefab, Vector3 pos)
    {
        if (!prefab) return;
        var me = I;
        if (!me.pools.TryGetValue(prefab, out var pool)) { pool = new TCPool(prefab, me.transform, 6); me.pools[prefab] = pool; }
        var g = pool.Get(pos, Quaternion.identity);
        foreach (var ps in g.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Clear();
            ps.Play();
            // Force immediate burst emission so one-shot impact effects don't depend solely on rateOverTime
            int burstCount = Mathf.Max(12, ps.main.maxParticles > 0 ? Mathf.Min(24, ps.main.maxParticles) : 16);
            ps.Emit(burstCount);
        }
        me.StartCoroutine(me.Return(pool, g));
    }
    System.Collections.IEnumerator Return(TCPool p, GameObject g) { yield return new WaitForSeconds(1.2f); p.Release(g); }

    // ------------------------------------------------------------------ Dedicated Wood Impact Burst FX
    static readonly List<ParticleSystem> woodBurstPool = new List<ParticleSystem>();
    static int nextWoodBurst = 0;
    static Transform woodBurstHost;

    /// Spawns high-visibility wood splinters, sawdust puffs, and impact sparks at the exact contact point.
    /// Offset slightly along the camera view line so particles are never buried or clipped inside the trunk/branch geometry.
    public static void SpawnWoodImpact(Vector3 hitPoint, GameObject prefab = null)
    {
        if (prefab != null)
        {
            Spawn(prefab, hitPoint);
        }

        if (!Application.isPlaying) return;

        if (!woodBurstHost)
        {
            woodBurstPool.Clear();
            nextWoodBurst = 0;
            var pl = Object.FindObjectOfType<player>();
            var hostGo = new GameObject("TC_WoodImpactBursts");
            woodBurstHost = hostGo.transform;
            if (pl) woodBurstHost.SetParent(pl.p1 ? pl.p1 : pl.transform, false);
        }

        Vector3 spawnPos = hitPoint;
        var cam = Camera.main;
        if (cam)
        {
            // Pull slightly towards camera (0.18m) so it renders prominently in front of trunk bark / branch wood
            Vector3 camDir = (cam.transform.position - hitPoint).normalized;
            spawnPos = hitPoint + camDir * 0.18f;
        }

        const int cap = 4;
        ParticleSystem ps;
        if (woodBurstPool.Count < cap)
        {
            ps = BuildWoodImpactBurst(woodBurstHost);
            woodBurstPool.Add(ps);
        }
        else
        {
            ps = woodBurstPool[nextWoodBurst];
            nextWoodBurst = (nextWoodBurst + 1) % cap;
        }

        ps.transform.position = spawnPos;
        if (cam) ps.transform.rotation = Quaternion.LookRotation(cam.transform.position - spawnPos, cam.transform.up);

        ps.Clear(true);
        ps.Play(true);
    }

    static ParticleSystem BuildWoodImpactBurst(Transform host)
    {
        // Root container: sawdust cloud burst (natural wood dust, no sharp glow or spark)
        var root = TCFxLib.PS(host, "WoodImpactBurst", "Smoke", Vector3.zero, 24);
        TCFxLib.OneShot(root, 0.45f, 0.75f);
        var rMain = root.main;
        rMain.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
        rMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        rMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.70f, 0.52f, 0.85f), // natural wood sawdust
            new Color(0.62f, 0.45f, 0.30f, 0.75f)  // soft bark brown
        );
        rMain.gravityModifier = -0.05f; // drifts softly upward
        TCFxLib.Burst(root, 14);
        TCFxLib.Sphere(root, 0.18f);
        TCFxLib.SpinRandom(root, 100f);
        TCFxLib.SizeLife(root, 0, 0.4f, 0.25f, 1f, 1f, 1.4f);
        TCFxLib.ColorLife(root, TCFxLib.Fade(new Color(0.88f, 0.74f, 0.56f, 0.85f), new Color(0.60f, 0.42f, 0.26f, 0.6f), 0.05f, 0.45f));

        // Soft wood dust debris / powder puff
        var powder = TCFxLib.PS(root.transform, "SawdustPowder", "Smoke", Vector3.zero, 16);
        TCFxLib.OneShot(powder, 0.35f, 0.55f);
        var pMain = powder.main;
        pMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
        pMain.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.40f);
        pMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.92f, 0.80f, 0.64f, 0.7f),
            new Color(0.72f, 0.55f, 0.38f, 0.6f)
        );
        pMain.gravityModifier = 0.05f;
        TCFxLib.Burst(powder, 10);
        TCFxLib.Sphere(powder, 0.12f);
        TCFxLib.SpinRandom(powder, 80f);
        TCFxLib.SizeLife(powder, 0, 0.5f, 0.2f, 1f, 1f, 1.3f);
        TCFxLib.ColorLife(powder, TCFxLib.Fade(new Color(0.90f, 0.78f, 0.62f, 0.75f), new Color(0.68f, 0.50f, 0.34f, 0.5f), 0.04f, 0.4f));

        return root;
    }

    public static void Shake(float amount) { I.shake = Mathf.Max(I.shake, amount); }

    public static void ScatterBeeSwarm(GameObject beeRoot, Vector3 spiderPos, float duration = 1.0f)
    {
        TCHitFlash.ScatterBeeSwarm(beeRoot, spiderPos, duration);
    }

    void LateUpdate()
    {
        if (!cam && Camera.main) cam = Camera.main.transform;
        if (!cam) return;
        cam.localPosition -= lastOffset;
        lastOffset = Vector3.zero;
        if (shake > 0.001f && Time.timeScale > 0)
        {
            lastOffset = Random.insideUnitSphere * shake * 0.25f;
            cam.localPosition += lastOffset;
            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.2f);
        }
    }
}

/// <summary>
/// Arcade-style pure white flash mask when enemies or branches are smashed/destroyed.
/// <summary>
/// Spawns a smooth fade-away ghost for branches or enemies when hit/destroyed by the spider.
/// Clones the renderers, preserving their textures and colors with a transparent fade shader,
/// and smoothly animates alpha from 1.0 down to 0.0 over 0.5s before cleanly destroying itself.
/// </summary>
public static class TCHitFlash
{
    static Shader fadeShader;

    public static Shader FadeShader
    {
        get
        {
            if (!fadeShader)
            {
                fadeShader = Shader.Find("TallCrawl/TCFadeEntity")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Mobile/Particles/Alpha Blended")
                    ?? Shader.Find("Unlit/Transparent");
            }
            return fadeShader;
        }
    }

    /// <summary>
    /// Backward-compatible alias for FadeAndDestroy.
    /// </summary>
    public static void FlashAndDestroy(GameObject target, float duration = 0.5f)
    {
        FadeAndDestroy(target, duration);
    }

    /// <summary>
    /// When hit by spider in chilli mode, active bees in the swarm scatter and fly away dynamically off the tree.
    /// </summary>
    public static void ScatterBeeSwarm(GameObject beeRoot, Vector3 spiderPos, float duration = 1.0f)
    {
        if (!beeRoot) return;

        // Prevent recursive clone-on-clone triggering or scattering already handled bee objects
        if (beeRoot.name.StartsWith("BeesScattering_Clone") || beeRoot.CompareTag("Untagged"))
            return;

        var sourcePS = beeRoot.GetComponentInChildren<ParticleSystem>();
        if (!sourcePS) return;

        // Immediately disable all colliders on source so physics/sweeps cannot detect it again
        var sourceColliders = beeRoot.GetComponentsInChildren<Collider>();
        for (int i = 0; i < sourceColliders.Length; i++)
        {
            if (sourceColliders[i]) sourceColliders[i].enabled = false;
        }

        int maxP = sourcePS.main.maxParticles;
        var existing = new ParticleSystem.Particle[maxP];
        int count = sourcePS.GetParticles(existing);
        if (count == 0) return;

        // Clone the particle system object to fly away independently
        var host = Object.Instantiate(sourcePS.gameObject, sourcePS.transform.position, sourcePS.transform.rotation);
        host.name = "BeesScattering_Clone";
        host.tag = "Untagged";
        host.layer = 2; // Ignore Raycast layer

        // Immediately disable and destroy colliders, rigidbodies, and audio on clone
        foreach (var col in host.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
            Object.Destroy(col);
        }
        foreach (var rb in host.GetComponentsInChildren<Rigidbody>())
        {
            Object.Destroy(rb);
        }
        foreach (var aud in host.GetComponentsInChildren<AudioSource>())
        {
            aud.Stop();
            Object.Destroy(aud);
        }
        foreach (var mb in host.GetComponents<MonoBehaviour>())
        {
            if (!(mb is TCHitFlashRunner)) Object.Destroy(mb);
        }

        var clonePS = host.GetComponent<ParticleSystem>();
        var em = clonePS.emission;
        em.enabled = false; // Stop spawning new bees
        clonePS.SetParticles(existing, count);

        var runner = host.AddComponent<TCHitFlashRunner>();
        runner.StartBeeScatter(host, clonePS, spiderPos, duration);
    }

    /// <summary>
    /// Spawns an immediate visual clone of the target that pulses/flashes 3 times across 0.3s (without fading out)
    /// while being knocked off the tree trunk away to the left/right and pushed back in the spider's direction.
    /// </summary>
    public static void ChilliKnockbackAndDestroy(GameObject target, Vector3 spiderPos, float duration = 0.3f)
    {
        if (!target) return;

        var renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0) return;

        Vector3 startPos = target.transform.position;
        Quaternion startRot = target.transform.rotation;

        var knockHost = new GameObject("ChilliKnockback_" + target.name);
        knockHost.transform.position = startPos;
        knockHost.transform.rotation = startRot;
        knockHost.transform.localScale = Vector3.one;

        Shader shader = FadeShader;
        var createdMats = new List<Material>();
        var baseColors = new List<Color>();
        var bakedMeshes = new List<Mesh>();
        int renderersCloned = 0;

        foreach (var r in renderers)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            Mesh meshToRender = null;
            if (r is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                meshToRender = mf.sharedMesh;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (!smr.sharedMesh) continue;
                Mesh baked = new Mesh();
                smr.BakeMesh(baked);
                bakedMeshes.Add(baked);
                meshToRender = baked;
            }

            if (!meshToRender) continue;

            var go = new GameObject(r.name + "_KnockGhost");
            go.transform.position = r.transform.position;
            go.transform.rotation = r.transform.rotation;
            go.transform.localScale = r.transform.lossyScale;
            go.transform.SetParent(knockHost.transform, true);

            var nmf = go.AddComponent<MeshFilter>();
            nmf.sharedMesh = meshToRender;

            var nmr = go.AddComponent<MeshRenderer>();
            var sharedMats = r.sharedMaterials;
            int matCount = (sharedMats != null && sharedMats.Length > 0) ? sharedMats.Length : 1;
            var matsForR = new Material[matCount];

            for (int m = 0; m < matCount; m++)
            {
                Material orig = (sharedMats != null && m < sharedMats.Length) ? sharedMats[m] : null;
                Material fMat = (shader != null) ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
                fMat.name = "KnockMat_" + (orig ? orig.name : "Default");

                Color col = Color.white;
                Texture tex = null;
                if (orig != null)
                {
                    if (orig.HasProperty("_MainTex") && orig.mainTexture != null)
                        tex = orig.mainTexture;
                    else if (orig.HasProperty("_BaseMap") && orig.GetTexture("_BaseMap") != null)
                        tex = orig.GetTexture("_BaseMap");

                    if (orig.HasProperty("_Color"))
                        col = orig.GetColor("_Color");
                    else if (orig.HasProperty("_BaseColor"))
                        col = orig.GetColor("_BaseColor");
                }

                if (tex != null)
                {
                    fMat.mainTexture = tex;
                    if (fMat.HasProperty("_BaseMap")) fMat.SetTexture("_BaseMap", tex);
                }
                if (fMat.HasProperty("_Color")) fMat.SetColor("_Color", col);
                if (fMat.HasProperty("_BaseColor")) fMat.SetColor("_BaseColor", col);

                matsForR[m] = fMat;
                createdMats.Add(fMat);
                baseColors.Add(col);
            }

            nmr.sharedMaterials = matsForR;
            nmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            nmr.receiveShadows = false;
            renderersCloned++;
        }

        if (renderersCloned == 0)
        {
            Object.Destroy(knockHost);
            return;
        }

        // Calculate directional knockback vectors
        Vector3 radialOut = new Vector3(startPos.x, 0f, startPos.z);
        if (radialOut.sqrMagnitude < 0.001f) radialOut = target.transform.forward;
        else radialOut.Normalize();

        Vector3 playerDir = Vector3.up; // Spider climbs vertically along +Y
        Vector3 tangentRight = Vector3.Cross(Vector3.up, radialOut).normalized;
        Vector3 tangentLeft = -tangentRight;

        Vector3 toEnemy = startPos - spiderPos;
        float sideDot = Vector3.Dot(toEnemy, tangentRight);
        Vector3 chosenSide;
        if (sideDot > 0.04f) chosenSide = tangentRight;
        else if (sideDot < -0.04f) chosenSide = tangentLeft;
        else chosenSide = (Random.value < 0.5f) ? tangentRight : tangentLeft;

        // Deflection speed reduced by another 50% so the enemy stays in camera view throughout the hit
        float outwardForce = 0.18f; // was 0.375f
        float sideForce = 0.22f;    // was 0.4375f
        float pushForce = 0.12f;    // was 0.25f
        Vector3 totalKnockback = chosenSide * sideForce + radialOut * outwardForce + playerDir * pushForce;

        // User requested: do NOT roll the enemy when deflecting away (keep upright / no roll tumble)
        Vector3 rollAxis = Vector3.up;
        float rollAngle = 0f;

        var runner = knockHost.AddComponent<TCHitFlashRunner>();
        runner.StartChilliKnockback(knockHost, createdMats, baseColors, bakedMeshes, startPos, startRot, totalKnockback, rollAxis, rollAngle, duration);
    }

    /// <summary>
    /// Spawns an immediate visual ghost of the target GameObject that smoothly fades away over duration (0.5f).
    /// Preserves original mesh, baked skinned poses, textures, and color tints while fading alpha to zero.
    /// </summary>
    public static void FadeAndDestroy(GameObject target, float duration = 0.5f)
    {
        if (!target) return;

        var renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0) return;

        var fadeHost = new GameObject("HitFade_" + target.name);
        fadeHost.transform.position = Vector3.zero;
        fadeHost.transform.rotation = Quaternion.identity;
        fadeHost.transform.localScale = Vector3.one;

        Shader shader = FadeShader;
        var createdMats = new List<Material>();
        var baseColors = new List<Color>();
        var bakedMeshes = new List<Mesh>();
        int renderersCloned = 0;

        foreach (var r in renderers)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            Mesh meshToRender = null;
            if (r is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                meshToRender = mf.sharedMesh;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (!smr.sharedMesh) continue;
                Mesh baked = new Mesh();
                smr.BakeMesh(baked);
                bakedMeshes.Add(baked);
                meshToRender = baked;
            }

            if (!meshToRender) continue;

            var go = new GameObject(r.name + "_FadeGhost");
            go.transform.position = r.transform.position;
            go.transform.rotation = r.transform.rotation;
            go.transform.localScale = r.transform.lossyScale;
            go.transform.SetParent(fadeHost.transform, true);

            var nmf = go.AddComponent<MeshFilter>();
            nmf.sharedMesh = meshToRender;

            var nmr = go.AddComponent<MeshRenderer>();
            var sharedMats = r.sharedMaterials;
            int matCount = (sharedMats != null && sharedMats.Length > 0) ? sharedMats.Length : 1;
            var matsForR = new Material[matCount];

            for (int m = 0; m < matCount; m++)
            {
                Material orig = (sharedMats != null && m < sharedMats.Length) ? sharedMats[m] : null;
                Material fMat = (shader != null) ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
                fMat.name = "FadeMat_" + (orig ? orig.name : "Default");

                Color col = Color.white;
                Texture tex = null;
                if (orig != null)
                {
                    if (orig.HasProperty("_MainTex") && orig.mainTexture != null)
                        tex = orig.mainTexture;
                    else if (orig.HasProperty("_BaseMap") && orig.GetTexture("_BaseMap") != null)
                        tex = orig.GetTexture("_BaseMap");

                    if (orig.HasProperty("_Color"))
                        col = orig.GetColor("_Color");
                    else if (orig.HasProperty("_BaseColor"))
                        col = orig.GetColor("_BaseColor");
                }

                if (tex != null)
                {
                    fMat.mainTexture = tex;
                    if (fMat.HasProperty("_BaseMap")) fMat.SetTexture("_BaseMap", tex);
                }
                if (fMat.HasProperty("_Color")) fMat.SetColor("_Color", col);
                if (fMat.HasProperty("_BaseColor")) fMat.SetColor("_BaseColor", col);

                matsForR[m] = fMat;
                createdMats.Add(fMat);
                baseColors.Add(col);
            }

            nmr.sharedMaterials = matsForR;
            nmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            nmr.receiveShadows = false;
            renderersCloned++;
        }

        if (renderersCloned == 0)
        {
            Object.Destroy(fadeHost);
            return;
        }

        var runner = fadeHost.AddComponent<TCHitFlashRunner>();
        runner.StartFade(fadeHost, createdMats, baseColors, bakedMeshes, duration);
    }
}

public static class TCHitFade
{
    public static void FadeAndDestroy(GameObject target, float duration = 0.5f)
    {
        TCHitFlash.FadeAndDestroy(target, duration);
    }
}

public class TCHitFlashRunner : MonoBehaviour
{
    public void StartFade(GameObject host, List<Material> mats, List<Color> baseColors, List<Mesh> bakedMeshes, float duration)
    {
        StartCoroutine(FadeRoutine(host, mats, baseColors, bakedMeshes, duration));
    }

    IEnumerator FadeRoutine(GameObject host, List<Material> mats, List<Color> baseColors, List<Mesh> bakedMeshes, float duration)
    {
        if (duration <= 0.001f) duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;

            if (mats != null && baseColors != null)
            {
                int count = Mathf.Min(mats.Count, baseColors.Count);
                for (int i = 0; i < count; i++)
                {
                    var m = mats[i];
                    if (m != null)
                    {
                        Color c = baseColors[i];
                        c.a *= alpha;
                        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    }
                }
            }
            yield return null;
        }

        // Clean up created material instances and baked meshes
        if (mats != null)
        {
            for (int i = 0; i < mats.Count; i++)
            {
                if (mats[i] != null) Destroy(mats[i]);
            }
        }
        if (bakedMeshes != null)
        {
            for (int i = 0; i < bakedMeshes.Count; i++)
            {
                if (bakedMeshes[i] != null) Destroy(bakedMeshes[i]);
            }
        }

        if (host != null)
        {
            Destroy(host);
        }
    }

    public void StartChilliKnockback(GameObject host, List<Material> mats, List<Color> baseColors, List<Mesh> bakedMeshes, Vector3 startPos, Quaternion startRot, Vector3 totalKnockback, Vector3 tumbleAxis, float tumbleAngle, float duration)
    {
        StartCoroutine(ChilliKnockbackRoutine(host, mats, baseColors, bakedMeshes, startPos, startRot, totalKnockback, tumbleAxis, tumbleAngle, duration));
    }

    IEnumerator ChilliKnockbackRoutine(GameObject host, List<Material> mats, List<Color> baseColors, List<Mesh> bakedMeshes, Vector3 startPos, Quaternion startRot, Vector3 totalKnockback, Vector3 tumbleAxis, float tumbleAngle, float duration)
    {
        if (duration <= 0.01f) duration = 0.3f;
        float elapsed = 0f;
        Vector3 baseScale = host != null ? host.transform.localScale : Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // Three distinct full-cycle pulses in 0.3 seconds (f = 10 Hz)
            float phase = progress * 3f * Mathf.PI * 2f;
            float flashK = Mathf.Pow(Mathf.Clamp01(0.5f - 0.5f * Mathf.Cos(phase)), 0.75f);

            // Explosive initial impulse easing out
            float easeOut = 1f - Mathf.Pow(1f - progress, 2.2f);
            if (host != null)
            {
                host.transform.position = startPos + totalKnockback * easeOut;
                host.transform.rotation = Quaternion.AngleAxis(tumbleAngle * easeOut, tumbleAxis) * startRot;

                // Impact punch on hit
                float punch = (progress < 0.25f) ? Mathf.Sin(progress / 0.25f * Mathf.PI) * 0.16f : 0f;
                host.transform.localScale = baseScale * (1f + punch);
            }

            // Material pulsing without fading out (maintaining 100% opacity)
            if (mats != null && baseColors != null)
            {
                int count = Mathf.Min(mats.Count, baseColors.Count);
                for (int i = 0; i < count; i++)
                {
                    var m = mats[i];
                    if (m != null)
                    {
                        Color baseCol = baseColors[i];
                        Color flashCol = Color.Lerp(baseCol, Color.white, flashK);
                        flashCol.a = baseCol.a; // Maintain full opacity (NO fading!)

                        if (m.HasProperty("_FlashAmount")) m.SetFloat("_FlashAmount", flashK);
                        if (m.HasProperty("_FlashColor")) m.SetColor("_FlashColor", Color.white);
                        if (m.HasProperty("_Color")) m.SetColor("_Color", flashCol);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", flashCol);
                    }
                }
            }

            yield return null;
        }

        // Clean up created material instances and baked meshes
        if (mats != null)
        {
            for (int i = 0; i < mats.Count; i++)
            {
                if (mats[i] != null) Destroy(mats[i]);
            }
        }
        if (bakedMeshes != null)
        {
            for (int i = 0; i < bakedMeshes.Count; i++)
            {
                if (bakedMeshes[i] != null) Destroy(bakedMeshes[i]);
            }
        }

        if (host != null)
        {
            Destroy(host);
        }
    }

    public void StartBeeScatter(GameObject host, ParticleSystem ps, Vector3 spiderPos, float duration)
    {
        StartCoroutine(BeeScatterRoutine(host, ps, spiderPos, duration));
    }

    IEnumerator BeeScatterRoutine(GameObject host, ParticleSystem ps, Vector3 spiderPos, float duration)
    {
        if (!ps)
        {
            if (host) Destroy(host);
            yield break;
        }

        int maxP = ps.main.maxParticles;
        var particles = new ParticleSystem.Particle[maxP];
        int count = ps.GetParticles(particles);

        if (count == 0)
        {
            if (host) Destroy(host);
            yield break;
        }

        // Limit simulated scatter count to at most 80 particles to prevent mobile CPU spikes
        int simCount = Mathf.Min(count, 80);

        // Initialize scattering velocities for each active bee particle
        var velocities = new Vector3[simCount];
        var rotations = new float[simCount];

        for (int i = 0; i < simCount; i++)
        {
            Vector3 worldP = ps.transform.TransformPoint(particles[i].position);
            Vector3 fromSpider = worldP - spiderPos;
            Vector3 radialOut = new Vector3(worldP.x, 0f, worldP.z);
            if (radialOut.sqrMagnitude < 0.01f) radialOut = Random.insideUnitSphere;
            radialOut.y = 0f;
            radialOut.Normalize();

            Vector3 tangent = Vector3.Cross(Vector3.up, radialOut).normalized;
            float side = (Random.value < 0.5f ? 1f : -1f);

            // Explosive fly-away vector: outward radial blast + left/right tangential burst + upward lift + randomized chaotic flutter
            Vector3 flyDir = (radialOut * Random.Range(4.5f, 7.5f))
                           + (tangent * (side * Random.Range(3.5f, 6.0f)))
                           + (Vector3.up * Random.Range(2.0f, 5.0f))
                           + (Random.insideUnitSphere * 1.5f);

            // Convert world velocity delta to particle system local space
            velocities[i] = ps.transform.InverseTransformDirection(flyDir);
            rotations[i] = Random.Range(-720f, 720f);

            // Extend lifetime so particles don't expire prematurely during the flight animation
            particles[i].remainingLifetime = Mathf.Max(particles[i].remainingLifetime, duration + 0.5f);
            particles[i].startLifetime = Mathf.Max(particles[i].startLifetime, duration + 0.5f);
        }

        ps.SetParticles(particles, count);

        float elapsed = 0f;
        while (elapsed < duration && host && ps)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) dt = Time.unscaledDeltaTime;
            if (dt <= 0f) dt = 0.016f;
            elapsed += dt;
            float progress = Mathf.Clamp01(elapsed / duration);

            count = ps.GetParticles(particles);
            if (count == 0) break;

            int activeCount = Mathf.Min(count, simCount);
            for (int i = 0; i < activeCount; i++)
            {
                // Move each bee along its fly-away trajectory with high speed
                particles[i].position += velocities[i] * dt;
                particles[i].rotation += rotations[i] * dt;

                // High speed wing flutter wobble
                Vector3 flutter = new Vector3(Mathf.Sin(elapsed * 25f + i) * 0.05f, Mathf.Cos(elapsed * 30f + i) * 0.05f, 0f);
                particles[i].position += flutter;

                // Smoothly fade alpha in the final 35% of flight
                if (progress > 0.65f)
                {
                    float fadeT = (progress - 0.65f) / 0.35f;
                    Color32 c = particles[i].startColor;
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * (1f - fadeT)), 0, 255);
                    particles[i].startColor = c;
                }
            }

            ps.SetParticles(particles, count);
            yield return null;
        }

        if (host) Destroy(host);
    }
}
