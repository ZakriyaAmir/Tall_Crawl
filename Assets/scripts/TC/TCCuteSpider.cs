using System.Collections.Generic;
using UnityEngine;

/// Round, cute hypercasual spider built from simple meshes on top of the original rig:
/// modelled on the 3D-printed chibi toy spiders: big round abdomen, smaller round head, two big glossy eyes
/// (+2 tiny side eyes), tiny fangs and 8 short chunky bent legs.
/// Skins only change colours/pattern, back spikes and the body shape. The old realistic mesh is hidden,
/// its rig/animator still drives jump/death motion which we follow (position) and stylise (squash, flip).
public partial class TCCuteSpider : MonoBehaviour
{
    const string RootName = "__cute";
    public Transform bone; Vector3 boneRest; Animator anim; playerInteract pi;
    Transform body; readonly List<Transform> legs = new List<Transform>(); readonly List<Quaternion> legRest = new List<Quaternion>();
    Vector3 bodyRest, bodyScale; float flip, t, blink = 2.5f;
    float rollTimer, rollDuration, rollSpinAngle;
    Transform head; Vector3 headRest; readonly List<Transform> eyes = new List<Transform>(); readonly List<Vector3> eyeRest = new List<Vector3>();
    Transform abdomen; readonly List<Transform> spinnerets = new List<Transform>();
    Transform wingPivotL, wingPivotR;
    Quaternion wingRestL = Quaternion.identity, wingRestR = Quaternion.identity;
    string wingType;
    float gaitPhase, gaitOmega, lastYaw;
    float landBounce;
    bool wasJumping;
    float landingTimer;
    const float LandingDuration = 0.32f;
    float swingLean, swingRoll, swingTwist, swingSquash, swingTuck;
    bool hasSwingWorldRot;
    Quaternion swingWorldRot = Quaternion.identity;
    Quaternion abdomenRest = Quaternion.identity;
    bool hasAbdomenRest;

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
        if (cs.cachedBounds.size != Vector3.zero) b = cs.cachedBounds;
        else if (hasB) cs.cachedBounds = b;
        else cs.cachedBounds = b;

        float scale = Mathf.Max(0.0001f, st.lossyScale.x);
        float R = b.size.magnitude * 0.16f / scale;               // body radius, local units
        Vector3 center = cs.hasRestRoot ? cs.cachedCenter : st.InverseTransformPoint(b.center);
        cs.cachedCenter = center;
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
        root.localPosition = cs.hasRestRoot ? cs.restRoot : (center - up * Vector3.Dot(center, up) + up * Mathf.Min(Vector3.Dot(center, up), R * 0.2f));
        root.localRotation = Quaternion.LookRotation(fwd, up);
        cs.legs.Clear(); cs.legRest.Clear(); cs.eyeRest.Clear(); cs.spinnerets.Clear();
        cs.wingPivotL = null; cs.wingPivotR = null; cs.wingType = null;

        var bodyMat = Resources.Load<Material>("Skins/Skin_" + skin);
        if (!bodyMat) bodyMat = Flat(def.body * 0.85f, null, 0, def.metallic, def.gloss);
        else
        {
            if (bodyMat.HasProperty("_OutlineColor")) bodyMat.SetColor("_OutlineColor", new Color(0.12f, 0.08f, 0.10f, 1f));
            if (bodyMat.HasProperty("_OutlineWidth")) bodyMat.SetFloat("_OutlineWidth", 1.4f);
            if (bodyMat.HasProperty("_EdgeThreshold")) bodyMat.SetFloat("_EdgeThreshold", 0.22f);
            if (def.name == "Golden")
            {
                // Pure deep 24k gold emission on abdomen - eliminates any white bleaching
                if (bodyMat.HasProperty("_EmissionColor")) bodyMat.SetColor("_EmissionColor", new Color(0.65f, 0.48f, 0.02f, 1.0f));
            }
        }
        // Photo look (3D-printed chibi spider): one solid plastic colour for head + legs, the abdomen carries the skin pattern.
        // Tuned to medium tone so spider parts are not overly bright.
        // Bee: warm velvet charcoal head & legs with golden fuzz. Wasp: sleek black head & legs. Squirrel: deep warm chestnut head with russet legs.
        // Golden: vibrant solid 24k polished gold across all parts with bright golden luster.
        Color headBodyCol = def.body * 0.85f;
        Color legCol = def.body * 0.80f;
        if (def.name == "Golden")
        {
            headBodyCol = new Color(1.0f, 0.88f, 0.12f); // Pure vibrant 24k gold
            legCol = new Color(0.96f, 0.80f, 0.08f);      // Gleaming golden legs
        }
        else if (def.name == "Bee" || def.name == "Bees")
        {
            headBodyCol = new Color(0.12f, 0.10f, 0.14f); // Charcoal velvety bee head
            legCol = new Color(0.14f, 0.12f, 0.16f);      // Fuzzy bee legs
        }
        else if (def.name == "Wasp")
        {
            headBodyCol = new Color(0.10f, 0.08f, 0.12f); // Sleek hornet black head/thorax
            legCol = new Color(0.12f, 0.10f, 0.14f);      // Black wasp legs
        }
        else if (def.name == "Squirrel")
        {
            headBodyCol = new Color(0.72f, 0.28f, 0.09f); // Rich warm squirrel chestnut
            legCol = new Color(0.65f, 0.25f, 0.08f);
        }
        else if (def.name == "Ladybug")
        {
            headBodyCol = new Color(0.10f, 0.08f, 0.11f); // Sleek glossy black ladybug head & pronotum
            legCol = new Color(0.12f, 0.10f, 0.13f);      // Black ladybug legs
        }
        else if (def.name == "Lava")
        {
            headBodyCol = new Color(0.72f, 0.14f, 0.10f); // Glowing volcanic magma red cephalothorax & head
            legCol = new Color(0.58f, 0.12f, 0.08f);      // Deep molten crimson legs
        }
        else if (def.name == "Bat")
        {
            headBodyCol = new Color(0.18f, 0.14f, 0.22f); // Deep velvety midnight-violet bat head & cephalothorax
            legCol = new Color(0.15f, 0.12f, 0.18f);      // Dusky midnight legs
        }
        else if (def.name == "Hiss")
        {
            headBodyCol = new Color(0.52f, 0.83f, 0.32f); // Glossy snake green head
            legCol = new Color(0.42f, 0.72f, 0.26f);      // Viper green legs
        }

        var plain = Flat(headBodyCol, null, def.emission * 0.5f, def.metallic, def.gloss);
        var legMat = Flat(legCol, null, def.emission * 0.35f, def.metallic, def.gloss);
        var spikeMat = Flat(def.accent * 0.95f, null, def.emission, def.metallic, def.gloss);
        var eyeMat = Flat(def.eye, null, def.eyeGlow, 0.1f, 0.92f);
        var shine = Flat(new Color(0.92f, 0.92f, 0.92f, 1f), null, 0.5f, 0, 0.9f);
        var fangMat = Flat(headBodyCol * 0.85f, null, def.emission * 0.4f, def.metallic, def.gloss);

        Vector3 s = def.BodyScale;
        float pivotY = R * 0.70f;
        var bodyT = new GameObject("body").transform; bodyT.SetParent(root, false); bodyT.localPosition = new Vector3(0, pivotY, 0);
        cs.body = bodyT; cs.bodyRest = bodyT.localPosition; cs.bodyScale = Vector3.one;
        Vector3 P(float x, float y, float z) => new Vector3(x * R, y * R - pivotY, z * R);

        // --- Anatomically Sleek Spider Body (Cephalothorax + Pedicel + Abdomen) ---
        // 1. Cephalothorax (prosoma): sleek, tapered forward, low center of mass, anchors legs & head (length reduced 20%)
        Vector3 cephScale = new Vector3(1.15f, 0.72f, 1.00f) * R;
        var ceph = Part(PrimitiveType.Sphere, bodyT, P(0, 0.62f, 0.10f), cephScale, plain).transform; ceph.name = "cephalothorax";
        ceph.localRotation = Quaternion.Euler(-4, 0, 0);

        // 2. Head / Ocular Region: sleek front snout with cute rounded silhouette
        float hr = 0.65f * Mathf.Lerp(1f, s.x, 0.35f);
        var headT = Part(PrimitiveType.Sphere, bodyT, P(0, 0.66f, 0.52f), new Vector3(1.35f, 1.15f, 1.00f) * hr * R, plain).transform; headT.name = "head";
        cs.head = headT; cs.headRest = headT.localPosition;

        // 3. Waist (pedicel): slender anatomical junction connecting thorax to abdomen
        Part(PrimitiveType.Cylinder, bodyT, P(0, 0.60f, -0.30f), new Vector3(0.52f, 0.13f, 0.52f) * R, plain, Quaternion.Euler(90, 0, 0)).name = "pedicel";

