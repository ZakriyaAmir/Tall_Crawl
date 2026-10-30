using System.Collections.Generic;
using UnityEngine;

/// Round, cute hypercasual spider built from simple meshes on top of the original rig:
/// modelled on the 3D-printed chibi toy spiders: big round abdomen, smaller round head, two big glossy eyes
/// (+2 tiny side eyes), tiny fangs and 8 short chunky bent legs.
/// Skins only change colours/pattern, back spikes and the body shape. The old realistic mesh is hidden,
/// its rig/animator still drives jump/death motion which we follow (position) and stylise (squash, flip).
public class TCCuteSpider : MonoBehaviour
{
    const string RootName = "__cute";
    public Transform bone; Vector3 boneRest; Animator anim; playerInteract pi;
    Transform body; readonly List<Transform> legs = new List<Transform>(); readonly List<Quaternion> legRest = new List<Quaternion>();
    Vector3 bodyRest, bodyScale; float flip, t, blink = 2.5f;
    Transform head; Vector3 headRest; readonly List<Transform> eyes = new List<Transform>(); readonly List<Vector3> eyeRest = new List<Vector3>();

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Mesh cone;
    static Mesh abdomenMesh;

    public static void Build(GameObject spider, SkinDef def, int skin)
    {
        var st = spider.transform;
        // Hide the realistic mesh (once), remember its framing.
        var smrs = spider.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Bounds b = new Bounds(st.position, Vector3.one * 0.3f); bool hasB = false;
        foreach (var r in smrs) { if (r.enabled) { if (!hasB) { b = r.bounds; hasB = true; } else b.Encapsulate(r.bounds); } r.enabled = false; }
        var old = st.Find(RootName);
        var cs = spider.GetComponent<TCCuteSpider>();
        if (old) { if (cs && cs.cachedBounds.size != Vector3.zero) { b = cs.cachedBounds; } old.name = "__old"; old.gameObject.SetActive(false); Destroy(old.gameObject); }
        if (!cs) cs = spider.AddComponent<TCCuteSpider>();
        if (hasB || cs.cachedBounds.size == Vector3.zero) cs.cachedBounds = b; else b = cs.cachedBounds;

        float scale = Mathf.Max(0.0001f, st.lossyScale.x);
        float R = b.size.magnitude * 0.16f / scale;               // body radius, local units
        Vector3 center = st.InverseTransformPoint(b.center);
        // Up = spider's back. On the trunk that is radially outward, in the menu it is world up.
        Vector3 upW = spider.GetComponentInParent<player>() ? new Vector3(st.position.x, 0, st.position.z).normalized : Vector3.up;
        if (upW.sqrMagnitude < 0.01f) upW = Vector3.up;
        Vector3 up = st.InverseTransformDirection(upW).normalized;
        var head = TCSkins.FindDeep(st, "Spider_Head");
        bool inGame = spider.GetComponentInParent<player>();
        // Face: in game the spider looks up the trunk (climb direction); in menu previews it faces the preview camera (-Z).
        Vector3 fwdW = Vector3.up;
        Vector3 fwd = inGame ? st.InverseTransformDirection(fwdW) : Vector3.back; // menu preview camera sits at -Z
        fwd = Vector3.ProjectOnPlane(fwd, up); if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
        fwd.Normalize();

        var root = new GameObject(RootName).transform; root.SetParent(st, false);
        root.localPosition = center - up * Vector3.Dot(center, up) + up * Mathf.Min(Vector3.Dot(center, up), R * 0.2f);
        root.localRotation = Quaternion.LookRotation(fwd, up);
        cs.legs.Clear(); cs.legRest.Clear(); cs.eyeRest.Clear();

        var bodyMat = Resources.Load<Material>("Skins/Skin_" + skin);
        if (!bodyMat) bodyMat = Flat(def.body * 0.85f, null, 0, def.metallic, def.gloss);
        else
        {
            if (bodyMat.HasProperty("_OutlineColor")) bodyMat.SetColor("_OutlineColor", new Color(0.12f, 0.08f, 0.10f, 1f));
            if (bodyMat.HasProperty("_OutlineWidth")) bodyMat.SetFloat("_OutlineWidth", 1.4f);
            if (bodyMat.HasProperty("_EdgeThreshold")) bodyMat.SetFloat("_EdgeThreshold", 0.22f);
        }
        // Photo look (3D-printed chibi spider): one solid plastic colour for head + legs, the abdomen carries the skin pattern.
        // Tuned to medium tone so spider parts are not overly bright.
        var plain = Flat(def.body * 0.85f, bodyMat, 0, def.metallic, def.gloss);
        var legMat = Flat(def.body * 0.80f, bodyMat, 0, def.metallic, def.gloss);
        var spikeMat = Flat(def.accent * 0.88f, bodyMat, def.emission, def.metallic, def.gloss);
        var eyeMat = Flat(def.eye, bodyMat, def.eyeGlow, 0.1f, 0.92f);
        var shine = Flat(new Color(0.85f, 0.85f, 0.85f, 1f), bodyMat, 0.4f, 0, 0.9f);
        var fangMat = Flat(def.body * 0.70f, bodyMat, 0, def.metallic, def.gloss);

        Vector3 s = def.BodyScale;
        float pivotY = R * 0.70f;
        var bodyT = new GameObject("body").transform; bodyT.SetParent(root, false); bodyT.localPosition = new Vector3(0, pivotY, 0);
        cs.body = bodyT; cs.bodyRest = bodyT.localPosition; cs.bodyScale = Vector3.one;
        Vector3 P(float x, float y, float z) => new Vector3(x * R, y * R - pivotY, z * R);

        // --- Anatomically Sleek Spider Body (Cephalothorax + Pedicel + Abdomen) ---
        // 1. Cephalothorax (prosoma): sleek, tapered forward, low center of mass, anchors legs & head
        Vector3 cephScale = new Vector3(1.15f, 0.72f, 1.25f) * R;
        var ceph = Part(PrimitiveType.Sphere, bodyT, P(0, 0.62f, 0.12f), cephScale, plain).transform; ceph.name = "cephalothorax";
        ceph.localRotation = Quaternion.Euler(-4, 0, 0);

        // 2. Head / Ocular Region: sleek front snout with cute rounded silhouette
        float hr = 0.65f * Mathf.Lerp(1f, s.x, 0.35f);
        var headT = Part(PrimitiveType.Sphere, bodyT, P(0, 0.66f, 0.65f), new Vector3(1.35f, 1.15f, 1.25f) * hr * R, plain).transform; headT.name = "head";
        cs.head = headT; cs.headRest = headT.localPosition;

        // 3. Waist (pedicel): slender anatomical junction connecting thorax to abdomen
        Part(PrimitiveType.Cylinder, bodyT, P(0, 0.60f, -0.38f), new Vector3(0.52f, 0.16f, 0.52f) * R, plain, Quaternion.Euler(90, 0, 0)).name = "pedicel";

        // 4. Abdomen (opisthosoma): lore-accurate, sleek, beautifully proportioned spider butt
        abdomenMesh = MakeSpiderAbdomenMesh();
        Vector3 ab = Vector3.Scale(new Vector3(1.10f, 1.05f, 1.28f), s) * R;
        var abdObj = new GameObject("abdomen");
        abdObj.transform.SetParent(bodyT, false);
        abdObj.transform.localPosition = P(0, 0.62f, -0.88f);
        abdObj.transform.localRotation = Quaternion.Euler(-2, 0, 0);
        abdObj.transform.localScale = ab;
        var abdMf = abdObj.AddComponent<MeshFilter>();
        abdMf.sharedMesh = abdomenMesh;
        var abdMr = abdObj.AddComponent<MeshRenderer>();
        abdMr.sharedMaterial = bodyMat;
        abdMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var abd = abdObj.transform;

        // 5. Lore-accurate posterior spinnerets (silk-spinning nubs at rear-ventral apex of butt)
        var spinMat = Flat(def.body * 0.76f, bodyMat, 0, def.metallic, def.gloss);
        float rearZ = -0.88f * R - 0.53f * ab.z;
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var spin = Part(PrimitiveType.Sphere, bodyT, new Vector3(0.08f * sgn * R, 0.56f * R - pivotY, rearZ), new Vector3(0.12f, 0.10f, 0.14f) * R, spinMat).transform;
            spin.localRotation = Quaternion.Euler(14, 10 * sgn, 0);
            spin.name = "spinneret";
        }

