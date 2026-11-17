using UnityEngine;

/// polish3 enemy remakes in the SPIDER's design language (TCToon): smooth chunky chibi shapes, the spider's
/// cel shader + ink outline, the spider's dark glossy eyes with coloured pupils and gleams, soft fur/stripe textures.
/// Same pivots / names / footprint as before so TCSquirrel / TCWasp movement and the hitboxes are untouched.
public static class TCCritters
{
    // ================================================================ squirrel
    static readonly Color Fur = new Color(1.0f, 0.58f, 0.24f), FurDark = new Color(0.58f, 0.27f, 0.10f), Cream = new Color(1f, 0.93f, 0.80f),
        Pink = new Color(1f, 0.66f, 0.66f), Nose = new Color(0.30f, 0.14f, 0.12f), Amber = new Color(1f, 0.70f, 0.16f);

    /// Builds the squirrel under bodyPivot (local +Z = head / down the trunk, +Y = away from the bark, X = across).
    public static void BuildSquirrel(Transform bodyPivot, out Transform head, out Transform tail, out Transform fl, out Transform fr, out Transform bl, out Transform br)
    {
        var bodyTex = TCToon.Fur("tc_sq_body", Fur, Cream, 0.26f, FurDark, 0.07f);
        var fur = TCToon.Mat("sq_body", Color.white, bodyTex);
        var furPlain = TCToon.Mat("sq_fur", Fur);
        var dark = TCToon.Mat("sq_dark", FurDark);
        var cream = TCToon.Mat("sq_cream", Cream);
        var pink = TCToon.Mat("sq_pink", Pink, null, 0f, 0f, 0f);
        var tailTex = TCToon.Fur("tc_sq_tail", Fur, new Color(1f, 0.66f, 0.32f), 0.12f, null, 0f, 0.10f);
        var tailMat = TCToon.Mat("sq_tail", Color.white, tailTex);

        // torso: one smooth chubby pear (big haunches at the back, chest toward the head), dorsal stripe on top
        var torso = TCToon.Part("Torso", bodyPivot, TCToon.Lathe("tc_sq_torso", -0.215f, 0.17f,
            a => Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.55f) * Mathf.Lerp(0.125f, 0.095f, a), 1f, 0.82f, a => new Vector2(0, 0.012f * Mathf.Sin(a * Mathf.PI))),
            fur, new Vector3(0, 0.075f, 0f), Vector3.zero, Vector3.one);
        // neck: fills the gap between the chest and the skull so the head is one with the body
        TCToon.Ball("Neck", bodyPivot, furPlain, new Vector3(0, 0.10f, 0.15f), new Vector3(0.082f, 0.078f, 0.075f));

        // head (pivot at the neck so the nod rotates around the join, never detaching)
        head = TCToon.Pivot("HeadPivot", bodyPivot, new Vector3(0, 0.10f, 0.16f), new Vector3(10f, 0, 0));
        TCToon.Ball("Skull", head, furPlain, new Vector3(0, 0.04f, 0.08f), new Vector3(0.13f, 0.118f, 0.11f));
        TCToon.Ball("Muzzle", head, cream, new Vector3(0, -0.002f, 0.158f), new Vector3(0.068f, 0.052f, 0.052f));
        TCToon.Ball("Nose", head, TCToon.Mat("sq_nose", Nose, null, 0f, 1.0f, 0f), new Vector3(0, 0.024f, 0.207f), new Vector3(0.021f, 0.016f, 0.014f));
        for (int sg = -1; sg <= 1; sg += 2)
        {
            string side = sg < 0 ? "L" : "R";
            TCToon.Ball("Cheek" + side, head, cream, new Vector3(sg * 0.064f, -0.012f, 0.125f), new Vector3(0.052f, 0.046f, 0.046f));
            TCToon.Ball("Blush" + side, head, pink, new Vector3(sg * 0.085f, 0.012f, 0.13f), new Vector3(0.02f, 0.012f, 0.016f), new Vector3(0, sg * 40f, 0));
            TCToon.Eye("Eye" + side, head, new Vector3(sg * 0.058f, 0.094f, 0.14f), new Vector3(sg * 0.40f, 0.74f, 0.56f), 0.045f, Amber);
            var ear = TCToon.Pivot("Ear" + side, head, new Vector3(sg * 0.08f, 0.135f, 0.03f), new Vector3(-20f, sg * 10f, sg * -24f));
            TCToon.Ball("Outer", ear, furPlain, new Vector3(0, 0.045f, 0), new Vector3(0.040f, 0.058f, 0.020f));
            TCToon.Ball("Inner", ear, pink, new Vector3(0, 0.042f, 0.012f), new Vector3(0.024f, 0.038f, 0.010f));
            TCToon.Ball("Tuft", ear, dark, new Vector3(0, 0.10f, -0.004f), new Vector3(0.014f, 0.024f, 0.012f));
            TCToon.Ball("Tooth" + side, head, TCToon.Mat("sq_tooth", Color.white, null, 0f, 1.0f, 0f, null, 1f), new Vector3(sg * 0.009f, -0.043f, 0.19f), new Vector3(0.009f, 0.014f, 0.006f));
        }