        // 4. Abdomen (opisthosoma): lore-accurate, sleek, beautifully proportioned spider butt
        abdomenMesh = MakeSpiderAbdomenMesh();
        Vector3 ab = Vector3.Scale(new Vector3(1.10f, 1.05f, 1.024f), s) * R;
        var abdObj = new GameObject("abdomen");
        abdObj.transform.SetParent(bodyT, false);
        abdObj.transform.localPosition = P(0, 0.62f, -0.70f);
        abdObj.transform.localRotation = Quaternion.Euler(-2, 0, 0);
        abdObj.transform.localScale = ab;
        var abdMf = abdObj.AddComponent<MeshFilter>();
        abdMf.sharedMesh = abdomenMesh;
        var abdMr = abdObj.AddComponent<MeshRenderer>();
        abdMr.sharedMaterial = bodyMat;
        abdMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var abd = abdObj.transform;

        // 5. Lore-accurate posterior spinnerets (silk-spinning nubs at rear-ventral apex of butt)
        // For wasp: sharp black stinger cone/spinneret tip
        Color spinCol = (def.name == "Wasp") ? new Color(0.08f, 0.06f, 0.09f) : (def.body * 0.76f);
        var spinMat = Flat(spinCol, bodyMat, 0, def.metallic, def.gloss);
        float rearZ = -0.70f * R - 0.53f * ab.z;
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            var spin = Part(PrimitiveType.Sphere, bodyT, new Vector3(0.08f * sgn * R, 0.56f * R - pivotY, rearZ), new Vector3(0.12f, 0.10f, 0.14f) * R, spinMat).transform;
            spin.localRotation = Quaternion.Euler(14, 10 * sgn, 0);
            spin.name = "spinneret";
            cs.spinnerets.Add(spin);
        }
        cs.abdomen = abdObj.transform;
        cs.abdomenRest = abdObj.transform.localRotation;
        cs.hasAbdomenRest = true;

        // Eyes: stylish, expressive, beautiful arachnid gaze with distinct skin-colored pupils
        Vector3 HS(Vector3 dir, float lift) { dir.Normalize(); return headT.localPosition + Vector3.Scale(dir, new Vector3(0.68f, 0.58f, 0.62f) * hr * R) + dir * lift * R; }
        cs.eyes.Clear();

        // Outer eyeball base: sleek dark glossy sclera so colored pupils pop beautifully
        var eyeballBaseMat = Flat(new Color(0.08f, 0.07f, 0.09f, 1f), null, 0f, 0.1f, 0.95f);

        // Pupil color derived directly from skin: if def.eye is dark/unset, use accent or vibrant body color
        Color pupilColor = def.eye;
        float eyeBrightness = Mathf.Max(pupilColor.r, Mathf.Max(pupilColor.g, pupilColor.b));
        if (eyeBrightness < 0.22f)
        {
            // Pick prominent contrasting skin color
            float accentBrightness = Mathf.Max(def.accent.r, Mathf.Max(def.accent.g, def.accent.b));
            if (accentBrightness > 0.35f) pupilColor = def.accent;
            else pupilColor = Color.Lerp(def.body, Color.white, 0.15f);
        }
        float pupilGlow = Mathf.Max(def.eyeGlow, 0.25f);
        var pupilMat = Flat(pupilColor, null, pupilGlow, 0.1f, 0.95f);

        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            // Main pair of prominent glossy forward-facing eyes
            var d = new Vector3(0.38f * sgn, 0.16f, 0.92f).normalized;
            var e = Part(PrimitiveType.Sphere, bodyT, HS(d, -0.03f), new Vector3(0.42f, 0.42f, 0.18f) * R, eyeballBaseMat).transform;
            e.localRotation = Quaternion.LookRotation(d, Vector3.up); e.name = "eye";

            // Colored pupil dot resting visibly in the center of the eye
            var pupil = Part(PrimitiveType.Sphere, e, new Vector3(0f, 0f, 0.34f), new Vector3(0.62f, 0.62f, 0.42f), pupilMat).transform;
            pupil.name = "pupil";

            // Inner eye highlight shine dot (offset to the top-right / cute anime gleam)
            var shineObj = Part(PrimitiveType.Sphere, e, new Vector3(0.18f, 0.20f, 0.46f), new Vector3(0.24f, 0.24f, 0.28f), shine).transform;
            shineObj.name = "shine";

            cs.eyes.Add(e); cs.eyeRest.Add(e.localScale);

            // Secondary lateral eyes
            var sd = new Vector3(0.82f * sgn, 0.24f, 0.52f).normalized;
            var se = Part(PrimitiveType.Sphere, bodyT, HS(sd, -0.02f), new Vector3(0.14f, 0.14f, 0.07f) * R, eyeballBaseMat).transform;
            se.localRotation = Quaternion.LookRotation(sd, Vector3.up);
            Part(PrimitiveType.Sphere, se, new Vector3(0f, 0f, 0.28f), new Vector3(0.65f, 0.65f, 0.45f), pupilMat);
            cs.eyes.Add(se); cs.eyeRest.Add(se.localScale);

            // Cute chelicerae / fangs seamlessly attached to the front-lower snout of the head
            // In local head space: snout extends forward along +Z, lower half around Y ~ -0.25 to -0.40
            var fangBase = new Vector3(0.18f * sgn * hr, -0.24f * hr, 0.44f * hr);
            var f = Part(PrimitiveType.Capsule, headT, fangBase, new Vector3(0.18f, 0.26f, 0.18f) * hr, fangMat).transform;
            f.localRotation = Quaternion.Euler(25, 10 * sgn, -8 * sgn);
            f.name = "fang";

            // Tiny cute curved tooth tip at the bottom of each chelicera
            var tooth = Part(PrimitiveType.Sphere, f, new Vector3(0, -0.42f, 0.10f), new Vector3(0.7f, 0.7f, 0.7f), fangMat).transform;
            tooth.name = "fang_tip";
        }

        // --- Character Ears for Animal Skins (Squirrel & Tiger) ---
        if (def.name == "Squirrel")
        {
            // Perked, tufted squirrel ears sitting proudly on the dorsal-lateral crown of the head
            var earBaseMat = Flat(headBodyCol, null, def.emission * 0.4f, def.metallic, def.gloss);
            var earInnerMat = Flat(new Color(0.98f, 0.88f, 0.78f, 1f), null, 0f, 0.05f, 0.90f);
            var tuftMat = Flat(new Color(0.52f, 0.18f, 0.06f, 1f), null, 0f, def.metallic, def.gloss);

            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                // Main upright ear body tilted charmingly outward
                var earPos = new Vector3(0.35f * sgn, 0.46f, 0.04f);
                var ear = Part(PrimitiveType.Capsule, headT, earPos, new Vector3(0.22f, 0.38f, 0.15f), earBaseMat).transform;
                ear.localRotation = Quaternion.Euler(-8f, 16f * sgn, -24f * sgn);
                ear.name = sgn < 0 ? "ear_squirrel_L" : "ear_squirrel_R";

                // Soft warm cream inner ear fold
                var inner = Part(PrimitiveType.Capsule, ear, new Vector3(0, -0.02f, 0.22f), new Vector3(0.65f, 0.72f, 0.45f), earInnerMat).transform;
                inner.name = "inner_ear";

                // Signature squirrel ear tuft / fluffy fur tip at the pointed apex
                var tuft = Part(PrimitiveType.Sphere, ear, new Vector3(0, 0.46f, 0.02f), new Vector3(0.48f, 0.44f, 0.40f), tuftMat).transform;
                tuft.name = "ear_tuft";
            }
        }
        else if (def.name == "Tiger")
        {
            // Rounded, alert predator tiger ears with black outer rim and soft cream inner cup
            var earOuterMat = Flat(headBodyCol, null, def.emission * 0.4f, def.metallic, def.gloss);
            var earRimMat = Flat(new Color(0.12f, 0.10f, 0.12f, 1f), null, 0f, 0.05f, 0.85f); // Black tiger ear back / rim
            var earInnerMat = Flat(new Color(0.98f, 0.96f, 0.90f, 1f), null, 0f, 0.05f, 0.92f); // Fluffy white/cream inner cup

            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                // Rounded ear cup perched on the lateral crown of the head
                var earPos = new Vector3(0.38f * sgn, 0.44f, 0.06f);
                var ear = Part(PrimitiveType.Sphere, headT, earPos, new Vector3(0.28f, 0.26f, 0.14f), earOuterMat).transform;
                ear.localRotation = Quaternion.Euler(14f, 22f * sgn, -16f * sgn);
                ear.name = sgn < 0 ? "ear_tiger_L" : "ear_tiger_R";

                // Black back-shell and rim marking
                var rim = Part(PrimitiveType.Sphere, ear, new Vector3(0, 0, -0.15f), new Vector3(0.98f, 0.98f, 0.75f), earRimMat).transform;
                rim.name = "ear_rim";

                // Fluffy white/cream inner cup
                var inner = Part(PrimitiveType.Sphere, ear, new Vector3(0, 0, 0.22f), new Vector3(0.68f, 0.68f, 0.50f), earInnerMat).transform;
                inner.name = "inner_ear";
            }
        }
        else if (def.name == "Ruby")
        {
            if (!gemMesh) gemMesh = MakeGemMesh();
            var foreheadGemMat = Flat(new Color(1.0f, 0.22f, 0.40f, 1f), null, 0.65f, 0.3f, 0.98f);
            if (foreheadGemMat.HasProperty("_OutlineWidth")) foreheadGemMat.SetFloat("_OutlineWidth", 1.2f);
            if (foreheadGemMat.HasProperty("_OutlineColor")) foreheadGemMat.SetColor("_OutlineColor", new Color(0.22f, 0.02f, 0.06f, 1f));

            // Forehead crown royal ruby gemstone
            var headGem = new GameObject("ruby_forehead_gem");
            headGem.transform.SetParent(headT, false);
            headGem.transform.localPosition = new Vector3(0, 0.38f, 0.32f);
            headGem.transform.localRotation = Quaternion.Euler(-32f, 0, 0);
            headGem.transform.localScale = new Vector3(0.18f, 0.16f, 0.18f);

            headGem.AddComponent<MeshFilter>().sharedMesh = gemMesh;
            var mr = headGem.AddComponent<MeshRenderer>();
            mr.sharedMaterial = foreheadGemMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else if (def.name == "Bat")
        {
            if (!cone) cone = MakeCone();
            // Authentic cute stylized pointy bat ears: tall, razor-pointy alert pinnae with dusky mauve inner ear canal
            var earBaseMat = Flat(headBodyCol, null, def.emission * 0.4f, def.metallic, def.gloss);
            var earInnerMat = Flat(new Color(0.72f, 0.45f, 0.62f, 1f), null, 0f, 0.05f, 0.90f);
            var tragusMat = Flat(new Color(0.62f, 0.38f, 0.54f, 1f), null, 0f, def.metallic, def.gloss);

            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                // Main pointy bat ear (positioned right on the top-front crown of the head, framing the eyes)
                var earPos = new Vector3(0.22f * sgn, 0.38f, 0.14f);
                var ear = new GameObject(sgn < 0 ? "ear_bat_L" : "ear_bat_R");
                ear.transform.SetParent(headT, false);
                ear.transform.localPosition = earPos;
                ear.transform.localRotation = Quaternion.Euler(8f, 10f * sgn, -22f * sgn);
                ear.transform.localScale = new Vector3(0.20f, 0.48f, 0.12f);
                ear.AddComponent<MeshFilter>().sharedMesh = cone;
                var mrEar = ear.AddComponent<MeshRenderer>();
                mrEar.sharedMaterial = earBaseMat;
                mrEar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Inner pointed ear cavity (scalloped dusky mauve membrane with sharp tip)
                var inner = new GameObject("inner_ear");
                inner.transform.SetParent(ear.transform, false);
                inner.transform.localPosition = new Vector3(0, 0.015f, 0.02f);
                inner.transform.localRotation = Quaternion.identity;
                inner.transform.localScale = new Vector3(0.72f, 0.82f, 0.60f);
                inner.AddComponent<MeshFilter>().sharedMesh = cone;
                var mrInner = inner.AddComponent<MeshRenderer>();
                mrInner.sharedMaterial = earInnerMat;
                mrInner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Cute bat tragus notch on the inner margin of the ear lobe
                var tragus = Part(PrimitiveType.Sphere, ear.transform, new Vector3(-0.06f * sgn, 0.06f, 0.05f), new Vector3(0.28f, 0.22f, 0.22f), tragusMat).transform;
                tragus.name = "ear_tragus";
            }
        }

        // --- 6 Sleek, Slender Legs (3 on each side uniformly spaced) Seamlessly Attached with Smooth Tips ---
        float[] legZ = { 0.28f, 0.00f, -0.28f };
        float[] legX = { 0.40f, 0.45f, 0.41f };
        float[] legY = { 0.49f, 0.50f, 0.47f };
        float[] legYaw = { 42f, 90f, 138f };
        float[] legLengthMult = { 1.08f, 1.15f, 1.12f };

        for (int sgn = -1; sgn <= 1; sgn += 2)
            for (int k = 0; k < 3; k++)
            {
                float z = legZ[k];
                float sideX = sgn * legX[k];
                float hipY = legY[k];

                var hip = new GameObject("leg").transform; hip.SetParent(bodyT, false);
                hip.localPosition = P(sideX, hipY, z);

                // Natural spider leg spread angle radiating around the cephalothorax
                float yaw = sgn * legYaw[k];
                hip.localRotation = Quaternion.Euler(0, yaw, 0);

                // Thinner, elegant, stylized proportions
                float mult = legLengthMult[k];
                float thUpper = 0.13f;
                float thLower = 0.10f;
                float upperLen = 0.62f * mult;
                float lowerLen = 0.74f * mult;

                // Upper leg (femur): slender segment rising from the body
                float archAngle = 55f;
                float rad = archAngle * Mathf.Deg2Rad;
                Vector3 midUpper = new Vector3(0, Mathf.Sin(rad) * upperLen * 0.5f * R, Mathf.Cos(rad) * upperLen * 0.5f * R);
                var up1 = Part(PrimitiveType.Capsule, hip, midUpper, new Vector3(thUpper, upperLen * 0.5f, thUpper) * R, legMat).transform;
                up1.localRotation = Quaternion.Euler(archAngle, 0, 0);

                // Knee position where the two segments meet cleanly (no sphere ball)
                Vector3 kneePos = new Vector3(0, Mathf.Sin(rad) * upperLen * R, Mathf.Cos(rad) * upperLen * R);

                // Lower leg (tibia): slender segment stepping downward from the knee to ground
                // Capsule mesh has a naturally smooth hemisphere cap at both ends (clean joint and smooth rounded tip)
                float downAngle = -42f;
                float downRad = downAngle * Mathf.Deg2Rad;
                Vector3 midLower = kneePos + new Vector3(0, Mathf.Sin(downRad) * lowerLen * 0.5f * R, Mathf.Cos(downRad) * lowerLen * 0.5f * R);
                var lo = Part(PrimitiveType.Capsule, hip, midLower, new Vector3(thLower, lowerLen * 0.5f, thLower) * R, legMat).transform;
                lo.localRotation = Quaternion.Euler(downAngle, 0, 0);

                cs.legs.Add(hip); cs.legRest.Add(hip.localRotation);
            }
        // Spikes along the back ridge (skin feature) directly seated on the sculpted abdomen
        if (def.spikes > 0)
        {
            if (!cone) cone = MakeCone();

            if (def.name == "Golden")
            {
                // Royal Golden Coronet / Crown of 7 radiant golden spikes arched across dorsal apex
                // Spikes arranged in an ornamental coronet: majestic central spire flanked by graded royal tiara points
                int crownCount = 7;
                float apexV = 0.38f;
                float theta = apexV * Mathf.PI;
                float sTheta = Mathf.Max(0f, Mathf.Sin(theta));
                float cTheta = Mathf.Cos(theta);
                float zBase = cTheta * 0.52f - 0.03f;
                float widthMod = 1f + 0.16f * cTheta - 0.05f * (cTheta * cTheta);
                float ry = sTheta * widthMod * 0.40f;
                float rx = sTheta * widthMod * 0.52f;
                float yCenter = sTheta * sTheta * 0.04f - 0.02f * apexV;
                float yBase = yCenter + ry;

                float crownRadiusX = 0.28f * rx;
                float crownRadiusZ = 0.16f;

                for (int k = 0; k < crownCount; k++)
                {
                    // Semi-circular tiara / ring arch around the dorsal crown
                    float t = (float)k / (crownCount - 1); // 0 to 1
                    float angleDeg = Mathf.Lerp(-95f, 95f, t);
                    float rad = angleDeg * Mathf.Deg2Rad;

                    float cx = Mathf.Sin(rad) * crownRadiusX;
                    float cz = zBase + Mathf.Cos(rad) * crownRadiusZ - crownRadiusZ * 0.5f;
                    float cy = yBase - (cx * cx) * 0.8f - ((cz - zBase) * (cz - zBase)) * 0.6f;

                    var g = new GameObject("crown_spike");
                    g.transform.SetParent(abd, false);
                    g.transform.localPosition = new Vector3(cx, cy - 0.02f, cz);

                    // Splay spikes slightly outward like an authentic royal crown/coronet
                    Vector3 upDir = (Vector3.up * 1.6f + new Vector3(Mathf.Sin(rad) * 0.55f, 0, Mathf.Cos(rad) * 0.35f)).normalized;
                    g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, upDir);

                    // Crown height profile: Center spire is tall & grand (k=3), side spires step down harmoniously
                    int distFromCenter = Mathf.Abs(k - crownCount / 2); // 0 at center, 1, 2, 3 at sides
                    float heightFactor = distFromCenter == 0 ? 1.45f : (distFromCenter == 1 ? 1.18f : (distFromCenter == 2 ? 0.95f : 0.78f));
                    float len = def.spikeLength * heightFactor * 1.15f;
                    float thickness = 0.22f * (distFromCenter == 0 ? 1.25f : 1.0f);
                    g.transform.localScale = new Vector3(thickness, len, thickness);

                    // Distinct royal imperial amber-gold crown spikes - clearly distinct from bright canary-gold spider body
                    Color spkColor = (distFromCenter == 0)
                        ? new Color(1.0f, 0.65f, 0.04f)   // Majestic central spire: Deep Imperial Amber Gold (#FFA60A)
                        : ((k % 2 == 0)
                            ? new Color(0.96f, 0.58f, 0.02f) // Flanking spires: Warm Burnished Gold (#F59405)
                            : new Color(1.0f, 0.72f, 0.08f)); // Outer spires: Radiant Honey Gold (#FFB814)

                    float spkEmis = (distFromCenter == 0) ? 0.28f : 0.20f;
                    var crownMat = Flat(spkColor, null, spkEmis, 0.95f, 0.98f);
                    if (crownMat.HasProperty("_OutlineWidth")) crownMat.SetFloat("_OutlineWidth", 1.4f);
                    if (crownMat.HasProperty("_OutlineColor")) crownMat.SetColor("_OutlineColor", new Color(0.18f, 0.10f, 0.04f, 1f));

                    g.AddComponent<MeshFilter>().sharedMesh = cone;
                    var mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = crownMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            else if (def.name == "Ruby")
            {
                if (!gemMesh) gemMesh = MakeGemMesh();

                // Ruby Gem Material: luminous pigeon-blood ruby red with glowing facet luster
                var rubyGemMat = Flat(new Color(0.92f, 0.10f, 0.28f, 1f), null, 0.45f, 0.2f, 0.95f);
                if (rubyGemMat.HasProperty("_OutlineWidth")) rubyGemMat.SetFloat("_OutlineWidth", 1.2f);
                if (rubyGemMat.HasProperty("_OutlineColor")) rubyGemMat.SetColor("_OutlineColor", new Color(0.22f, 0.02f, 0.06f, 1f));

                // Secondary alternating garnet/rose sapphire gem material for rich jewelry contrast
                var roseGemMat = Flat(new Color(1.0f, 0.25f, 0.42f, 1f), null, 0.55f, 0.25f, 0.98f);
                if (roseGemMat.HasProperty("_OutlineWidth")) roseGemMat.SetFloat("_OutlineWidth", 1.2f);
                if (roseGemMat.HasProperty("_OutlineColor")) roseGemMat.SetColor("_OutlineColor", new Color(0.22f, 0.02f, 0.06f, 1f));

                // Helper to compute exact dorsal surface height at local (gx, gz) on the sculpted abdomen
                float GetDorsalSurfaceY(float gx, float gz)
                {
                    float cT = Mathf.Clamp((gz + 0.03f) / 0.52f, -0.99f, 0.99f);
                    float theta = Mathf.Acos(cT);
                    float sT = Mathf.Sin(theta);
                    float vT = theta / Mathf.PI;
                    float wM = 1.0f + 0.16f * cT - 0.05f * (cT * cT);
                    float rxT = sT * wM * 0.52f;
                    float ryT = sT * wM * 0.40f;
                    float ycT = sT * sT * 0.04f - 0.02f * vT;
                    float term = Mathf.Max(0f, 1.0f - (gx * gx) / Mathf.Max(0.0001f, rxT * rxT));
                    return ycT + ryT * Mathf.Sqrt(term);
                }

                // Royal Gemstone Brooch Formation:
                // 1 Grand Majestic Centerpiece Ruby crowned at dorsal apex + 6 Halo Gemstones in an elegant symmetrical ring
                // All gems sit strictly UPRIGHT with their sparkling flat table facets facing skyward (+Y).
                float apexV = 0.42f;
                float cApex = Mathf.Cos(apexV * Mathf.PI);
                float zApex = cApex * 0.52f - 0.03f;
                float yApex = GetDorsalSurfaceY(0f, zApex);

                // 1. Grand Centerpiece Ruby
                var centerGem = new GameObject("ruby_centerpiece");
                centerGem.transform.SetParent(abd, false);
                centerGem.transform.localPosition = new Vector3(0f, yApex - 0.01f, zApex);
                // abd is pitched by -2 deg; rotate +2 deg around X so the table facet is 100% upright in world space
                centerGem.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
                centerGem.transform.localScale = new Vector3(0.27f, 0.23f, 0.27f);

                centerGem.AddComponent<MeshFilter>().sharedMesh = gemMesh;
                var mrCenter = centerGem.AddComponent<MeshRenderer>();
                mrCenter.sharedMaterial = rubyGemMat;
                mrCenter.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // 2. Halo of 6 Sparkling Gemstones in a symmetrical oval ring around the centerpiece
                float haloRx = 0.175f;
                float haloRz = 0.185f;
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * Mathf.PI * 2.0f / 6.0f;
                    float gx = Mathf.Sin(ang) * haloRx;
                    float gz = zApex + Mathf.Cos(ang) * haloRz;
                    float gy = GetDorsalSurfaceY(gx, gz) - 0.015f;

                    var haloGem = new GameObject("ruby_halo_gem_" + i);
                    haloGem.transform.SetParent(abd, false);
                    haloGem.transform.localPosition = new Vector3(gx, gy, gz);
                    haloGem.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
                    haloGem.transform.localScale = new Vector3(0.165f, 0.14f, 0.165f);

                    haloGem.AddComponent<MeshFilter>().sharedMesh = gemMesh;
                    var mrHalo = haloGem.AddComponent<MeshRenderer>();
                    mrHalo.sharedMaterial = (i % 2 == 0) ? roseGemMat : rubyGemMat;
                    mrHalo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            else
            {
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

                    // Rhythmic alternating heights and pattern diversity across the spine
                    float lengthWave = def.spikes > 3 ? (k % 2 == 0 ? 1.0f : 0.82f) : 1.0f;
                    float len = def.spikeLength * (k == 0 || k == def.spikes - 1 ? 0.72f : 1.0f) * lengthWave;
                    float thickness = 0.26f * (k % 2 == 0 ? 1.05f : 0.92f);
                    g.transform.localScale = new Vector3(thickness, len, thickness);

                    // Diverse spike coloring based on skin pattern and accent/body contrast
                    Color spkColor;
                    float spkEmis = def.emission;
                    if (def.name == "Wasp")
                    {
                        // Alternating sharp black and luminous yellow-amber wasp stinger thorns
                        spkColor = (k % 2 == 0) ? new Color(0.10f, 0.08f, 0.12f) : new Color(1f, 0.85f, 0.15f);
                        if (k % 2 != 0) spkEmis = 0.45f;
                    }
                    else if (def.name == "Squirrel")
                    {
                        // Warm rustic gradient matching squirrel coat from chestnut to russet orange
                        spkColor = Color.Lerp(new Color(0.42f, 0.16f, 0.08f), new Color(0.92f, 0.45f, 0.18f), (float)k / Mathf.Max(1, def.spikes - 1));
                    }
                    else switch (def.pattern)
                    {
                        case SkinPattern.Stripes:
                        case SkinPattern.Checker:
                            // Alternating contrasting stripe colors
                            spkColor = (k % 2 == 0) ? def.accent : Color.Lerp(def.body, def.accent, 0.4f);
                            break;
                        case SkinPattern.Dots:
                            // Central crest / focal highlights
                            spkColor = (k == def.spikes / 2) ? Color.Lerp(def.accent, Color.white, 0.35f) : def.accent;
                            if (k == def.spikes / 2 && spkEmis > 0) spkEmis *= 1.4f;
                            break;
                        case SkinPattern.Gradient:
                            // Beautiful gradient transition from front to rear
                            spkColor = Color.Lerp(def.accent, def.body, (float)k / Mathf.Max(1, def.spikes - 1));
                            break;
                        case SkinPattern.Zigzag:
                            // Dynamic energetic zigzag alternations
                            spkColor = (k % 2 == 0) ? def.accent * 1.15f : Color.Lerp(def.accent, def.body, 0.3f);
                            break;
                        default: // Solid
                            // Two-tone cadence matching skin theme
                            bool alt = (k % 2 == 1);
                            spkColor = alt ? Color.Lerp(def.accent, def.body, 0.5f) : def.accent;
                            break;
                    }

                    // If accent is very dark (e.g. Tiger stripes), brighten the alternating spikes with a warm golden highlight
                    if (spkColor.grayscale < 0.20f)
                    {
                        spkColor = Color.Lerp(def.body, Color.white, 0.15f);
                    }

                    var customSpikeMat = Flat(spkColor, null, spkEmis, def.metallic, def.gloss);

                    g.AddComponent<MeshFilter>().sharedMesh = cone;
                    var mr = g.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = customSpikeMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        // Character wings on spider's back (Bee, Wasp, Bat)
        if (def.name == "Bee")
        {
            var wingLMesh = MakeBeeWingMesh(true);
            var wingRMesh = MakeBeeWingMesh(false);

            var beeTex = Resources.Load<Texture2D>("Enemies/Bee_Wing");
#if UNITY_EDITOR
            if (!beeTex) beeTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Resources/Enemies/Bee_Wing.png");
#endif
            // Transparent alpha-blended material with translucent silver-grey membrane and cellular venation
            var transShader = Shader.Find("TallCrawl/TCFadeEntity")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Mobile/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");
            var beeWingMat = new Material(transShader);
            if (beeTex) beeWingMat.mainTexture = beeTex;
            beeWingMat.color = Color.white;

            // Left Wing Pivot & Mesh (3x smaller than 7.6f, perched upright on abdomen)
            var wingPivotL = new GameObject("bee_wing_pivot_L");
            wingPivotL.transform.SetParent(abd, false);
            wingPivotL.transform.localPosition = new Vector3(-0.18f, 0.42f, 0.16f);
            wingPivotL.transform.localRotation = Quaternion.Euler(22f, -26f, 18f);
            cs.wingPivotL = wingPivotL.transform;
            cs.wingRestL = wingPivotL.transform.localRotation;
            cs.wingType = "Bee";

            var wingMeshL = new GameObject("wing_mesh_L");
            wingMeshL.transform.SetParent(wingPivotL.transform, false);
            wingMeshL.transform.localScale = Vector3.one * 2.5f; // 3x smaller!
            var mfL = wingMeshL.AddComponent<MeshFilter>();
            mfL.sharedMesh = wingLMesh;
            var mrL = wingMeshL.AddComponent<MeshRenderer>();
            mrL.sharedMaterial = beeWingMat;
            mrL.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrL.receiveShadows = false;

            // Right Wing Pivot & Mesh
            var wingPivotR = new GameObject("bee_wing_pivot_R");
            wingPivotR.transform.SetParent(abd, false);
            wingPivotR.transform.localPosition = new Vector3(0.18f, 0.42f, 0.16f);
            wingPivotR.transform.localRotation = Quaternion.Euler(22f, 26f, -18f);
            cs.wingPivotR = wingPivotR.transform;
            cs.wingRestR = wingPivotR.transform.localRotation;

            var wingMeshR = new GameObject("wing_mesh_R");
            wingMeshR.transform.SetParent(wingPivotR.transform, false);
            wingMeshR.transform.localScale = Vector3.one * 2.5f; // 3x smaller!
            var mfR = wingMeshR.AddComponent<MeshFilter>();
            mfR.sharedMesh = wingRMesh;
            var mrR = wingMeshR.AddComponent<MeshRenderer>();
            mrR.sharedMaterial = beeWingMat;
            mrR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrR.receiveShadows = false;
        }
        else if (def.name == "Wasp")
        {
            var wingLMesh = MakeWaspWingMesh(true);
            var wingRMesh = MakeWaspWingMesh(false);

            var waspTex = Resources.Load<Texture2D>("Enemies/Wasp_Wing");
#if UNITY_EDITOR
            if (!waspTex) waspTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Resources/Enemies/Wasp_Wing.png");
#endif
            // Transparent alpha-blended material with translucent smoky dark slate membrane and sharp costal edge
            var transShader = Shader.Find("TallCrawl/TCFadeEntity")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Mobile/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");
            var waspWingMat = new Material(transShader);
            if (waspTex) waspWingMat.mainTexture = waspTex;
            waspWingMat.color = Color.white;

            // Left Wing Pivot & Mesh (3x smaller than 7.6f, long razor blade swept back along abdomen)
            var wingPivotL = new GameObject("wasp_wing_pivot_L");
            wingPivotL.transform.SetParent(abd, false);
            wingPivotL.transform.localPosition = new Vector3(-0.20f, 0.40f, 0.14f);
            wingPivotL.transform.localRotation = Quaternion.Euler(14f, -12f, 8f);
            cs.wingPivotL = wingPivotL.transform;
            cs.wingRestL = wingPivotL.transform.localRotation;
            cs.wingType = "Wasp";

            var wingMeshL = new GameObject("wing_mesh_L");
            wingMeshL.transform.SetParent(wingPivotL.transform, false);
            wingMeshL.transform.localScale = Vector3.one * 2.5f; // 3x smaller!
            var mfL = wingMeshL.AddComponent<MeshFilter>();
            mfL.sharedMesh = wingLMesh;
            var mrL = wingMeshL.AddComponent<MeshRenderer>();
            mrL.sharedMaterial = waspWingMat;
            mrL.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrL.receiveShadows = false;

            // Right Wing Pivot & Mesh
            var wingPivotR = new GameObject("wasp_wing_pivot_R");
            wingPivotR.transform.SetParent(abd, false);
            wingPivotR.transform.localPosition = new Vector3(0.20f, 0.40f, 0.14f);
            wingPivotR.transform.localRotation = Quaternion.Euler(14f, 12f, -8f);
            cs.wingPivotR = wingPivotR.transform;
            cs.wingRestR = wingPivotR.transform.localRotation;

            var wingMeshR = new GameObject("wing_mesh_R");
            wingMeshR.transform.SetParent(wingPivotR.transform, false);
            wingMeshR.transform.localScale = Vector3.one * 2.5f; // 3x smaller!
            var mfR = wingMeshR.AddComponent<MeshFilter>();
            mfR.sharedMesh = wingRMesh;
            var mrR = wingMeshR.AddComponent<MeshRenderer>();
            mrR.sharedMaterial = waspWingMat;
            mrR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrR.receiveShadows = false;
        }
        else if (def.name == "Bat")
        {
            // Guaranteed robust bat wing mesh (uses 3D model asset with automatic procedural fallback)
            var wingLMesh = TCEnemy.GetBatMesh("WingL") ?? TCEnemy.MakeBatWingMesh(true);
            var wingRMesh = TCEnemy.GetBatMesh("WingR") ?? TCEnemy.MakeBatWingMesh(false);

            var batTex = Resources.Load<Texture2D>("Enemies/Bat_Texture");
#if UNITY_EDITOR
            if (!batTex) batTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TC/Resources/Enemies/Bat_Texture.png");
#endif
            var batWingMat = Flat(new Color(0.28f, 0.22f, 0.36f, 1f), null, 0.28f, 0.1f, 0.85f);
            if (batTex) batWingMat.mainTexture = batTex;
            if (batWingMat.HasProperty("_OutlineWidth")) batWingMat.SetFloat("_OutlineWidth", 1.4f);
            if (batWingMat.HasProperty("_OutlineColor")) batWingMat.SetColor("_OutlineColor", new Color(0.38f, 0.14f, 0.52f, 1f));

            // Left Wing Pivot & Mesh (anchored on upper dorsal back, parallel to body surface)
            var wingPivotL = new GameObject("bat_wing_pivot_L");
            wingPivotL.transform.SetParent(abd, false);
            wingPivotL.transform.localPosition = new Vector3(0.1f, 0.44f, 0.35f);
            wingPivotL.transform.localRotation = Quaternion.Euler(82f, -24f, 12f);
            cs.wingPivotL = wingPivotL.transform;
            cs.wingRestL = wingPivotL.transform.localRotation;
            cs.wingType = "Bat";

            var wingMeshL = new GameObject("wing_mesh_L");
            wingMeshL.transform.SetParent(wingPivotL.transform, false);
            wingMeshL.transform.localRotation = Quaternion.Euler(0f, 0f, -17.23f);
            wingMeshL.transform.localScale = Vector3.one * 2.56f; // 1.6x bigger!
            var mfL = wingMeshL.AddComponent<MeshFilter>();
            mfL.sharedMesh = wingLMesh;
            var mrL = wingMeshL.AddComponent<MeshRenderer>();
            mrL.sharedMaterial = batWingMat;
            mrL.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrL.receiveShadows = false;

            // Right Wing Pivot & Mesh
            var wingPivotR = new GameObject("bat_wing_pivot_R");
            wingPivotR.transform.SetParent(abd, false);
            wingPivotR.transform.localPosition = new Vector3(-0.1f, 0.44f, 0.35f);
            wingPivotR.transform.localRotation = Quaternion.Euler(82f, 24f, -12f);
            cs.wingPivotR = wingPivotR.transform;
            cs.wingRestR = wingPivotR.transform.localRotation;

            var wingMeshR = new GameObject("wing_mesh_R");
            wingMeshR.transform.SetParent(wingPivotR.transform, false);
            wingMeshR.transform.localRotation = Quaternion.Euler(0f, 0f, 17.23f);
            wingMeshR.transform.localScale = Vector3.one * 2.56f; // 1.6x bigger!
            var mfR = wingMeshR.AddComponent<MeshFilter>();
            mfR.sharedMesh = wingRMesh;
            var mrR = wingMeshR.AddComponent<MeshRenderer>();
            mrR.sharedMaterial = batWingMat;
            mrR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrR.receiveShadows = false;
        }

        cs.anim = spider.GetComponentInChildren<Animator>();
        cs.pi = spider.GetComponent<playerInteract>();
        var smr0 = smrs.Length > 0 ? smrs[0] : null;
        cs.bone = smr0 && smr0.rootBone ? smr0.rootBone : head;
        cs.boneRest = cs.bone ? st.InverseTransformPoint(cs.bone.position) : Vector3.zero;
        cs.restRoot = root.localPosition; cs.hasRestRoot = true; cs.root = root; cs.flip = 0;

        // Setup Rainbow wave animation if Rainbow skin
        cs.isRainbow = def.name == "Rainbow";
        cs.rainbowRenderers.Clear();
        cs.rainbowOriginalMats.Clear();
        if (cs.isRainbow)
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // We shift colors of body parts, legs, abdomen, spikes, spinnerets, fangs (keep eyes & shine crisp)
                if (mr.name == "shine" || mr.name == "eye") continue;
                cs.rainbowRenderers.Add(mr);
                cs.rainbowOriginalMats.Add(mr.material); // instantiate local instance so it doesn't affect shared cache
            }
        }
    }

    [System.NonSerialized] public float wave; // 0..1, menu wave (front leg up)
    public Bounds cachedBounds; public Vector3 cachedCenter; public bool hasRestRoot; Transform root; public Vector3 restRoot;
    public Transform BodyTransform => body != null ? body : (root != null ? root : transform);
    public Transform CuteRoot => root;
    public float CurrentAirHeight => currentAirHeight;
    bool isRainbow;
    readonly List<MeshRenderer> rainbowRenderers = new List<MeshRenderer>();
    readonly List<Material> rainbowOriginalMats = new List<Material>();
    float backflipTimer, backflipDuration;
    float currentAirHeight;
    float jumpBlend; // 0 (on ground/running) -> 1 (fully in jump pose)
    float rollBlend;

    public void TriggerBackflip(float duration = 0.75f)
    {
        backflipDuration = duration;
        backflipTimer = duration;
    }

    public void TriggerRoll(float duration = 1.2f)
    {
        rollDuration = duration;
        rollTimer = duration;
    }

    public void OnRevive()
    {
        flip = 0f;
        backflipTimer = 0f;
        rollTimer = 0f;
        rollBlend = 0f;
        currentAirHeight = 0f;
        jumpBlend = 0f;
        hasSwingWorldRot = false;
        if (body)
        {
            body.localPosition = bodyRest;
            body.localRotation = Quaternion.identity;
            body.localScale = bodyScale != Vector3.zero ? bodyScale : Vector3.one;
        }
        if (root && hasRestRoot)
        {
            root.localPosition = restRoot;
        }
    }

    public Vector3 VisualCenter()
    {
        if (body) return body.position;
        if (root) return root.position;
        if (abdomen) return abdomen.position;
        return transform.position;
    }

    /// World position of the spinnerets (abdomen tip / rear of the body) for silk.
    public Vector3 SpinneretTip()
    {
        if (spinnerets != null && spinnerets.Count > 0)
        {
            Vector3 acc = Vector3.zero; int n = 0;
            for (int i = 0; i < spinnerets.Count; i++)
                if (spinnerets[i]) { acc += spinnerets[i].position; n++; }
            if (n > 0) return acc / n;
        }
        if (abdomen) return abdomen.TransformPoint(new Vector3(0f, -0.02f, -0.55f));
        if (body) return body.TransformPoint(new Vector3(0f, 0.15f, -0.7f));
        return transform.position;
    }

    /// World position of the front claws/face where forward swing silk shoots cleanly without intersecting the body.
    public Vector3 WebOrigin()
    {
        if (head) return head.TransformPoint(new Vector3(0f, 0.18f, 0.55f));
        if (body) return body.TransformPoint(new Vector3(0f, 0.24f, 0.72f));
        return transform.position;
    }

    public void SetSwingPose(float lean, float roll, float twist, float squash, float tuck = 0f)
    {
        hasSwingWorldRot = false;
        swingLean = lean; swingRoll = roll; swingTwist = twist; swingSquash = squash; swingTuck = tuck;
    }

    /// Drives the spider's world rotation directly (e.g. hanging upside down below a web string).
    public void SetSwingWorldOrientation(Quaternion worldRot, float squash, float tuck)
    {
        hasSwingWorldRot = true;
        swingWorldRot = worldRot;
        swingSquash = squash;
        swingTuck = tuck;
        if (body) body.rotation = worldRot;
    }

    public void ClearSwingPose()
    {
        hasSwingWorldRot = false;
        swingWorldRot = Quaternion.identity;
        swingLean = swingRoll = swingTwist = swingSquash = swingTuck = 0f;
    }

    public void TriggerLandBounce()
    {
        landingTimer = LandingDuration;
        TCSfx.Play("land", 0.5f);
    }

    void LateUpdate()
    {
        if (!root) return;
        float dt = Time.deltaTime;
        t += dt;
        var st = transform;
        // Follow the rig's root motion (jump hop etc.) but keep our own clean orientation.
        Vector3 rigOff = (!hasSwingWorldRot && bone) ? st.InverseTransformPoint(bone.position) - boneRest : Vector3.zero;
        if (rigOff.magnitude > 5f) rigOff = Vector3.zero;

        // Is the spider actively moving/climbing, jumping, rolling, or idle?
        var playerComp = GetComponentInParent<player>();
        bool isRolling = (pi != null && pi.boostDuration > 0) || (playerComp != null && playerComp.boost) || rollTimer > 0;
        if (rollTimer > 0) rollTimer -= dt;
        rollBlend = Mathf.MoveTowards(rollBlend, isRolling ? 1f : 0f, dt * 10f);
        if (isRolling) rollSpinAngle += dt * 1440f; // Sonic spin roll at 4 rev/sec
        else if (rollBlend <= 0.001f) rollSpinAngle = 0f;

        bool isIntroWalking = anim && anim.isActiveAndEnabled && anim.GetCurrentAnimatorStateInfo(0).IsName("spiderIntro");
        bool isJumping = (pi && pi.IsJumping) || (anim && anim.isActiveAndEnabled && anim.GetCurrentAnimatorStateInfo(0).IsName("SpiderJump")) || backflipTimer > 0;
        bool swingingNow = TCWebBonus.Instance && TCWebBonus.Instance.IsSwinging;
        bool isMoving = ((playerComp != null && !playerComp.paused && !playerComp.GameOver && !isJumping) || isIntroWalking) && !swingingNow;

        // Smooth jump/landing blend transition with Hermite curve for zero-hitch motion
        float targetJumpBlend = isJumping ? 1f : 0f;
        jumpBlend = Mathf.MoveTowards(jumpBlend, targetJumpBlend, dt * (isJumping ? 14f : 6.5f));
        float smoothJump = Mathf.SmoothStep(0f, 1f, jumpBlend);

        // Landing event & juicy spring squash:
        bool justLanded = wasJumping && !isJumping;
        wasJumping = isJumping;
        if (justLanded)
        {
            landingTimer = LandingDuration;
            TCSfx.Play("land", 0.5f);
        }

        if (landingTimer > 0f)
        {
            landingTimer = Mathf.MoveTowards(landingTimer, 0f, dt);
        }

        float landProgress = 1f - Mathf.Clamp01(landingTimer / LandingDuration); // 0 at touchdown -> 1 when recovered
        float landSquash = 0f;
        float landSpread = 0f;
        float landNod = 0f;
        if (landingTimer > 0f)
        {
            // Damped elastic spring squash: quick impact cushion, then smooth spring rebound into sprint
            float spring = Mathf.Sin(landProgress * Mathf.PI) * Mathf.Exp(-landProgress * 3.2f);
            landSquash = spring * 0.22f; // 22% vertical compression
            landSpread = spring * 0.14f; // 14% lateral cushion spread
            landNod = spring * 12f;      // 12 deg forward nod absorption
        }

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

        // Keep player colliders (hit box and points/pickup collector) synchronized with cute spider jumping altitude
        if (pi != null)
        {
            // In the spider's local coordinate system (direction=1, Y is outward radially from the trunk):
            // colliderLift moves outward along local Y when jumping off the trunk.
            Vector3 colliderLift = new Vector3(0f, currentAirHeight, 0f);
            if (pi.HitCollider != null)
            {
                pi.HitCollider.center = playerInteract.BASE_COLLIDER_CENTER + colliderLift;
            }
            if (pi.PointsCollider != null)
            {
                pi.PointsCollider.center = playerInteract.BASE_COLLIDER_CENTER + colliderLift;
            }
        }

        float hop = Mathf.Clamp01(currentAirHeight * 3.5f);

        // Stride × cadence ≈ climb speed so feet don't slide, including 2.5x cap and chilli boost.
        // Maintain cadence even during in-air flight so landing connects seamlessly with running stride.
        const float StrideLength = 0.36f; // world units per step cycle
        bool isActivelyRunning = (playerComp != null && !playerComp.paused && !playerComp.GameOver && !swingingNow);
        float climb = (isActivelyRunning && (isMoving || isJumping)) ? Mathf.Max(0f, playerComp.calcSpeed) : (isIntroWalking ? 1.5f : 0f);
        float yawNow = playerComp ? playerComp.CurrentYaw : lastYaw;
        float yawRate = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yawNow)) / Mathf.Max(dt, 1e-4f);
        lastYaw = yawNow;
        float steps = climb > 0.04f ? climb / StrideLength : 0f;
        if (isIntroWalking && steps < 1f) steps = 5f;
        // A little quicker when circling the trunk so steering doesn't look sluggish.
        if (yawRate > 25f) steps = Mathf.Max(steps, 4.5f) * 1.08f;
        float targetOmega = steps * (Mathf.PI * 2f);
        gaitOmega = Mathf.Lerp(gaitOmega, targetOmega, 1f - Mathf.Exp(-10f * dt));
        if ((isMoving || isJumping) && gaitOmega > 0.1f) gaitPhase += gaitOmega * dt;
        float phWalk = gaitPhase;
        // Keep amplitude readable at high cadence (boost at 2.5x) without jitter.
        float stepRate = gaitOmega / (Mathf.PI * 2f);
        float amp = Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(10f, 18f, stepRate));

        // Bob is synced to footsteps (down on plant). Idle is a slow breath. Jump is a soft stretch.
        float bob = isJumping ? 0.06f * Mathf.Sin(t * 14f)
                  : isMoving ? (0.028f - Mathf.Abs(Mathf.Cos(phWalk)) * 0.055f) * amp
                  : Mathf.Sin(t * 2.2f) * 0.014f;
        bob += swingSquash * 0.12f;

        // Apply landing squash and lateral spread onto body scale
        float scaleX = (1f + bob - hop * 0.08f) * (1f + landSpread);
        float scaleY = (1f - bob + hop * 0.15f) * (1f - landSquash);
        float scaleZ = (1f + bob - hop * 0.08f) * (1f + landSpread);
        body.localScale = new Vector3(scaleX, scaleY, scaleZ);

        if (head) head.localPosition = headRest + new Vector3(
            Mathf.Sin(isMoving ? phWalk : t * 2.2f) * (isMoving ? 0.020f : 0.012f),
            Mathf.Abs(Mathf.Sin(isMoving ? phWalk : t * 2.2f)) * (isMoving ? 0.020f : 0.014f), 0)
            * head.localScale.y;
        float sway = isMoving ? Mathf.Sin(phWalk) * 7.5f * amp : 0f;
        float nod = (isMoving ? Mathf.Cos(phWalk) * 4.5f * amp : 0f) + landNod;
        float yawSway = isMoving ? Mathf.Sin(phWalk) * 5f * amp : 0f;
        // Lean into climb and into the turn (anticipation / follow-through).
        float speedLean = isMoving && playerComp ? Mathf.Clamp(playerComp.calcSpeed * 2.4f, 0f, 14f) : 0f;
        nod -= speedLean * 0.35f;
        sway += swingRoll;
        nod += swingLean;
        yawSway += swingTwist;

        // Legs:
        // Alternating tripod gait (for 6 legs: 3 on each side):
        // Tripod 1: L0 (front left), L2 (rear left), R1 (mid right)
        // Tripod 2: R0 (front right), R2 (rear right), L1 (mid left)
        for (int i = 0; i < legs.Count; i++)
        {
            bool isLeft = i < 3;
            int legIdx = i % 3; // 0: front, 1: middle, 2: rear

            // Tripod assignment: group A = (L0, L2, R1), group B = (R0, R2, L1)
            bool isTripodA = isLeft ? (legIdx != 1) : (legIdx == 1);
            float tripodPhase = isTripodA ? 0f : Mathf.PI;
            // Overlapping per-leg offsets so the gait isn't a stiff locked tripod
            float ph = phWalk + tripodPhase + legIdx * 0.55f + i * 0.11f;

            // Forward/backward stride: softened sine so the plant and reach feel weighty, not jittery
            float swingAmp = (legIdx == 0 ? 24f : (legIdx == 2 ? 22f : 18f)) * amp;
            float swing = Mathf.Sin(ph) * swingAmp;

            // Lift only on the swing half, eased in and out (longer stance, quicker pick-up)
            float liftNorm = Mathf.Max(0f, Mathf.Sin(ph));
            float stepLift = liftNorm * liftNorm * (3f - 2f * liftNorm) * 16f;

            // Lateral foot splay/curl during stride (curls inward during lift, splays outward on stance):
            float splay = Mathf.Cos(ph) * (isLeft ? -7f : 7f);

            // Seamless landing blend:
            if (landingTimer > 0f)
            {
                // Firm foot contact on touchdown (damp step lift so feet plant firmly on bark without slipping)
                stepLift *= Mathf.Lerp(0.2f, 1f, landProgress);
                // Stride swing accelerates smoothly from solid plant into full sprint stride
                swing *= Mathf.Lerp(0.45f, 1f, landProgress);
                // Impact cushion splay
                splay += landSpread * (isLeft ? -30f : 30f);
            }

            Quaternion moveRot = Quaternion.Euler(0, swing, splay) * legRest[i] * Quaternion.Euler(-stepLift, 0, 0);

            // 2. Jumping pose calculation:
            // Front legs reach upward and spread out to grab surfaces/balance
            // Rear legs tuck inward and stream backward
            float tuckAngle;
            float spreadAngle;
            if (legIdx == 0)
            {
                // As spider descends near landing surface, front legs reach slightly forward in anticipation
                float preLandReach = (1f - hop) * 8f;
                tuckAngle = -32f + preLandReach + Mathf.Sin(t * 12f + i) * 5f;
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
            Quaternion targetLegPose = Quaternion.Slerp(groundRot, jumpRot, smoothJump);
            if (rollBlend > 0.001f)
            {
                // In Sonic roll attack: tuck legs tightly against the spinning body
                Quaternion rollTuck = legRest[i] * Quaternion.Euler(isLeft ? 45f : -45f, 0, 70f);
                targetLegPose = Quaternion.Slerp(targetLegPose, rollTuck, rollBlend);
            }
            if (swingTuck > 0.001f)
            {
                // Action web-swing pose: rear legs reach up along the silk line while front/mid legs flare into the wind.
                float k = Mathf.Clamp01(swingTuck);
                Quaternion webLeg;
                if (legIdx == 0)
                    webLeg = legRest[i] * Quaternion.Euler(-24f, isLeft ? -26f : 26f, isLeft ? -16f : 16f);
                else if (legIdx == 1)
                    webLeg = legRest[i] * Quaternion.Euler(4f, isLeft ? -14f : 14f, isLeft ? 18f : -18f);
                else
                    webLeg = legRest[i] * Quaternion.Euler(46f, isLeft ? 20f : -20f, isLeft ? 28f : -28f);
                targetLegPose = Quaternion.Slerp(targetLegPose, webLeg, k);
            }
            else if (swingTuck < -0.001f)
            {
                // Thwip: legs reach out toward the shot
                float reach = Mathf.Clamp01(-swingTuck);
                float pitch = legIdx == 0 ? -48f : 14f;
                Quaternion webReach = legRest[i] * Quaternion.Euler(pitch, (isLeft ? -1f : 1f) * 20f, -8f);
                targetLegPose = Quaternion.Slerp(targetLegPose, webReach, reach);
            }
            legs[i].localRotation = targetLegPose;

            // Friendly wave in main menu (uses front right leg i == 3)
            if (i == 3 && wave > 0)
            {
                legs[i].localRotation = Quaternion.Slerp(
                    legs[i].localRotation,
                    legRest[i] * Quaternion.Euler(-70f * wave, 0, Mathf.Sin(t * 16f) * 25f * wave),
                    wave
                );
            }
        }

        // Eyes: blink every few seconds (unless eye override is active)
        if (!eyeOverrideActive)
        {
            blink -= dt;
            if (blink < -0.14f) blink = Random.Range(1.8f, 4f);
            float lid = blink < 0 ? 0.12f : 1f;
            for (int i = 0; i < eyes.Count; i++) if (eyes[i]) { var r = eyeRest[i]; eyes[i].localScale = new Vector3(r.x, r.y * lid, r.z); }
        }

        // Death: roll on the back, cartoon style. Backflip pitch combined with death roll.
        bool dead = pi ? pi.life <= 0 : anim && anim.isActiveAndEnabled && HasBool(anim, "death") && anim.GetBool("death");
        flip = Mathf.MoveTowards(flip, dead ? 1 : 0, dt * 3f);
        if (dead)
        {
            body.localRotation = Quaternion.Euler(flipPitch, 0, 180f * Mathf.SmoothStep(0, 1, flip));
        }
        else if (rollBlend > 0.001f)
        {
            body.localRotation = Quaternion.Slerp(Quaternion.Euler(flipPitch, 0, 0), Quaternion.Euler(rollSpinAngle, 0, 0), rollBlend);
            body.localScale = Vector3.Lerp(new Vector3(1 + bob - hop * 0.08f, 1 - bob + hop * 0.15f, 1 + bob - hop * 0.08f), new Vector3(0.85f, 0.85f, 0.85f), rollBlend);
        }
        else if (hasSwingWorldRot)
        {
            body.rotation = swingWorldRot;
        }
        else
        {
            body.localRotation = Quaternion.Euler(flipPitch + nod, yawSway, sway);
        }

        // Abdomen follow-through: lags the body slightly so motion feels organic.
        if (abdomen && hasAbdomenRest)
        {
            var lag = Quaternion.Euler(-nod * 0.45f, -yawSway * 0.3f, -sway * 0.35f);
            abdomen.localRotation = Quaternion.Slerp(abdomen.localRotation, abdomenRest * lag, 1f - Mathf.Exp(-8f * dt));
        }

        // Dynamic wing flutter for winged skins (Bee, Wasp, Bat)
        if (wingPivotL && wingPivotR)
        {
            float flapAngle = 0f;
            if (wingType == "Bee")
            {
                // High-frequency buzzing flutter typical of bees (45Hz buzz)
                float buzz = Mathf.Sin(t * 45f) * (isJumping ? 22f : (isMoving ? 16f : 7f));
                flapAngle = buzz;
            }
            else if (wingType == "Wasp")
            {
                // Sharp, darting hornet wing stroke (32Hz stroke)
                float hornetStroke = Mathf.Sin(t * 32f) * (isJumping ? 25f : (isMoving ? 18f : 6f));
                flapAngle = hornetStroke;
            }
            else // Bat
            {
                flapAngle = isJumping
                    ? Mathf.Sin(t * 26f) * 20f
                    : isMoving
                        ? Mathf.Sin(phWalk * 2f) * 12f * amp
                        : Mathf.Sin(t * 3.5f) * 5f;
            }

            wingPivotL.localRotation = wingRestL * Quaternion.Euler(0, 0, flapAngle);
            wingPivotR.localRotation = wingRestR * Quaternion.Euler(0, 0, -flapAngle);
        }

        // Rainbow Spider: dynamically wave colors across all parts over time
        if (isRainbow && rainbowRenderers.Count > 0)
        {
            float waveSpeed = 0.65f;
            for (int rIdx = 0; rIdx < rainbowRenderers.Count; rIdx++)
            {
                var rend = rainbowRenderers[rIdx];
                if (!rend) continue;
                var mat = rend.material;
                // Compute spatial offset based on position in body to create a continuous shifting rainbow wave
                float spatialOffset = rend.transform.position.z * 1.8f + rend.transform.position.y * 1.2f + (rend.name.Contains("leg") ? 0.35f : 0f);
                float hue = Mathf.Repeat(t * waveSpeed + spatialOffset, 1f);
                Color rainbowCol = Color.HSVToRGB(hue, 0.85f, 1.0f);

                mat.color = rainbowCol;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", rainbowCol * 0.45f);
                }
            }
        }

        TickTouch(dt);
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

    /// Watertight unit cone: base radius 0.5 at y=0 with bottom cap, tip at y=1 (clean outward normals, zero inverted triangles).
    static Mesh MakeCone()
    {
        const int N = 16;
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tri = new List<int>();

        // 1. Bottom disc base cap (faces downwards into body)
        v.Add(Vector3.zero);
        n.Add(Vector3.down);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i < N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * 0.5f);
            n.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
        }
        for (int i = 0; i < N; i++)
        {
            int next = (i + 1) % N;
            tri.Add(0);
            tri.Add(next + 1);
            tri.Add(i + 1);
        }

        // 2. Cone sides with outward sloping normals and correct front-facing winding
        int sideStart = v.Count;
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var sideNormal = (d * 1.0f + Vector3.up * 0.5f).normalized;

            v.Add(d * 0.5f);
            n.Add(sideNormal);
            uvs.Add(new Vector2(i / (float)N, 0f));

            v.Add(Vector3.up);
            n.Add(sideNormal);
            uvs.Add(new Vector2(i / (float)N, 1f));
        }
        for (int i = 0; i < N; i++)
        {
            int idx = sideStart + i * 2;
            tri.Add(idx);
            tri.Add(idx + 1);
            tri.Add(idx + 2);
        }

        var m = new Mesh { name = "spike" };
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetUVs(0, uvs);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    static Mesh gemMesh;

    /// <summary>
    /// Generates a sparkling faceted brilliant-cut 3D gemstone mesh with flat facet normals.
    /// Every facet reflects light distinctly with vibrant jewelry specular highlights.
    /// </summary>
    static Mesh MakeGemMesh()
    {
        const int N = 8;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float tableR = 0.50f;
        float tableY = 0.45f;
        float girdleR = 0.95f;
        float girdleY = 0.15f;
        float culetY = -0.55f;

        var tablePts = new Vector3[N];
        var girdlePts = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            float a1 = i * Mathf.PI * 2f / N;
            float a2 = (i + 0.5f) * Mathf.PI * 2f / N;
            tablePts[i] = new Vector3(Mathf.Cos(a1) * tableR, tableY, Mathf.Sin(a1) * tableR);
            girdlePts[i] = new Vector3(Mathf.Cos(a2) * girdleR, girdleY, Mathf.Sin(a2) * girdleR);
        }
        var tableCenter = new Vector3(0, tableY, 0);
        var culet = new Vector3(0, culetY, 0);

        void AddFlatTri(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            var faceCenter = (p0 + p1 + p2) / 3f;
            var fn = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            if (Vector3.Dot(fn, faceCenter) < 0f)
            {
                var tmp = p1; p1 = p2; p2 = tmp;
                fn = -fn;
            }
            int idx = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            norms.Add(fn); norms.Add(fn); norms.Add(fn);
            uvs.Add(new Vector2(0.5f, 0.5f));
            uvs.Add(new Vector2(0.5f + p1.x * 0.4f, 0.5f + p1.z * 0.4f));
            uvs.Add(new Vector2(0.5f + p2.x * 0.4f, 0.5f + p2.z * 0.4f));
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
        }

        // 1. Table fan facets
        for (int i = 0; i < N; i++)
        {
            int next = (i + 1) % N;
            AddFlatTri(tableCenter, tablePts[i], tablePts[next]);
        }

        // 2. Crown kite & triangular facets
        for (int i = 0; i < N; i++)
        {
            int next = (i + 1) % N;
            AddFlatTri(tablePts[i], girdlePts[i], tablePts[next]);
            AddFlatTri(tablePts[next], girdlePts[i], girdlePts[next]);
        }

        // 3. Pavilion facets tapering to culet
        for (int i = 0; i < N; i++)
        {
            int next = (i + 1) % N;
            AddFlatTri(girdlePts[i], culet, girdlePts[next]);
        }

        var m = new Mesh { name = "ruby_gem_mesh" };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    static Mesh beeWingMeshL, beeWingMeshR;
    static Mesh waspWingMeshL, waspWingMeshR;

    /// <summary>
    /// Generates a smooth ribbon mesh for the honeybee wing mapped with full [0..1] UVs,
    /// so the high-resolution transparent texture renders the rounded honeybee wing with crisp alpha transparency.
    /// </summary>
    static Mesh MakeBeeWingMesh(bool isLeft)
    {
        if (isLeft && beeWingMeshL) return beeWingMeshL;
        if (!isLeft && beeWingMeshR) return beeWingMeshR;

        const int segments = 16;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float sgn = isLeft ? -1f : 1f;
        float length = 0.46f;
        float leadZ = 0.12f;
        float trailZ = -0.12f;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = sgn * t * length;
            float y = Mathf.Sin(t * Mathf.PI) * 0.015f; // subtle natural wing camber

            verts.Add(new Vector3(x, y, leadZ));
            verts.Add(new Vector3(x, y, trailZ));

            uvs.Add(new Vector2(t, 1f));
            uvs.Add(new Vector2(t, 0f));
        }

        for (int i = 0; i < segments; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = (i + 1) * 2;
            int i3 = i2 + 1;

            // Front face
            tris.Add(i0); tris.Add(i2); tris.Add(i1);
            tris.Add(i1); tris.Add(i2); tris.Add(i3);
            // Back face (double-sided)
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
            tris.Add(i1); tris.Add(i3); tris.Add(i2);
        }

        var m = new Mesh { name = isLeft ? "bee_wing_L" : "bee_wing_R" };
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();

        if (isLeft) beeWingMeshL = m; else beeWingMeshR = m;
        return m;
    }

    /// <summary>
    /// Generates a sleek ribbon mesh for the wasp wing mapped with full [0..1] UVs,
    /// so the high-resolution transparent texture renders the predatory dagger blade hornet wing with crisp alpha transparency.
    /// </summary>
    static Mesh MakeWaspWingMesh(bool isLeft)
    {
        if (isLeft && waspWingMeshL) return waspWingMeshL;
        if (!isLeft && waspWingMeshR) return waspWingMeshR;

        const int segments = 16;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float sgn = isLeft ? -1f : 1f;
        float length = 0.58f;
        float leadZ = 0.10f;
        float trailZ = -0.10f;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = sgn * t * length;
            float y = Mathf.Sin(t * Mathf.PI) * 0.010f; // subtle natural wing camber

            verts.Add(new Vector3(x, y, leadZ));
            verts.Add(new Vector3(x, y, trailZ));

            uvs.Add(new Vector2(t, 1f));
            uvs.Add(new Vector2(t, 0f));
        }

        for (int i = 0; i < segments; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = (i + 1) * 2;
            int i3 = i2 + 1;

            // Front face
            tris.Add(i0); tris.Add(i2); tris.Add(i1);
            tris.Add(i1); tris.Add(i2); tris.Add(i3);
            // Back face (double-sided)
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
            tris.Add(i1); tris.Add(i3); tris.Add(i2);
        }

        var m = new Mesh { name = isLeft ? "wasp_wing_L" : "wasp_wing_R" };
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();

        if (isLeft) waspWingMeshL = m; else waspWingMeshR = m;
        return m;
    }
}