        // Eyes: stylish, expressive, beautiful arachnid gaze
        Vector3 HS(Vector3 dir, float lift) { dir.Normalize(); return headT.localPosition + Vector3.Scale(dir, new Vector3(0.68f, 0.58f, 0.62f) * hr * R) + dir * lift * R; }
        cs.eyes.Clear();
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            // Main pair of prominent glossy forward-facing eyes
            var d = new Vector3(0.38f * sgn, 0.16f, 0.92f).normalized;
            var e = Part(PrimitiveType.Sphere, bodyT, HS(d, -0.03f), new Vector3(0.42f, 0.42f, 0.18f) * R, eyeMat).transform;
            e.localRotation = Quaternion.LookRotation(d, Vector3.up); e.name = "eye";
            Part(PrimitiveType.Sphere, e, new Vector3(0.20f, 0.22f, 0.40f), new Vector3(0.22f, 0.22f, 0.28f), shine);
            cs.eyes.Add(e); cs.eyeRest.Add(e.localScale);

            // Secondary lateral eyes
            var sd = new Vector3(0.82f * sgn, 0.24f, 0.52f).normalized;
            var se = Part(PrimitiveType.Sphere, bodyT, HS(sd, -0.02f), new Vector3(0.14f, 0.14f, 0.07f) * R, eyeMat).transform;
            se.localRotation = Quaternion.LookRotation(sd, Vector3.up);
            cs.eyes.Add(se); cs.eyeRest.Add(se.localScale);