        // big bushy tail in two flexible pieces (base rises off the rump, tip trails back up-trunk with a curl)
        tail = TCToon.Pivot("TailPivot", bodyPivot, new Vector3(0, 0.085f, -0.2f));
        TCToon.Part("TailBase", tail, TCToon.Lathe("tc_sq_tail_a", -0.02f, 0.25f,
            a => Mathf.Lerp(0.05f, 0.11f, Mathf.SmoothStep(0, 1, a)) * Mathf.Pow(Mathf.Sin(Mathf.Lerp(0.12f, 1f, a) * Mathf.PI), 0.45f), 1.05f, 0.9f),
            tailMat, Vector3.zero, new Vector3(-128f, 0, 0), Vector3.one);
        var tip = TCToon.Pivot("TailTip", tail, new Vector3(0, 0.20f * 0.788f, -0.20f * 0.616f));
        TCToon.Part("Plume", tip, TCToon.Lathe("tc_sq_tail_b", -0.06f, 0.24f,
            a => 0.14f * Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.5f) * Mathf.Lerp(0.85f, 1.05f, Mathf.Sin(a * Mathf.PI)), 1.1f, 0.9f,
            a => new Vector2(0, 0.08f * a * a)),
            tailMat, Vector3.zero, new Vector3(-190f, 0, 0), Vector3.one);

        // legs: shoulder/hip pivot -> thigh -> knee pivot -> shin + paw (the rig bends the knee)
        fl = Leg("LegFL", bodyPivot, new Vector3(-0.08f, 0.06f, 0.10f), furPlain, dark, false);
        fr = Leg("LegFR", bodyPivot, new Vector3(0.08f, 0.06f, 0.10f), furPlain, dark, false);
        bl = Leg("LegBL", bodyPivot, new Vector3(-0.105f, 0.07f, -0.12f), furPlain, dark, true);
        br = Leg("LegBR", bodyPivot, new Vector3(0.105f, 0.07f, -0.12f), furPlain, dark, true);

        var rig = bodyPivot.gameObject.AddComponent<TCSquirrelRig>();
        rig.torso = torso; rig.head = head; rig.tailBase = tail; rig.tailTip = tip; rig.legs = new[] { fl, fr, bl, br };
        bodyPivot.gameObject.AddComponent<TCBlink>();
    }

    static Transform Leg(string name, Transform parent, Vector3 pos, Material fur, Material paw, bool hind)
    {
        var pivot = TCToon.Pivot(name + "_Pivot", parent, pos);
        float s = hind ? 1.22f : 1f;
        if (hind) TCToon.Ball("Haunch", pivot, fur, new Vector3(0, -0.015f, -0.01f), new Vector3(0.055f, 0.07f, 0.08f));
        TCToon.Part("Thigh", pivot, TCToon.Egg("tc_sq_thigh", 0.11f, 0.034f, 0.3f), fur, new Vector3(0, -0.045f * s, 0), new Vector3(90f, 0, 0), Vector3.one * s);
        var knee = TCToon.Pivot("Knee", pivot, new Vector3(0, -0.09f * s, 0));
        TCToon.Part("Shin", knee, TCToon.Egg("tc_sq_shin", 0.08f, 0.022f, 0.2f), fur, new Vector3(0, -0.03f * s, 0), new Vector3(90f, 0, 0), Vector3.one * s);
        TCToon.Ball("Paw", knee, paw, new Vector3(0, -0.068f * s, 0.016f), new Vector3(0.028f, 0.018f, 0.036f) * s);
        return pivot;
    }

    // ================================================================ bat
    static readonly Color BatFur = new Color(0.62f, 0.50f, 0.86f), BatBelly = new Color(0.86f, 0.76f, 0.96f), BatWing = new Color(0.40f, 0.30f, 0.62f),
        BatBone = new Color(0.30f, 0.22f, 0.44f), BatPupil = new Color(1f, 0.36f, 0.62f), BatInk = new Color(0.14f, 0.07f, 0.20f, 1f);

    /// Chubby cute bat facing +Z (out of the bark), wings in the X/Y plane. Keeps the rig names
    /// ("Body", "Body/Head", "BatWingPivot_L/R", "WingTip") so TCWasp finds it; TCBatFlap animates it.
    public static GameObject BuildBat(Transform parent)
    {
        var root = new GameObject("Bat_Visual"); if (parent) root.transform.SetParent(parent, false);
        var furTex = TCToon.Fur("tc_bat_fur", BatFur, BatBelly, 0.20f, null, 0f, 0.08f);
        var fur = TCToon.Mat("bat_body", Color.white, furTex, 0f, 1.4f, 0.22f, BatInk);
        var furPlain = TCToon.Mat("bat_fur", BatFur, null, 0f, 1.4f, 0.22f, BatInk);
        var belly = TCToon.Mat("bat_belly", BatBelly, null, 0f, 1.2f, 0.18f, BatInk);
        var wing = TCToon.Mat("bat_wing", BatWing, null, 0f, 1.3f, 0f, BatInk);
        var bone = TCToon.Mat("bat_bone", BatBone, null, 0f, 1.2f, 0f, BatInk);
        var pink = TCToon.Mat("bat_pink", Pink, null, 0f, 0f, 0f);
        var white = TCToon.Mat("bat_fang", Color.white, null, 0f, 1.0f, 0f, BatInk, 1f);

        var body = TCToon.Pivot("Body", root.transform, Vector3.zero);
        // round fluffy torso, belly facing the camera (+Z)
        TCToon.Part("Torso", body, TCToon.Lathe("tc_bat_torso", -0.085f, 0.085f, a => Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.6f) * Mathf.Lerp(0.082f, 0.072f, a), 1f, 0.92f),
            fur, new Vector3(0, 0.02f, 0), new Vector3(-90f, 0, 0), Vector3.one);
        TCToon.Ball("Belly", body, belly, new Vector3(0, 0.01f, 0.052f), new Vector3(0.052f, 0.06f, 0.03f));
        var head = TCToon.Pivot("Head", body, new Vector3(0, 0.125f, 0.012f));
        TCToon.Ball("Skull", head, furPlain, Vector3.zero, new Vector3(0.098f, 0.088f, 0.086f));
        TCToon.Ball("Tuft", head, furPlain, new Vector3(0, 0.082f, 0.01f), new Vector3(0.026f, 0.03f, 0.024f), new Vector3(0, 0, 12f));
        TCToon.Ball("Snout", head, belly, new Vector3(0, -0.028f, 0.066f), new Vector3(0.048f, 0.034f, 0.032f));
        TCToon.Ball("Nose", head, pink, new Vector3(0, -0.012f, 0.097f), new Vector3(0.014f, 0.009f, 0.008f));
        var earMesh = TCToon.Lathe("tc_bat_ear", 0f, 0.115f, a => 0.046f * Mathf.Pow(1f - a, 0.75f) * Mathf.Pow(Mathf.Sin(Mathf.Min(1f, a * 3f + 0.2f) * Mathf.PI * 0.5f), 0.5f), 1f, 0.42f);
        var earInMesh = TCToon.Lathe("tc_bat_ear_in", 0f, 0.085f, a => 0.03f * Mathf.Pow(1f - a, 0.75f) * Mathf.Pow(Mathf.Sin(Mathf.Min(1f, a * 3f + 0.2f) * Mathf.PI * 0.5f), 0.5f), 1f, 0.3f);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            string side = sg < 0 ? "L" : "R";
            TCToon.Eye("Eye" + side, head, new Vector3(sg * 0.04f, 0.014f, 0.068f), new Vector3(sg * 0.36f, 0.08f, 1f), 0.033f, BatPupil);
            var ear = TCToon.Pivot("Ear" + side, head, new Vector3(sg * 0.058f, 0.055f, -0.005f), new Vector3(0, 0, sg * -24f));
            TCToon.Part("Outer", ear, earMesh, furPlain, Vector3.zero, new Vector3(-90f, 0, 0), Vector3.one);
            TCToon.Part("Inner", ear, earInMesh, pink, new Vector3(0, 0.012f, 0.012f), new Vector3(-90f, 0, 0), Vector3.one);
            TCToon.Part("Fang" + side, head, TCToon.Egg("tc_bat_fang", 0.03f, 0.007f, 0.9f), white, new Vector3(sg * 0.015f, -0.056f, 0.084f), new Vector3(-90f, 0, 0), Vector3.one);
            TCToon.Ball("Blush" + side, head, pink, new Vector3(sg * 0.07f, -0.02f, 0.055f), new Vector3(0.018f, 0.01f, 0.012f), new Vector3(0, sg * 40f, 0));
            TCToon.Ball("Foot" + side, body, bone, new Vector3(sg * 0.032f, -0.07f, 0.012f), new Vector3(0.017f, 0.022f, 0.015f));
        }

        // wings: shoulder -> arm bone + inner membrane; wrist ("WingTip") -> two finger bones + scalloped outer membrane
        var boneMesh = TCToon.Lathe("tc_bat_bone", 0f, 1f, a => Mathf.Lerp(0.011f, 0.006f, a) * Mathf.Pow(Mathf.Sin(Mathf.Lerp(0.05f, 0.95f, a) * Mathf.PI), 0.3f), 1f, 1f, null, 12, 10);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            var pv = TCToon.Pivot(sg < 0 ? "BatWingPivot_L" : "BatWingPivot_R", root.transform, new Vector3(sg * 0.068f, 0.055f, 0.002f));
            TCToon.Part("Arm", pv, boneMesh, bone, Vector3.zero, new Vector3(-13f, sg * 90f, 0), new Vector3(1, 1, 0.19f));
            var inner = new[] { new Vector2(-0.01f, 0.02f), new Vector2(0.06f, 0.04f), new Vector2(0.12f, 0.05f), new Vector2(0.19f, 0.05f), new Vector2(0.18f, 0.0f),
                                new Vector2(0.165f, -0.04f), new Vector2(0.12f, -0.055f), new Vector2(0.07f, -0.075f), new Vector2(0.02f, -0.08f), new Vector2(-0.01f, -0.06f) };
            TCToon.Part("Membrane", pv, TCToon.Membrane(sg < 0 ? "tc_bat_mem_in_L" : "tc_bat_mem_in_R", Mirror(inner, sg), 0.006f, 0.004f), wing, new Vector3(0, 0, -0.004f), Vector3.zero, Vector3.one);
            var tip = TCToon.Pivot("WingTip", pv, new Vector3(sg * 0.19f, 0.045f, 0));
            var outer = new[] { new Vector2(-0.015f, 0.01f), new Vector2(0.06f, 0.05f), new Vector2(0.13f, 0.06f), new Vector2(0.20f, 0.03f), new Vector2(0.19f, -0.02f),
                                new Vector2(0.15f, -0.035f), new Vector2(0.12f, -0.09f), new Vector2(0.085f, -0.06f), new Vector2(0.05f, -0.10f), new Vector2(0.015f, -0.075f), new Vector2(-0.02f, -0.07f) };
            TCToon.Part("Membrane", tip, TCToon.Membrane(sg < 0 ? "tc_bat_mem_out_L" : "tc_bat_mem_out_R", Mirror(outer, sg), 0.006f, 0.004f), wing, new Vector3(0, 0, -0.004f), Vector3.zero, Vector3.one);
            TCToon.Part("Finger1", tip, boneMesh, bone, Vector3.zero, new Vector3(-8f, sg * 90f, 0), new Vector3(0.8f, 0.8f, 0.2f));
            TCToon.Part("Finger2", tip, boneMesh, bone, Vector3.zero, new Vector3(30f, sg * 90f, 0), new Vector3(0.7f, 0.7f, 0.14f));
            TCToon.Part("Finger3", tip, boneMesh, bone, Vector3.zero, new Vector3(62f, sg * 90f, 0), new Vector3(0.7f, 0.7f, 0.11f));
            TCToon.Part("Claw", tip, TCToon.Egg("tc_bat_claw", 0.026f, 0.007f, 0.9f), white, new Vector3(sg * -0.004f, 0.012f, 0), new Vector3(-90f, 0, sg * 20f), Vector3.one);
        }
        root.AddComponent<TCBatFlap>();
        root.AddComponent<TCBlink>();
        return root;
    }

    // ================================================================ wasp
    static readonly Color WaspYellow = new Color(1f, 0.80f, 0.16f), WaspBlack = new Color(0.17f, 0.14f, 0.19f), WaspPupil = new Color(1f, 0.28f, 0.16f),
        WaspWing = new Color(0.82f, 0.94f, 1f, 0.42f);

    /// Chibi hornet facing +Z (toward the camera, away from the bark), +Y = up the trunk. Same rig slots as the old
    /// wasp: "Head", "Abdomen_Pivot", "WingPivot_L/R" (TCWasp buzzes the wings, pumps the abdomen, twitches the head).
    public static GameObject BuildWasp(Transform parent, out Transform head, out Transform abdomen, out Transform wingL, out Transform wingR)
    {
        var root = new GameObject("Wasp_Visual"); if (parent) root.transform.SetParent(parent, false);
        var yellow = TCToon.Mat("wasp_yellow", WaspYellow);
        var black = TCToon.Mat("wasp_black", WaspBlack);
        var fuzzTex = TCToon.Fur("tc_wasp_thorax", WaspBlack, WaspBlack * 1.25f, 0.2f, WaspYellow, 0.0f, 0.12f);
        var thoraxMat = TCToon.Mat("wasp_thorax", Color.white, fuzzTex);
        var bandTex = TCToon.Bands("tc_wasp_bands", WaspYellow, WaspBlack, 4, 0.07f, 0.06f);
        var abdMat = TCToon.Mat("wasp_abd", Color.white, bandTex);
        var white = TCToon.Mat("wasp_white", Color.white, null, 0f, 1.0f, 0f, null, 1f);

        // fuzzy thorax with yellow shoulder spots
        TCToon.Ball("Thorax", root.transform, thoraxMat, new Vector3(0, 0.05f, 0), new Vector3(0.105f, 0.098f, 0.105f));
        for (int sg = -1; sg <= 1; sg += 2) TCToon.Ball(sg < 0 ? "SpotL" : "SpotR", root.transform, yellow, new Vector3(sg * 0.058f, 0.11f, 0.04f), new Vector3(0.03f, 0.022f, 0.03f), new Vector3(0, 0, sg * 30f));

        // big chibi head: yellow face, black cap, spider eyes, angry brows, mandibles, antennae
        var hd = TCToon.Pivot("Head", root.transform, new Vector3(0, 0.07f, 0.13f)); head = hd;
        TCToon.Ball("Face", hd, yellow, Vector3.zero, new Vector3(0.105f, 0.095f, 0.09f));
        TCToon.Ball("Cap", hd, black, new Vector3(0, 0.034f, -0.012f), new Vector3(0.1f, 0.072f, 0.082f));
        for (int sg = -1; sg <= 1; sg += 2)
        {
            string side = sg < 0 ? "L" : "R";
            TCToon.Eye("Eye" + side, hd, new Vector3(sg * 0.046f, 0.012f, 0.066f), new Vector3(sg * 0.42f, 0.12f, 1f), 0.039f, WaspPupil);
            TCToon.Part("Brow" + side, hd, TCToon.Egg("tc_wasp_brow", 0.05f, 0.011f, 0.3f), black, new Vector3(sg * 0.045f, 0.058f, 0.078f), new Vector3(0, 90f, sg * -22f), Vector3.one);
            TCToon.Part("Mandible" + side, hd, TCToon.Egg("tc_wasp_mand", 0.03f, 0.009f, 0.7f), black, new Vector3(sg * 0.02f, -0.07f, 0.06f), new Vector3(-70f, sg * -20f, 0), Vector3.one);
            var ant = TCToon.Pivot("Antenna" + side, hd, new Vector3(sg * 0.03f, 0.085f, 0.04f), new Vector3(-35f, sg * 22f, 0));
            TCToon.Part("Stalk", ant, TCToon.Lathe("tc_wasp_ant", 0f, 0.10f, a => 0.0065f, 1f, 1f, a => new Vector2(0, 0.02f * a * a), 10, 10), black, Vector3.zero, new Vector3(-90f, 0, 0), Vector3.one);
            TCToon.Ball("Knob", ant, black, new Vector3(0, 0.10f, -0.02f), Vector3.one * 0.014f);
            // three dangling legs per side
            for (int k = 0; k < 3; k++)
            {
                var lp = TCToon.Pivot("Leg" + side + k, root.transform, new Vector3(sg * 0.055f, -0.02f, 0.03f - k * 0.035f), new Vector3(10f + k * 8f, 0, sg * (28f + k * 6f)));
                TCToon.Part("Seg", lp, TCToon.Egg("tc_wasp_leg", 0.09f, 0.010f, 0.2f), black, new Vector3(0, -0.04f, 0), new Vector3(90f, 0, 0), Vector3.one);
                TCToon.Ball("Foot", lp, yellow, new Vector3(0, -0.085f, 0.004f), new Vector3(0.012f, 0.01f, 0.014f));
            }
        }

        // striped abdomen + stinger (pivot pumps)
        var ab = TCToon.Pivot("Abdomen_Pivot", root.transform, new Vector3(0, 0.02f, -0.07f)); abdomen = ab;
        TCToon.Part("Abdomen", ab, TCToon.Lathe("tc_wasp_abd", -0.30f, 0.0f, a => 0.115f * Mathf.Pow(Mathf.Sin(a * Mathf.PI), 0.62f) * Mathf.Lerp(0.55f, 1.05f, Mathf.Sqrt(a)), 1f, 0.95f),
            abdMat, new Vector3(0, -0.01f, 0), Vector3.zero, Vector3.one);
        TCToon.Part("Stinger", ab, TCToon.Lathe("tc_wasp_sting", -0.06f, 0f, a => 0.016f * Mathf.Pow(a, 0.8f), 1f, 1f, null, 12, 8), black, new Vector3(0, -0.012f, -0.285f), Vector3.zero, Vector3.one);

        // wings: glassy fore + hind wings with dark veins rim (TCWasp buzzes the pivots)
        var fore = new[] { new Vector2(0f, 0.012f), new Vector2(0.08f, 0.04f), new Vector2(0.18f, 0.045f), new Vector2(0.25f, 0.025f), new Vector2(0.27f, -0.005f), new Vector2(0.22f, -0.035f), new Vector2(0.12f, -0.04f), new Vector2(0.03f, -0.02f) };
        var hind = new[] { new Vector2(0f, 0.0f), new Vector2(0.06f, 0.02f), new Vector2(0.14f, 0.02f), new Vector2(0.17f, -0.005f), new Vector2(0.13f, -0.03f), new Vector2(0.05f, -0.025f) };
        var film = TCToon.Film("wasp_wing", WaspWing);
        Transform W(int sg)
        {
            var pv = TCToon.Pivot(sg < 0 ? "WingPivot_L" : "WingPivot_R", root.transform, new Vector3(sg * 0.07f, 0.13f, 0.0f));
            TCToon.Part("Fore", pv, TCToon.Membrane(sg < 0 ? "tc_wasp_fore_L" : "tc_wasp_fore_R", Mirror(fore, sg), 0.003f, 0.002f), film, Vector3.zero, new Vector3(90f, sg * 18f, 0), Vector3.one);
            TCToon.Part("Hind", pv, TCToon.Membrane(sg < 0 ? "tc_wasp_hind_L" : "tc_wasp_hind_R", Mirror(hind, sg), 0.003f, 0.002f), film, new Vector3(0, -0.006f, -0.02f), new Vector3(90f, sg * 38f, 0), Vector3.one);
            return pv;
        }
        wingL = W(-1); wingR = W(1);
        root.AddComponent<TCWaspRig>();
        root.AddComponent<TCBlink>();
        return root;
    }

    static Vector2[] Mirror(Vector2[] pts, int sg) { var r = new Vector2[pts.Length]; for (int i = 0; i < pts.Length; i++) r[i] = new Vector2(pts[i].x * sg, pts[i].y); if (sg < 0) System.Array.Reverse(r); return r; }
}