            // Sleek pedipalps / sensory feelers in front of the mouth
            var f = Part(PrimitiveType.Capsule, bodyT, P(0.16f * sgn, 0.28f, 1.12f), new Vector3(0.14f, 0.22f, 0.14f) * R, fangMat).transform;
            f.localRotation = Quaternion.Euler(32, 12 * sgn, -6 * sgn);
        }

        // --- 8 Perfectly Proportioned Legs radiating from the Cephalothorax ---
        // Leg placement and splay angles from biological arachnid morphology:
        // Leg 1 (anterior): reaches forward-outward to explore and grasp
        // Leg 2 (mid-anterior): longest lateral-forward reach
        // Leg 3 (mid-posterior): lateral-backward balance and pivot
        // Leg 4 (posterior): strong rearward propulsion reach
        float[] legZ = { 0.45f, 0.22f, -0.02f, -0.25f };
        float[] legX = { 0.48f, 0.56f, 0.56f, 0.50f };
        float[] legY = { 0.52f, 0.54f, 0.52f, 0.50f };
        float[] legYaw = { 36f, 68f, 114f, 146f };
        float[] legLengthMult = { 1.05f, 1.12f, 1.08f, 1.15f }; // Leg 2 and Leg 4 are naturally longer for elegance

        for (int sgn = -1; sgn <= 1; sgn += 2)
            for (int k = 0; k < 4; k++)
            {
                float z = legZ[k];
                float sideX = sgn * legX[k];
                float hipY = legY[k];

                var hip = new GameObject("leg").transform; hip.SetParent(bodyT, false);
                hip.localPosition = P(sideX, hipY, z);

                // Natural spider leg spread angle radiating around the cephalothorax
                float yaw = sgn * legYaw[k];
                hip.localRotation = Quaternion.Euler(0, yaw, 0);

                // Sleek, beautifully tapered segments
                float mult = legLengthMult[k];
                float thUpper = 0.22f;
                float thLower = 0.18f;
                float upperLen = 0.62f * mult;
                float lowerLen = 0.72f * mult;

                // Upper leg (femur): graceful arch outward and upward
                var up1 = Part(PrimitiveType.Capsule, hip, new Vector3(0, 0.18f * R, upperLen * 0.5f * R), new Vector3(thUpper, upperLen * 0.5f, thUpper) * R, legMat).transform;
                up1.localRotation = Quaternion.Euler(60, 0, 0);

                // Knee joint (patella): neat rounded connector
                Part(PrimitiveType.Sphere, hip, new Vector3(0, 0.32f * R, upperLen * R), Vector3.one * thUpper * 1.12f * R, legMat);

                // Lower leg (tibia & metatarsus): slender, tapering downward to a delicate foot contact
                var lo = Part(PrimitiveType.Capsule, hip, new Vector3(0, -0.26f * R, (upperLen + lowerLen * 0.38f) * R), new Vector3(thLower, lowerLen * 0.55f, thLower) * R, legMat).transform;
                lo.localRotation = Quaternion.Euler(-34, 0, 0);

                cs.legs.Add(hip); cs.legRest.Add(hip.localRotation);
            }
        // Spikes along the back ridge (skin feature) directly seated on the sculpted abdomen
        if (def.spikes > 0)
        {
            if (!cone) cone = MakeCone();
            for (int k = 0; k < def.spikes; k++)
            {
                // Distribute spikes along the dorsal spine of the sculpted opisthosoma
                float v = def.spikes == 1 ? 0.45f : Mathf.Lerp(0.18f, 0.78f, k / (float)(def.spikes - 1));
                float theta = v * Mathf.PI;
                float sTheta = Mathf.Max(0f, Mathf.Sin(theta));
                float cTheta = Mathf.Cos(theta);

                float zMesh = cTheta * 0.52f - 0.03f;
                float widthMod = 1f + 0.16f * cTheta - 0.05f * (cTheta * cTheta);
                float ry = sTheta * widthMod * 0.40f;
                float rx = sTheta * widthMod * 0.52f;
                float yCenter = sTheta * sTheta * 0.04f - 0.02f * v;
                float yTop = yCenter + ry;

                // Alternate slightly left/right if many spikes (double crest effect)
                float sideJ = def.spikes > 5 ? ((k % 2) * 2 - 1) * 0.18f * rx : 0f;
                float xMesh = sideJ;

                // Exact dorsal surface normal in local abdomen mesh space
                // dz/dv is negative (z decreases from front to rear)
                float dz = -sTheta * Mathf.PI * 0.52f;
                // dy/dv
                float dWidth = (-0.16f * sTheta - 0.10f * cTheta * sTheta) * Mathf.PI;
                float dry = (cTheta * Mathf.PI * widthMod + sTheta * dWidth) * 0.40f;
                float dyCenter = (2f * sTheta * cTheta * Mathf.PI * 0.04f) - 0.02f;
                float dy = dyCenter + dry;

                // Outward surface normal pointing generally upwards (+Y) and perpendicular to spine
                Vector3 nLocal = new Vector3(sideJ * 1.5f, -dz, dy).normalized;

                // Position in local mesh space (abd object scales this by ab)
                Vector3 localSpikePos = new Vector3(xMesh, yTop, zMesh);

                // Parent to abd so spikes inherit abdomen orientation & scaling perfectly
                var g = new GameObject("spike");
                g.transform.SetParent(abd, false);
                // Sink slightly into the mesh base so spikes don't hover/float in the air
                g.transform.localPosition = localSpikePos - Vector3.up * 0.04f;
                g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, nLocal);

                float len = def.spikeLength * (k == 0 || k == def.spikes - 1 ? 0.75f : 1f);
                float thickness = 0.26f;
                g.transform.localScale = new Vector3(thickness, len, thickness);

                g.AddComponent<MeshFilter>().sharedMesh = cone;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = spikeMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        cs.anim = spider.GetComponentInChildren<Animator>();
        cs.pi = spider.GetComponent<playerInteract>();
        var smr0 = smrs.Length > 0 ? smrs[0] : null;
        cs.bone = smr0 && smr0.rootBone ? smr0.rootBone : head;
        cs.boneRest = cs.bone ? st.InverseTransformPoint(cs.bone.position) : Vector3.zero;
        cs.restRoot = root.localPosition; cs.root = root; cs.flip = 0;
    }

    [System.NonSerialized] public float wave; // 0..1, menu wave (front leg up)
    public Bounds cachedBounds; Transform root; Vector3 restRoot;
    float backflipTimer, backflipDuration;
    float currentAirHeight;
    float jumpBlend; // 0 (on ground/running) -> 1 (fully in jump pose)

    public void TriggerBackflip(float duration = 0.75f)
    {
        backflipDuration = duration;
        backflipTimer = duration;
    }

    void LateUpdate()
    {
        if (!root) return;
        float dt = Time.deltaTime;
        t += dt;
        var st = transform;
        // Follow the rig's root motion (jump hop etc.) but keep our own clean orientation.
        Vector3 rigOff = bone ? st.InverseTransformPoint(bone.position) - boneRest : Vector3.zero;
        if (rigOff.magnitude > 5f) rigOff = Vector3.zero;

        // Is the spider actively moving/climbing, jumping, or idle?
        var playerComp = GetComponentInParent<player>();
        bool isIntroWalking = anim && anim.isActiveAndEnabled && anim.GetCurrentAnimatorStateInfo(0).IsName("spiderIntro");
        bool isJumping = (pi && pi.IsJumping) || (anim && anim.isActiveAndEnabled && anim.GetCurrentAnimatorStateInfo(0).IsName("SpiderJump")) || backflipTimer > 0;
        bool isMoving = (playerComp != null && !playerComp.paused && !playerComp.GameOver && !isJumping) || isIntroWalking;

        // Smooth jump/landing blend transition (prevents sudden snapping into or out of jump pose)
        float targetJumpBlend = isJumping ? 1f : 0f;
        jumpBlend = Mathf.MoveTowards(jumpBlend, targetJumpBlend, dt * (isJumping ? 12f : 7f));

        // Smooth height calculation:
        // Rig jump base height:
        float rigY = Mathf.Max(0f, rigOff.y);

        // Double jump extra height: launches directly from current height, attaining more vertical height
        float flipPitch = 0f;
        float extraHeight = 0f;
        if (backflipTimer > 0)
        {
            backflipTimer -= dt;
            float p = 1f - Mathf.Clamp01(backflipTimer / backflipDuration);
            // Smooth backflip rotation 0 -> -360 degrees along local X (pitching backward)
            flipPitch = -360f * Mathf.SmoothStep(0, 1, p);
            // Extra vertical boost: smooth parabolic arc reaching +2.6m above current position
            extraHeight = Mathf.Sin(p * Mathf.PI) * 2.6f;
        }

        float targetHeight = isJumping ? (rigY + extraHeight) : 0f;
        // When jumping or in air, smooth vertical displacement cleanly
        currentAirHeight = Mathf.Lerp(currentAirHeight, targetHeight, 1f - Mathf.Exp(-14f * dt));
        if (!isJumping && currentAirHeight < 0.01f) currentAirHeight = 0f;

        Vector3 off = new Vector3(rigOff.x, currentAirHeight, rigOff.z);
        root.localPosition = restRoot + off;
        float hop = Mathf.Clamp01(currentAirHeight * 3.5f);

        // Squash & stretch + scuttle bounce (gentle breathing when idle, active bounce when moving, dynamic stretch when jumping)
        float bob = isJumping ? 0.08f * Mathf.Sin(t * 18f) : isMoving ? Mathf.Sin(t * 24f) * 0.035f : Mathf.Sin(t * 2.5f) * 0.015f;
        body.localScale = new Vector3(1 + bob - hop * 0.08f, 1 - bob + hop * 0.15f, 1 + bob - hop * 0.08f);
        if (head) head.localPosition = headRest + new Vector3(Mathf.Sin(t * (isMoving ? 12f : 2f)) * 0.015f, Mathf.Abs(Mathf.Sin(t * (isMoving ? 12f : 2f))) * 0.02f, 0) * head.localScale.y;

        // Speed of movement affects leg gait rate
        float moveSpeed = (playerComp && playerComp.calcSpeed > 0) ? playerComp.calcSpeed : 10f;
        float gaitFreq = Mathf.Clamp(moveSpeed * 1.5f, 12f, 26f);

        // Legs:
        // Real spider gait is a wave-like alternating tetrapod gait:
        // Left side:  L1, L3 swing while L2, L4 stand
        // Right side: R2, R4 swing while R1, R3 stand
        // In arachnid locomotion, adjacent contralateral pairs alternate (L1 & R2, L3 & R4, etc.)
        // with phase propagation down the body (front legs step first, followed sequentially by rear legs)
        for (int i = 0; i < legs.Count; i++)
        {
            bool isLeft = i < 4;
            int legIdx = i % 4; // 0: front (L1/R1), 1: mid-front (L2/R2), 2: mid-rear (L3/R3), 3: rear (L4/R4)

            // 1. Moving gait calculation (real spider sequential ripple tetrapod):
            // Base alternating phase between odd and even legs, plus a realistic metachronal ripple along the body
            float phaseOffset = (legIdx % 2 == 0 ? 0f : Mathf.PI) + (isLeft ? 0f : Mathf.PI) + (legIdx * 0.35f);
            float ph = t * gaitFreq + phaseOffset;

            // Forward/backward stride swing:
            // Front legs reach forward and pull; rear legs push backward
            float swingAmp = legIdx == 0 ? 24f : (legIdx == 3 ? 20f : 16f);
            float swing = Mathf.Sin(ph) * swingAmp;

            // Realistic vertical lift: legs lift high during swing phase, step firmly onto surface
            float liftNormalized = Mathf.Clamp01(Mathf.Sin(ph));
            // Steeper parabolic lift (quick step up and down, longer stance)
            float stepLift = Mathf.Pow(liftNormalized, 1.8f) * 22f;

            // Lateral foot splay/curl during stride (spiders curl inward during lift, splay outward on stance):
            float splay = Mathf.Cos(ph) * (isLeft ? -7f : 7f);

            Quaternion moveRot = Quaternion.Euler(0, swing, splay) * legRest[i] * Quaternion.Euler(-stepLift, 0, 0);

            // 2. Jumping pose calculation:
            // Front legs reach upward and spread out to grab surfaces/balance
            // Rear legs tuck inward and stream backward
            float tuckAngle;
            float spreadAngle;
            if (legIdx <= 1)
            {
                tuckAngle = -32f + Mathf.Sin(t * 12f + i) * 5f;
                spreadAngle = (isLeft ? -18f : 18f);
            }
            else
            {
                tuckAngle = 26f + Mathf.Sin(t * 12f + i) * 5f;
                spreadAngle = (isLeft ? 12f : -12f);
            }
            Quaternion jumpRot = Quaternion.Euler(0, spreadAngle, 0) * legRest[i] * Quaternion.Euler(tuckAngle, 0, 0);

            // 3. Ground / Idle stance:
            Quaternion groundRot = isMoving ? moveRot : legRest[i];

            // 4. Smooth blend between Ground (walk/run/idle) and Jump poses:
            legs[i].localRotation = Quaternion.Slerp(groundRot, jumpRot, jumpBlend);

            // Friendly wave in main menu
            if (i == 4 && wave > 0)
            {
                legs[i].localRotation = Quaternion.Slerp(
                    legs[i].localRotation,
                    legRest[i] * Quaternion.Euler(-70f * wave, 0, Mathf.Sin(t * 16f) * 25f * wave),
                    wave
                );
            }
        }

        // Eyes: blink every few seconds
        blink -= dt;
        if (blink < -0.14f) blink = Random.Range(1.8f, 4f);
        float lid = blink < 0 ? 0.12f : 1f;
        for (int i = 0; i < eyes.Count; i++) if (eyes[i]) { var r = eyeRest[i]; eyes[i].localScale = new Vector3(r.x, r.y * lid, r.z); }

        // Death: roll on the back, cartoon style. Backflip pitch combined with death roll.
        bool dead = pi ? pi.life <= 0 : anim && anim.isActiveAndEnabled && HasBool(anim, "death") && anim.GetBool("death");
        flip = Mathf.MoveTowards(flip, dead ? 1 : 0, dt * 3f);
        body.localRotation = Quaternion.Euler(flipPitch, 0, 180f * Mathf.SmoothStep(0, 1, flip));
    }

    static bool HasBool(Animator a, string n) { foreach (var p in a.parameters) if (p.name == n) return true; return false; }

    static Material Flat(Color c, Material src, float emission = 0, float metallic = 0, float gloss = 0.55f)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c) + emission.ToString("0.00") + metallic.ToString("0.00") + gloss.ToString("0.00");
        if (mats.TryGetValue(key, out var m) && m) return m;
        var shader = Shader.Find("TallCrawl/SpiderPreBaked") ?? (src ? src.shader : Shader.Find("Mobile/Diffuse"));
        m = src ? new Material(src) : new Material(shader);
        m.shader = shader;
        m.mainTexture = Texture2D.whiteTexture; m.color = c;
        if (m.HasProperty("_ShadowDull")) m.SetFloat("_ShadowDull", 0.55f);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission > 0 ? c * emission : Color.black);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        bool isShine = emission > 0.35f && c.r > 0.8f;
        if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0.12f, 0.08f, 0.10f, 1f));
        if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", isShine ? 0f : 1.4f);
        if (m.HasProperty("_EdgeThreshold")) m.SetFloat("_EdgeThreshold", isShine ? 0f : 0.22f);
        if (emission > 0) { m.EnableKeyword("_EMISSION"); }
        else m.DisableKeyword("_EMISSION");
        mats[key] = m; return m;
    }

    static GameObject Part(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(t);
        var col = g.GetComponent<Collider>(); if (col) Object.DestroyImmediate(col);
        g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        if (rot.HasValue) g.transform.localRotation = rot.Value;
        var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    /// Lore-accurate, anatomically sculpted spider opisthosoma (abdomen/butt):
    /// Broad rounded anterior shoulders behind the pedicel, gentle continuous dorsal arch (no bumps/humps),
    /// and a smooth, plump, rounded posterior dome with dual spinnerets.
    static Mesh MakeSpiderAbdomenMesh()
    {
        const int rings = 28;
        const int segments = 36;
        var verts = new List<Vector3>((rings + 1) * (segments + 1));
        var uvs = new List<Vector2>((rings + 1) * (segments + 1));
        var tri = new List<int>(rings * segments * 6);

        for (int r = 0; r <= rings; r++)
        {
            float v = r / (float)rings; // 0 at front pedicel, 1 at rear tip
            float theta = v * Mathf.PI;
            float st = Mathf.Max(0f, Mathf.Sin(theta));
            float ct = Mathf.Cos(theta);

            // Z coordinate: smooth mapping from +0.50 (front) to -0.55 (rear)
            // Using cos(theta) guarantees derivative dz/dtheta = 0 at both poles -> zero crease or pinch point!
            float z = ct * 0.52f - 0.03f;

            // Lore-accurate width modulation:
            // Widest at anterior-mid section (shoulders at v ~ 0.38), gracefully tapering to a rounded butt dome
            float widthMod = 1f + 0.16f * ct - 0.05f * (ct * ct);

            // Radii (X is width, Y is dorsoventral height)
            float rx = st * widthMod * 0.52f;
            float ry = st * widthMod * 0.40f;

            // Dorsal curve: subtle, continuous arch perfectly blended across the back (zero bump)
            float yCenter = st * st * 0.04f - 0.02f * v;

            for (int s = 0; s <= segments; s++)
            {
                float u = s / (float)segments;
                float phi = u * Mathf.PI * 2f;
                float cosP = Mathf.Cos(phi);
                float sinP = Mathf.Sin(phi);

                float x = cosP * rx;
                float y = yCenter + sinP * ry;

                verts.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int i0 = r * (segments + 1) + s;
                int i1 = i0 + 1;
                int i2 = (r + 1) * (segments + 1) + s;
                int i3 = i2 + 1;

                if (r == 0)
                {
                    // Front pole fan (non-degenerate)
                    tri.Add(i0); tri.Add(i2); tri.Add(i3);
                }
                else if (r == rings - 1)
                {
                    // Rear pole fan (non-degenerate)
                    tri.Add(i0); tri.Add(i2); tri.Add(i1);
                }
                else
                {
                    // Body quads
                    tri.Add(i0); tri.Add(i2); tri.Add(i1);
                    tri.Add(i1); tri.Add(i2); tri.Add(i3);
                }
            }
        }

        var m = new Mesh { name = "spider_abdomen" };
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// Unit cone: base radius 0.5 at y=0, tip at y=1 (smooth, rounded look via normals).
    static Mesh MakeCone()
    {
        const int N = 14; var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v.Add(d * 0.5f); n.Add((d + Vector3.up * 0.5f).normalized);
            v.Add(Vector3.up); n.Add((d + Vector3.up * 0.5f).normalized);
        }
        for (int i = 0; i < N; i++) { int a = i * 2; tri.AddRange(new[] { a, a + 1, a + 2, a, a + 2, a + 1 }); }
        var m = new Mesh { name = "spike" }; m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return m;
    }
}
